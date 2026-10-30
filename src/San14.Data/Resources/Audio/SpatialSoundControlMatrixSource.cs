namespace San14.Data;

/// <summary>The ordinary game emitter's actual Mono1 control send, carried
/// in a stereo buffer's left lane. The right lane is always silent. This is
/// calculated before the independent audible master/device fold.</summary>
public static class SpatialSoundControlMatrixSource
{
    public static SpatialSoundSpeakerMatrix PrepareOrdinaryEffect(SpatialEffectSoundLayer layer, int sourceChannels)
    {
        ArgumentNullException.ThrowIfNull(layer);
        uint flags = layer.Playback.SourceSpatialModeFlags ?? throw new NotSupportedException("spatial.sound.sample_spatial_mode");
        uint mask = layer.Playback.SourceChannelMask ?? throw new NotSupportedException("spatial.sound.source_channel_mask");
        if (flags is 0x1000u or 0x3000u) mask = 0;
        bool mode = flags is 0x2000u or 0x4000u;
        float first = 1f, second = 0f, center = 0f;
        if (mode)
        {
            float coefficient = layer.Playback.SourceCenterGain ?? throw new NotSupportedException("spatial.sound.sample_center");
            if (coefficient > 0f) { first = 0f; center = 1f; }
            else first = second = 0.70710677f;
        }
        //404FF0's ordinary packet flags20 do not admit the separate spatial
        //callback cache.12AA2D0 therefore receives its actual initial gain1.
        return Prepare(sourceChannels, mask, mode, first, second, center);
    }

    public static SpatialSoundSpeakerMatrix PrepareSpatialNarration(int sourceChannels, uint? sourceMask)
        => Prepare(sourceChannels, sourceMask ?? throw new NotSupportedException("spatial.voice.source_channel_mask"),
            false, 1f, 0f, 0f);

    public static SpatialSoundSpeakerMatrix PrepareNonPositionVoice(UniqueTacticVoicePlayback playback, int sourceChannels)
    {
        ArgumentNullException.ThrowIfNull(playback);
        //Both actual admitted readers prove sample mode0. The old reader
        //requires leaf flags0; the A517 reader allows only registry bit10000.
        uint? mask = playback.StaticReference?.SourceChannelMask ?? playback.LegacyReference?.Sample.SourceChannelMask;
        return Prepare(sourceChannels, mask ?? throw new NotSupportedException("audio.spatial.control_sender_non_position_mask"),
            false, 1f, 0f, 0f);
    }

    private static SpatialSoundSpeakerMatrix Prepare(int channels, uint mask, bool mode, float first, float second, float center)
    {
        if (channels is not (1 or 2)) throw new NotSupportedException("spatial.control.source_channels");
        var coefficients = new float[channels * 2];
        for (int input = 0; input < channels; input++)
        {
            int speaker = SpeakerAt(mask, input);
            coefficients[input * 2] = mask == 0 ? first : mode ? speaker switch
            {
                0 => first, 1 => second, 2 => center, _ => 0f
            } : speaker is 0 or 1 or 2 or 4 or 5 or 6 or 7 ? 1f : 0f;
        }
        return new(channels, 2, coefficients);
    }

    private static int SpeakerAt(uint mask, int ordinal)
    {
        for (int bit = 0; bit < 8; bit++)
            if ((mask & (1u << bit)) != 0 && ordinal-- == 0) return bit;
        return -1;
    }
}
