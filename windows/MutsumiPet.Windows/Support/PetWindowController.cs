using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MutsumiPet.Models;

namespace MutsumiPet.Support
{
    public enum WanderStepKind
    {
        Paused,
        Moving,
        Arrived
    }

    public struct WanderStepResult
    {
        public readonly WanderStepKind Kind;
        public readonly int Direction;
        public readonly double Distance;

        private WanderStepResult(WanderStepKind kind, int direction, double distance)
        {
            Kind = kind;
            Direction = direction;
            Distance = distance;
        }

        public static WanderStepResult Paused()
        {
            return new WanderStepResult(WanderStepKind.Paused, 0, 0);
        }

        public static WanderStepResult Arrived()
        {
            return new WanderStepResult(WanderStepKind.Arrived, 0, 0);
        }

        public static WanderStepResult Moving(int direction, double distance)
        {
            return new WanderStepResult(WanderStepKind.Moving, direction, distance);
        }
    }

    /// Owns everything about the pet's host window that WPF cannot express: the
    /// non-activating borderless style, the three z-order modes, and sub-pixel
    /// positioning while the pet walks.
    ///
    /// All positions handled here are in device-independent units (matching the
    /// macOS build's points) and converted to physical pixels only when calling
    /// SetWindowPos, so the walk constants stay identical across display scales.
    public sealed class PetWindowController
    {
        private const int HotkeyInteract = 0xB001;
        private const int HotkeyBubble = 0xB002;
        private const int HotkeyQuit = 0xB003;

        private readonly Window window;
        private readonly WanderMotion motion = new WanderMotion();

        private HwndSource source;
        private IntPtr handle = IntPtr.Zero;
        private WindowLayerMode layerMode = WindowLayerMode.Front;

        private double positionX;
        private double positionY;
        private bool placed;

        private Point? dragStartPosition;
        private Point? dragStartCursor;

        public PetWindowController(Window window)
        {
            this.window = window;
        }

        /// Raised for Ctrl+Alt+M, Ctrl+Alt+B and Ctrl+Alt+Q respectively. The macOS
        /// build hangs these off its menu bar, which an accessory-style Windows app
        /// does not have.
        public event Action InteractRequested;
        public event Action ToggleBubbleRequested;
        public event Action QuitRequested;

        public bool IsDragging
        {
            get { return dragStartPosition != null; }
        }

        public Point Position
        {
            get { return new Point(positionX, positionY); }
        }

        public double DpiScale
        {
            get
            {
                if (source != null && source.CompositionTarget != null)
                {
                    double scale = source.CompositionTarget.TransformToDevice.M11;
                    if (scale > 0) return scale;
                }
                return 1;
            }
        }

        /// The current monitor's work area (excludes the taskbar), in DIPs.
        public Rect WorkArea
        {
            get
            {
                if (handle != IntPtr.Zero)
                {
                    IntPtr monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
                    if (monitor != IntPtr.Zero)
                    {
                        var info = new NativeMethods.MONITORINFO();
                        info.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                        if (NativeMethods.GetMonitorInfo(monitor, ref info))
                        {
                            double scale = DpiScale;
                            NativeMethods.RECT work = info.rcWork;
                            return new Rect(
                                work.Left / scale,
                                work.Top / scale,
                                (work.Right - work.Left) / scale,
                                (work.Bottom - work.Top) / scale);
                        }
                    }
                }

                return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            }
        }

        /// The stage size in DIPs. Set by the view whenever the size preference
        /// changes, rather than read back from Win32, so placement never depends on
        /// whether WPF has finished applying the new size yet.
        public Size WindowSize { get; set; }

