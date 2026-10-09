using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Shared AB5310 scalar RNG. Construct once for the original owning
/// World, before its first wind generator; do not reseed on each request.</summary>
public sealed class Tree2WorldWindRandom
{
    public uint State { get; private set; } = 12345;
    internal uint Next16() { State = unchecked(State * 0x10DCDu + 1u); return State >> 16; }
}

public readonly record struct Tree2CurrentWindGlobals(
    Vector4 TargetDirection, Vector4 Direction680, float Strength690, float Phase694,
    float ShaderWindScale, float EffectiveDelta3350, float CurrentCommon254,
    uint SharedRandomState, int GeneratorStage);

/// <summary>Source D01580/AB5310 retained generator and D12A80 current View
/// writer. Main bytes are the actually prepared18C0 camera, not a declaration.
/// Current zero jitter inputs take the original no-noise branch. Positive
/// jitter requires the actual seeded104A180 producer supplied by the caller.</summary>
public sealed class Tree2CurrentWindSource
{
    private readonly float[] generator = new float[37];
    private readonly Tree2WorldWindRandom random;
    private Tree2SceneWindState scene = Tree2WindFrameSource.CreateFreshSceneWindState();
    public Tree2CurrentWindSource(Tree2WorldWindRandom actualWorldRandom)
        => random = actualWorldRandom ?? throw new ArgumentNullException(nameof(actualWorldRandom));
    public const string SourceEvidence = "CE_TC:D01580/AB5310-seed12345;D12A80-MainDC0/DD0/DF4/DF8;AB5390/AB53F0/AB54C0/AB5380;14117AA30/14117A950;FrameSync3350";

