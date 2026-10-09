using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public enum WorldPrimitiveSortFamily { Opaque, Alpha, Shadow }

/// <summary>Actual ViewParam class B5EA88F0. Its three byte lists are sort
/// passes, in execution order, rather than a lexicographic comparator list.</summary>
public sealed class WorldPrimitiveOrderSource
{
    private readonly byte[][] fields;
    private readonly ushort[][] descriptors;
    private WorldPrimitiveOrderSource(uint objectId, uint parallelPrimitiveUnitCount,
        byte[][] fields, string sourceIdentity)
    {
        ObjectId = objectId; ParallelPrimitiveUnitCount = parallelPrimitiveUnitCount;
        this.fields = fields; SourceIdentity = sourceIdentity;
        descriptors = fields.Select(value => WorldPrimitiveOrderSource.BuildDescriptor(value)).ToArray();
    }
    public uint ObjectId { get; }
    public uint ParallelPrimitiveUnitCount { get; }
    public string SourceIdentity { get; }
    public IReadOnlyList<byte> Fields(WorldPrimitiveSortFamily family) => Array.AsReadOnly(fields[(int)family]);
    public IReadOnlyList<ushort> Descriptor(WorldPrimitiveSortFamily family) => Array.AsReadOnly(descriptors[(int)family]);

    /// <summary>Import once with the actual default View0 declaration. Current
    /// retargeting can load another explicitly selected ViewParam declaration.</summary>
    public static WorldPrimitiveOrderSource LoadDefaultView0(GameResourceRepository repository)
    {
        var view = DefaultView0Source.LoadDeclarationDefaults(repository);
        return Read(view.CopyViewParameterDeclaration(), view.SourceIdentity);
    }

    public static WorldPrimitiveOrderSource Read(DokObject actualViewParameter, string sourceIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualViewParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentity);
        if (actualViewParameter.Kind != DokObjectKind.Inline || actualViewParameter.ClassId != 0xB5EA88F0)
            throw new InvalidDataException("Original primitive ordering requires the selected ViewParam class B5EA88F0.");
        uint[] hashes = [0xDD5AF9BB, 0xB5467DC8, 0x8A96C17A];
        byte[][] fields = hashes.Select(hash =>
        {
            DokProperty property = actualViewParameter.Properties.Single(p => p.NameHash == hash);
            if (property.TypeCode != 1 || property.ElementCount != 15 || property.ElementSize != 1 || property.RawValue.Length != 15)
                throw new InvalidDataException("Original ViewParam needs its complete three fifteen-byte sort lists.");
            return property.RawValue.ToArray();
        }).ToArray();
        DokProperty parallel = actualViewParameter.Properties.Single(p => p.NameHash == 0x1BA8C540);
        if (parallel.TypeCode != 5 || parallel.ElementCount != 1 || parallel.RawValue.Length != 4)
            throw new InvalidDataException("Original ViewParam needs the actual primitive parallel-unit declaration.");
        return new(actualViewParameter.ObjectId, BinaryPrimitives.ReadUInt32LittleEndian(parallel.RawValue), fields, sourceIdentity);
    }

    /// <summary>D4D590's calls to D71B60: initialize all fields to 0F00,
    /// enable each declared field with direction1 and priority15-ordinal.
    /// Repeated fields replace their previous position exactly as the setter.</summary>
    public static ushort[] BuildDescriptor(ReadOnlySpan<byte> actualFields)
    {
        if (actualFields.Length != 15) throw new InvalidDataException("Original ViewParam sort list has fifteen bytes.");
        var words = Enumerable.Repeat((ushort)0x0F00, 15).ToArray();
        for (int ordinal = 0; ordinal < 15; ordinal++)
        {
            int field = actualFields[ordinal];
            if (field >= 15) continue;
            List<int> chain = WorldPrimitiveSortDescriptor.ReadActualFields(words).ToList();
            chain.Remove(field);
            byte priority = (byte)(15 - ordinal);
            int insertion = chain.FindIndex(other => (byte)words[other] <= priority);
            if (insertion < 0) insertion = chain.Count;
            chain.Insert(insertion, field);
            words[field] = (ushort)(0x2000 | 0x0F00 | priority);
            for (int i = 0; i < chain.Count; i++)
            {
                int next = i + 1 < chain.Count ? chain[i + 1] : 15;
                words[chain[i]] = (ushort)((words[chain[i]] & 0xE0FF) | (next << 8));
            }
        }
        return words;
    }

    /// <summary>D4B7E0 selected passes, each an ascending stable eight-byte
    /// radix sort. The last pass is most significant; equal keys retain the
    /// input publication order. The caller supplies actual tuple key fields.
    /// An independently owned shader-state identity may use its own ordered
    /// allocation identity; original-process addresses are unnecessary.</summary>
    public T[] Sort<T>(WorldPrimitiveSortFamily family, IReadOnlyList<T> actualInsertionOrder,
        Func<T, int, ulong> readActualUnsignedField)
    {
        ArgumentNullException.ThrowIfNull(actualInsertionOrder); ArgumentNullException.ThrowIfNull(readActualUnsignedField);
        T[] current = actualInsertionOrder.ToArray(), scratch = new T[current.Length];
        Span<int> counts = stackalloc int[256];
        foreach (int field in WorldPrimitiveSortDescriptor.ReadActualFields(descriptors[(int)family]))
        {
            ulong[] keys = current.Select(item => readActualUnsignedField(item, field)).ToArray();
            ulong[] scratchKeys = new ulong[keys.Length];
            for (int shift = 0; shift < 64; shift += 8)
            {
                counts.Clear();
                foreach (ulong key in keys) counts[(byte)(key >> shift)]++;
                int sum = 0;
                for (int bucket = 0; bucket < counts.Length; bucket++)
                { int count = counts[bucket]; counts[bucket] = sum; sum += count; }
                for (int i = 0; i < current.Length; i++)
                {
                    int target = counts[(byte)(keys[i] >> shift)]++;
                    scratch[target] = current[i]; scratchKeys[target] = keys[i];
                }
                (current, scratch) = (scratch, current); (keys, scratchKeys) = (scratchKeys, keys);
            }
        }
        return current;
    }
}

