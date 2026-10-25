using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Allocation capacities for one ordinary Effect2 draw host.</summary>
public sealed record G1EffectHostConfiguration(int BucketCount, int QueueCapacity,
    int DynamicCapacity32, int RowCapacity)
{
    /// <summary>Loads the View declaration and applies each nonzero Common
    /// override. Call when constructing the host, using its resource generation.</summary>
    public static G1EffectHostConfiguration Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        DokObject view = FindObject(repository.LoadObjects("system", 0x62550860).Document,
            0xE1B6A687, 0x97AF26FD);
        DokObject common = FindObject(repository.LoadObjects("KIDSSystemResource", 0x6D011726).Document,
            0x899F101B, 0xEF3D849D);

        int buckets = ReadCapacity(view, 0xB54C3E60);
        int queue = ReadCapacity(view, 0x98C69595);
        int commonBuckets = ReadCapacity(common, 0xB54C3E60);
        int commonQueue = ReadCapacity(common, 0x98C69595);
        if (commonBuckets != 0) buckets = commonBuckets;
        if (commonQueue != 0) queue = commonQueue;
        if (buckets <= 0 || queue <= 0 || queue > int.MaxValue / 4)
            throw new InvalidDataException("Effect2 host capacities must be positive and fit their derived allocation counts.");

        // The ordinary host supplies no dynamic-capacity override. Its dynamic
        // allocation has four 32-byte entries per queue entry; rows share the
        // queue capacity and have a separate 56-byte storage layout.
        return new(buckets, queue, queue * 4, queue);
    }

    private static DokObject FindObject(DokDocument document, uint objectId, uint classId)
    {
        DokObject[] matches = document.Objects.Where(value => value.ObjectId == objectId).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline ||
            matches[0].Reference is not null || matches[0].ClassId != classId)
            throw new InvalidDataException($"Effect2 host requires inline object {objectId:X8} of class {classId:X8}.");
        return matches[0];
    }

    private static int ReadCapacity(DokObject owner, uint nameHash)
    {
        DokProperty[] matches = owner.Properties.Where(value => value.NameHash == nameHash).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != 5 || matches[0].ElementCount != 1 ||
            matches[0].ElementSize != 4 || matches[0].RawValue.Length != 4)
            throw new InvalidDataException($"Effect2 host object {owner.ObjectId:X8} requires one UInt32 property {nameHash:X8}.");
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(matches[0].RawValue);
        if (value > int.MaxValue)
            throw new InvalidDataException($"Effect2 host property {nameHash:X8} exceeds the supported allocation count.");
        return (int)value;
    }
}
