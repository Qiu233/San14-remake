using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Actual ordinary Army v20 inputs. Frame milliseconds/interval come
/// from 936440/32C510; gameplay direction36 is independent of Action37.</summary>
public readonly record struct ArmyGraphicsMotionUpdate(ushort CurrentHex2A,
    byte Direction36, bool ArmyValid, byte Flags26, byte? Byte23, float Mobility2B0660,
    float FrameMilliseconds936440, int UpdateInterval32C510);

public readonly record struct ArmyGraphicsMotionStep(bool ExecutedMotion,
    bool Changed, bool TargetChanged, int StepMilliseconds, int GraphicsAction60, byte Skip8DC);

/// <summary>Retained ordinary CUnitArmy+50/70 motion scalars and 19 complete 64-byte
/// follower records. No assets, Core mission inference or renderer elapsed clock.</summary>
public sealed class ArmyGraphicsMotion
{
    const float Epsilon = 1.1920929e-05f;
    const float TwoPi = 6.2831855f;
    readonly byte[] records;
    readonly string callerSource, language;
    Vector4 centerCurrent, centerTarget;
    float centerHeading, targetHeading, centerSpeed;
    public int TargetHex64 { get; private set; }
    public int AuxiliaryTargetHex68 { get; private set; }
    public int GraphicsAction60 { get; private set; }
    public byte Skip8DC { get; private set; }
    public Vector4 CenterCurrent => centerCurrent;
    public Vector4 CenterTarget => centerTarget;
    public float CenterHeading => centerHeading;
    public float TargetHeading => targetHeading;
    public float CenterSpeed => centerSpeed;
    public ReadOnlyMemory<byte> RecordBytes => records;
    /// <summary>Current record7 XYZ for model roots. The actual Unit position
    /// copy writes only X at +10 and Z at +18, exposed separately below.</summary>
    public Vector3 UnitPosition10 { get { int at = ArmyInstancePoseCodeSources.BannerRecordIndex * 64 + 0x10;
        return new(F(at), F(at + 4), F(at + 8)); } }
    public Vector2 UnitPosition10Xz => new(UnitPosition10.X, UnitPosition10.Z);
    public float UnitHeading744 => F(ArmyInstancePoseCodeSources.BannerRecordIndex * 64 + 0x30);

    /// <summary>Consumes the already source-initialized nineteen records and the
    /// actual initial center/heading. Fresh1C54C0 action0 and1C8170 skip are retained;
    /// the caller supplies the independent auxiliary Hex field.</summary>
    public ArmyGraphicsMotion(ArmyInstancePoseSnapshot initial,
        Vector4 actualCenter, float actualHeading, ushort actualHex2A,
        ushort actualCommanderWord12, int actualUpdateInterval32C510, int actualAuxiliaryHex68)
    {
        if (initial.Records.Count != ArmyInstancePose.RecordCount ||
            initial.Records.Any(record => record.RawBytes.Length != ArmyInstancePose.RecordBytes))
            throw new InvalidDataException("Graphics motion requires complete source-initialized nineteen records.");
        records = initial.Records.SelectMany(record => record.RawBytes).ToArray();
        callerSource = initial.CallerSource; language = initial.CodeSource.Language;
        centerCurrent = centerTarget = actualCenter;
        centerHeading = targetHeading = actualHeading;
        TargetHex64 = actualHex2A; AuxiliaryTargetHex68 = actualAuxiliaryHex68;
        Skip8DC = (byte)(actualCommanderWord12 % actualUpdateInterval32C510);
    }

    public ArmyInstancePoseSnapshot CaptureRecords() =>
        ArmyInstancePose.ReadRecords(records, callerSource + "; retained ordinary1CA280 motion", language);

