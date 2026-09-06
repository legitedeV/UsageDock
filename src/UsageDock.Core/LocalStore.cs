using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace UsageDock.Core;
public sealed class LocalStore
{
    private readonly string directory;
    private bool corrupt;
    public string DirectoryPath => directory;
    public LocalStore(string directory) { this.directory = Path.GetFullPath(directory); }
    public StoredState Load()
    {
        var path = Path.Combine(directory, "settings.json");
        if (!File.Exists(path)) return new(Array.Empty<ConnectionProfile>(), new());
        try
        {
            var info = new FileInfo(path); if (info.Length > 1024 * 1024) throw new InvalidDataException();
            var state = JsonSerializer.Deserialize<StoredState>(File.ReadAllText(path), new JsonSerializerOptions { MaxDepth = 16 }) ?? throw new InvalidDataException();
            ValidateState(state);
            corrupt = false; return state with { Connections = Array.AsReadOnly(state.Connections.ToArray()) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        { corrupt = true; throw new InvalidDataException("Local settings could not be read. Restore or rename the settings file before saving."); }
    }
    public void Save(StoredState state)
    {
        if (File.Exists(Path.Combine(directory, "settings.json")) && !corrupt) _ = Load();
        if (corrupt) throw new InvalidDataException("Unreadable settings must be recovered before saving.");
        ValidateState(state);
        WriteState(state, ReadVersions());
    }
    public string? ReadSecret(Guid id)
    {
        var path = SecretPath(id); if (!File.Exists(path)) return null;
        try { if (new FileInfo(path).Length > 65536) throw new CryptographicException(); return Encoding.UTF8.GetString(Protect(File.ReadAllBytes(path), false)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException) { throw new InvalidDataException("The saved credential cannot be decrypted. Reconnect this account."); }
    }
    public void WriteSecret(Guid id, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 32768 || secret.Any(char.IsControl)) throw new InvalidDataException("Invalid credential.");

        WriteAtomic(SecretPath(id), Protect(Encoding.UTF8.GetBytes(secret), true));
    }
    public void DeleteSecret(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invalid connection ID.");
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(file);
                var prefix = id.ToString("N");
                var parts = name.Split('.');
                if (name == prefix + ".secret" ||
                    (parts.Length == 3 && parts[0] == prefix && parts[2] == "secret" && Guid.TryParseExact(parts[1], "N", out _)))
                    File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new InvalidDataException("The saved credential could not be completely removed. Retry deletion."); }
    }
    private static void ValidateState(StoredState state)
    {
        if (state is null || state.Connections is null || state.Settings is null ||
            state.Connections.Count > 1000 || state.Connections.Any(x => x is null) ||
            state.Connections.Select(x => x.Id).Distinct().Count() != state.Connections.Count ||
            state.Connections.Any(p => ProfileValidator.Validate(p, "validation") != null) ||
            state.Settings.RefreshSeconds is < 30 or > 86400 ||
            state.Settings.Theme is not ("Dark" or "Light" or "System"))
            throw new InvalidDataException("Invalid settings.");
    }
    private string SecretPath(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invalid connection ID.");
        var versions = ReadVersions();
        return Path.Combine(directory, id.ToString("N") + (versions.TryGetValue(id.ToString("N"), out var version) ? "." + version : "") + ".secret");
    }
    private Dictionary<string, string> ReadVersions()
    {
        var path = Path.Combine(directory, "settings.json");
        if (!File.Exists(path)) return new();
        try
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException();
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 16 });
            if (!doc.RootElement.TryGetProperty("SecretVersions", out var versions)) return new();
            var result = new Dictionary<string, string>();
            foreach (var entry in versions.EnumerateObject())
            {
                var value = entry.Value.GetString();
                if (!Guid.TryParseExact(entry.Name, "N", out _) || !Guid.TryParseExact(value, "N", out _)) throw new InvalidDataException();
                result.Add(entry.Name, value!);
            }
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        { throw new InvalidDataException("Local credential references could not be read."); }
    }
    private void WriteState(StoredState state, Dictionary<string, string> versions)
    {
        var json = JsonSerializer.SerializeToNode(state)!.AsObject();
        json["SecretVersions"] = JsonSerializer.SerializeToNode(versions);
        WriteAtomic(Path.Combine(directory, "settings.json"), JsonSerializer.SerializeToUtf8Bytes(json));
    }
    public void SaveConnection(StoredState state, Guid id, string? secret)
    {
        ValidateState(state);
        if (!state.Connections.Any(p => p.Id == id)) throw new InvalidDataException("Connection is missing from settings.");
        if (File.Exists(Path.Combine(directory, "settings.json"))) _ = Load();
        if (corrupt) throw new InvalidDataException("Unreadable settings must be recovered before saving.");

        var versions = ReadVersions();
        if (secret is null) { WriteState(state, versions); return; }
        if (ProfileValidator.Validate(state.Connections.Single(p => p.Id == id), secret) != null) throw new InvalidDataException("Invalid credential.");
        var version = Guid.NewGuid().ToString("N");
        var path = Path.Combine(directory, id.ToString("N") + "." + version + ".secret");
        WriteAtomic(path, Protect(Encoding.UTF8.GetBytes(secret), true));
        // Publishing the metadata reference is the only commit point. Failed writes leave an unreferenced encrypted blob.
        WriteState(state, new Dictionary<string, string>(versions) { [id.ToString("N")] = version });
    }
    private void WriteAtomic(string path, byte[] bytes)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new InvalidDataException("Local data could not be saved."); }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    private static byte[] Protect(byte[] value, bool encrypt)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Credential storage requires Windows.");
        var input = new Blob { Size = value.Length, Data = Marshal.AllocHGlobal(value.Length) }; var output = new Blob();
        try
        {
            Marshal.Copy(value, 0, input.Data, value.Length);
            var ok = encrypt ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new CryptographicException("Windows credential protection failed.");
            var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally { Marshal.Copy(new byte[value.Length], 0, input.Data, value.Length); Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); CryptographicOperations.ZeroMemory(value); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
}
