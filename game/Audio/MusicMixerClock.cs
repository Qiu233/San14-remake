using Godot;

/// <summary>A service-owned silent bus measures real mixed frames. Its bounded
/// capture ring is never drained: pushed plus discarded counts the full stream.
/// The caller holds AudioServer.Lock for reads and associated player commands.</summary>
internal sealed class MusicMixerClock : IDisposable
{
    internal readonly string BusName;
    internal readonly int MixRate;
    internal readonly int Pairs;
    private readonly AudioEffectCapture capture = new() { BufferLength = .1f };
    private readonly AudioServer.SpeakerMode speakerMode;
    private readonly string device;
    private ulong last;
    private bool disposed;

    internal MusicMixerClock(ulong owner, string role = "Music")
    {
        BusName = "San14" + role + "Clock" + owner;
        if (AudioServer.GetBusIndex(BusName) >= 0) throw new InvalidOperationException("Music clock already exists.");
        int bus = AudioServer.BusCount;
        AudioServer.AddBus();
        try
        {
            AudioServer.SetBusName(bus, BusName);
            AudioServer.AddBusEffect(bus, capture);
            MixRate = checked((int)AudioServer.GetMixRate());
            Pairs = AudioServer.GetBusChannels(bus);
            speakerMode = AudioServer.GetSpeakerMode();
            device = AudioServer.OutputDevice;
            if (MixRate <= 0 || Pairs <= 0) throw new InvalidOperationException("Music clock has no mixer format.");
            ReadFrames();
        }
        catch { AudioServer.RemoveBus(bus); capture.Dispose(); throw; }
    }

    internal ulong ReadFrames()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        int bus = AudioServer.GetBusIndex(BusName);
        if (bus < 0 || AudioServer.GetMixRate() != MixRate || AudioServer.GetBusChannels(bus) != Pairs ||
            AudioServer.GetSpeakerMode() != speakerMode || AudioServer.OutputDevice != device ||
            AudioServer.IsBusBypassingEffects(bus) || AudioServer.GetBusEffectCount(bus) != 1 ||
            !AudioServer.IsBusEffectEnabled(bus, 0) || AudioServer.GetBusEffect(bus, 0) != capture)
            throw new InvalidOperationException("Music mixer configuration changed; playback needs a new clock epoch.");
        ulong count = checked((ulong)capture.GetPushedFrames() + (ulong)capture.GetDiscardedFrames());
        if (count % (uint)Pairs != 0 || count / (uint)Pairs < last)
            throw new InvalidOperationException("Music mixer counter epoch changed.");
        return last = count / (uint)Pairs;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        int bus = AudioServer.GetBusIndex(BusName);
        if (bus >= 0) AudioServer.RemoveBus(bus);
        capture.Dispose();
    }
}

/// <summary>One looping voice's active intervals and exact integer carry.
/// Mixer output duration is separate from source position, loops and pitch.</summary>
internal sealed class MusicMixerInterval(int mixRate, ulong baseline)
{
    internal ulong Baseline { get; private set; } = baseline;
    internal ulong ActiveFrames { get; private set; }
    internal ulong SourceTicks { get; private set; }
    internal ulong Remainder { get; private set; }
    internal bool Running { get; private set; } = true;
    internal ulong Drain(ulong frames)
    {
        if (frames < Baseline) throw new InvalidOperationException("Music clock moved backwards.");
        ulong delta = frames - Baseline;
        Baseline = frames;
        if (!Running) return 0;
        ActiveFrames = checked(ActiveFrames + delta);
        ulong numerator = checked(Remainder + delta * 100);
        ulong ticks = checked(numerator / (uint)mixRate * 480);
        Remainder = numerator % (uint)mixRate;
        SourceTicks = checked(SourceTicks + ticks);
        return ticks;
    }
    internal void SetRunning(bool running, ulong frames)
    { Baseline = frames; Running = running; }
}
