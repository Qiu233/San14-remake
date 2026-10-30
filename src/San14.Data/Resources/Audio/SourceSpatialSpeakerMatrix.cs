using System.Numerics;

namespace San14.Data;

/// <summary>The actual source-channel to device-channel coefficient matrix.
/// Volume, category and distance attenuation remain separate sound owners.</summary>
public sealed class SpatialSoundSpeakerMatrix
{
    private readonly float[] coefficients;
    public SpatialSoundSpeakerMatrix(int sourceChannels, int outputChannels, IReadOnlyList<float> coefficients)
    {
        if (sourceChannels is not (1 or 2) || outputChannels is not (2 or 4 or 6 or 8) ||
            coefficients.Count != sourceChannels * outputChannels)
            throw new InvalidDataException("Spatial sound speaker matrix shape is invalid.");
        SourceChannels = sourceChannels;
        OutputChannels = outputChannels;
        this.coefficients = coefficients.ToArray();
        if (this.coefficients.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Spatial sound speaker matrix contains a nonfinite coefficient.");
    }
    public int SourceChannels { get; }
    public int OutputChannels { get; }
    public float ReadGain(int input, int output)
    {
        if ((uint)input >= (uint)SourceChannels || (uint)output >= (uint)OutputChannels)
            throw new ArgumentOutOfRangeException(nameof(input));
        return coefficients[input * OutputChannels + output];
    }
}

/// <summary>One actual map-camera observation, captured by the host once per
/// update. The matrix follows the source's row-vector convention.</summary>
public sealed record SpatialSoundCameraFrame(Matrix4x4 WorldToView,
    Vector3 CameraPosition, float CameraDistance)
{
    public Vector3? ListenerPosition { get; init; }
}


/// <summary>The ordinary game emitter's point-source coefficients. The graph's
/// internal layout owns panning; the host applies its final device fold later.</summary>
public static class SpatialSoundSpeakerMatrixSource
{
    public static SpatialSoundSpeakerMatrix PrepareEffect(SpatialEffectSoundPlayback cue,
        SpatialEffectSoundLayer layer, SpatialSoundCameraFrame camera, Vector3 sourcePosition,
        int sourceChannels, int logicalOutputChannels, int outputProfileChannels, float? currentSpread = null)
    {
        ArgumentNullException.ThrowIfNull(cue);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(camera);
        if (sourceChannels is not (1 or 2) || logicalOutputChannels is not (2 or 6 or 8))
            throw new NotSupportedException("spatial.sound.channel_layout");
        if (outputProfileChannels != cue.OutputPlan.Graph.MixChannels)
            throw new InvalidDataException("Spatial sound panning profile differs from its loaded output graph.");
        uint sourceMask = layer.Playback.SourceChannelMask ??
            throw new NotSupportedException("spatial.sound.source_channel_mask");
        uint flags = layer.Playback.SourceSpatialModeFlags ??
            throw new NotSupportedException("spatial.sound.sample_spatial_mode");
        // The ordinary game packet has no cue-context force-zero override.
        // These two actual declaration modes independently force mask zero.
        if (flags is 0x1000u or 0x3000u) sourceMask = 0;
        int mode = flags switch { 0x2000u => 1, 0x4000u => 2, _ => 0 };
        float spread = currentSpread ?? cue.OutputPlan.SourceSpreadGain ??
            throw new NotSupportedException("spatial.sound.classification_spread_control");
        var matrix = new float[sourceChannels * logicalOutputChannels];
        var main = new float[8];
        SourceSpatialDeclaredRows.PrepareLfe(main.AsSpan(0, logicalOutputChannels), sourceMask, spread, layer.SourceLfeGain, mode != 0);
        var alternate = new float[8];
        float directWeight = sourceMask == 0 ? 0f : 1f, spatialWeight = 0f;
        if (sourceMask == 0 || mode != 0)
        {
            float scale = cue.OutputPlan.SourceDistanceScale ??
                throw new NotSupportedException("spatial.sound.listener_distance_scale");
            float center = layer.Playback.SourceCenterGain ??
                throw new NotSupportedException("spatial.sound.sample_center");
            float radius = cue.Group.FilterSource.Parameter4C / (scale * 0.01f);
            // Pan uses the camera-eye offset; the independent mode2 distance
            // getter uses the actual listener base B produced by the same frame.
            Vector3 point = Vector3.Transform(sourcePosition, camera.WorldToView);
            float length = point.Length(), azimuth = 0f, blend = 0f;
            if (length > 0f)
            {
                if (radius > 0f)
                {
                    float horizontal = MathF.Sqrt(point.X * point.X + point.Z * point.Z);
                    blend = Math.Clamp(((MathF.Min(horizontal, radius) / length) * horizontal) / radius, 0f, 1f);
                }
                else blend = 1f;
                if (blend != 0f) azimuth = Math.Clamp(MathF.Atan2(point.X, -point.Z), -3.1415927f, 3.1415927f);
            }
            SourceSpatialPanning.Prepare(azimuth, blend,
                logicalOutputChannels, outputProfileChannels, center, spread).CopyTo(main, 0);
            SourceSpatialDeclaredRows.PrepareLfe(main.AsSpan(0, logicalOutputChannels),
                sourceMask, spread, layer.SourceLfeGain, mode != 0);
            if (sourceMask != 0 && mode == 2 && radius > 0f)
            {
                Vector3 listener = camera.ListenerPosition ??
                    throw new NotSupportedException("spatial.sound.listener_base_position");
                float ratio = Vector3.Distance(sourcePosition, listener) / radius;
                if (ratio > 0.5f)
                {
                    directWeight = ratio < 1f ? MathF.Cos((ratio - 0.5f) * 3.1415927f) : 0f;
                    spatialWeight = 1f - directWeight;
                }
            }
            if (spatialWeight > 0f)
            {
                // The kind1 alternate callback forces geometry blend one and
                // spread zero while retaining this exact cached azimuth.
                SourceSpatialPanning.Prepare(azimuth, 1f,
                    logicalOutputChannels, outputProfileChannels, center, 0f).CopyTo(alternate, 0);
            }
        }
        if (sourceMask == 0)
        {
            for (int input = 0; input < sourceChannels; input++)
                main.AsSpan(0, logicalOutputChannels).CopyTo(matrix.AsSpan(input * logicalOutputChannels));
        }
        else
        {
            for (int input = 0; input < sourceChannels; input++)
                SourceSpatialDeclaredRows.Write(matrix.AsSpan(input * logicalOutputChannels, logicalOutputChannels),
                    SpeakerAt(sourceMask, input), sourceMask, main, alternate, directWeight, spatialWeight, mode != 0);
        }
        return new(sourceChannels, logicalOutputChannels, matrix);
    }

    private static int SpeakerAt(uint mask, int ordinal)
    {
        for (int bit = 0; bit < 8; bit++)
            if ((mask & (1u << bit)) != 0 && ordinal-- == 0) return bit;
        return -1;
    }

}
