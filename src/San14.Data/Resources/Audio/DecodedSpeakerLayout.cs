namespace San14.Data;

/// <summary>The decoder output's actual native speaker-bit order, as recorded
/// in its WAVEFORMATEXTENSIBLE format. This is format metadata, not a playback
/// proof or a private verification result.</summary>
public sealed record DecodedSpeakerLayout(uint NativeChannelMask, int Channels)
{
    public bool AppliesTo(DecodedAudio audio) => audio.Wave.Channels == Channels &&
        audio.Wave.ChannelMask == NativeChannelMask;
}
