using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace WenMind.Daily;

internal static class Palette
{
    public static void Apply(ResourceDictionary resources)
    {
        if (SystemParameters.HighContrast)
        {
            resources["Card"] = SystemColors.WindowBrush;
            resources["Text"] = SystemColors.WindowTextBrush;
            resources["Secondary"] = SystemColors.WindowTextBrush;
            resources["Accent"] = SystemColors.WindowTextBrush;
            resources["Action"] = SystemColors.ControlBrush;
            resources["Divider"] = SystemColors.WindowTextBrush;
            resources["Hover"] = SystemColors.ControlBrush;
            return;
        }

        bool dark = Equals(Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 1), 0);
        resources["Card"] = Brush(dark ? "#182522" : "#F4F7F4");
        resources["Text"] = Brush(dark ? "#EBF5EE" : "#172720");
        resources["Secondary"] = Brush(dark ? "#AEC3B9" : "#486257");
        resources["Accent"] = Brush(dark ? "#A2DABC" : "#265E47");
        resources["Action"] = Brush(dark ? "#253C33" : "#DEEEE4");
        resources["Divider"] = Brush(dark ? "#40584D" : "#C6D9CD");
        resources["Hover"] = Brush(dark ? "#353C48" : "#DEE4E8");
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