    /// <summary>1C15D0/1C8A60. Only the caller's actual current Army Hex change
    /// selects a new target; current positions, original delay and unrelated raw
    /// record bytes survive. The original target reset predicate is preserved.</summary>
    public bool SetCurrentArmyHex(ushort actualCurrentHex2A, Vector4 actualHexPosition188880, float actualDirectionHeading)
    {
        if (TargetHex64 == actualCurrentHex2A) return false;
        TargetHex64 = actualCurrentHex2A;
        SetTargets(actualHexPosition188880, actualDirectionHeading);
        return true;
    }

    public void SetTargets(Vector4 actualPosition, float actualHeading)
    {
        centerTarget = actualPosition; targetHeading = actualHeading;
        var (sin, cos) = ArmyAnimationSampler.SourceSinCos(actualHeading);
        for (int i = 0; i < ArmyInstancePose.RecordCount; i++)
        {
            int at = i * 64;
            if (F(at + 0x1C) < 0 || F(at + 0x2C) < 0 || F(at + 0x30) < 0 ||
                (MathF.Abs(Subtract(F(at + 0x10), F(at + 0x20))) < Epsilon &&
                 MathF.Abs(Subtract(F(at + 0x18), F(at + 0x28))) < Epsilon))
            { W(at + 0x34, 0); I(at + 0x3C, 0); }
            Vector3 row = ArmyInstancePoseCodeSources.FormationRows[i];
            float x = Add(Add(Product(Product(row.Y, 90), sin), Product(Product(row.X, 90), cos)), actualPosition.X);
            float y = Add(actualPosition.Y, 0);
            float z = Add(Subtract(Product(Product(row.Y, 90), cos), Product(Product(row.X, 90), sin)), actualPosition.Z);
            if (y < 445) y = 445;
            V(at + 0x20, new(x, y, z, 1));
        }
    }

    /// <summary>The normal nonretiring1CA710 motion prefix, before camera/visible
    /// admission. sourceHexPosition is queried only when2A changes on an actual
    /// motion step. Null terrain-query result means known absent terrain owner.</summary>
    public ArmyGraphicsMotionStep UpdateOrdinary(ArmyGraphicsMotionUpdate input,
        Func<ushort, Vector4> sourceHexPosition188880, Func<float, float, float?> actualTerrainQuery)
    {
        bool executed = false, targetChanged = false, changed = false;
        int step = 0;
        if ((int)input.Mobility2B0660 < 27)
        {
            if (Skip8DC == 0)
            {
                step = unchecked((int)input.FrameMilliseconds936440 * input.UpdateInterval32C510);
                Execute();
            }
            Skip8DC = (byte)((Skip8DC + 1) % input.UpdateInterval32C510);
        }
        else
        {
            float factor = (int)input.Mobility2B0660 < 30 ? 1 : Divide(input.Mobility2B0660, 30);
            step = (int)Product(input.FrameMilliseconds936440, Add(Skip8DC, factor));
            Execute(); Skip8DC = 0;
        }
        return new(executed, changed, targetChanged, step, GraphicsAction60, Skip8DC);

        void Execute()
        {
            executed = true;
            if (TargetHex64 != input.CurrentHex2A)
                targetChanged = SetCurrentArmyHex(input.CurrentHex2A, sourceHexPosition188880(input.CurrentHex2A),
                    ArmyGraphicsMotionMath.Direction(input.Direction36));
            bool active = false;
            if (input.ArmyValid && (input.Flags26 & 1) != 0)
                active = (input.Byte23 ?? throw new InvalidDataException("Reached source Army.byte23 gate is unknown.")) == 0;
            changed = Advance(step, active, actualTerrainQuery);
        }
    }

