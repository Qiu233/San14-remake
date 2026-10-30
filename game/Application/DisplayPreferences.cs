using Godot;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

internal enum GameWindowMode { Fullscreen, Windowed, Borderless }

internal sealed record DisplayPreferences(
    [property: JsonRequired] GameWindowMode Mode,
    [property: JsonRequired] int Width,
    [property: JsonRequired] int Height)
{
    internal void Validate()
    {
        if (!Enum.IsDefined(Mode) || Width is <= 0 or > 32768 || Height is <= 0 or > 32768)
            throw new InvalidDataException("Application display preferences are invalid.");
    }
    internal Vector2I Extent => new(Width, Height);
}

internal sealed record DisplayResolution(int Width, int Height, bool Windowed, bool Fullscreen)
{
    internal bool Supports(GameWindowMode mode) => mode == GameWindowMode.Fullscreen ? Fullscreen : Windowed;
}

internal sealed record DisplayWindowSnapshot(DisplayServer.WindowMode Mode, bool Borderless,
    Vector2I Size, Vector2I Position, int Screen, DisplayPreferences? Requested = null)
{
    internal DisplayPreferences Preferences => Requested ?? new(
        Mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen
            ? GameWindowMode.Fullscreen : Borderless ? GameWindowMode.Borderless : GameWindowMode.Windowed,
        Size.X, Size.Y);
}

/// <summary>The main window owns one current-monitor catalog and, only when
/// needed, a temporary physical display-mode lease. Settings visits borrow it;
/// focus loss suspends the mode and final retirement restores the desktop.</summary>
internal sealed class DisplaySettingsHost : IDisposable
{
    private readonly Window window;
    private readonly nint windowHandle;
    private readonly string? guardPath;
    private int screen;
    private string device = "";
    private Vector2I desktopSize;
    private DisplayModeLease? lease;
    private DisplayPreferences? requested;
    private bool updating, disposed;
    internal IReadOnlyList<DisplayResolution> Resolutions { get; private set; } = [];

    private DisplaySettingsHost(Window window, nint handle)
    {
        this.window = window; windowHandle = handle; guardPath = DisplayModeLease.FindGuard();
        RefreshPublication();
        window.FocusExited += Suspend;
        window.FocusEntered += Resume;
        window.TreeExiting += Dispose;
    }

    internal static DisplaySettingsHost? Create(Window window)
    {
        // Must precede any Window handle, monitor, process or Win32 query.
        if (!OperatingSystem.IsWindows() || DisplayServer.GetName() == "headless") return null;
        nint handle = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        return new(window, handle);
    }

