using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Rectangle = System.Drawing.Rectangle;

namespace WenMind.Daily;

internal partial class QuoteCard : Window
{
    private Quote? shown;
    private Rectangle work;
    private System.Drawing.Point anchor;

    public QuoteCard(Action browse, Action current)
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
        NextButton.Click += (_, _) => browse();
        TodayButton.Click += (_, _) => current();
        CopyButton.Click += (_, _) => Copy();
        SizeChanged += (_, _) => Position();
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                Close();
            }
        };
    }

    public void SetQuote(Quote? quote, bool preview, bool daily)
    {
        shown = quote;
        Heading.Text = preview ? "随手翻一条" : daily ? "今日一言" : "当前名言";
        FullText.Text = quote?.Text ?? "正在准备名言…";
        Attribution.Text = quote?.Attribution ?? "";
        Attribution.Visibility = Attribution.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Provider.Text = quote?.Provider ?? "";
        Provider.Visibility = quote is null ? Visibility.Collapsed : Visibility.Visible;
        NextButton.IsEnabled = CopyButton.IsEnabled = quote is not null;
        CopyButton.Content = "复制";
        TodayButton.Content = daily ? "回到今日" : "回到当前";
        TodayButton.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
    }

    public void OpenNear(Rectangle? strip)
    {
        anchor = strip is { } bounds
            ? new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top)
            : Forms.Cursor.Position;
        work = Forms.Screen.FromPoint(anchor).WorkingArea;
        nint handle = new WindowInteropHelper(this).EnsureHandle();
        var origin = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            .Transform(new Point(work.Left, work.Top));
        if (!IsVisible && origin is { } point)
        {
            Left = point.X;
            Top = point.Y;
            UpdateLayout();
        }

        // Move onto the intended monitor before it becomes visible so its DPI is applied.
        Position(handle);
        if (!IsVisible)
            Show();
        Position();
        Activate();
        CopyButton.Focus();
    }

    private void Position(nint handle = 0)
    {
        handle = handle == 0 ? new WindowInteropHelper(this).Handle : handle;
        if (handle == 0 || work.IsEmpty || !Native.GetWindowRect(handle, out var current))
            return;

        Rectangle window = current.Rectangle;
        if (window.Width <= 0 || window.Height <= 0)
            return;

        int gap = Math.Max(6, Native.GetDpiForWindow(handle) / 12);
        int left = Math.Clamp(anchor.X - window.Width / 2, work.Left + gap,
            Math.Max(work.Left + gap, work.Right - window.Width - gap));
        int top = Math.Max(work.Top + gap, work.Bottom - window.Height - gap);
        if (window.Left != left || window.Top != top)
            Native.PlaceWithoutActivating(handle, new Rectangle(left, top, window.Width, window.Height));
    }

    private void Copy()
    {
        if (shown is not { } quote)
            return;

        try
        {
            string suffix = quote.Attribution.Length == 0 ? "" : $"\n—— {quote.Attribution}";
            Clipboard.SetText(quote.Text + suffix);
            CopyButton.Content = "已复制";
        }
        catch (ExternalException)
        {
            CopyButton.Content = "重试复制";
        }
    }
}
