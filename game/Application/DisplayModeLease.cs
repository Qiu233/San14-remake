using Godot;
using System.Diagnostics;
using System.Globalization;

/// <summary>One lazily created hidden helper owns the temporary mode of one
/// monitor for this game process. Closing its pipe or this process restores it.
/// There is no system service, installation, persistent setting or task.</summary>
internal sealed class DisplayModeLease : IDisposable
{
    private readonly Process process;
    private bool disposed;

    internal static string? FindGuard()
    {
        string installed = Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!,
            "native", "windows-x64", "display", "San14.DisplayMode.Guard.exe");
        if (File.Exists(installed)) return installed;
        string development = ProjectSettings.GlobalizePath("res://../native/San14.DisplayMode.Guard/bin/x64/Release/San14.DisplayMode.Guard.exe");
        return File.Exists(development) ? development : null;
    }

    internal DisplayModeLease(string guard, string device, nint window)
    {
        var start = new ProcessStartInfo(guard)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true
        };
        start.ArgumentList.Add(System.Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(((nuint)window).ToString("X", CultureInfo.InvariantCulture));
        start.ArgumentList.Add(device);
        process = Process.Start(start) ?? throw new InvalidOperationException("environment.display.mode_guard_start");
        try { ReadReply(); }
        catch { Dispose(); throw; }
    }

    internal void Apply(int width, int height) => Send(FormattableString.Invariant($"A {width} {height}"));
    internal void Restore() => Send("R");

    internal Vector2I ReadDesktopExtent()
    {
        Write("D");
        string[] parts = (ReadText() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "0" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int height) ||
            width is <= 0 or > 32768 || height is <= 0 or > 32768)
            throw new InvalidOperationException("environment.display.mode_guard_desktop_extent");
        return new(width, height);
    }

    private void Send(string command)
    {
        Write(command);
        ReadReply();
    }

    private void Write(string command)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        process.StandardInput.WriteLine(command);
        process.StandardInput.Flush();
    }

    private void ReadReply()
    {
        string? text = ReadText();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result != 0)
            throw new InvalidOperationException("environment.display.mode_guard_response:" + (text ?? "closed"));
    }

    private string? ReadText()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return process.StandardOutput.ReadLineAsync(deadline.Token).AsTask().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (disposed) return;
        // Do not kill the guard: EOF makes its actual owner restore before
        // exiting, and its independent parent handle covers forced game exit.
        disposed = true;
        try { process.StandardInput.Dispose(); }
        catch (IOException) { }
        // EOF is acknowledged by process retirement before another settings
        // transition can acquire this same monitor's named ownership mutex.
        try { process.WaitForExit(5000); }
        catch (InvalidOperationException) { }
        process.Dispose();
    }
}
