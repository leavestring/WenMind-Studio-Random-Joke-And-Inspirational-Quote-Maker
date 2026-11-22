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
    private QuoteCard? card;
    private RefreshSettingsWindow? settingsWindow;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem visibilityItem = null!;
    private Forms.ToolStripMenuItem startupItem = null!;
    private Icon? icon;
    private Mutex? instance;
    private DailyQuote? daily;
    private Quote? preview;
    private Rectangle? slot;
    private long lastPlacement;
    private AppSettings settings = new();
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
        settings = state.Read<AppSettings>(SettingsName) ?? new AppSettings();
        if (settings.RefreshMinutes is < AppSettings.MinimumMinutes or > AppSettings.MaximumMinutes)
            settings = settings with { RefreshMinutes = null };
        service = new QuoteService(state);
        strip = new QuoteStrip(OpenDetails);
        CreateTray();
        daily = service.ReadCache(DateTimeOffset.Now);
        if (daily is not null)
            ShowCurrentQuote(daily.Quote);
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
        var openItem = new Forms.ToolStripMenuItem("查看名言", null, (_, _) => OpenDetails());
        visibilityItem = new Forms.ToolStripMenuItem(VisibilityLabel, null, (_, _) => ToggleStrip());
        var settingsItem = new Forms.ToolStripMenuItem("设置…", null, (_, _) => OpenSettings());
        startupItem = new Forms.ToolStripMenuItem("登录时启动", null, (_, _) => ToggleLoginStartup())
        {
            Checked = LocalState.StartupEnabled(),
        };
        var exitItem = new Forms.ToolStripMenuItem("退出", null, (_, _) => Shutdown());
        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange([openItem, visibilityItem, settingsItem, startupItem, new Forms.ToolStripSeparator(), exitItem]);
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

    private string VisibilityLabel => settings.ShowStrip ? "隐藏任务栏文字" : "显示任务栏文字";

    private void ToggleStrip()
    {
        settings = settings with { ShowStrip = !settings.ShowStrip };
        if (!state.Write(SettingsName, settings))
            tray?.ShowBalloonTip(2500, "WenMind", "本次已切换，设置未能保存。", Forms.ToolTipIcon.Info);
        visibilityItem.Text = VisibilityLabel;
        Refresh();
    }

    private void OpenSettings()
    {
        if (settingsWindow is not null)
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new RefreshSettingsWindow(settings.RefreshMinutes, SaveRefreshMinutes);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private bool SaveRefreshMinutes(int? minutes)
    {
        AppSettings updated = settings with { RefreshMinutes = minutes };
        if (!state.Write(SettingsName, updated))
            return false;

        settings = updated;
        UpdateCard();
        Refresh();
        return true;
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
        _ = RefreshQuoteAsync();
        _ = RefreshPlacementAsync();
    }

    private async Task RefreshQuoteAsync()
    {
        if (loadingQuote || daily?.NeedsRefresh(DateTimeOffset.Now, settings.RefreshMinutes) == false)
            return;

        loadingQuote = true;
        try
        {
            Quote next = await service.FetchNextAsync(daily?.Quote, settings.RefreshMinutes is null, shutdown.Token);
            var now = DateTimeOffset.Now;
            if (shutdown.IsCancellationRequested || daily?.NeedsRefresh(now, settings.RefreshMinutes) == false)
                return;

            daily = new DailyQuote(DateOnly.FromDateTime(now.DateTime), next, now);
            service.Save(daily);
            ShowCurrentQuote(next);
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

    private void ShowCurrentQuote(Quote quote)
    {
        strip.SetQuote(quote);
        if (preview is null)
            UpdateCard();

        string text = quote.Text.ReplaceLineEndings(" ");
        if (text.Length > 58)
        {
            int cutoff = Array.FindLast(StringInfo.ParseCombiningCharacters(text), index => index <= 56);
            text = text[..cutoff] + "…";
        }
        if (tray is not null)
            tray.Text = text;
    }

    private async Task RefreshPlacementAsync()
    {
        TaskbarSnapshot? snapshot = settings.ShowStrip && daily is not null ? TaskbarLayout.Capture() : null;
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

            if (settings.ShowStrip && found is { } bounds && TaskbarLayout.Capture() == snapshot && strip.ShowAt(bounds))
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
        if (card is null)
        {
            card = new QuoteCard(Browse, ReturnToCurrent);
            card.Closed += (_, _) =>
            {
                card = null;
                preview = null;
            };
        }

        ReturnToCurrent();
        card.OpenNear(slot);
    }

    private void ReturnToCurrent()
    {
        preview = null;
        UpdateCard();
    }

    private void Browse()
    {
        if (daily is not { Quote: var quote })
            return;
        preview = QuoteService.Browse(quote, preview);
        UpdateCard();
    }

    private void UpdateCard() =>
        card?.SetQuote(preview ?? daily?.Quote, preview is not null, settings.RefreshMinutes is null);

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
}