    /// <summary>1C9F70/1CA280 for one actual integer motion step. Active is the
    /// caller's actual validity/Flags26.bit0/byte23 conjunction, not a mission.</summary>
    public bool Advance(int actualStepMilliseconds, bool actualActiveArmyGate,
        Func<float, float, float?> actualTerrainQuery)
    {
        if (!actualActiveArmyGate) AuxiliaryTargetHex68 = 48400;
        bool changed = AdvanceCenter(unchecked((uint)actualStepMilliseconds), actualTerrainQuery);
        var (sin, cos) = ArmyAnimationSampler.SourceSinCos(centerHeading);
        for (int i = 0; i < ArmyInstancePose.RecordCount; i++)
        {
            int at = i * 64;
            float currentW = F(at + 0x1C), targetW = F(at + 0x2C), heading = F(at + 0x30);
            bool retainedMoving = (0 < currentW || currentW == 0 && (0 < targetW || targetW == 0)) &&
                (0 < heading || heading == 0) &&
                (Epsilon <= MathF.Abs(Subtract(F(at + 0x10), F(at + 0x20))) ||
                 Epsilon <= MathF.Abs(Subtract(F(at + 0x18), F(at + 0x28))));
            if (!changed && !retainedMoving) continue;
            Vector3 row = ArmyInstancePoseCodeSources.FormationRows[i];
            Vector4 followTarget = new(
                Add(Add(Product(Product(row.Y, 90), sin), Product(Product(row.X, 90), cos)), centerCurrent.X),
                Add(0, centerCurrent.Y),
                Add(Subtract(Product(Product(row.Y, 90), cos), Product(Product(row.X, 90), sin)), centerCurrent.Z), 1);
            int step = actualStepMilliseconds, delay = R(at + 0x38), elapsed = R(at + 0x3C);
            if (unchecked((uint)elapsed) < unchecked((uint)delay))
            {
                elapsed = unchecked(elapsed + actualStepMilliseconds); I(at + 0x3C, elapsed);
                if (unchecked((uint)delay) >= unchecked((uint)elapsed)) continue;
                step = unchecked(elapsed - delay); I(at + 0x3C, delay);
            }
            bool moved = AdvanceFollower(at, unchecked((uint)step), followTarget, centerHeading, actualTerrainQuery);
            if (!moved)
            {
                if (MathF.Abs(Subtract(F(at + 0x30), centerHeading)) < Epsilon) continue;
                W(at + 0x30, centerHeading);
            }
            changed = true;
        }
        GraphicsAction60 = changed ? 1 : actualActiveArmyGate ? 2 : 0;
        return changed;
    }

    bool AdvanceCenter(uint step, Func<float, float, float?> ground)
    {
        bool translated = false;
        float dx = Subtract(centerTarget.X, centerCurrent.X), dz = Subtract(centerTarget.Z, centerCurrent.Z);
        if (Epsilon <= MathF.Abs(dx) || Epsilon <= MathF.Abs(dz))
        {
            float distance = MathF.Sqrt(Add(Add(Product(dx, dx), 0), Product(dz, dz)));
            if (Epsilon <= MathF.Abs(distance))
            {
                centerHeading = TurnHeading(centerHeading, ArmyGraphicsMotionMath.Heading(dx, dz), step).Heading;
                centerSpeed = ResolveSpeed(centerSpeed, step, distance);
                float move = Product(step, centerSpeed);
                if (MathF.Abs(move) <= distance)
                {
                    var (sin, cos) = ArmyAnimationSampler.SourceSinCos(centerHeading);
                    centerCurrent = new(Add(Product(Add(Product(cos, 0), sin), move), centerCurrent.X),
                        Add(Product(move, 0), centerCurrent.Y),
                        Add(Product(Subtract(cos, Product(sin, 0)), move), centerCurrent.Z), centerCurrent.W);
                }
                else centerCurrent = centerTarget;
                if (ground(centerCurrent.X, centerCurrent.Z) is float height) centerCurrent.Y = height;
                if (!CenterIsMoving()) centerSpeed = 0;
                translated = true;
            }
        }
        else centerSpeed = 0;
        if (translated) return true;
        if (Epsilon <= MathF.Abs(Subtract(centerHeading, targetHeading)))
        {
            var turn = TurnHeading(centerHeading, targetHeading, step);
            centerHeading = turn.Heading; return turn.Changed;
        }
        return false;
    }

