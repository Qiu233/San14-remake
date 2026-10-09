namespace San14.Data;

/// <summary>The exact completed side of one World150 palette. These are
/// source resolved S/Q/T records, before conversion to skinning matrices.</summary>
public sealed record WorldSdfCompletedPose(object ActualWorldOwner,
    uint PaletteCounter98, int PaletteSide, ReadOnlyMemory<byte> SqtRecords48)
{
    public const string SourceProvenance =
        "TC:DBFDAA-byte88=1/DBFE1C-World28/DBFE2B-D9E630/DBFE33-World150/" +
        "DBFE55-D8A620;D9E6BF-palette98;D8A66F-writeSide;D8A969-xor1;DBC75E-completedSide";
}

/// <summary>Retained World15018/20 publication for the ordinary Model ready
/// path. The animation hierarchy performs D8A620 arithmetic; this owner keeps
/// its same-World side selection and completion publication. It does not
/// supply World344 kind or World348 profile.</summary>
public sealed class WorldSdfPosePublication
{
    private readonly byte[]?[] completedSides = new byte[2][];
    private readonly int recordBytes;
    private uint counter98;

    private WorldSdfPosePublication(object worldOwner, int jointCount)
    {
        ActualWorldOwner = worldOwner;
        recordBytes = checked(jointCount * 48);
        // DBFDDA passes true and DBFE1C passes this World. D9E630 writes2
        // under precisely those inputs. Neither side has completed yet.
        counter98 = 2;
    }

    public object ActualWorldOwner { get; }
    public uint PaletteCounter98 => counter98;

    /// <summary>Called when the real ordinary Model ready path creates
    /// World150. The actual source skeleton supplies the complete record count.</summary>
    public static WorldSdfPosePublication CreateForOrdinaryModelReady(
        object actualWorldOwner, int actualJointCount)
    {
        ArgumentNullException.ThrowIfNull(actualWorldOwner);
        if (actualJointCount < 0 || actualJointCount > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(actualJointCount));
        return new(actualWorldOwner, actualJointCount);
    }

    /// <summary>Publish the actual current output of the existing D8A620
    /// hierarchy writer. Keep SQT48; do not convert from shader affine rows.</summary>
    public WorldSdfCompletedPose PublishCompletedHierarchy(
        ArmyAnimationHierarchyPose actualCurrentPose)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentPose);
        if (actualCurrentPose.ResolvedPoseBytes.Length != recordBytes)
            throw new InvalidDataException("Completed World palette needs its complete actual SQT48 record count.");
        int writeSide = (int)(counter98 & 1);
        completedSides[writeSide] = actualCurrentPose.ResolvedPoseBytes.ToArray();
        counter98 ^= 1; // D8A969..D8A96C, after completing the write.
        return ReadCompletedPose();
    }

    /// <summary>C17ED0's actual external-palette branch when World44.bit3
    /// is clear: copy the smaller skeleton count into the already completed
    /// side. It preserves the destination tail and does not flip98.</summary>
    public WorldSdfCompletedPose CopyFromCompletedExternalPalette(
        WorldSdfCompletedPose actualExternalPalette)
    {
        ArgumentNullException.ThrowIfNull(actualExternalPalette);
        int sourceBytes = actualExternalPalette.SqtRecords48.Length;
        if (sourceBytes % 48 != 0 || sourceBytes / 48 > short.MaxValue)
            throw new InvalidDataException("External palette needs its complete actual SQT48 skeleton records.");
        int side = (int)((~counter98) & 1);
        byte[] previous = completedSides[side] ??
            throw new InvalidOperationException("External palette copy requires this World's completed ready pose.");
        byte[] destination = previous.ToArray();
        actualExternalPalette.SqtRecords48.Span[..Math.Min(recordBytes, sourceBytes)].CopyTo(destination);
        completedSides[side] = destination;
        return ReadCompletedPose();
    }

    /// <summary>DBC560 reads(~98)&1. An uncompleted side has no fabricated
    /// record contents; ordinary ready must finish its hierarchy first.</summary>
    public WorldSdfCompletedPose ReadCompletedPose()
    {
        int completedSide = (int)((~counter98) & 1);
        byte[] records = completedSides[completedSide] ??
            throw new InvalidOperationException("This World palette has not completed a hierarchy write.");
        return new(ActualWorldOwner, counter98, completedSide, records);
    }
}
