using System.Security.Cryptography;
using Godot;
using San14.Data;
using AudioStream = Godot.AudioStream;

public sealed record AudioClip(AudioStream Stream, AudioDecodeProvenance Provenance,
    int SampleRate, int Channels, int SampleFrames, bool UsesSourceLoop) : IDisposable
{
    // Record copies attach cue metadata but retain the same resource lifetime.
    private readonly StreamResources resources = new(Stream);
    private sealed class StreamResources(AudioStream stream)
    {
        internal readonly List<AudioStream> Children = [];
        private bool disposed;
        internal void Dispose()
        {
            if (disposed) return;
            disposed = true;
            stream.Dispose();
            foreach (var child in Children) child.Dispose();
            Children.Clear();
        }
    }
    internal void OwnChild(AudioStream stream) => resources.Children.Add(stream);
    public int? LoopBegin { get; init; }
    public int? LoopEnd { get; init; }
    public int OutputChannels { get; init; } = Channels;
    public void Dispose() => resources.Dispose();
    public AudioClipSelection? Selection { get; init; }
    public AudioClipCue? Cue { get; init; }
    public AudioCueStaticPlaybackReference? StaticPlayback { get; init; }
    internal MapMusicPlaybackProfile? MapMusic { get; init; }
}
internal sealed record MapMusicPlaybackProfile(float RootGain, float ReleaseSeconds, OriginalAudioOutputs Outputs);
public sealed record AudioClipSelection(string ExactSampleName, string BankUri, string BankSha256,
    uint SampleId, int SectionOffset, int SectionBytes);
public sealed record AudioClipCue(string ExactCueName, uint CueId, int SectionOffset, int? LeafOffset);

/// <summary>Creates the Godot resource on the caller's engine thread. PCM samples are
/// preserved without gain, resampling or downmixing. Multichannel routing requires
/// a later explicit speaker contract; it cannot silently become stereo.</summary>
public static class AudioStreamFactory
{
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
        if (Convert.ToHexString(SHA256.HashData(wave.Pcm16)) != decoded.Provenance.PcmSha256)
            throw new InvalidDataException("Decoded PCM samples changed after their identity was captured.");
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
        AudioLoopPolicy loopPolicy)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        ArgumentNullException.ThrowIfNull(route);
        if (!Enum.IsDefined(loopPolicy)) throw new ArgumentOutOfRangeException(nameof(loopPolicy));
        var wave = decoded.Wave;
        if (wave.Channels != 6 || wave.SampleRate is < 1 or > 192000 ||
            wave.Pcm16.Length == 0 || wave.Pcm16.Length % 12 != 0)
            throw new InvalidDataException("Explicit six-channel routing requires complete source PCM16 frames.");
        if (Convert.ToHexString(SHA256.HashData(wave.Pcm16)) != decoded.Provenance.PcmSha256)
            throw new InvalidDataException("Decoded PCM samples changed after their identity was captured.");
        bool loop = loopPolicy == AudioLoopPolicy.RequireSource ||
            (loopPolicy == AudioLoopPolicy.SourceIfPresent &&
             (decoded.LoopStartSample is not null || decoded.LoopEndSample is not null));
        if (loop && (decoded.LoopStartSample is null || decoded.LoopEndSample is null ||
            decoded.LoopStartSample < 0 || decoded.LoopStartSample >= decoded.LoopEndSample ||
            decoded.LoopEndSample > wave.SampleFrames))
            throw new InvalidDataException("Selected audio has no valid original loop interval.");
        var terms = new List<(int Input, int Output, float Gain)>();
        for (int output = 0; output < 2; output++)
            for (int input = 0; input < 6; input++)
                if (route.Gain(input, output) is > 0 and var gain) terms.Add((input, output, gain));
        // A zero matrix still has the selected source's duration and loop clock.
        if (terms.Count == 0) terms.Add((0, 0, 0));
        var synchronized = new AudioStreamSynchronized { StreamCount = terms.Count };
        var clip = new AudioClip(synchronized, decoded.Provenance, wave.SampleRate, 6, wave.SampleFrames, loop)
        {
            OutputChannels = 2,
            LoopBegin = loop ? checked((int)decoded.LoopStartSample!.Value) : null,
            LoopEnd = loop ? checked((int)decoded.LoopEndSample!.Value) : null
        };
        try
        {
            for (int term = 0; term < terms.Count; term++)
            {
                var (input, output, gain) = terms[term];
                var pcm = new byte[checked(wave.SampleFrames * 4)];
                if (gain != 0)
                    for (int frame = 0; frame < wave.SampleFrames; frame++)
                    {
                        int source = frame * 12 + input * 2, target = frame * 4 + output * 2;
                        pcm[target] = wave.Pcm16[source]; pcm[target + 1] = wave.Pcm16[source + 1];
                    }
                var stem = new AudioStreamWav
                {
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = wave.SampleRate, Stereo = true, Data = pcm,
                    LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
                    LoopBegin = clip.LoopBegin ?? 0, LoopEnd = clip.LoopEnd ?? 0
                };
                clip.OwnChild(stem);
                synchronized.SetSyncStream(term, stem);
                synchronized.SetSyncStreamVolume(term, gain == 0 ? 0 : Mathf.LinearToDb(gain));
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
    public StereoAudioRoute(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left); ArgumentNullException.ThrowIfNull(right);
        if (left.Count != 6 || right.Count != 6) throw new ArgumentException("A six-channel route needs six coefficients per output.");
        coefficients = left.Concat(right).ToArray();
        if (coefficients.Any(value => !float.IsFinite(value) || value is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(left), "This matrix supports finite coefficients from zero through one.");
    }
    public float Gain(int input, int output)
    {
        if ((uint)input >= 6 || (uint)output >= 2) throw new ArgumentOutOfRangeException(nameof(input));
        return coefficients[output * 6 + input];
    }
}
