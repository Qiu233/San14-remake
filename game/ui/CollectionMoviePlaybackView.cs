using Godot;
using San14.Data;

/// <summary>A bounded WMV stream presented on the existing Godot canvas. The
/// audio mixer consumes the movie's samples; source timestamps drive video
/// and scenario captions. No original executable or external player is used.</summary>
internal partial class CollectionMoviePlaybackView : Control
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly Vector2[] audioScratch = new Vector2[4096];
    private WindowsMovieDecoder? decoder;
    private MovieVideoFrame? pendingVideo;
    private MovieAudioFrame? pendingAudio;
    private int audioOffset, audioCapacity;
    private long writtenAudioFrames, lastVideoEnd;
    private AudioStreamGenerator? generator;
    private AudioStreamGeneratorPlayback? audioPlayback;
    private AudioStreamPlayer? audioPlayer;
    private TextureRect image = null!;
    private ImageTexture? texture;
    private CollectionMovieSubtitleView? subtitles;
    private Func<bool>? admitted;
    private Func<float>? volume;
    private Action? returned;
    private Action<Exception>? failed;
    private Task? opening, retirement;
    private bool abandoned, started, audioDraining, completing;
    private ulong clockStart, drainStart;
    private double clock, drainClock;
    private float pictureAspect = 16f / 9;
    internal Task Retirement => retirement ?? Task.CompletedTask;
    internal bool PlaybackActive => started && !abandoned && !completing && decoder is not null;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionMoviePlayback publication, string nativeLibrary, Func<bool> admitted,
        Func<float> volume, Action returned, Action<Exception> failed, AdministrationPageChrome.Resources? resources = null)
    {
        this.admitted = admitted; this.volume = volume; this.returned = returned; this.failed = failed;
        Name = "CollectionMoviePlayback"; ZIndex = 20; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        image = new() { Name = "OriginalMovieFrame", MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        AddChild(image);
        if (publication.Subtitles.Count != 0)
        {
            subtitles = new(); AddChild(subtitles);
            subtitles.Configure(repository, text, publication, resources);
        }
        SetMeta("movie_id", publication.MovieId); SetMeta("movie_resource", publication.ResourceUri);
        Resized += Fit; Fit();
        opening = OpenAsync(publication.PhysicalPath, nativeLibrary);
    }

    private async Task OpenAsync(string path, string nativeLibrary)
    {
        WindowsMovieDecoder? opened = null;
        try
        {
            opened = await WindowsMovieDecoder.OpenAsync(path, nativeLibrary, lifetime.Token);
            if (abandoned) { await opened.DisposeAsync(); return; }
            decoder = opened;
            var video = opened.VideoInfo;
            pictureAspect = video.Width * (video.PixelAspectNumerator / (float)video.PixelAspectDenominator) / video.Height;
            Fit();
            if (opened.AudioInfo.HasStream)
            {
                generator = new() { MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom,
                    MixRate = opened.AudioInfo.SampleRate, BufferLength = .5f };
                audioPlayer = new() { Name = "OriginalMovieAudio", Stream = generator, StreamPaused = true };
                AddChild(audioPlayer); audioPlayer.Play();
                audioPlayer.StreamPaused = true;
                audioPlayback = (AudioStreamGeneratorPlayback)audioPlayer.GetStreamPlayback();
                audioCapacity = audioPlayback.GetFramesAvailable();
            }
            SetMeta("decoder_open", true);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { if (!abandoned) Fail(error); }
    }

    public override void _Process(double delta)
    {
        if (abandoned || completing || decoder is not { } source) return;
        try
        {
            if (source.VideoCompletion.IsFaulted) source.VideoCompletion.GetAwaiter().GetResult();
            if (source.AudioCompletion.IsFaulted) source.AudioCompletion.GetAwaiter().GetResult();
            if (pendingVideo is null) source.TryReadVideo(out pendingVideo);
            FillAudio();
            if (!started)
            {
                if (pendingVideo is null || source.AudioInfo.HasStream && writtenAudioFrames == 0 && !source.AudioEndOfStream) return;
                started = true; clockStart = Time.GetTicksUsec();
                if (audioPlayer is not null) audioPlayer.StreamPaused = false;
                SetMeta("playback_started", true);
            }
            if (audioPlayer is not null && volume is { } readVolume) audioPlayer.VolumeLinear = readVolume();
            clock = Math.Max(clock, ReadClock());
            long now = (long)(clock * 10_000_000);
            while (pendingVideo is { } frame && frame.Timestamp100ns <= now)
            {
                pendingVideo = null;
                try { Present(frame); }
                finally { frame.Dispose(); }
                lastVideoEnd = frame.Timestamp100ns + (frame.Duration100ns > 0 ? frame.Duration100ns :
                    source.VideoInfo.FrameRateNumerator > 0 ?
                        10_000_000L * source.VideoInfo.FrameRateDenominator / source.VideoInfo.FrameRateNumerator : 0);
                source.TryReadVideo(out pendingVideo);
            }
            subtitles?.AdvanceTo(clock * 1000);
            bool audioEnded = !source.AudioInfo.HasStream || source.AudioEndOfStream && pendingAudio is null &&
                now >= writtenAudioFrames * 10_000_000L / source.AudioInfo.SampleRate;
            if (source.VideoEndOfStream && pendingVideo is null && now >= lastVideoEnd && audioEnded) Complete();
        }
        catch (Exception error) { Fail(error); }
    }

    private double ReadClock()
    {
        var source = decoder!;
        if (audioPlayback is null) return (Time.GetTicksUsec() - clockStart) / 1_000_000d;
        int queued = Math.Max(0, audioCapacity - audioPlayback.GetFramesAvailable());
        double consumed = (writtenAudioFrames - queued) / (double)source.AudioInfo.SampleRate;
        if (source.AudioEndOfStream && pendingAudio is null && queued == 0)
        {
            if (!audioDraining)
            { audioDraining = true; drainStart = Time.GetTicksUsec(); drainClock = Math.Max(clock, consumed - AudioServer.GetOutputLatency()); }
            return drainClock + (Time.GetTicksUsec() - drainStart) / 1_000_000d;
        }
        // Count consumed source samples, so a decoder starvation cannot advance
        // the video past audio which has never reached the mixer.
        return Math.Min(consumed, consumed - AudioServer.GetOutputLatency() + AudioServer.GetTimeSinceLastMix());
    }

    private void FillAudio()
    {
        if (audioPlayback is null || decoder is not { } source) return;
        while (audioPlayback.GetFramesAvailable() is > 0 and var available)
        {
            if (pendingAudio is null)
            {
                if (!source.TryReadAudio(out pendingAudio)) return;
                audioOffset = 0;
            }
            var packet = pendingAudio!;
            int channels = packet.Channels;
            long start = (long)Math.Round(packet.Timestamp100ns * (source.AudioInfo.SampleRate / 10_000_000d));
            long next = start + audioOffset;
            int count = Math.Min(audioScratch.Length, available);
            if (next > writtenAudioFrames)
            {
                count = (int)Math.Min(count, next - writtenAudioFrames);
                Array.Clear(audioScratch, 0, count); // A timestamped source gap.
            }
            else
            {
                if (next < writtenAudioFrames)
                    audioOffset += (int)Math.Min(writtenAudioFrames - next, packet.InterleavedSamples.Length / channels - audioOffset);
                count = Math.Min(count, packet.InterleavedSamples.Length / channels - audioOffset);
                if (count == 0) { pendingAudio = null; continue; }
                for (int frame = 0; frame < count; frame++)
                {
                    int at = (audioOffset + frame) * channels;
                    audioScratch[frame] = new(packet.InterleavedSamples[at], packet.InterleavedSamples[at + (channels == 1 ? 0 : 1)]);
                }
                audioOffset += count;
            }
            if (!audioPlayback.PushBuffer(audioScratch.AsSpan(0, count)))
                throw new InvalidOperationException("Movie audio buffer rejected its available source frames.");
            writtenAudioFrames += count;
            if (audioOffset == packet.InterleavedSamples.Length / channels) pendingAudio = null;
        }
    }

    private void Present(MovieVideoFrame frame)
    {
        using var decoded = Image.CreateFromData(frame.Width, frame.Height, false, Image.Format.Rgba8, frame.Rgba);
        if (texture is null) { texture = ImageTexture.CreateFromImage(decoded); image.Texture = texture; }
        else if (texture.GetWidth() != frame.Width || texture.GetHeight() != frame.Height)
        { texture.SetImage(decoded); pictureAspect = frame.Width / (float)frame.Height; Fit(); }
        else texture.Update(decoded);
        SetMeta("decoded_frame_timestamp", frame.Timestamp100ns);
    }

    private void Fit()
    {
        if (image is null) return;
        Vector2 size = Size;
        if (size.X > size.Y * pictureAspect) size.X = size.Y * pictureAspect;
        else size.Y = size.X / pictureAspect;
        image.Size = size; image.Position = (Size - size) / 2;
        subtitles?.Fit(Size);
    }
    private bool ReadAdmission() => !abandoned && !completing && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (ReadAdmission()) Complete(); }
    private void Complete() { if (completing) return; completing = true; returned?.Invoke(); }
    private void Fail(Exception error) { if (completing) return; completing = true; failed?.Invoke(error); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; returned = null; failed = null; volume = null;
        lifetime.Cancel();
        pendingVideo?.Dispose(); pendingVideo = null; pendingAudio = null;
        Task audioClosed = RetireAudio();
        Task closed = Task.CompletedTask;
        if (decoder is { } owner) { decoder = null; owner.Cancel(); closed = owner.DisposeAsync().AsTask(); }
        retirement = Task.WhenAll(opening ?? Task.CompletedTask, closed, audioClosed);
        Hide();
    }

    private Task RetireAudio()
    {
        var playback = audioPlayback; var stream = generator;
        audioPlayback = null; generator = null;
        AudioServer.Lock();
        try
        {
            audioPlayer?.Stop();
            if (audioPlayer is not null) audioPlayer.Stream = null;
        }
        finally { AudioServer.Unlock(); }
        return ReleaseAudioAfterMixer(GetTree(), playback, stream);
    }

    private static async Task ReleaseAudioAfterMixer(SceneTree tree,
        AudioStreamGeneratorPlayback? playback, AudioStreamGenerator? stream)
    {
        // Stop schedules a final mixer fade. Generator playback retains a raw
        // stream pointer, so the view cannot release that stream while the
        // AudioServer still owns playback. Keep both actual resources until
        // only our managed playback reference remains; no fixed-frame delay.
        if (playback is not null)
            while (playback.GetReferenceCount() > 1)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        playback?.Dispose(); stream?.Dispose();
    }

    public override void _ExitTree()
    {
        Abandon();
        if (image is not null) image.Texture = null;
        texture?.Dispose(); lifetime.Dispose();
    }
}
