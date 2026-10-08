using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Reads the verified four Army SM1G2300 hierarchy group tables. Native AB9FC0
/// copies raw SM+16 to parsed skeleton+0 and raw SM+24 to parsed+8; map entries
/// begin at parsed+0C and the signed group values follow the map. Each nonnegative
/// value gives run length value+1. This helper retains the supported single-group
/// source profile; hierarchy arithmetic and explicit root S/Q/T are separate.
/// </summary>
public static class OriginalArmyAnimationHierarchySources
{
    public static IReadOnlyList<int> ReadJointRuns(G1ModelTransforms skeleton,
        bool useAlternateSourceCount = false)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        byte[] header = skeleton.RawSkeletonHeader;
        if (header.Length != 28 || !header.AsSpan(0, 8).SequenceEqual("SM1G2300"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) != skeleton.JointDataOffset ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16)) != skeleton.UnknownValue16 ||
            BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(20)) != skeleton.Joints.Count ||
            BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(22)) != skeleton.RawJointMap.Count ||
            BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(24)) != skeleton.RawValue24 ||
            BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26)) != skeleton.RawValue26)
            throw new InvalidDataException("Army hierarchy source header differs from its parsed skeleton fields.");
        if (skeleton.UnknownValue16 != 0 || skeleton.RawValue24 != 1 || skeleton.RawValue26 != 0 ||
            skeleton.RawJointMap.Count > short.MaxValue)
            throw new InvalidDataException("Army hierarchy group extraction requires the verified flags-0, single-group SM1G2300 source profile.");
        int groupCount = useAlternateSourceCount ? unchecked((short)skeleton.RawValue24) :
            (int)((skeleton.UnknownValue16 >> 1) & 0x7FFF) + 1;
        int mapBytes = skeleton.RawJointMap.Count * 2;
        int expectedTailBytes = ((mapBytes + groupCount * 2 + 3) & ~3) - mapBytes;
        byte[] tail = skeleton.UnknownBeforeJoints;
        if (tail.Length != expectedTailBytes || skeleton.JointDataOffset != 28 + mapBytes + tail.Length ||
            tail.AsSpan(groupCount * 2).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Army hierarchy group table has unsupported length or alignment bytes.");
        int sourceValue = BinaryPrimitives.ReadInt16LittleEndian(tail);
        if (sourceValue < 0 || sourceValue + 1 != skeleton.Joints.Count)
            throw new InvalidDataException("Army hierarchy single source group does not exactly cover its joint array.");
        return Array.AsReadOnly(new[] { sourceValue + 1 });
    }
}
