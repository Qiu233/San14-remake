using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One actual compact controller pass. BE0 is the current light
/// transform; Basis60 comes from the same pass's numeric matrix producer.
/// The caster accumulator and dirty word retain their current values.</summary>
public readonly record struct OriginalDeferredEarlyShadowCasterPassNumericFields(
    Vector4 Minimum0, Vector4 Maximum10, Vector4 CasterMinimum20, Vector4 CasterMaximum30,
    uint CasterDirty44, Matrix4x4 Basis60, Matrix4x4 WorldToLightBE0);

public sealed class OriginalDeferredEarlyShadowCasterNumericState
{
    private readonly OriginalDeferredEarlyShadowCasterPassNumericFields?[] passes;
    private readonly float?[] distances;
    public OriginalDeferredEarlyShadowCasterNumericState(
        IReadOnlyList<OriginalDeferredEarlyShadowCasterPassNumericFields?> actualPasses,
        uint actualEffectiveMaskBC4, uint actualTouchedMaskBC8, IReadOnlyList<float?> actualDistances400)
    {
        ArgumentNullException.ThrowIfNull(actualPasses); ArgumentNullException.ThrowIfNull(actualDistances400);
        if (actualPasses.Count is < 1 or > 4 || actualDistances400.Count != actualPasses.Count)
            throw new ArgumentException("The compact caster callback needs its actual one to four pass fields and distances.");
        passes = actualPasses.ToArray(); distances = actualDistances400.ToArray();
        EffectiveMaskBC4 = actualEffectiveMaskBC4; TouchedMaskBC8 = actualTouchedMaskBC8;
    }
    public IReadOnlyList<OriginalDeferredEarlyShadowCasterPassNumericFields?> Passes => Array.AsReadOnly(passes);
    public IReadOnlyList<float?> Distances400 => Array.AsReadOnly(distances);
    public uint EffectiveMaskBC4 { get; }
    public uint TouchedMaskBC8 { get; private set; }

    /// <summary>1057330/1057350 (CPU128): independent shadow culling and
    /// accumulation, before queue-mask intersection in D08F80. Main-camera
    /// visibility never supplies this result.</summary>
    public uint TestAndAccumulate(Matrix4x4 actualWorldBoxMatrix)
    {
        uint accepted = 0;
        for (int i = 0; i < passes.Length; i++)
        {
            uint bit = 1u << i;
            if ((EffectiveMaskBC4 & bit) == 0) continue;
            var pass = passes[i] ?? throw new InvalidDataException("An effective caster pass has no actual clip/basis/matrix fields.");
            var (lo, hi) = OriginalDeferredEarlyShadowCasterSource.TransformBoundA05580(
                actualWorldBoxMatrix, pass.WorldToLightBE0, pass.Basis60);
            if (!(lo.Z < pass.Maximum10.Z) ||
                !(Min(hi.X, pass.Maximum10.X) > Max(lo.X, pass.Minimum0.X)) ||
                !(Min(hi.Y, pass.Maximum10.Y) > Max(lo.Y, pass.Minimum0.Y))) continue;
            accepted |= bit;
            passes[i] = pass with { CasterMinimum20 = new(Min(pass.CasterMinimum20.X, lo.X),
                    Min(pass.CasterMinimum20.Y, lo.Y), Min(pass.CasterMinimum20.Z, lo.Z), Min(pass.CasterMinimum20.W, lo.W)),
                CasterMaximum30 = new(Max(pass.CasterMaximum30.X, hi.X), Max(pass.CasterMaximum30.Y, hi.Y),
                    Max(pass.CasterMaximum30.Z, hi.Z), Max(pass.CasterMaximum30.W, hi.W)), CasterDirty44 = 1 };
            distances[i] = lo.Z;
        }
        TouchedMaskBC8 |= accepted;
        return accepted;
    }
    private static float Min(float a, float b) => a < b ? a : b;
    private static float Max(float a, float b) => a > b ? a : b;
}

