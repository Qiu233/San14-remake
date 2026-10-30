using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Channels;

public sealed record MovieVideoInfo(int Width, int Height, int FrameRateNumerator,
    int FrameRateDenominator, int PixelAspectNumerator, int PixelAspectDenominator,
    long Duration100ns);

public sealed record MovieAudioInfo(bool HasStream, int SampleRate, int Channels, long Duration100ns)
{
    public int SourceChannels { get; init; }
    public uint SourceChannelMask { get; init; }
}

/// <summary>
/// A top-down tightly packed RGBA32 frame. Dispose after copying/uploading its
/// pixels; Rgba belongs to this frame only until Dispose. Times use 100 ns units.
/// </summary>
public sealed class MovieVideoFrame : IDisposable
{
    private readonly Action<byte[]> returnBuffer;
    private int returned;
    public long Timestamp100ns { get; }
    public long Duration100ns { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] Rgba { get; }
    internal MovieVideoFrame(long timestamp100ns, long duration100ns, int width, int height,
        byte[] rgba, Action<byte[]> returnBuffer)
    {
        Timestamp100ns = timestamp100ns;
        Duration100ns = duration100ns;
        Width = width;
        Height = height;
        Rgba = rgba;
        this.returnBuffer = returnBuffer;
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref returned, 1) == 0) returnBuffer(Rgba);
    }
}

/// <summary>Interleaved mono/stereo float samples, decoded from source-rate PCM16.</summary>
public sealed record MovieAudioFrame(long Timestamp100ns, long Duration100ns,
    int SampleRate, int Channels, float[] InterleavedSamples);

