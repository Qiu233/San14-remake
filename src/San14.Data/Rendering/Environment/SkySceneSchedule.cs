namespace San14.Data;

public enum SkySceneCopyOperation { ExternalBase, WorkMip, LightSnapshotBase, LightSnapshotMip }

public sealed record SkySceneCopyStep(SkySceneCopyOperation Operation,
    uint Face, uint DestinationMip, uint DestinationSurfaceId, uint? SourceSingleMip);

public sealed record SkySceneFilterStep(uint ProgressFlat, uint Face, uint Mip,
    uint DestinationSurfaceId);

/// <summary>The bounded 1198CA0 / D1F0F0 work order. Scheduler flat indices
/// are mip-major; native RTV SurfaceID is face-major. Caller time, budget,
/// current k and optional-light selection are deliberately not inferred.</summary>
public static class SkySceneSchedule
{
    public static uint SurfaceId(byte levels, uint face, uint mip)
    {
        ValidateLevels(levels);
        if (face >= 6 || mip >= levels)
            throw new ArgumentOutOfRangeException(nameof(face), "Sky work requires a declared face/mip.");
        return checked(face * levels + mip);
    }

    /// <summary>One original preparation call: external mip0 copy, followed
    /// immediately by all W downsample mips, for each face in source order.
    /// The original caller charges this whole call as one work unit.</summary>
    public static IReadOnlyList<SkySceneCopyStep> BasePreparation(byte levels)
    {
        ValidateLevels(levels);
        var steps = new List<SkySceneCopyStep>(6 * levels);
        for (uint face = 0; face < 6; face++)
        {
            steps.Add(new(SkySceneCopyOperation.ExternalBase, face, 0,
                SurfaceId(levels, face, 0), null));
            for (uint mip = 1; mip < levels; mip++)
                steps.Add(new(SkySceneCopyOperation.WorkMip, face, mip,
                    SurfaceId(levels, face, mip), mip - 1));
        }
        return steps.AsReadOnly();
    }

    /// <summary>11975D0 snapshots W into P. P's final mip is deliberately
    /// absent: native m &lt; L-1, unlike the main W mip preparation.</summary>
    public static IReadOnlyList<SkySceneCopyStep> LightSnapshot(byte levels)
    {
        ValidateLevels(levels);
        var steps = new List<SkySceneCopyStep>();
        for (uint face = 0; face < 6; face++)
            steps.Add(new(SkySceneCopyOperation.LightSnapshotBase, face, 0,
                SurfaceId(levels, face, 0), 0));
        for (uint mip = 1; mip + 1 < levels; mip++)
            for (uint face = 0; face < 6; face++)
                steps.Add(new(SkySceneCopyOperation.LightSnapshotMip, face, mip,
                    SurfaceId(levels, face, mip), mip - 1));
        return steps.AsReadOnly();
    }

    public static IReadOnlyList<SkySceneFilterStep> Filter(byte levels,
        uint firstFlat, uint maximumFaceWork)
    {
        ValidateLevels(levels);
        uint last = checked(6u * levels);
        if (firstFlat < 6 || firstFlat > last)
            throw new ArgumentOutOfRangeException(nameof(firstFlat), "Filter progress begins after six base faces.");
        uint count = Math.Min(maximumFaceWork, last - firstFlat);
        var steps = new List<SkySceneFilterStep>(checked((int)count));
        for (uint flat = firstFlat; flat < firstFlat + count; flat++)
        {
            uint face = flat % 6, mip = flat / 6;
            steps.Add(new(flat, face, mip, SurfaceId(levels, face, mip)));
        }
        return steps.AsReadOnly();
    }

    public static (uint InputA, uint InputB, uint Destination) BlendIndices(uint explicitGenerationIndex)
    {
        if (explicitGenerationIndex >= 3)
            throw new ArgumentOutOfRangeException(nameof(explicitGenerationIndex), "Original generation rotates across indices 0..2.");
        return ((explicitGenerationIndex + 1) % 3, (explicitGenerationIndex + 2) % 3, 3);
    }

    private static void ValidateLevels(byte levels)
    {
        if (levels is < 1 or > 31)
            throw new InvalidDataException("Sky work requires a finite explicit mip count.");
    }
}