    internal void RefreshPublication()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), DeviceName = "" };
        if (!GetMonitorInfo(MonitorFromWindow(windowHandle, 2), ref monitor))
            throw new InvalidOperationException("Current display monitor could not be read.");
        bool sameMonitor = device == monitor.DeviceName;
        if (!sameMonitor)
        {
            ReleaseLease(); requested = null; device = monitor.DeviceName;
        }
        screen = DisplayServer.WindowGetCurrentScreen();
        // Query the real baseline once per settings visit/focus transition,
        // rather than publishing the temporary game mode as desktop capacity.
        desktopSize = lease?.ReadDesktopExtent() ?? DisplayServer.ScreenGetSize(screen);
        var rows = new List<DisplayResolution>();
        var seen = new HashSet<(int, int)>();
        for (int index = 0;; index++)
        {
            var mode = new DeviceMode { Size = (ushort)Marshal.SizeOf<DeviceMode>() };
            if (!EnumDisplaySettings(device, index, ref mode)) break;
            // Preserve the original admission, enumeration order and 720p
            // window exception. The helper uses these actual driver modes.
            if (mode.Width < 1280 || mode.Height < 720 || mode.BitsPerPixel != 32 ||
                (mode.DisplayFlags & 2) != 0 || !seen.Add((mode.Width, mode.Height))) continue;
            bool windowed = mode.Width <= desktopSize.X && mode.Height <= desktopSize.Y ||
                mode.Width == 1280 && mode.Height == 720;
            bool fullscreen = mode.Width == desktopSize.X && mode.Height == desktopSize.Y || guardPath is not null;
            rows.Add(new(mode.Width, mode.Height, windowed, fullscreen));
        }
        Resolutions = rows;
    }

    internal DisplayWindowSnapshot Capture() => new(DisplayServer.WindowGetMode(),
        DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless),
        DisplayServer.WindowGetSize(), DisplayServer.WindowGetPosition(), DisplayServer.WindowGetCurrentScreen(),
        requested?.Mode == GameWindowMode.Fullscreen ? requested : null);

    internal bool CanApply(DisplayPreferences value) => !disposed && Resolutions.Any(row =>
        row.Width == value.Width && row.Height == value.Height && row.Supports(value.Mode));

    internal void Apply(DisplayPreferences value)
    {
        value.Validate();
        if (!CanApply(value)) throw new InvalidOperationException("environment.display.current_host_mode_extent");
        if (updating) throw new InvalidOperationException("environment.display.mode_transition_active");
        updating = true;
        try
        {
            bool physicalMode = value.Mode == GameWindowMode.Fullscreen && value.Extent != desktopSize;
            // A timeout/cancel while Alt+Tabbed changes only the desired owner.
            // Regain focus reacquires it; never seize another app's monitor.
            if (value.Mode == GameWindowMode.Fullscreen && !window.HasFocus())
            {
                lease?.Restore(); requested = value; return;
            }
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            if (physicalMode)
            {
                lease ??= new DisplayModeLease(guardPath ?? throw new InvalidOperationException(
                    "environment.display.mode_guard_missing"), device, windowHandle);
                lease.Apply(value.Width, value.Height);
            }
            else lease?.Restore();
            DisplayServer.WindowSetCurrentScreen(screen);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, value.Mode == GameWindowMode.Borderless);
            if (value.Mode == GameWindowMode.Fullscreen)
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
            else
            {
                DisplayServer.WindowSetSize(value.Extent);
                DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition(screen) +
                    (DisplayServer.ScreenGetSize(screen) - value.Extent) / 2);
            }
            requested = value;
        }
        catch
        {
            // EOF remains an independent restore path if this reply failed.
            ReleaseLease(); throw;
        }
        finally { updating = false; }
    }

    internal void Restore(DisplayWindowSnapshot snapshot)
    {
        if (snapshot.Requested is { Mode: GameWindowMode.Fullscreen } desired)
        {
            Apply(desired); return;
        }
        lease?.Restore(); requested = null;
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetCurrentScreen(snapshot.Screen);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, snapshot.Borderless);
        DisplayServer.WindowSetSize(snapshot.Size);
        DisplayServer.WindowSetPosition(snapshot.Position);
        DisplayServer.WindowSetMode(snapshot.Mode);
    }

    private void Suspend()
    {
        if (disposed || updating || lease is null) return;
        try { lease.Restore(); }
        catch (Exception error) { ReleaseLease(); GD.PushWarning(error.ToString()); }
    }

    private void Resume()
    {
        if (disposed || updating) return;
        try
        {
            RefreshPublication();
            if (requested is { Mode: GameWindowMode.Fullscreen } desired && desired.Extent != desktopSize)
                Apply(desired);
        }
        catch (Exception error) { GD.PushWarning(error.ToString()); }
    }

    private void ReleaseLease()
    {
        var old = lease; lease = null;
        old?.Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (GodotObject.IsInstanceValid(window))
        {
            window.FocusExited -= Suspend; window.FocusEntered -= Resume; window.TreeExiting -= Dispose;
        }
        // Normal shutdown gets a real restore reply before releasing the pipe.
        // The hidden parent-handle watcher additionally covers forced exits.
        try { lease?.Restore(); }
        catch (Exception error) { GD.PushWarning(error.ToString()); }
        finally { ReleaseLease(); requested = null; }
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 220)]
    private struct DeviceMode
    {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(168)] public int BitsPerPixel;
        [FieldOffset(172)] public int Width;
        [FieldOffset(176)] public int Height;
        [FieldOffset(180)] public int DisplayFlags;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public int Left, Top, Right, Bottom, WorkLeft, WorkTop, WorkRight, WorkBottom;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplaySettings(string device, int index, ref DeviceMode mode);
}