/// <summary>
/// Windows Media Foundation streaming decode only. Each stream owns one dedicated
/// thread, COM apartment and reader. The caller owns the presentation clock and
/// audio/video devices. Cancellation never closes a reader under an active read.
/// </summary>
public sealed class WindowsMovieDecoder : IAsyncDisposable
{
    private readonly Channel<MovieVideoFrame> video = Channel.CreateBounded<MovieVideoFrame>(
        new BoundedChannelOptions(3) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Channel<MovieAudioFrame> audio = Channel.CreateBounded<MovieAudioFrame>(
        new BoundedChannelOptions(8) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly CancellationTokenSource stop;
    private readonly NativeRuntime native;
    private readonly object lifetime = new();
    private Task? disposal;
    private bool disposed;
    private readonly object videoBuffers = new();
    private readonly Stack<byte[]> availableVideoBuffers = new();
    private int videoBufferSize;
    private bool acceptingVideoBuffers = true;

    public MovieVideoInfo VideoInfo { get; private set; } = null!;
    public MovieAudioInfo AudioInfo { get; private set; } = null!;
    public Task VideoCompletion { get; }
    public Task AudioCompletion { get; }
    public Task Completion { get; }
    /// <summary>True only after successful decode completion and consumption of every queued frame.</summary>
    public bool VideoEndOfStream => VideoCompletion.IsCompletedSuccessfully && !video.Reader.TryPeek(out _);
    public bool AudioEndOfStream => AudioCompletion.IsCompletedSuccessfully && !audio.Reader.TryPeek(out _);

    private readonly TaskCompletionSource<NativeInfo> videoOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<NativeInfo> audioOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private WindowsMovieDecoder(string moviePath, string nativeLibraryPath, CancellationToken cancellationToken)
    {
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try { native = new NativeRuntime(nativeLibraryPath); }
        catch { stop.Dispose(); throw; }
        VideoCompletion = Task.Factory.StartNew(() => Decode(moviePath, 1, videoOpened),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        AudioCompletion = Task.Factory.StartNew(() => Decode(moviePath, 2, audioOpened),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Completion = Task.WhenAll(VideoCompletion, AudioCompletion);
    }

    /// <summary>
    /// Both paths are explicit absolute filesystem paths. The caller resolves its
    /// installed/source native runtime, so this reader never searches CWD or caches.
    /// It starts only a bounded decode prefetch, not playback.
    /// </summary>
    public static async Task<WindowsMovieDecoder> OpenAsync(string moviePath, string nativeLibraryPath,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The movie decoder requires Windows x64.");
        if (!Path.IsPathFullyQualified(moviePath) || !Path.IsPathFullyQualified(nativeLibraryPath))
            throw new ArgumentException("Movie and native decoder paths must be absolute.");
        cancellationToken.ThrowIfCancellationRequested();
        var result = new WindowsMovieDecoder(Path.GetFullPath(moviePath), Path.GetFullPath(nativeLibraryPath), cancellationToken);
        try
        {
            NativeInfo[] formats = await Task.WhenAll(result.videoOpened.Task, result.audioOpened.Task).ConfigureAwait(false);
            NativeInfo v = formats[0], a = formats[1];
            result.VideoInfo = new(v.Width, v.Height, v.FrameRateNumerator, v.FrameRateDenominator,
                v.PixelAspectNumerator, v.PixelAspectDenominator, v.Duration100ns);
            result.AudioInfo = new(a.HasStream != 0, a.SampleRate, a.Channels, a.Duration100ns)
            {
                SourceChannels = a.SourceChannels,
                SourceChannelMask = a.SourceChannelMask
            };
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            await result.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public bool TryReadVideo([NotNullWhen(true)] out MovieVideoFrame? frame) => video.Reader.TryRead(out frame);
    public bool TryReadAudio([NotNullWhen(true)] out MovieAudioFrame? frame) => audio.Reader.TryRead(out frame);

    public void Cancel()
    {
        lock (lifetime) { if (!disposed) stop.Cancel(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetime) { return new(disposal ??= DisposeCoreAsync()); }
    }

    private async Task DisposeCoreAsync()
    {
        stop.Cancel();
        // A synchronous local-file ReadSample may finish after cancellation.
        // Joining its actual owner thread is mandatory before unloading the DLL.
        try { await Completion.ConfigureAwait(false); }
        catch { /* Decode errors remain observable on the public completion tasks. */ }
        lock (lifetime)
        {
            disposed = true;
            native.Dispose();
            stop.Dispose();
        }
        while (video.Reader.TryRead(out MovieVideoFrame? frame)) frame.Dispose();
        while (audio.Reader.TryRead(out _)) { }
        lock (videoBuffers)
        {
            acceptingVideoBuffers = false;
            availableVideoBuffers.Clear();
        }
    }

    private byte[] RentVideoBuffer(int size)
    {
        lock (videoBuffers)
        {
            if (videoBufferSize != size)
            {
                videoBufferSize = size;
                availableVideoBuffers.Clear();
            }
            return availableVideoBuffers.TryPop(out byte[]? buffer) ? buffer : new byte[size];
        }
    }

    private void ReturnVideoBuffer(byte[] buffer)
    {
        lock (videoBuffers)
        {
            if (acceptingVideoBuffers && buffer.Length == videoBufferSize && availableVideoBuffers.Count < 5)
                availableVideoBuffers.Push(buffer);
        }
    }

    private void Decode(string path, int kind, TaskCompletionSource<NativeInfo> opened)
    {
        nint handle = 0;
        Exception? failure = null;
        CancellationToken cancellation = stop.Token;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            ThrowNative(native.Open(path, kind, out handle, out NativeInfo info), "open", kind);
            opened.TrySetResult(info);
            while (info.HasStream != 0)
            {
                cancellation.ThrowIfCancellationRequested();
                int status = native.Read(handle, out NativePacket packet);
                if (status == 1) break; // S_FALSE is actual source EOS, not an empty/stalled packet.
                ThrowNative(status, "read", kind);
                cancellation.ThrowIfCancellationRequested();
                if (kind == 1)
                {
                    byte[] rgba = RentVideoBuffer(packet.Bytes);
                    try
                    {
                        Marshal.Copy(packet.Data, rgba, 0, rgba.Length);
                        var frame = new MovieVideoFrame(packet.Timestamp100ns, packet.Duration100ns,
                            packet.Format.Width, packet.Format.Height, rgba, ReturnVideoBuffer);
                        video.Writer.WriteAsync(frame, cancellation).AsTask().GetAwaiter().GetResult();
                    }
                    catch { ReturnVideoBuffer(rgba); throw; }
                }
                else
                {
                    short[] pcm = new short[packet.Bytes / 2];
                    Marshal.Copy(packet.Data, pcm, 0, pcm.Length);
                    float[] samples = new float[pcm.Length];
                    for (int i = 0; i < pcm.Length; ++i) samples[i] = pcm[i] / 32768f;
                    var frame = new MovieAudioFrame(packet.Timestamp100ns, packet.Duration100ns,
                        packet.Format.SampleRate, packet.Format.Channels, samples);
                    audio.Writer.WriteAsync(frame, cancellation).AsTask().GetAwaiter().GetResult();
                }
            }
        }
        catch (OperationCanceledException error) when (cancellation.IsCancellationRequested)
        {
            failure = error;
            opened.TrySetCanceled(cancellation);
            throw;
        }
        catch (Exception error)
        {
            failure = error;
            opened.TrySetException(error);
            // Cancel the sibling to avoid a full queue keeping an already failed
            // decoder session alive. Its reader still closes on its own thread.
            stop.Cancel();
            throw;
        }
        finally
        {
            if (handle != 0) native.Close(handle);
            if (kind == 1) video.Writer.TryComplete(failure);
            else audio.Writer.TryComplete(failure);
        }
    }

    private static void ThrowNative(int result, string operation, int kind)
    {
        if (result < 0)
            throw new IOException($"Movie {(kind == 1 ? "video" : "audio")} {operation} failed (0x{result:X8}).",
                Marshal.GetExceptionForHR(result));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInfo
    {
        public int HasStream, Kind, Width, Height, FrameRateNumerator, FrameRateDenominator,
            PixelAspectNumerator, PixelAspectDenominator, SampleRate, Channels;
        public long Duration100ns;
        public int SourceChannels;
        public uint SourceChannelMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePacket
    {
        public nint Data;
        public int Bytes, Reserved;
        public long Timestamp100ns, Duration100ns;
        public NativeInfo Format;
    }

    private sealed class NativeRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal delegate int OpenCall([MarshalAs(UnmanagedType.LPWStr)] string path, int kind,
            out nint handle, out NativeInfo info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int ReadCall(nint handle, out NativePacket packet);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int CloseCall(nint handle);
        private readonly nint library;
        internal readonly OpenCall Open;
        internal readonly ReadCall Read;
        internal readonly CloseCall Close;
        internal NativeRuntime(string path)
        {
            library = NativeLibrary.Load(path);
            try
            {
                Open = Marshal.GetDelegateForFunctionPointer<OpenCall>(NativeLibrary.GetExport(library, "san14_movie_open_v1"));
                Read = Marshal.GetDelegateForFunctionPointer<ReadCall>(NativeLibrary.GetExport(library, "san14_movie_read_v1"));
                Close = Marshal.GetDelegateForFunctionPointer<CloseCall>(NativeLibrary.GetExport(library, "san14_movie_close_v1"));
            }
            catch { NativeLibrary.Free(library); throw; }
        }
        public void Dispose() => NativeLibrary.Free(library);
    }
}
