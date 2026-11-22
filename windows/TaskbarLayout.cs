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

internal readonly record struct TaskbarScan(bool Complete, Rectangle? Slot, bool PreviousSlotOccupied);

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

    public static TaskbarScan FindSlot(TaskbarSnapshot snapshot, Rectangle? previous)
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

            return Evaluate(snapshot, occupied, startFound && trayFound, previous);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or
            InvalidOperationException or ArgumentException or Win32Exception)
        {
            return default;
        }
    }

    internal static TaskbarScan Evaluate(TaskbarSnapshot snapshot, List<Rectangle> occupied,
        bool complete, Rectangle? previous)
    {
        if (complete)
            return new TaskbarScan(true, FindFreeSpace(snapshot, occupied), false);

        // A partial tree may still show that the old slot has been taken by a button.
        bool overlaps = previous is { } oldSlot && occupied.Exists(oldSlot.IntersectsWith);
        return new TaskbarScan(false, null, overlaps);
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

    internal static Rectangle? FindSpaceBesideQuickSettings(TaskbarSnapshot snapshot, Rectangle slot, Rectangle panel)
    {
        int gap = snapshot.Pixels(8);
        int top = snapshot.Bounds.Top - gap - slot.Height;
        if (top < snapshot.Monitor.Top + gap)
            return null;

        int leftEdge = snapshot.Monitor.Left + gap;
        int rightEdge = snapshot.Monitor.Right - gap;
        var stripArea = Rectangle.FromLTRB(leftEdge, top, rightEdge, top + slot.Height);
        panel.Inflate(gap, gap);
        if (!stripArea.IntersectsWith(panel))
            return Place(leftEdge, rightEdge);

        Rectangle? left = Place(leftEdge, Math.Min(rightEdge, panel.Left));
        Rectangle? right = Place(Math.Max(leftEdge, panel.Right), rightEdge);
        if (left is not { } leftSlot || right is not { } rightSlot)
            return left ?? right;

        return Distance(leftSlot) <= Distance(rightSlot) ? leftSlot : rightSlot;

        Rectangle? Place(int left, int right)
        {
            int width = Math.Min(slot.Width, right - left);
            if (width < snapshot.Pixels(150))
                return null;

            return new Rectangle(Math.Clamp(slot.Left, left, right - width), top, width, slot.Height);
        }

        int Distance(Rectangle candidate) =>
            Math.Abs((candidate.Left + candidate.Right) - (slot.Left + slot.Right));
    }
}

internal static class Native
{
    private static readonly nint Topmost = new(-1);
    private const uint ForegroundEvent = 0x0003;
    private const uint SkipOwnProcess = 0x0002;
    private const int ExtendedFrameBounds = 9;

    internal delegate void WinEventCallback(nint hook, uint eventType, nint window, int objectId, int childId,
        uint threadId, uint eventTime);

    public static nint WatchForeground(WinEventCallback callback) =>
        SetWinEventHook(ForegroundEvent, ForegroundEvent, 0, callback, 0, 0, SkipOwnProcess);

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

        if (!TryGetFrame(window, out var bounds))
            return false;

        const int tolerance = 2;
        return bounds.Left <= monitor.Left + tolerance && bounds.Top <= monitor.Top + tolerance &&
            bounds.Right >= monitor.Right - tolerance && bounds.Bottom >= monitor.Bottom - tolerance;
    }

    public static Rectangle? QuickSettingsBounds(Rectangle monitor)
    {
        nint panel = FindWindow("ControlCenterWindow", null);
        if (panel == 0 || !IsWindowVisible(panel) || !TryGetFrame(panel, out var bounds))
            return null;

        Rectangle frame = bounds.Rectangle;
        return frame.IntersectsWith(monitor) ? frame : null;
    }

    private static bool TryGetFrame(nint window, out Rect bounds) =>
        DwmGetWindowAttribute(window, ExtendedFrameBounds, out bounds, Marshal.SizeOf<Rect>()) == 0 ||
        GetWindowRect(window, out bounds);

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

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventCallback callback,
        uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(nint hook);
}