public readonly record struct WorldPrimitiveDepthRange(float Span19C0, float Scale19C4);
public readonly record struct WorldPrimitiveModelDistance(float ForceRegisterNormalZDistance1A0,
    float RegisterNormalZDistanceBias1A4);

/// <summary>The first-counter tuple+8 key. Camera values and current oriented
/// bounds stay current; none of these calculations reads files or fingerprints.</summary>
public static class WorldPrimitiveDistanceKey
{
    /// <summary>D073C5 -> 1090710 -> 1090890 constructs the current
    /// PreparedView18C0 frustum from Camera100, rather than copying a row of
    /// Camera40. Its +40 plane uses the actual Camera158 reverse-depth bit.</summary>
    public static Vector4 PrepareViewZPlane(Matrix4x4 actualWorldToProjection100, uint actualCameraFlags158)
    {
        Vector4 plane = new(actualWorldToProjection100.M13, actualWorldToProjection100.M23,
            actualWorldToProjection100.M33, actualWorldToProjection100.M43);
        if ((actualCameraFlags158 & 2) != 0)
            plane = new(plane.X - actualWorldToProjection100.M14, plane.Y - actualWorldToProjection100.M24,
                plane.Z - actualWorldToProjection100.M34, plane.W - actualWorldToProjection100.M44);
        else
        {
            float Negate(float value) => BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value) ^ 0x80000000u);
            plane = new(Negate(plane.X), Negate(plane.Y), Negate(plane.Z), Negate(plane.W));
        }
        float lengthSquared = plane.X * plane.X + plane.Y * plane.Y;
        lengthSquared += plane.Z * plane.Z;
        float inverse = 1f / MathF.Sqrt(lengthSquared);
        return new(inverse * plane.X, inverse * plane.Y, inverse * plane.Z, inverse * plane.W);
    }

    public static WorldPrimitiveDepthRange PrepareView(float actualCameraNear140, float actualCameraFar144)
    {
        float span = actualCameraFar144 - actualCameraNear140;
        span += 1f;
        float doubled = span + span;
        return new(span, (float)(18446744073709551616d / (double)doubled));
    }

    public static WorldPrimitiveModelDistance ReadModelDeclaration(DokObject actualResolvedModel)
    {
        ArgumentNullException.ThrowIfNull(actualResolvedModel);
        float Read(uint hash)
        {
            DokProperty property = actualResolvedModel.Properties.Single(p => p.NameHash == hash);
            if (property.TypeCode != 8 || property.ElementCount != 1 || property.RawValue.Length != 4)
                throw new InvalidDataException("Model normal-Z distance fields require the actual float32 declaration.");
            return BinaryPrimitives.ReadSingleLittleEndian(property.RawValue);
        }
        return new(Read(0x6D640FFA), Read(0x5B6918C2));
    }

    /// <summary>A10FD0 support point along the current View's Z-plane normal,
    /// followed by C39090's separately rounded negative plane distance.</summary>
    public static float ReadOrientedBoundsDistance(MapModelNativeOrientedBounds actualBounds, Vector4 actualViewZPlane)
    {
        ArgumentNullException.ThrowIfNull(actualBounds);
        Vector4 point = actualBounds.Center0;
        Vector4 AddAxis(Vector4 axis, float extent, Vector4 current)
        {
            float dot = axis.Y * actualViewZPlane.Y + axis.X * actualViewZPlane.X;
            dot += axis.Z * actualViewZPlane.Z;
            float signedExtent = (dot > 0f ? 1f : -1f) * extent;
            return new(current.X + signedExtent * axis.X, current.Y + signedExtent * axis.Y,
                current.Z + signedExtent * axis.Z, current.W + signedExtent * axis.W);
        }
        point = AddAxis(actualBounds.Basis10, actualBounds.HalfExtents40.X, point);
        point = AddAxis(actualBounds.Basis20, actualBounds.HalfExtents40.Y, point);
        point = AddAxis(actualBounds.Basis30, actualBounds.HalfExtents40.Z, point);
        float distance = point.Y * actualViewZPlane.Y + point.X * actualViewZPlane.X;
        distance += point.Z * actualViewZPlane.Z; distance += actualViewZPlane.W;
        return BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(distance) ^ 0x80000000u);
    }

    public static ulong PrepareFirstCounter(WorldPrimitiveDepthRange actualViewRange,
        float actualModelOrBoundsDistance, float actualModelBias)
    {
        float upper = actualViewRange.Span19C0 - 1f;
        upper = upper > 0f ? upper : 0f;
        float bounded = actualModelOrBoundsDistance + actualModelBias;
        if (!(upper >= bounded)) bounded = upper;
        float value = actualViewRange.Span19C0 + bounded;
        if (0f >= value) return 0;
        value *= actualViewRange.Scale19C4;
        ulong add = 0;
        const float two63 = 9223372036854775808f;
        if (!(value < two63))
        {
            value -= two63;
            if (!(value >= two63)) add = 0x8000000000000000UL;
        }
        // Exact CVTTSS2SI signed64 result, including its hardware indefinite
        // result for a nonfinite or out-of-range scalar.
        long converted = !float.IsFinite(value) || value >= two63 || value < -two63 ? long.MinValue : (long)value;
        return unchecked((ulong)converted + add);
    }
}
