using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UsageDock.Core;

namespace UsageDock.App;

internal sealed class UsageClock : IDisposable
{
    private sealed record Entry(WeakReference<TextBlock> Target, DateTimeOffset? At, bool Full);
    private readonly List<Entry> entries = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(30) };

    public UsageClock(Window owner)
    {
        timer.Tick += Tick;
        owner.Closed += (_, _) => Dispose();
        timer.Start();
    }

    public TextBlock Label(DateTimeOffset? at, bool full = false, double size = 13)
    {
        var label = new TextBlock { FontSize = size, Foreground = Ui.Muted,
            TextTrimming = TextTrimming.CharacterEllipsis };
        var entry=new Entry(new WeakReference<TextBlock>(label),at,full);
        entries.Add(entry);
        label.Unloaded+=(_,_)=>entries.Remove(entry);
        label.Loaded+=(_,_)=>{if(!entries.Contains(entry))entries.Add(entry);};
        Update(label, at, full, DateTimeOffset.UtcNow);
        return label;
    }

    internal void Refresh(DateTimeOffset now)
    {
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (!entries[i].Target.TryGetTarget(out var label)) { entries.RemoveAt(i); continue; }
            Update(label, entries[i].At, entries[i].Full, now);
        }
    }

    private static void Update(TextBlock label, DateTimeOffset? at, bool full, DateTimeOffset now)
    {
        label.Text = full ? UsageTime.Full(at, now, TimeZoneInfo.Local,Localization.CurrentLanguage) : UsageTime.Relative(at, now, Localization.CurrentLanguage);
        label.ToolTip = UsageTime.Full(at, now, TimeZoneInfo.Local,Localization.CurrentLanguage);
    }
    private void Tick(object? sender, EventArgs e) => Refresh(DateTimeOffset.UtcNow);
    public void Dispose() { timer.Stop(); timer.Tick -= Tick; entries.Clear(); }
}

public sealed partial class Dashboard
{
    private readonly UsageClock clock;
    internal void RefreshClock(DateTimeOffset now) => clock.Refresh(now);

    private UIElement ResetDisplay(DateTimeOffset? at, double width = 170)
    {
        var panel = new StackPanel { Width = width, ToolTip = UsageTime.Absolute(at, TimeZoneInfo.Local,Localization.CurrentLanguage) };
        var relative = clock.Label(at);
        panel.Children.Add(relative);
        if (at.HasValue)
        {
            var local = TimeZoneInfo.ConvertTime(at.Value, TimeZoneInfo.Local);
            panel.Children.Add(new TextBlock { Text = local.ToString("d MMM, HH:mm", Localization.Culture),
                FontSize = 11, Foreground = Ui.Muted, ToolTip = UsageTime.Absolute(at, TimeZoneInfo.Local,Localization.CurrentLanguage) });
        }
        return panel;
    }
}
