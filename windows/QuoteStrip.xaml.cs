using System;
using System.Drawing;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;

namespace WenMind.Daily;

internal partial class QuoteStrip : Window
{
    public QuoteStrip(Action open)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.PreventActivation(new WindowInteropHelper(this).Handle);
        QuoteButton.Click += (_, _) => open();
    }

    public void SetQuote(Quote quote)
    {
        Excerpt.Text = quote.Text.ReplaceLineEndings(" ");
        AutomationProperties.SetName(QuoteButton, $"查看名言，{Excerpt.Text}");
    }

    public bool ShowAt(Rectangle slot, bool floating)
    {
        QuoteButton.Tag = floating ? "floating" : null;
        nint handle = new WindowInteropHelper(this).EnsureHandle();
        if (!Native.PlaceWithoutActivating(handle, slot))
            return false;
        if (!IsVisible)
            Show();
        return true;
    }
}
