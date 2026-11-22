using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        var occupied = new List<Rectangle>();
        bool complete = ReadButtons(snapshot, snapshot.Handle, occupied);
        if (!complete)
        {
            // Explorer can keep its controls in the input site while the outer taskbar exposes none.
            nint inputSite = Native.FindTaskbarInputSite(snapshot.Handle);
            if (inputSite != 0)
                complete = ReadButtons(snapshot, inputSite, occupied);
        }

        return Evaluate(snapshot, occupied, complete, previous);
    }

    private static bool ReadButtons(TaskbarSnapshot snapshot, nint handle, List<Rectangle> occupied)
    {
        try
        {
            AutomationElement root = AutomationElement.FromHandle(handle);
            var request = new CacheRequest();
            request.Add(AutomationElement.AutomationIdProperty);
            request.Add(AutomationElement.BoundingRectangleProperty);
            request.Add(AutomationElement.IsOffscreenProperty);
            var condition = new OrCondition(
                InteractiveTypes.Select(type => new PropertyCondition(AutomationElement.ControlTypeProperty, type))
                    .Cast<Condition>()
                    .Append(new PropertyCondition(AutomationElement.ClassNameProperty, "TrayNotifyWnd"))
                    .ToArray());

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

            return startFound && trayFound;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or
            InvalidOperationException or ArgumentException or Win32Exception)
        {
            return false;
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
}

internal static class Native
{
    private static readonly nint Topmost = new(-1);
    private const uint ForegroundEvent = 0x0003;
    private const uint ObjectHideEvent = 0x8003;
    private const uint SkipOwnProcess = 0x0002;

    internal delegate void WinEventCallback(nint hook, uint eventType, nint window, int objectId, int childId,
        uint threadId, uint eventTime);
    private delegate bool ChildWindowCallback(nint window, nint parameter);

    public static nint WatchForeground(WinEventCallback callback) =>
        SetWinEventHook(ForegroundEvent, ForegroundEvent, 0, callback, 0, 0, SkipOwnProcess);

    public static nint FindQuickSettings() => FindWindow("ControlCenterWindow", null);

    public static nint WatchWindowHide(nint window, WinEventCallback callback)
    {
        GetWindowThreadProcessId(window, out uint processId);
        return processId == 0 ? 0 : SetWinEventHook(ObjectHideEvent, ObjectHideEvent, 0, callback, processId, 0, 0);
    }

    public static nint FindTaskbarInputSite(nint taskbar)
    {
        nint found = 0;
        var className = new StringBuilder(96);
        EnumChildWindows(taskbar, (child, _) =>
        {
            GetClassName(child, className, className.Capacity);
            if (className.ToString() != "Windows.UI.Input.InputSite.WindowClass")
                return true;

            found = child;
            return false;
        }, 0);
        return found;
    }

    public static bool PlaceWithoutActivating(nint window, Rectangle rectangle) =>
        SetWindowPos(window, Topmost, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, 0x0210);

    public static bool OwnsCenterPoint(nint window, Rectangle bounds)
    {
        var point = new Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 };
        return window != 0 && GetAncestor(WindowFromPoint(point), 2) == window;
    }

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
    private struct Point
    {
        public int X, Y;
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
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(nint parent, ChildWindowCallback callback, nint parameter);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder className, int capacity);

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

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

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
