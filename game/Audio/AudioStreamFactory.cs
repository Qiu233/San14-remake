using Godot;
using San14.Data;
using AudioStream = Godot.AudioStream;

public sealed record AudioClip(AudioStream Stream, AudioDecodeProvenance Provenance,
    int SampleRate, int Channels, int SampleFrames, bool UsesSourceLoop) : IDisposable
{
    // Record copies attach cue metadata but retain the same resource lifetime.
    private StreamResources resources = new(Stream);
    private sealed class StreamResources
    {
        private sealed class Shared(AudioStream stream)
        {
            internal readonly AudioStream Stream = stream;
            internal readonly List<AudioStream> Children = [];
            internal int Users = 1;
        }
        private readonly Shared shared;
        private int released;
        internal List<AudioStream> Children => shared.Children;
        internal StreamResources(AudioStream stream) => shared = new(stream);
        private StreamResources(Shared shared) => this.shared = shared;
        internal StreamResources Borrow()
        {
            if (Volatile.Read(ref released) != 0) throw new ObjectDisposedException(nameof(AudioClip));
            int users;
            do
            {
                users = Volatile.Read(ref shared.Users);
                if (users <= 0) throw new ObjectDisposedException(nameof(AudioClip));
            }
            while (Interlocked.CompareExchange(ref shared.Users, checked(users + 1), users) != users);
            return new(shared);
        }
        internal void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) != 0 || Interlocked.Decrement(ref shared.Users) != 0) return;
            shared.Stream.Dispose();
            foreach (var child in shared.Children) child.Dispose();
            shared.Children.Clear();
        }
    }
    internal void OwnChild(AudioStream stream) => resources.Children.Add(stream);

    // A process-owned immutable stream creates independent playback state in
    // each player. The scene owns this handle and its metadata, not the stream.
    internal AudioClip BorrowStaticResources()
    {
        var borrowed = this with { };
        borrowed.resources = resources.Borrow();
        return borrowed;
    }
    public int? LoopBegin { get; init; }
    public int? LoopEnd { get; init; }
    public int OutputChannels { get; init; } = Channels;
    internal IReadOnlyList<AudioStream>? SpeakerPairs { get; init; }
    internal AudioDeviceSpeakerMatrix? SpeakerDeviceRoute { get; init; }
    public void Dispose() => resources.Dispose();
    public AudioClipSelection? Selection { get; init; }
    public IReadOnlyList<AudioClipComponent>? Components { get; init; }
    public AudioClipCue? Cue { get; init; }
    public AudioCueStaticPlaybackReference? StaticPlayback { get; init; }
    internal MapMusicPlaybackProfile? MapMusic { get; init; }
}
internal sealed record MapMusicPlaybackProfile(float RootGain, float ReleaseSeconds, OriginalAudioOutputs Outputs);
public sealed record AudioClipSelection(string ExactSampleName, string BankUri, string BankSha256,
    uint SampleId, int SectionOffset, int SectionBytes);
public sealed record AudioClipCue(string ExactCueName, uint CueId, int SectionOffset, int? LeafOffset);
public sealed record AudioClipComponent(AudioClipSelection Selection, AudioDecodeProvenance Provenance);