    public Tree2CurrentWindGlobals Advance(ReadOnlySpan<byte> actualPreparedMain18C0,
        MapCommonFrameSyncTime actualFrameClock, Func<float, float>? actualSeeded104A180 = null)
    {
        if (actualPreparedMain18C0.Length < 0x18C0)
            throw new ArgumentException("Current wind requires the complete prepared Main18C0.", nameof(actualPreparedMain18C0));
        float jitterAngle = Mul(F(actualPreparedMain18C0, 0xDF4), 0.017453292f);
        float jitterFrequency = F(actualPreparedMain18C0, 0xDF8);
        if (jitterAngle > 0 && jitterFrequency > 0 && actualSeeded104A180 is null)
            throw new NotSupportedException("Current positive wind jitter requires its actual shared seeded104A180 source.");

        // D12A80 exact quaternion direction expression (forward +Z).
        float x = F(actualPreparedMain18C0, 0xDC0), y = F(actualPreparedMain18C0, 0xDC4),
            z = F(actualPreparedMain18C0, 0xDC8), w = F(actualPreparedMain18C0, 0xDCC);
        float a = Add(Sub(y, Mul(z, 0)), Mul(w, 0));
        float b = Add(Sub(Mul(z, 0), x), Mul(w, 0));
        float c = Add(Sub(Mul(x, 0), Mul(y, 0)), w);
        float d = Add(Add(Mul(x, 0), Mul(y, 0)), z);
        Vector4 target = new(Add(Add(Mul(a, w), Sub(Mul(c, y), Mul(b, z))), Mul(d, x)),
            Add(Add(Mul(b, w), Sub(Mul(a, z), Mul(c, x))), Mul(d, y)),
            Add(Add(Mul(c, w), Sub(Mul(b, x), Mul(a, y))), Mul(d, z)), 0);
        float reciprocal = 1f / MathF.Sqrt(Add(Add(Mul(target.Y, target.Y), Mul(target.X, target.X)), Mul(target.Z, target.Z)));
        target.X = Mul(target.X, reciprocal); target.Y = Mul(target.Y, reciprocal); target.Z = Mul(target.Z, reciprocal);

        // AB5390 copies the eight real loaded weather amplitude parameters.
        for (int i = 0; i < 8; i++) generator[i] = F(actualPreparedMain18C0, 0xDD0 + 4 * i);
        float lower = Sub(generator[0], Mul(generator[1], .5f));
        generator[5] = lower <= generator[5] ? lower : (0 <= generator[5] ? generator[5] : 0);
        // AB53F0(...,0) directly publishes normalized target into50/60.
        generator[14] = 0; generator[15] = 0;
        generator[20] = generator[24] = target.X;
        generator[21] = generator[25] = target.Y;
        generator[22] = generator[26] = target.Z;
        generator[23] = generator[27] = 0;
        generator[28] = jitterAngle; generator[29] = jitterFrequency;

        float dt = actualFrameClock.EffectiveDelta3350;
        generator[16] = Add(dt, generator[16]); generator[13] = dt;
        float peak = generator[11];
        float admittedPeak = Add(generator[0], Mul(generator[1], .5f));
        if (peak < admittedPeak)
        {
            admittedPeak = Sub(generator[0], Mul(generator[1], .5f));
            if (admittedPeak < peak) admittedPeak = peak;
        }
        generator[11] = admittedPeak;
        int stage = BitConverter.SingleToInt32Bits(generator[36]);
        bool wave = false;
        if (stage == 0)
        {
            generator[12] = generator[5]; generator[9] = Add(dt, generator[9]);
            if (generator[10] < generator[9])
            {
                generator[36] = BitConverter.Int32BitsToSingle(1); generator[8] = 0;
                generator[11] = Add(Mul(Sub(.5f, random.Next16() / 65535f), generator[1]), generator[0]);
            }
        }
        else if (stage == 1)
        {
            float rise = generator[2] <= .01f ? .01f : generator[2];
            generator[8] = Add(Mul(dt, 3.1415927f) / rise, generator[8]);
            if (generator[8] > 3.1415927f)
            {
                generator[8] = 3.1415927f; generator[9] = 0;
                generator[36] = BitConverter.Int32BitsToSingle(2);
            }
            wave = true;
        }
        else if (stage == 2)
        {
            generator[9] = Add(dt, generator[9]);
            if (generator[4] < generator[9])
            {
                generator[9] = 0; generator[36] = BitConverter.Int32BitsToSingle(3);
            }
            generator[12] = generator[11];
        }
        else if (stage == 3)
        {
            float fall = generator[3] <= .01f ? .01f : generator[3];
            generator[8] = Add(Mul(dt, 3.1415927f) / fall, generator[8]);
            if (generator[8] > 6.2831855f)
            {
                generator[36] = 0; generator[8] = 0; generator[9] = 0;
                generator[10] = Add(Mul(Sub(.5f, random.Next16() / 65535f), generator[7]), generator[6]);
            }
            wave = true;
        }
        if (wave)
        {
            // Scalar197420 uses the same native sine polynomial as014590.
            float sine = NativeTrigonometry.SinCos014590(Sub(generator[8], 1.5707964f)).Sin;
            generator[12] = Add(Mul(Mul(Add(sine, 1), Sub(generator[11], generator[5])), .5f), generator[5]);
        }
        if (generator[12] <= 0) generator[12] = 0;
        // Actual D12A80 AB53F0(...,0) makes the interpolation duration zero.
        target = new(generator[20], generator[21], generator[22], generator[23]);
        if (jitterAngle > 0 && jitterFrequency > 0)
        {
            float noise = actualSeeded104A180!(Mul(jitterFrequency, generator[16]));
            var trig = NativeTrigonometry.SinCos014590(Mul(Mul(noise, .5f), jitterAngle));
            target = new(Add(Mul(target.Z, trig.Sin), Mul(target.X, trig.Cos)), target.Y,
                Sub(Mul(target.Z, trig.Cos), Mul(target.X, trig.Sin)), target.W);
        }
        scene = Tree2WindFrameSource.AdvanceSceneWind(scene, target, generator[12], dt,
            F(actualPreparedMain18C0, 0x143C));
        float phase = Add(Mul(scene.PhasePair.Y, 3600), scene.PhasePair.X);
        float scale = Mul(MathF.Sqrt(Mul(Mul(generator[12], F(actualPreparedMain18C0, 0x143C)), .01f)), .1f);
        return new(target, scene.SmoothedDirection, generator[12], phase, scale, dt,
            actualFrameClock.CurrentTime3348, random.State, BitConverter.SingleToInt32Bits(generator[36]));
    }

