using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed record OriginalOrdinarySdfDualSphere(Vector4 First, Vector4 Second, float ExtraRadius20);
public sealed record OriginalOrdinarySdfModelShapeRecords(uint Profile,
    IReadOnlyList<Vector4> Spheres, IReadOnlyList<OriginalOrdinarySdfDualSphere> DualSpheres);
public sealed record OriginalOrdinarySdfShapeRule(int FirstBoneKey, int? SecondBoneKey,
    float FirstRadiusScale, float SecondRadiusScale, Vector4 FirstOffset, Vector4 SecondOffset);
public sealed record OriginalOrdinarySdfProfileRule(int SphereCount, int DualSphereCount,
    uint ShapeMask, float RadiusScale);

/// <summary>Original D65F30's 30 literal shapes and 11 profile masks. This
/// consumes the actual World150 selected 48B S/Q/T array, not shader matrices
/// and not an unproven model-space animation array.</summary>
public sealed partial class OriginalOrdinarySdfModelShapeCatalog
{
    private readonly uint[] boneNameHashes;
    internal OriginalOrdinarySdfModelShapeCatalog(OriginalOrdinarySdfShapeRule[] shapes,
        OriginalOrdinarySdfProfileRule[] profiles, uint[] names)
        => (ShapeRules, ProfileRules, boneNameHashes) =
            (Array.AsReadOnly(shapes), Array.AsReadOnly(profiles), names);
    public IReadOnlyList<OriginalOrdinarySdfShapeRule> ShapeRules { get; }
    public IReadOnlyList<OriginalOrdinarySdfProfileRule> ProfileRules { get; }
    public IReadOnlyList<uint> BoneNameHashes => Array.AsReadOnly(boneNameHashes);

    /// <summary>DBC560 preserves the original signed lookup. A missing name
    /// produces nameIndex=-1; when that is below signed count6 it reads header
    /// WORD+A, rather than replacing that case with mappedJoint=-1.</summary>
    public int[] MapCurrentSkeletonNames(IReadOnlyList<uint> actualNames,
        ReadOnlySpan<byte> actualFirstPaletteTable)
    {
        int count = BinaryPrimitives.ReadInt16LittleEndian(actualFirstPaletteTable[6..]);
        int[] mapped = new int[26];
        for (int key = 0; key < mapped.Length; key++)
        {
            int nameIndex = -1;
            for (int i = 0; i < actualNames.Count; i++)
                if (actualNames[i] == boneNameHashes[key]) { nameIndex = i; break; }
            mapped[key] = nameIndex >= count ? -1 :
                BinaryPrimitives.ReadInt16LittleEndian(actualFirstPaletteTable[(12 + nameIndex * 2)..]);
        }
        return mapped;
    }

    /// <summary>Pure actual-source emitter after the DBC560 kind2/metadata/
    /// Common.bit22 gates. Profile&gt;10 has the original no-append return.</summary>
    public OriginalOrdinarySdfModelShapeRecords? BuildCurrent(uint profile,
        ReadOnlySpan<byte> actualSelectedSqtPose48, IReadOnlyList<int> actualJointMap)
    {
        if (profile > 10) return null;
        if (actualJointMap.Count != 26 || actualSelectedSqtPose48.Length % 48 != 0)
            throw new ArgumentException("SDF model shapes need the actual26 mapping and complete source48B S/Q/T pose.");
        byte[] pose = actualSelectedSqtPose48.ToArray();
        Vector4 Pose(int key, int offset)
        {
            int index = actualJointMap[key];
            if (index < 0 || (long)(index + 1) * 48 > pose.Length)
                throw new NotSupportedException("Current SDF mapped joint requires source bytes outside this supplied pose array.");
            return V(pose, index * 48 + offset);
        }
        OriginalOrdinarySdfProfileRule recipe = ProfileRules[(int)profile];
        float radiusBase = 0;
        if (actualJointMap[3] != -1 && actualJointMap[4] != -1)
        {
            Vector4 a = Pose(4, 32), b = Pose(3, 32);
            float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            float squares = x * x + y * y; squares += z * z;
            radiusBase = MathF.Sqrt(squares) * .5f;
        }
        radiusBase = MathF.Abs(radiusBase) * recipe.RadiusScale;
        uint mask = recipe.ShapeMask;
        var spheres = new List<Vector4>(recipe.SphereCount);
        var pairs = new List<OriginalOrdinarySdfDualSphere>(recipe.DualSphereCount);
        for (int i = 0; i < 30; i++, mask >>= 1)
        {
            if ((mask & 1) == 0) continue;
            OriginalOrdinarySdfShapeRule shape = ShapeRules[i];
            int first = shape.FirstBoneKey;
            Vector4 qa = Pose(first, 16), pa = Pose(first, 32);
            Vector4 a = OriginalOrdinarySdfMath.RotateQuaternion16700(qa, shape.FirstOffset);
            a = new(a.X + pa.X, a.Y + pa.Y, a.Z + pa.Z, radiusBase * shape.FirstRadiusScale);
            if (shape.SecondBoneKey is not int second) { spheres.Add(a); continue; }
            Vector4 qb = Pose(second, 16), pb = Pose(second, 32);
            Vector4 b = OriginalOrdinarySdfMath.RotateQuaternion16700(qb, shape.SecondOffset);
            float bx = b.X + pb.X, by = b.Y + pb.Y, bz = b.Z + pb.Z;
            // D66BBB / D66BFC apply these exact original second-center offsets
            // through the first quaternion, after both ordinary centers.
            if (i is 15 or 16)
            {
                Vector4 extra = OriginalOrdinarySdfMath.RotateQuaternion16700(qa, new(-5, 0, 0, 1));
                bx += extra.X; by += extra.Y; bz += extra.Z;
            }
            if (i is 13 or 14)
            {
                Vector4 extra = OriginalOrdinarySdfMath.RotateQuaternion16700(qa, new(-5, 0, 0, 1));
                bx += extra.X; by += extra.Y; bz += extra.Z;
            }
            pairs.Add(new(a, new(bx, by, bz, radiusBase * shape.SecondRadiusScale), a.W));
        }
        return new(profile, spheres.AsReadOnly(), pairs.AsReadOnly());
    }

    private static float F(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadSingleLittleEndian(b[o..]);
    private static Vector4 V(ReadOnlySpan<byte> b, int o) => new(F(b,o),F(b,o+4),F(b,o+8),F(b,o+12));
}

public static class OriginalOrdinarySdfMath
{
    /// <summary>Complete original16700 scalar circuit, including nonunit
    /// quaternion scaling and the input fourth word's direct preservation.</summary>
    public static Vector4 RotateQuaternion16700(Vector4 q, Vector4 v)
    {
        float dot = q.X * v.X + v.Y * q.Y; dot += v.Z * q.Z;
        float px = q.Y * v.Z - q.Z * v.Y; px += v.X * q.W;
        float pz = q.Z * v.X - q.X * v.Z; pz += v.Y * q.W;
        float py = q.X * v.Y - v.X * q.Y; py += v.Z * q.W;
        float x = q.Y * py - q.Z * pz; x += q.X * dot; x += px * q.W;
        float y = q.Z * px - q.X * py; y += q.Y * dot; y += pz * q.W;
        float z = q.X * pz - q.Y * px; z += q.Z * dot; z += py * q.W;
        return new(x,y,z,v.W);
    }
}
