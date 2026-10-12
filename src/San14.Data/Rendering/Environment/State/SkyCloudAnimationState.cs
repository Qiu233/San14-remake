using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>An immutable animation candidate. Its owner commits it only after
/// the actual complete frame; preparing a draw material does not advance it.</summary>
public sealed record SkyCloudAnimationState(
    Vector3 BasePhase, Vector3 DetailPhase,
    Vector2 DistortPhase, Vector2 CirrusPhase, Vector2 CirrusDistortPhase,
    Vector2 ReservedPhase0, Vector2 ReservedPhase1, Vector3 PendingDisplacement)
{
    public static SkyCloudAnimationState Fresh { get; } = new(
        Vector3.Zero, Vector3.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero,
        Vector2.Zero, Vector2.Zero, Vector3.Zero);

    public SkyCloudAnimationState Advance(SkyCloudMaterial material, float delta) =>
        AdvanceCore(material, delta, false);

    private SkyCloudAnimationState AdvanceCore(SkyCloudMaterial material, float delta, bool initial)
    {
        ArgumentNullException.ThrowIfNull(material);
        return new(
            Phase(BasePhase, material.Base, delta), Phase(DetailPhase, material.Detail, delta),
            Phase(DistortPhase, material.Distort, delta), Phase(CirrusPhase, material.Cirrus, delta),
            Phase(CirrusDistortPhase, material.CirrusDistort, delta),
            Reserved(ReservedPhase0, delta), Reserved(ReservedPhase1, delta),
            new(Displacement(PendingDisplacement.X, delta, material.Base.Velocity.X, material.Base.Period.X, initial),
                Displacement(PendingDisplacement.Y, delta, material.Base.Velocity.Y, material.Base.Period.Y, initial),
                Displacement(PendingDisplacement.Z, delta, material.Base.Velocity.Z, material.Base.Period.Z, initial)));
    }

    /// <summary>Main's fresh displacement is a direct product, preserving its
    /// signed zero instead of adding it to a constructor zero.</summary>
    public static SkyCloudAnimationState PrepareInitial(SkyCloudMaterial material, float elapsed) =>
        Fresh.AdvanceCore(material, elapsed, true);

    private static float Displacement(float prior, float delta, float velocity, float period, bool initial)
    {
        float product = M(delta, velocity);
        return Remainder(initial ? product : A(prior, product), period);
    }
    private static Vector3 Phase(Vector3 phase, SkyCloudNoiseLayer3 layer, float delta) => new(
        Remainder(S(phase.X, M(delta, layer.Velocity.X)), layer.Period.X),
        Remainder(S(phase.Y, M(delta, layer.Velocity.Y)), layer.Period.Y),
        Remainder(S(phase.Z, M(delta, layer.Velocity.Z)), layer.Period.Z));
    private static Vector2 Phase(Vector2 phase, SkyCloudNoiseLayer2 layer, float delta) => new(
        Remainder(S(phase.X, M(delta, layer.Velocity.X)), layer.Period.X),
        Remainder(S(phase.Y, M(delta, layer.Velocity.Y)), layer.Period.Y));
    private static Vector2 Reserved(Vector2 phase, float delta) => new(
        Remainder(S(phase.X, M(delta, 0)), SkyCloudMaterial.ReservedPeriod),
        Remainder(S(phase.Y, M(delta, 0)), SkyCloudMaterial.ReservedPeriod));

    private static float Remainder(float value, float period)
    {
        if (!float.IsFinite(value) || !float.IsFinite(period) || period <= 0)
            throw new InvalidDataException("Cloud phase remainder requires finite values and positive periods.");
        double x = Math.Abs((double)value), y = period;
        if (x == y) return BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value) & 0x80000000u);
        if (x < y) return value;
        int a = (int)((BitConverter.DoubleToUInt64Bits(x) >> 52) & 2047);
        int b = (int)((BitConverter.DoubleToUInt64Bits(y) >> 52) & 2047);
        int blocks = a > b ? (a - b) / 24 : 0;
        double scaled = DM(y, BitConverter.UInt64BitsToDouble((ulong)(blocks * 24 + 1023) << 52));
        for (int n = 0; n < blocks; n++)
        {
            x = DS(x, DM((int)DD(x, scaled), scaled));
            scaled = DM(scaled, BitConverter.UInt64BitsToDouble(0x3E70000000000000));
        }
        x = DS(x, DM((int)DD(x, scaled), scaled));
        if (value < 0) x = -x;
        return (float)x;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double DM(double a, double b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double DS(double a, double b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double DD(double a, double b) => a / b;
}
