using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace WenMind.Daily;

internal sealed record TaskbarSnapshot(nint Handle, Rectangle Bounds, Rectangle Monitor, int Dpi)
{
    public int Pixels(int dip) => (int)Math.Round(dip * Dpi / 96.0);
}

internal static class TaskbarLayout
{
    private static readonly ControlType[] InteractiveTypes =
    [
        ControlType.Button, ControlType.CheckBox, ControlType.ComboBox, ControlType.Edit,
        ControlType.Hyperlink, ControlType.ListItem, ControlType.MenuItem, ControlType.RadioButton,
        ControlType.SplitButton, ControlType.TabItem,
    ];

    public static TaskbarSnapshot? Capture()
    {
        nint handle = Native.FindWindow("Shell_TrayWnd", null);
        if (handle == 0 || !Native.IsWindowVisible(handle) || Native.TaskbarAutoHides() ||
            !Native.GetWindowRect(handle, out var taskbar))
            return null;

        nint monitorHandle = Native.MonitorFromWindow(handle, 2);
        var monitor = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (monitorHandle == 0 || !Native.GetMonitorInfo(monitorHandle, ref monitor))
            return null;

        Rectangle bounds = taskbar.Rectangle;
        Rectangle screen = monitor.Monitor.Rectangle;
        int dpi = (int)Native.GetDpiForWindow(handle);
        if (dpi <= 0 || bounds.Bottom != screen.Bottom || bounds.Width < screen.Width / 2 ||
            bounds.Height < dpi / 4 || bounds.Height > screen.Height / 4 ||
            (monitor.Flags & 1) == 0)
            return null;

        if (Native.ForegroundCovers(screen, handle))
            return null;

        return new TaskbarSnapshot(handle, bounds, screen, dpi);
    }

    public static Rectangle? FindSlot(TaskbarSnapshot snapshot)
    {
        try
        {
            AutomationElement root = AutomationElement.FromHandle(snapshot.Handle);
            var request = new CacheRequest();
            request.Add(AutomationElement.AutomationIdProperty);
            request.Add(AutomationElement.BoundingRectangleProperty);
            request.Add(AutomationElement.IsOffscreenProperty);
            var condition = new OrCondition(
                InteractiveTypes.Select(type => new PropertyCondition(AutomationElement.ControlTypeProperty, type))
                    .Cast<Condition>()
                    .Append(new PropertyCondition(AutomationElement.ClassNameProperty, "TrayNotifyWnd"))
                    .ToArray());

            var occupied = new List<Rectangle>();
            bool startFound = false;
            bool trayFound = false;
            using (request.Activate())
            {
                foreach (AutomationElement item in root.FindAll(TreeScope.Descendants, condition))
                {
                    var element = item.Cached;
                    var rect = element.BoundingRectangle;
                    if (element.IsOffscreen || rect.IsEmpty)
                        continue;

                    Rectangle bounds = Rectangle.FromLTRB((int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
                        (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom));
                    if (!snapshot.Bounds.IntersectsWith(bounds))
                        continue;

                    startFound |= element.AutomationId == "StartButton";
                    trayFound |= element.AutomationId == "SystemTrayIcon";
                    occupied.Add(bounds);
                }
            }

            // Keep the overlay hidden when Explorer is rebuilding the taskbar or has an unknown layout.
            return startFound && trayFound ? FindFreeSpace(snapshot, occupied) : null;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or
            InvalidOperationException or ArgumentException or Win32Exception)
        {
            return null;
        }
    }

    internal static Rectangle? FindFreeSpace(TaskbarSnapshot snapshot, IEnumerable<Rectangle> occupied)
    {
        var bar = Rectangle.Intersect(snapshot.Bounds, snapshot.Monitor);
        int edge = snapshot.Pixels(8);
        int height = Math.Min(snapshot.Pixels(34), bar.Height - snapshot.Pixels(4));
        int minWidth = snapshot.Pixels(150);
        int maxWidth = snapshot.Pixels(286);
        int gapRight = bar.Right - edge;

        foreach (var item in occupied.Where(bar.IntersectsWith).OrderByDescending(bounds => bounds.Right))
        {
            int gapLeft = Math.Max(bar.Left + edge, item.Right + edge);
            if (gapRight - gapLeft >= minWidth)
                return Place(gapLeft, gapRight);

            gapRight = Math.Min(gapRight, item.Left - edge);
        }

        return gapRight - (bar.Left + edge) >= minWidth ? Place(bar.Left + edge, gapRight) : null;

        Rectangle Place(int left, int right)
        {
            int width = Math.Min(right - left, maxWidth);
            return new Rectangle(right - width, bar.Top + (bar.Height - height) / 2, width, height);
        }
    }
}

internal static class Native
{
    private static readonly nint Topmost = new(-1);

    public static bool PlaceWithoutActivating(nint window, Rectangle rectangle) =>
        SetWindowPos(window, Topmost, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, 0x0210);

    public static void PreventActivation(nint window) =>
        SetWindowLong(window, -20, GetWindowLong(window, -20) | 0x08000000);

    public static bool TaskbarAutoHides()
    {
        var data = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
        return (SHAppBarMessage(4, ref data) & 1) != 0;
    }

    public static bool ForegroundCovers(Rectangle monitor, nint taskbar)
    {
        nint window = GetForegroundWindow();
        if (window == 0 || window == taskbar || window == GetShellWindow())
            return false;

        if (DwmGetWindowAttribute(window, 9, out var bounds, Marshal.SizeOf<Rect>()) != 0 &&
            !GetWindowRect(window, out bounds))
            return false;

        const int tolerance = 2;
        return bounds.Left <= monitor.Left + tolerance && bounds.Top <= monitor.Top + tolerance &&
            bounds.Right >= monitor.Right - tolerance && bounds.Bottom >= monitor.Bottom - tolerance;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public nint Window;
        public uint CallbackMessage, Edge;
        public Rect Bounds;
        public nint Parameter;
    }

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindow(string className, string? title);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(nint handle);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(nint handle, out Rect bounds);

    [DllImport("user32.dll")]
    internal static extern int GetDpiForWindow(nint handle);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint handle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("shell32.dll")]
    private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out Rect bounds, int size);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint window, int index, int value);
}