    bool AdvanceFollower(int at, uint step, Vector4 target, float currentFormationHeading,
        Func<float, float, float?> ground)
    {
        float x = F(at + 0x10), y = F(at + 0x14), z = F(at + 0x18);
        float dx = Subtract(target.X, x), dz = Subtract(target.Z, z);
        float distance = MathF.Sqrt(Add(Add(Product(dx, dx), 0), Product(dz, dz)));
        if (!(Epsilon <= MathF.Abs(distance))) return false;
        W(at + 0x30, ArmyGraphicsMotionMath.Heading(dx, dz));
        float speed = ResolveSpeed(F(at + 0x34), step, distance); W(at + 0x34, speed);
        float move = MathF.Abs(Product(step, speed));
        if (move <= distance)
        {
            float length = MathF.Sqrt(Add(Add(Product(dx, dx), Product(0, 0)), Product(dz, dz)));
            float inverse = Divide(1, length);
            W(at + 0x10, Add(Product(Product(dx, inverse), move), x));
            W(at + 0x14, Add(Product(Product(inverse, 0), move), y));
            W(at + 0x18, Add(Product(Product(dz, inverse), move), z));
        }
        else V(at + 0x10, target);
        if (ground(F(at + 0x10), F(at + 0x18)) is float height) W(at + 0x14, height);
        if (F(at + 0x14) < 445) W(at + 0x14, 445);
        if (MathF.Abs(Subtract(F(at + 0x10), F(at + 0x20))) < Epsilon &&
            MathF.Abs(Subtract(F(at + 0x18), F(at + 0x28))) < Epsilon) W(at + 0x30, currentFormationHeading);
        return true;
    }

    bool CenterIsMoving() => Epsilon <= MathF.Abs(Subtract(centerCurrent.X, centerTarget.X)) ||
        Epsilon <= MathF.Abs(Subtract(centerCurrent.Z, centerTarget.Z));

    public static float ResolveSpeed(float currentSpeed, uint actualStep, float distance)
    {
        float desired = 3;
        if (distance <= 400)
        { float square = Product(Divide(distance, 400), Divide(distance, 400));
            desired = Add(Product(Subtract(1, square), .3f), Product(square, 3)); }
        if (Epsilon <= MathF.Abs(Subtract(currentSpeed, desired)))
        {
            if (currentSpeed < desired)
            { currentSpeed = Add(Product(actualStep, .04f), currentSpeed); if (desired <= currentSpeed) currentSpeed = desired; }
            else
            { currentSpeed = Subtract(currentSpeed, Product(actualStep, .04f)); if (currentSpeed <= desired) currentSpeed = desired; }
        }
        return currentSpeed;
    }

    public static (float Heading, bool Changed) TurnHeading(float current, float target, uint actualStep)
    {
        float delta = Subtract(target, current);
        if (MathF.Abs(delta) < Epsilon) return (current, false);
        if (delta <= 3.1415927f) { if (delta < -3.1415927f) delta = Add(delta, TwoPi); }
        else delta = Add(delta, -TwoPi);
        float angle = MathF.Abs(delta), max = Product(actualStep, .03f);
        if (max <= angle) angle = max;
        float sign = delta <= 0 ? -1 : 1;
        current = Add(Product(sign, angle), current);
        while (TwoPi <= current) current = Add(current, -TwoPi);
        while (current < 0) current = Add(current, TwoPi);
        return (current, true);
    }

    float F(int at) => BitConverter.Int32BitsToSingle(R(at));
    int R(int at) => BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan(at, 4));
    void W(int at, float value) => I(at, BitConverter.SingleToInt32Bits(value));
    void I(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(records.AsSpan(at, 4), value);
    void V(int at, Vector4 value) { W(at, value.X); W(at + 4, value.Y); W(at + 8, value.Z); W(at + 12, value.W); }
    [MethodImpl(MethodImplOptions.NoInlining)] static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Divide(float a, float b) => a / b;
}