/// <summary>The current independent shadow culler, after the actual compact
/// work-volume producer. Initial accumulation comes from its genuine v78
/// reset; inactive distance words are unknown until1057330 writes them.</summary>
public sealed class OriginalDeferredEarlyShadowCurrentCasterFields
{
    internal OriginalDeferredEarlyShadowCurrentCasterFields(OriginalDeferredEarlyShadowCurrentCompactVolumeFields volume)
    {
        Volume = volume;
        float positive = BitConverter.UInt32BitsToSingle(0x7722B3DB), negative = BitConverter.UInt32BitsToSingle(0xF722B3DB);
        var passes = volume.Numeric.Passes.Select(pass => pass is null ? (OriginalDeferredEarlyShadowCasterPassNumericFields?)null :
            new(pass.ClipMinimum,pass.ClipMaximum,new(positive,positive,positive,1),new(negative,negative,negative,1),
                0,pass.AxisBasis,pass.LightView)).ToArray();
        Numeric = new(passes,volume.Numeric.EffectiveMaskBC4,0,new float?[passes.Length]);
    }
    public OriginalDeferredEarlyShadowCurrentCompactVolumeFields Volume { get; }
    public OriginalDeferredEarlyShadowCasterNumericState Numeric { get; }
    public string SourceEvidence => Volume.SourceEvidence + ";D123E0-v78->1058680/1057510-true-reset20/30/44/BC8;" +
        "D08F80->1057330-independent-current-caster-accumulation;not-population-publication";
    /// <summary>D08F80 invokes the full culler before intersecting the
    /// caller mask. Even filtered passes retain their actual accumulation.</summary>
    public uint TestCurrentBound(OriginalMapModelNativeOrientedBounds actualBounds, uint actualAllowedPassMask)
        => Numeric.TestAndAccumulate(OriginalDeferredEarlyShadowCasterSource.PrepareWorldBox(actualBounds)) & actualAllowedPassMask;
}

/// <summary>Finite C394F0 → A0E800/D19B10 → D08F80/1057330
/// source kernels. Actual World caller masks, record selection and queue
/// allocation stay explicit; this does not certify a complete caster list.</summary>
public static class OriginalDeferredEarlyShadowCasterSource
{
    public const uint WorldCasterFlagsPropertyHash = 0x62C508EB;
    public const string SourceEvidence = "TC:C394F0-off-main-visible-shadow-path;C39F6E..C3A062;" +
        "D08F80-current-CPU128;compact1057330/1057350/A05580;View19A0-entry16/pass+2;" +
        "not-ordinary-Main-visible-list/not-current-queue-publication";

    public static OriginalDeferredEarlyShadowCurrentCasterFields PrepareCurrent(
        OriginalDeferredEarlyShadowCurrentCompactVolumeFields actualVolume)
    {
        ArgumentNullException.ThrowIfNull(actualVolume);
        return new(actualVolume);
    }

    /// <summary>Load boundary: DA6E64 initializes localC8 to zero; pointer
    /// table6F8 (ordinal31) reads62C508EB; DA9147/14D writes World10C.
    /// This is the source-loader value. An owned current node must retain it
    /// or supply a real later setter value before using the caller gates.</summary>
    public static int ReadLoadedWorldCasterFlags10C(DokObject actualWorldDefinition)
    {
        ArgumentNullException.ThrowIfNull(actualWorldDefinition);
        if (actualWorldDefinition.Kind != DokObjectKind.Inline ||
            actualWorldDefinition.ClassId != OriginalScenePlacements.ModelWorldPqClass)
            throw new InvalidDataException("Shadow caster declaration requires its actual ModelWorldPQ definition.");
        var fields = actualWorldDefinition.Properties.Where(p => p.NameHash == WorldCasterFlagsPropertyHash).ToArray();
        if (fields.Length == 0) return 0;
        if (fields.Length != 1 || fields[0].Index != 31 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 ||
            fields[0].RawValue.Length != 4)
            throw new InvalidDataException("World10C loader needs the exact31/62C508EB uint32 binding.");
        return BinaryPrimitives.ReadInt32LittleEndian(fields[0].RawValue);
    }

    /// <summary>The normal low-three-bit-zero route. C39F88..FC1 requires
    /// F1C.bit0 and selects/inverts the actual World10C.bit0 argument by bit1.
    /// An eligible instance still needs the selected record/exclusion gates.</summary>
    public static bool ReadFirstCasterGate(byte actualLightDataF1C, bool actualWorld10CBit0)
        => (actualLightDataF1C & 1) != 0 && ((actualLightDataF1C & 2) != 0 ? !actualWorld10CBit0 : actualWorld10CBit0);

    /// <summary>C392B0 clears the caller's shadow mask when Common.bit11
    /// is off, then intersects the actual View mask3340. No missing mask is
    /// replaced by ordinary eligibility.</summary>
    public static uint SelectCasterViewMask(uint actualCallerShadowMask, uint actualCommonFlags8,
        uint actualViewMask3340, int actualWorld10C)
        => (actualCommonFlags8 & (1u << 11)) == 0 || actualWorld10C < 0
            ? 0 : actualCallerShadowMask & actualViewMask3340;

