using System.Globalization;
using System.Text.Json;
namespace UsageDock.Core;
internal static class UsageParser
{
    internal static JsonElement Required(JsonElement root, string key) => root.TryGetProperty(key, out var v) ? v : throw new InvalidDataException("Unexpected provider response.");
    internal static string? Text(JsonElement root, string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    internal static decimal Number(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n)) return n;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out n)) return n;
        throw new InvalidDataException("Unexpected numeric data.");
    }
    private static double? Percent(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        var n = (double)Number(v);
        if (!double.IsFinite(n) || n < 0 || n > 100) throw new InvalidDataException("Invalid percentage data.");
        return n;
    }
    private static DateTimeOffset? Reset(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var seconds)) return DateTimeOffset.FromUnixTimeSeconds(seconds);
        if (v.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) return date.ToUniversalTime();
        throw new InvalidDataException("Invalid reset date.");
    }
    internal static IReadOnlyList<UsageWindow> Subscription(JsonElement root, ProviderKind provider)
    {
        var windows = new List<UsageWindow>();
        if (provider == ProviderKind.Codex)
        {
            if (root.TryGetProperty("rate_limit", out var rate) && rate.ValueKind == JsonValueKind.Object) AddRate(rate, "Codex", windows);
            if (root.TryGetProperty("additional_rate_limits", out var extra) && extra.ValueKind == JsonValueKind.Array)
                foreach (var item in extra.EnumerateArray()) if (item.TryGetProperty("rate_limit", out var r) && r.ValueKind == JsonValueKind.Object) AddRate(r, Text(item, "limit_name") ?? Text(item, "metered_feature") ?? "Additional", windows);
        }
        else
        {
            foreach (var pair in new[] { ("five_hour", "5 hours"), ("seven_day", "7 days"), ("seven_day_opus", "Opus - 7 days"), ("seven_day_sonnet", "Sonnet - 7 days"), ("seven_day_oauth_apps", "OAuth apps - 7 days") })
                if (root.TryGetProperty(pair.Item1, out var w) && w.ValueKind == JsonValueKind.Object) windows.Add(new(pair.Item2, Percent(w, "utilization"), Reset(w, "resets_at")));
            if (root.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Array)
                foreach (var w in limits.EnumerateArray())
                {
                    if (w.TryGetProperty("is_active", out var active) && active.ValueKind == JsonValueKind.False) continue;
                    var label = Text(w, "kind") ?? Text(w, "group") ?? Text(w, "type") ?? Text(w, "name") ?? "Scoped limit";
                    if (w.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Object)
                    {
                        if (scope.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object) label = (Text(model, "display_name") ?? Text(model, "id") ?? "Model") + " - " + label;
                        else label = Text(scope, "model") ?? label;
                    }
                    windows.Add(new(label, Percent(w, "percent"), Reset(w, "resets_at")));
                }
        }
        if (windows.Count == 0) throw new InvalidDataException("No supported usage windows were returned.");
        return windows.AsReadOnly();
    }
    private static void AddRate(JsonElement rate, string name, List<UsageWindow> windows)
    {
        foreach (var key in new[] { "primary_window", "secondary_window" })
            if (rate.TryGetProperty(key, out var w) && w.ValueKind == JsonValueKind.Object)
            {
                var duration = w.TryGetProperty("limit_window_seconds", out var d) ? Number(d) : 0;
                var label = duration > 0 ? (duration % 86400 == 0 ? $"{duration / 86400:0.##} days" : duration % 3600 == 0 ? $"{duration / 3600:0.##} hours" : $"{duration / 60:0.##} minutes") : key.Replace('_', ' ');
                windows.Add(new($"{name} - {label}", Percent(w, "used_percent"), Reset(w, "reset_at")));
            }
    }
    internal static decimal Cost(JsonElement result, ProviderKind provider)
    {
        var currency = provider == ProviderKind.OpenAiApi ? Text(Required(result, "amount"), "currency") : Text(result, "currency");
        if (!string.Equals(currency, "usd", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsupported or missing currency.");
        var amount = provider == ProviderKind.OpenAiApi ? Number(Required(Required(result, "amount"), "value")) : Number(Required(result, "amount")) / 100m;
        return amount;
    }
    private static decimal TokenNumber(JsonElement element)
    {
        var value = Number(element);
        if (value < 0 || value != decimal.Truncate(value)) throw new InvalidDataException("Invalid token count.");
        return value;
    }
    internal static long Tokens(JsonElement result, ProviderKind provider)
    {
        decimal sum = 0;
        var fields = provider == ProviderKind.OpenAiApi ? new[] { "input_tokens", "output_tokens" } : new[] { "uncached_input_tokens", "cache_read_input_tokens", "output_tokens" };
        foreach (var key in fields) sum += TokenNumber(Required(result, key));
        if (provider == ProviderKind.AnthropicApi)
        {
            var cache = Required(result, "cache_creation");
            sum += TokenNumber(Required(cache, "ephemeral_1h_input_tokens")) + TokenNumber(Required(cache, "ephemeral_5m_input_tokens"));
        }
        if (sum < 0 || sum != decimal.Truncate(sum)) throw new InvalidDataException("Invalid token count.");
        return checked((long)sum);
    }
}