/// <summary>Creates the Godot resource on the caller's engine thread. PCM samples are
/// preserved without integer premixing. Explicit stereo matrices and six-lane
/// speaker pairs are separate selected output contracts.</summary>
public static partial class AudioStreamFactory
{
    /// <summary>Starts the two authored 3244E2DF components together. Native
    /// speaker masks select the existing Windows stereo device matrix. Each
    /// unchanged PCM stem owns its own sample rate, natural end and loop.</summary>
    internal static AudioClip CreateSynchronousSpeakerPair(DecodedAudio first,
        AudioCueStaticPlaybackReference firstSource, DecodedAudio second,
        AudioCueStaticPlaybackReference secondSource, StereoAudioRoute route)
    {
        var components = new[] { (first, firstSource), (second, secondSource) };
        var stems = new List<(DecodedAudio Audio, int Channel, int Output, float Gain)>();
        foreach (var (audio, source) in components)
        {
            uint mask = source.SourceChannelMask ?? throw new InvalidDataException("A synchronous component needs its source speaker mask.");
            if (mask == 0 || (mask & ~0x3Fu) != 0 || System.Numerics.BitOperations.PopCount(mask) != audio.Wave.Channels ||
                audio.Wave.SampleRate != source.Sample.SampleRate || audio.Wave.SampleFrames != source.Sample.DeclaredSamples)
                throw new InvalidDataException("Synchronous sound source speaker lanes or timing differ from decoded PCM.");
            int channel = 0;
            for (int speaker = 0; speaker < 6; speaker++)
            {
                if ((mask & (1u << speaker)) == 0) continue;
                for (int output = 0; output < 2; output++)
                {
                    float gain = route.Gain(speaker, output) * source.Leaf.LinearGain;
                    if (gain > 0) stems.Add((audio, channel, output, gain));
                }
                channel++;
            }
        }
        // Keep a fully muted authored cue's actual natural duration.
        if (stems.Count == 0) stems.Add((first, 0, 0, 0));
        int frames = checked((int)Math.Ceiling(components.Max(component =>
            (double)component.Item1.Wave.SampleFrames / component.Item1.Wave.SampleRate) * first.Wave.SampleRate));
        var synchronized = new AudioStreamSynchronized { StreamCount = stems.Count };
        var clip = new AudioClip(synchronized, first.Provenance, first.Wave.SampleRate,
            first.Wave.Channels + second.Wave.Channels, frames,
            first.LoopStartSample is not null || second.LoopStartSample is not null)
            { OutputChannels = 2 };
        try
        {
            for (int index = 0; index < stems.Count; index++)
            {
                var (audio, channel, output, gain) = stems[index];
                var wave = audio.Wave;
                bool loop = audio.LoopStartSample is not null;
                if (wave.Pcm16.Length == 0 || wave.Pcm16.Length % (wave.Channels * 2) != 0 ||
                    (audio.LoopStartSample is null) != (audio.LoopEndSample is null) ||
                    loop && (audio.LoopStartSample < 0 || audio.LoopStartSample >= audio.LoopEndSample ||
                        audio.LoopEndSample > wave.SampleFrames))
                    throw new InvalidDataException("A synchronous component has incomplete PCM or invalid loop bounds.");
                byte[] pcm = new byte[checked(wave.SampleFrames * 4)];
                for (int frame = 0; gain != 0 && frame < wave.SampleFrames; frame++)
                {
                    int source = (frame * wave.Channels + channel) * 2, target = frame * 4 + output * 2;
                    pcm[target] = wave.Pcm16[source]; pcm[target + 1] = wave.Pcm16[source + 1];
                }
                var stream = new AudioStreamWav
                {
                    Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = wave.SampleRate,
                    Stereo = true, Data = pcm,
                    LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
                    LoopBegin = loop ? checked((int)audio.LoopStartSample!.Value) : 0,
                    LoopEnd = loop ? checked((int)audio.LoopEndSample!.Value) : 0
                };
                clip.OwnChild(stream);
                synchronized.SetSyncStream(index, stream);
                synchronized.SetSyncStreamVolume(index, gain == 0 ? 0 : Mathf.LinearToDb(gain));
            }
            return clip;
        }
        catch { clip.Dispose(); throw; }
    }

