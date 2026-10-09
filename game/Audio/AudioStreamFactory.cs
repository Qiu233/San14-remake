using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed record AudioClip(AudioStreamWav Stream, AudioDecodeProvenance Provenance,
    int SampleRate, int Channels, int SampleFrames, bool UsesSourceLoop)
{
    public AudioClipSelection? Selection { get; init; }
    public AudioClipCue? Cue { get; init; }
}
public sealed record AudioClipSelection(string ExactSampleName, string BankUri, string BankSha256,
    uint SampleId, int SectionOffset, int SectionBytes);
public sealed record AudioClipCue(string ExactCueName, uint CueId, int SectionOffset, int LeafOffset);

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
        return new(stream, decoded.Provenance, wave.SampleRate, wave.Channels, wave.SampleFrames, enableSourceLoop);
    }
}