        public void Attach(WindowLayerMode mode)
        {
            source = PresentationSource.FromVisual(window) as HwndSource;
            if (source == null) return;

            handle = source.Handle;
            source.AddHook(WndProc);

            int exStyle = NativeMethods.GetWindowLongSafe(handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLongSafe(
                handle,
                NativeMethods.GWL_EXSTYLE,
                exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);

            RegisterHotkeys();
            UpdateLayerMode(mode);
        }

        public void Detach()
        {
            if (handle == IntPtr.Zero) return;
            NativeMethods.UnregisterHotKey(handle, HotkeyInteract);
            NativeMethods.UnregisterHotKey(handle, HotkeyBubble);
            NativeMethods.UnregisterHotKey(handle, HotkeyQuit);
            if (source != null) source.RemoveHook(WndProc);
        }

        public void UpdateLayerMode(WindowLayerMode mode)
        {
            layerMode = mode;
            if (handle == IntPtr.Zero) return;

            window.Topmost = mode == WindowLayerMode.Front;

            IntPtr target;
            switch (mode)
            {
                case WindowLayerMode.Front: target = NativeMethods.HWND_TOPMOST; break;
                case WindowLayerMode.Desktop: target = NativeMethods.HWND_BOTTOM; break;
                default: target = NativeMethods.HWND_NOTOPMOST; break;
            }

            NativeMethods.SetWindowPos(
                handle,
                target,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        public void PlaceInitial()
        {
            if (placed) return;
            placed = true;

            Rect visible = WorkArea;
            Size size = WindowSize;
            positionX = visible.Right - size.Width - 28;
            positionY = visible.Bottom - size.Height - 24;
            ApplyPosition();
        }

        public void SetPosition(double x, double y)
        {
            positionX = x;
            positionY = y;
            ApplyPosition();
        }

        /// Keeps the pet's feet where they were when the size preference changes,
        /// matching the macOS build where the window origin stays put and the frame
        /// grows upward.
        public void ResizeKeepingBottomEdge(double previousBottom)
        {
            Size size = WindowSize;
            positionY = previousBottom - size.Height;
            ClampIntoWorkArea();
            ApplyPosition();
        }

        public void ClampIntoWorkArea()
        {
            Rect visible = WorkArea;
            Size size = WindowSize;
            if (visible.Width <= 0 || visible.Height <= 0) return;

            double maxX = Math.Max(visible.Left, visible.Right - size.Width);
            double maxY = Math.Max(visible.Top, visible.Bottom - size.Height);
            positionX = Math.Min(Math.Max(positionX, visible.Left), maxX);
            positionY = Math.Min(Math.Max(positionY, visible.Top), maxY);
        }

        public void BeginDrag()
        {
            if (dragStartPosition != null) return;
            StopWandering();
            SyncPositionFromWindow();
            dragStartPosition = new Point(positionX, positionY);
            dragStartCursor = CursorPosition();
        }

        public void DragWindowToCurrentMouse()
        {
            if (dragStartPosition == null || dragStartCursor == null) return;

            Point cursor = CursorPosition();
            positionX = dragStartPosition.Value.X + cursor.X - dragStartCursor.Value.X;
            positionY = dragStartPosition.Value.Y + cursor.Y - dragStartCursor.Value.Y;
            ApplyPosition();
        }

        public void EndDrag()
        {
            dragStartPosition = null;
            dragStartCursor = null;
        }

        public void BeginWandering()
        {
            motion.ResetTarget();
        }

        public void StopWandering()
        {
            motion.ResetTarget();
        }

        public WanderStepResult AdvanceWandering(double speed, double timeStep)
        {
            if (dragStartPosition != null) return WanderStepResult.Paused();
            if (handle == IntPtr.Zero) return WanderStepResult.Paused();

            Rect visible = WorkArea;
            Size size = WindowSize;
            double minX = visible.Left;
            double maxX = Math.Max(minX, visible.Right - size.Width);
            double clampedY = Math.Min(
                Math.Max(positionY, visible.Top),
                Math.Max(visible.Top, visible.Bottom - size.Height));

            motion.EnsureTarget(positionX, minX, maxX);

            double previousX = positionX;
            WanderMotionStep step = motion.NextStep(previousX, speed * timeStep);
            switch (step.Kind)
            {
                case WanderMotionStepKind.Paused:
                    return WanderStepResult.Paused();
                case WanderMotionStepKind.Arrived:
                    SetPosition(step.X, clampedY);
                    return WanderStepResult.Arrived();
                default:
                    SetPosition(step.X, clampedY);
                    break;
            }

            double appliedDelta = positionX - previousX;
            if (Math.Abs(appliedDelta) <= 0.01)
            {
                motion.ResetTarget();
                return WanderStepResult.Arrived();
            }

            motion.RecordApplied(appliedDelta);
            return WanderStepResult.Moving(appliedDelta < 0 ? -1 : 1, Math.Abs(appliedDelta));
        }

        private Point CursorPosition()
        {
            NativeMethods.POINT point;
            if (NativeMethods.GetCursorPos(out point) == false) return new Point(0, 0);
            double scale = DpiScale;
            return new Point(point.X / scale, point.Y / scale);
        }

        private void SyncPositionFromWindow()
        {
            NativeMethods.RECT rectangle;
            if (handle == IntPtr.Zero) return;
            if (NativeMethods.GetWindowRect(handle, out rectangle) == false) return;
            double scale = DpiScale;
            positionX = rectangle.Left / scale;
            positionY = rectangle.Top / scale;
        }

        private void ApplyPosition()
        {
            if (handle == IntPtr.Zero) return;
            double scale = DpiScale;
            NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                (int)Math.Round(positionX * scale),
                (int)Math.Round(positionY * scale),
                0, 0,
                NativeMethods.SWP_NOSIZE
                    | NativeMethods.SWP_NOZORDER
                    | NativeMethods.SWP_NOACTIVATE
                    | NativeMethods.SWP_NOOWNERZORDER);
        }

        private void RegisterHotkeys()
        {
            const uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
            // Failure just means another app already owns the combination; the
            // right-click menu remains the primary way to reach every command.
            NativeMethods.RegisterHotKey(handle, HotkeyInteract, modifiers, 0x4D); // M
            NativeMethods.RegisterHotKey(handle, HotkeyBubble, modifiers, 0x42); // B
            NativeMethods.RegisterHotKey(handle, HotkeyQuit, modifiers, 0x51); // Q
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (message)
            {
                case NativeMethods.WM_WINDOWPOSCHANGING:
                    if (layerMode == WindowLayerMode.Desktop)
                    {
                        var position = (NativeMethods.WINDOWPOS)Marshal.PtrToStructure(
                            lParam, typeof(NativeMethods.WINDOWPOS));
                        if ((position.flags & NativeMethods.SWP_NOZORDER) == 0)
                        {
                            position.hwndInsertAfter = NativeMethods.HWND_BOTTOM;
                            Marshal.StructureToPtr(position, lParam, true);
                        }
                    }
                    break;

                case NativeMethods.WM_HOTKEY:
                    switch (wParam.ToInt32())
                    {
                        case HotkeyInteract:
                            Raise(InteractRequested);
                            handled = true;
                            break;
                        case HotkeyBubble:
                            Raise(ToggleBubbleRequested);
                            handled = true;
                            break;
                        case HotkeyQuit:
                            Raise(QuitRequested);
                            handled = true;
                            break;
                    }
                    break;

                case NativeMethods.WM_DISPLAYCHANGE:
                    StopWandering();
                    SyncPositionFromWindow();
                    ClampIntoWorkArea();
                    ApplyPosition();
                    break;

                case NativeMethods.WM_DPICHANGED:
                    // WPF resizes the window itself; re-read where it ended up so the
                    // walk continues from the correct place at the new scale.
                    StopWandering();
                    SyncPositionFromWindow();
                    break;
            }

            return IntPtr.Zero;
        }

        private static void Raise(Action handler)
        {
            if (handler != null) handler();
        }
    }
}