    public static AudioClip Create(DecodedAudio decoded, AudioLoopPolicy loopPolicy)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        if (!Enum.IsDefined(loopPolicy)) throw new ArgumentOutOfRangeException(nameof(loopPolicy));
        bool enable = loopPolicy == AudioLoopPolicy.RequireSource ||
            (loopPolicy == AudioLoopPolicy.SourceIfPresent &&
                (decoded.LoopStartSample is not null || decoded.LoopEndSample is not null));
        return Create(decoded, enable);
    }

    public static AudioClip Create(DecodedAudio decoded, bool enableSourceLoop = false)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        var wave = decoded.Wave;
        if (wave.Channels is not (1 or 2))
            throw new NotSupportedException($"The current player preserves mono/stereo; {wave.Channels}-channel source needs explicit speaker routing.");
        if (wave.SampleRate is < 1 or > 192000 || wave.Pcm16.Length == 0 || wave.Pcm16.Length % (wave.Channels * 2) != 0)
            throw new InvalidDataException("Audio clip needs complete PCM16 frames at its source sample rate.");
        if (enableSourceLoop && (decoded.LoopStartSample is null || decoded.LoopEndSample is null ||
            decoded.LoopStartSample < 0 || decoded.LoopStartSample >= decoded.LoopEndSample ||
            decoded.LoopEndSample > wave.SampleFrames))
            throw new InvalidDataException("Selected audio has no valid original loop interval.");
        var stream = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = wave.SampleRate,
            Stereo = wave.Channels == 2,
            Data = wave.Pcm16,
            LoopMode = enableSourceLoop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = enableSourceLoop ? checked((int)decoded.LoopStartSample!.Value) : 0,
            LoopEnd = enableSourceLoop ? checked((int)decoded.LoopEndSample!.Value) : 0
        };
        return new(stream, decoded.Provenance, wave.SampleRate, wave.Channels, wave.SampleFrames, enableSourceLoop)
        {
            LoopBegin = enableSourceLoop ? stream.LoopBegin : null,
            LoopEnd = enableSourceLoop ? stream.LoopEnd : null
        };
    }

    /// <summary>Routes explicitly ordered six-channel PCM to a caller-selected
    /// stereo output. No channel labels, source mask or host layout are inferred.
    /// Each nonzero matrix term is an unchanged PCM stem with a float mixer gain;
    /// no integer pre-sum, clipping or peak normalization is applied.</summary>
    public static AudioClip CreateStereoRouted(DecodedAudio decoded, StereoAudioRoute route,
        AudioLoopPolicy loopPolicy) =>
        CreateStereoRouted(PreparedStereoAudio.Prepare(decoded, route, loopPolicy));

    internal static AudioClip CreateStereoRouted(PreparedStereoAudio prepared)
    {
        var decoded = prepared.Source;
        var wave = decoded.Wave;
        var synchronized = new AudioStreamSynchronized { StreamCount = prepared.Terms.Count };
        var clip = new AudioClip(synchronized, decoded.Provenance, wave.SampleRate, 6, wave.SampleFrames, prepared.UsesSourceLoop)
        {
            OutputChannels = 2, LoopBegin = prepared.LoopBegin, LoopEnd = prepared.LoopEnd
        };
        try
        {
            for (int term = 0; term < prepared.Terms.Count; term++)
            {
                var source = prepared.Terms[term];
                var stem = new AudioStreamWav
                {
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = wave.SampleRate, Stereo = true, Data = source.Pcm16,
                    LoopMode = prepared.UsesSourceLoop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
                    LoopBegin = prepared.LoopBegin ?? 0, LoopEnd = prepared.LoopEnd ?? 0
                };
                clip.OwnChild(stem);
                synchronized.SetSyncStream(term, stem);
                synchronized.SetSyncStreamVolume(term, source.Gain == 0 ? 0 : Mathf.LinearToDb(source.Gain));
            }
            return clip;
        }
        catch { clip.Dispose(); throw; }
    }

}

/// <summary>A snapshot of a caller's two-output matrix, indexed by the actual
/// decoded input order. This is not a default or a host speaker detector.</summary>
public sealed class StereoAudioRoute
{
    private readonly float[] coefficients;
    // Only this class's finite Windows declaration factory can mark a route
    // resident. Arbitrary public matrices retain ordinary per-request lifetime.
    internal ResidentStereoOutputProfile? ResidentProfile { get; private init; }
    public StereoAudioRoute(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left); ArgumentNullException.ThrowIfNull(right);
        if (left.Count != 6 || right.Count != 6) throw new ArgumentException("A six-channel route needs six coefficients per output.");
        coefficients = left.Concat(right).ToArray();
        if (coefficients.Any(value => !float.IsFinite(value) || value is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(left), "This matrix supports finite coefficients from zero through one.");
    }

    internal static StereoAudioRoute WindowsDevice(float lfeGain)
    {
        if (lfeGain is not (0 or 1))
            throw new NotSupportedException("Original stereo output needs its actual LFE configuration.");
        float lfe = .7071f * lfeGain;
        return new([1, 0, .7071f, lfe, .7071f, 0], [0, 1, .7071f, lfe, 0, .7071f])
        {
            ResidentProfile = lfeGain == 0 ? ResidentStereoOutputProfile.WindowsWithoutLfe :
                ResidentStereoOutputProfile.WindowsWithLfe
        };
    }
    public float Gain(int input, int output)
    {
        if ((uint)input >= 6 || (uint)output >= 2) throw new ArgumentOutOfRangeException(nameof(input));
        return coefficients[output * 6 + input];
    }
}