    /// <summary>C39FD4..A01C bounds preparation: actual oriented box support
    /// endpoints, followed by D19B10's unrotated box matrix. The source A0E800
    /// temporary stores maximum before minimum and C394F0 swaps them back.</summary>
    public static Matrix4x4 PrepareWorldBox(OriginalMapModelNativeOrientedBounds actualBounds)
    {
        ArgumentNullException.ThrowIfNull(actualBounds);
        Vector3 lo = new(OriginalMapModelNativeVisibilitySources.SupportPoint(actualBounds, -Vector3.UnitX).X,
            OriginalMapModelNativeVisibilitySources.SupportPoint(actualBounds, -Vector3.UnitY).Y,
            OriginalMapModelNativeVisibilitySources.SupportPoint(actualBounds, -Vector3.UnitZ).Z);
        Vector3 hi = new(OriginalMapModelNativeVisibilitySources.SupportPoint(actualBounds, Vector3.UnitX).X,
            OriginalMapModelNativeVisibilitySources.SupportPoint(actualBounds, Vector3.UnitY).Y,
            OriginalMapModelNativeVisibilitySources.SupportPoint(actualBounds, Vector3.UnitZ).Z);
        float x = M(S(hi.X, lo.X), .5f), y = M(S(hi.Y, lo.Y), .5f), z = M(S(hi.Z, lo.Z), .5f);
        return new(x,0,0,0,0,y,0,0,0,0,z,0,A(x,lo.X),A(y,lo.Y),A(z,lo.Z),1);
    }

    /// <summary>Actual A05580 scalar-SSE grouping. The radius uses the
    /// separately prepared Basis60 rows, not an assumed inverse matrix.</summary>
    public static (Vector4 Minimum, Vector4 Maximum) TransformBoundA05580(Matrix4x4 box,
        Matrix4x4 actualWorldToLight, Matrix4x4 actualBasis60)
    {
        float x = A(A(A(M(box.M42,actualWorldToLight.M21),M(box.M41,actualWorldToLight.M11)),
            M(box.M43,actualWorldToLight.M31)),actualWorldToLight.M41);
        float y = A(A(A(M(box.M41,actualWorldToLight.M12),M(box.M42,actualWorldToLight.M22)),
            M(box.M43,actualWorldToLight.M32)),actualWorldToLight.M42);
        float z = A(A(A(M(box.M41,actualWorldToLight.M13),M(box.M42,actualWorldToLight.M23)),
            M(box.M43,actualWorldToLight.M33)),actualWorldToLight.M43);
        float ex = Radius(box,new(actualBasis60.M11,actualBasis60.M12,actualBasis60.M13));
        float ey = Radius(box,new(actualBasis60.M21,actualBasis60.M22,actualBasis60.M23));
        float ez = Radius(box,new(actualBasis60.M31,actualBasis60.M32,actualBasis60.M33));
        return (new(S(x,ex),S(y,ey),S(z,ez),1), new(A(x,ex),A(y,ey),A(z,ez),1));
    }

    /// <summary>C3A085..B5: first accepted cascade initializes the shared
    /// tuple+8 key. BB85A0 reuses that same tuple for subsequent pass lists.</summary>
    public static ulong PrepareFirstAcceptedDistanceKey(float actualFirstAcceptedPassDistance)
    {
        // TC01343C4C is float32 100, loaded into xmm6 at C3986F.
        const float offsetAndScale = 100f;
        float value = M(A(actualFirstAcceptedPassDistance,offsetAndScale),offsetAndScale);
        const float two63 = 9223372036854775808f;
        ulong add = 0;
        if (!(value < two63))
        {
            value = S(value,two63);
            if (!(value >= two63)) add = 0x8000000000000000UL;
        }
        long result = !float.IsFinite(value) || value >= two63 || value < -two63 ? long.MinValue : (long)value;
        return unchecked((ulong)result+add);
    }
    private static float Radius(Matrix4x4 box, Vector3 axis)
    {
        float row1 = A(A(M(axis.Y,box.M22),M(axis.X,box.M21)),M(axis.Z,box.M23));
        float row0 = A(A(M(box.M11,axis.X),M(box.M12,axis.Y)),M(box.M13,axis.Z));
        float row2 = A(A(M(axis.Y,box.M32),M(axis.X,box.M31)),M(axis.Z,box.M33));
        return A(A(MathF.Abs(row1),MathF.Abs(row0)),MathF.Abs(row2));
    }
    private static float M(float a,float b)=>OriginalNativeFloat.Multiply(a,b);
    private static float A(float a,float b)=>OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b)=>OriginalNativeFloat.Subtract(a,b);
}
