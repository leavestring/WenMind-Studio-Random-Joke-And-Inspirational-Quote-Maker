using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace WenMind.Daily;

public partial class App : Application
{
    private const string SettingsName = "settings.json";
    private readonly LocalState state = new();
    private readonly CancellationTokenSource shutdown = new();
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(900),
    };
    private QuoteService service = null!;
    private QuoteStrip strip = null!;
    private QuoteCard card = null!;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem visibilityItem = null!;
    private Forms.ToolStripMenuItem startupItem = null!;
    private Icon? icon;
    private Mutex? instance;
    private DailyQuote? daily;
    private Quote? preview;
    private Rectangle? slot;
    private long lastPlacement;
    private bool showStrip;
    private bool checkingPlacement;
    private bool loadingQuote;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(initiallyOwned: false, @"Local\WenMind.Daily", out bool first);
        if (!first)
        {
            Shutdown();
            return;
        }

        Palette.Apply(Resources);
        showStrip = state.Read<Settings>(SettingsName)?.ShowStrip ?? true;
        service = new QuoteService(state);
        strip = new QuoteStrip(OpenDetails);
        card = new QuoteCard(Browse, BackToToday);
        CreateTray();
        SystemEvents.UserPreferenceChanged += ThemeChanged;
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();
    }

    private void CreateTray()
    {
        using var resource = GetResourceStream(new Uri("wenmind.ico", UriKind.Relative)).Stream;
        using var original = new Icon(resource);
        icon = (Icon)original.Clone();
        var openItem = new Forms.ToolStripMenuItem("查看今日一言", null, (_, _) => OpenDetails());
        visibilityItem = new Forms.ToolStripMenuItem(VisibilityLabel, null, (_, _) => ToggleStrip());
        startupItem = new Forms.ToolStripMenuItem("登录时启动", null, (_, _) => ToggleLoginStartup())
        {
            Checked = LocalState.StartupEnabled(),
        };
        var exitItem = new Forms.ToolStripMenuItem("退出", null, (_, _) => Shutdown());
        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange([openItem, visibilityItem, startupItem, new Forms.ToolStripSeparator(), exitItem]);
        menu.Opening += (_, _) => startupItem.Checked = LocalState.StartupEnabled();
        tray = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "WenMind 每日一言",
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                OpenDetails();
        };
    }

    private string VisibilityLabel => showStrip ? "隐藏任务栏文字" : "显示任务栏文字";

    private void ToggleStrip()
    {
        showStrip = !showStrip;
        if (!state.Write(SettingsName, new Settings(showStrip)))
            tray?.ShowBalloonTip(2500, "WenMind", "本次已切换，设置未能保存。", Forms.ToolTipIcon.Info);
        visibilityItem.Text = VisibilityLabel;
        Refresh();
    }

    private void ToggleLoginStartup()
    {
        if (LocalState.SetStartup(!LocalState.StartupEnabled()))
            startupItem.Checked = LocalState.StartupEnabled();
        else
            tray?.ShowBalloonTip(2500, "WenMind", "登录启动设置未能保存。", Forms.ToolTipIcon.Info);
    }

    private void Refresh()
    {
        _ = RefreshDailyAsync();
        _ = RefreshPlacementAsync();
    }

    private async Task RefreshDailyAsync()
    {
        var date = DateOnly.FromDateTime(DateTime.Now);
        if (loadingQuote || daily?.Date == date)
            return;

        loadingQuote = true;
        try
        {
            DailyQuote loaded = await service.LoadAsync(date, shutdown.Token);
            if (shutdown.IsCancellationRequested || loaded.Date != DateOnly.FromDateTime(DateTime.Now))
                return;

            daily = loaded;
            preview = null;
            strip.SetQuote(loaded.Quote);
            card.SetQuote(loaded.Quote, preview: false);
            string text = loaded.Quote.Text.ReplaceLineEndings(" ");
            if (text.Length > 58)
            {
                int cutoff = Array.FindLast(StringInfo.ParseCombiningCharacters(text), index => index <= 56);
                text = text[..cutoff] + "…";
            }
            if (tray is not null)
                tray.Text = text;
            await RefreshPlacementAsync();
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            loadingQuote = false;
        }
    }

    private async Task RefreshPlacementAsync()
    {
        TaskbarSnapshot? snapshot = showStrip && daily is not null ? TaskbarLayout.Capture() : null;
        if (snapshot is null || Stopwatch.GetElapsedTime(lastPlacement) > TimeSpan.FromSeconds(2.5))
            HideStrip();
        if (snapshot is null || checkingPlacement)
            return;

        checkingPlacement = true;
        try
        {
            Rectangle? found = await Task.Run(() => TaskbarLayout.FindSlot(snapshot));
            if (shutdown.IsCancellationRequested)
                return;

            if (showStrip && found is { } bounds && TaskbarLayout.Capture() == snapshot && strip.ShowAt(bounds))
            {
                slot = bounds;
                lastPlacement = Stopwatch.GetTimestamp();
            }
            else
            {
                HideStrip();
            }
        }
        finally
        {
            checkingPlacement = false;
        }
    }

    private void HideStrip()
    {
        if (strip.IsVisible)
            strip.Hide();
        slot = null;
    }

    private void OpenDetails()
    {
        BackToToday();
        card.OpenNear(slot);
    }

    private void BackToToday()
    {
        preview = null;
        card.SetQuote(daily?.Quote, preview: false);
    }

    private void Browse()
    {
        if (daily is not { Quote: var quote })
            return;
        preview = QuoteService.Browse(quote, preview);
        card.SetQuote(preview, preview: true);
    }

    private void ThemeChanged(object sender, UserPreferenceChangedEventArgs e) =>
        _ = Dispatcher.InvokeAsync(() => Palette.Apply(Resources));

    protected override void OnExit(ExitEventArgs e)
    {
        timer.Stop();
        SystemEvents.UserPreferenceChanged -= ThemeChanged;
        shutdown.Cancel();
        if (tray is not null)
        {
            tray.Visible = false;
            tray.ContextMenuStrip?.Dispose();
            tray.Dispose();
        }

        icon?.Dispose();
        service?.Dispose();
        instance?.Dispose();
        shutdown.Dispose();
        base.OnExit(e);
    }

    private sealed record Settings(bool ShowStrip);
}