    public static void WriteCurrentView(Span<byte> currentView, Tree2CurrentWindGlobals current)
    {
        W(currentView, 0x680, current.Direction680.X); W(currentView, 0x684, current.Direction680.Y);
        W(currentView, 0x688, current.Direction680.Z); W(currentView, 0x68C, current.Direction680.W);
        W(currentView, 0x690, current.Strength690); W(currentView, 0x694, current.Phase694);
    }

    /// <summary>D79B70/79C20, only after the actual D7BB60/D7BEC0 resource
    /// admission. The caller owns previous origins and ping-pong sides.</summary>
    public static void ApplyAdmittedCurrentWindows(Span<byte> actualGraphicsState,
        ReadOnlySpan<byte> currentSelectedCamera18C0, uint windSelector,
        bool interactionUpdateAdmitted, bool windUpdateAdmitted)
    {
        if (windSelector >= 16) throw new ArgumentOutOfRangeException(nameof(windSelector));
        Vector3 position = new(F(currentSelectedCamera18C0, 0x20), F(currentSelectedCamera18C0, 0x24), F(currentSelectedCamera18C0, 0x28));
        if (interactionUpdateAdmitted)
        {
            actualGraphicsState.Slice(0x4D0, 16).CopyTo(actualGraphicsState[0x4E0..]);
            float span = F(currentSelectedCamera18C0, 0x141C), half = Mul(span, .5f);
            W(actualGraphicsState, 0x4D0, QuantizeWindowOrigin(Sub(position.X, half), span, 512));
            W(actualGraphicsState, 0x4D4, 0);
            W(actualGraphicsState, 0x4D8, QuantizeWindowOrigin(Sub(position.Z, half), span, 512));
            W(actualGraphicsState, 0x4DC, 0);
            U(actualGraphicsState, 0x404, 1u - R(actualGraphicsState, 0x404));
        }
        if (windUpdateAdmitted)
        {
            int origin = 0x4F0 + (int)windSelector * 16;
            actualGraphicsState.Slice(origin, 16).CopyTo(actualGraphicsState[(origin + 0x100)..]);
            float span = F(currentSelectedCamera18C0, 0x1438), half = Mul(span, .5f);
            W(actualGraphicsState, origin, QuantizeWindowOrigin(Sub(position.X, half), span, 64));
            W(actualGraphicsState, origin + 4, QuantizeWindowOrigin(Sub(position.Y, half), span, 64));
            W(actualGraphicsState, origin + 8, QuantizeWindowOrigin(Sub(position.Z, half), span, 64));
            W(actualGraphicsState, origin + 12, 0);
            int side = 0x40C + (int)windSelector * 4;
            U(actualGraphicsState, side, 1u - R(actualGraphicsState, side));
        }
    }

    /// <summary>141177D80: CVTTSS2SI(span), unsigned DIV(resolution),
    /// CVTTSS2SI(position), signed IDIV(step), IMUL, CVTDQ2PS.</summary>
    public static float QuantizeWindowOrigin(float positionMinusHalfSpan, float span, uint resolution)
    {
        if (resolution == 0) return 0;
        int spanInt = Cvtt(span);
        int step = unchecked((int)(unchecked((uint)spanInt) / resolution));
        if (step <= 0) return 0;
        return unchecked(Cvtt(positionMinusHalfSpan) / step * step);
    }
    private static int Cvtt(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? int.MinValue : (int)value;
    private static float F(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadSingleLittleEndian(b[offset..]);
    private static void W(Span<byte> b, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(b[offset..], value);
    private static uint R(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    private static void U(Span<byte> b, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b[offset..], value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a, float b) => a - b;
}
