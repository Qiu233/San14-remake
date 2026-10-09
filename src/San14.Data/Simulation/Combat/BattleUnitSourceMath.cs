namespace San14.Data;

public enum BattleUnitArmyStatKind { FieldAttack, SiegeTroopAttack, Defense, WallAttack }

/// <summary>Resolved native inputs to SC 2A8FD0 / TC 2A9180. Leadership is the
/// full native kind-zero ability query; it is not the officer's base attribute.
/// The three factors include the original reached context and query branches.</summary>
public sealed record BattleUnitCommonInputs(int EffectiveLeadership, int Troops,
    int TroopDivisor, int CappedMorale, bool ApplyModifiers, bool RawFlag23Nonzero,
    int MoraleOffset, int MoraleDivisor, float RegionFactor,
    float FormationTerrainFactor, float NeighborFactor);

/// <summary>All numeric globals remain explicit because file initial values
/// do not establish their values after runtime configuration writes.</summary>
public sealed record BattleUnitCommonParameters(double TroopCapExponent,
    int RootDenominator, int LeadershipMultiplier, int AdditiveBase,
    float FlagNumerator, float FlagDenominator, int DivisorNumerator, int DivisorOffset)
{
    public static BattleUnitCommonParameters CeFileInitial { get; } =
        new(2.0, 1, 2, 200, 8f, 10f, 10, 9);
}

/// <summary>Army branch of SC 2B23B0, 2B2650 or 2AA0B0. FormationCoefficient
/// is the result of the native static/encoded getter before its conditional bonus.
/// QueryPercent is SC 41DBB0's result, or the explicit native transport branch's100.
/// The three effects are distinct native queries; their signed sum wraps before clamping.</summary>
public sealed record BattleUnitArmyStatInputs(float CommonFactor,
    int FormationCoefficient, int FormationBonus, int QueryPercent, bool ArmyTypeOne,
    int CommandEffect, int StatEffect, int SharedEffect, bool ApplyModifiers,
    sbyte RawStatus, bool ApplyDoctrineBonus, int DoctrineBonus);

public sealed record BattleUnitStatParameters(int EffectLimit, int EffectStepPercent,
    int StatusStepPercent, int ScalePercent, float PercentDenominator, float One)
{
    public static BattleUnitStatParameters CeFileInitial { get; } = new(10, 5, 5, 5, 100f, 1f);
}

/// <summary>SC 2AA540 / TC 2AA720. Wall attack uses full kind-zero leadership
/// and the Army troop word directly, independent of the common morale factor.</summary>
public sealed record BattleUnitWallInputs(int EffectiveLeadership, int Troops,
    int FormationCoefficient, int FormationBonus, int QueryPercent, bool ArmyTypeOne,
    int CommandEffect, int StatEffect, int SharedEffect, bool ApplyModifiers, sbyte RawStatus);

public sealed record BattleUnitWallParameters(int TroopOffset, int TroopDenominator,
    int LeadershipDenominator, BattleUnitStatParameters Effects)
{
    public static BattleUnitWallParameters CeFileInitial { get; } =
        new(20000, 10000, 100, BattleUnitStatParameters.CeFileInitial);
}

/// <summary>Inspected pure numeric subexpressions. No method resolves officers,
/// effects, region ownership, settings, the formation encoder or a native random source.</summary>
public static class BattleUnitSourceMath
{
    public static float CommonFactor(BattleUnitCommonInputs input,
        BattleUnitCommonParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(parameters);
        // 2A915C..2A91CE: signed32 leadership*4, signed division by3,
        // double pow -> CVTTSD2SI, cap troops, integer division BEFORE sqrt.
        int scaledLeadership = unchecked(input.EffectiveLeadership * 4) / 3;
        int cap = TruncateDoubleToInt32(Math.Pow(scaledLeadership, parameters.TroopCapExponent));
        int dividedTroops = Math.Min(cap, input.Troops) / input.TroopDivisor;
        float common = (float)(Math.Sqrt(dividedTroops) / parameters.RootDenominator);
        common += (float)unchecked(parameters.LeadershipMultiplier * input.EffectiveLeadership);
        common += parameters.AdditiveBase;
        // Keep every single-precision multiplication in the inspected order.
        float value = (input.ApplyModifiers && input.RawFlag23Nonzero
            ? parameters.FlagNumerator : parameters.FlagDenominator) / parameters.FlagDenominator;
        float divisorFactor = (float)parameters.DivisorNumerator /
            unchecked(parameters.DivisorOffset + input.TroopDivisor);
        float moraleFactor = (float)unchecked(input.MoraleOffset + input.CappedMorale) / input.MoraleDivisor;
        value *= common;
        value *= divisorFactor;
        value *= moraleFactor;
        value *= input.RegionFactor;
        value *= input.FormationTerrainFactor;
        value *= input.NeighborFactor;
        return value;
    }

    public static float ArmyStat(BattleUnitArmyStatKind kind,
        BattleUnitArmyStatInputs input, BattleUnitStatParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(parameters);
        if (kind is not (BattleUnitArmyStatKind.FieldAttack or
            BattleUnitArmyStatKind.SiegeTroopAttack or BattleUnitArmyStatKind.Defense))
            throw new ArgumentOutOfRangeException(nameof(kind));
        int coefficient = unchecked(input.FormationCoefficient + input.FormationBonus);
        float value = (float)unchecked(coefficient * input.QueryPercent) / parameters.PercentDenominator;
        float effectFactor = EffectFactor(input.ArmyTypeOne, input.CommandEffect, input.StatEffect,
            input.SharedEffect, parameters);
        float statusFactor = StatusFactor(input.ApplyModifiers, input.RawStatus, parameters);
        value *= input.CommonFactor;
        value *= (float)parameters.ScalePercent / parameters.PercentDenominator;
        value *= effectFactor;
        value *= statusFactor;
        if (kind == BattleUnitArmyStatKind.Defense) value += parameters.One;
        if (kind == BattleUnitArmyStatKind.FieldAttack && input.ApplyDoctrineBonus && input.DoctrineBonus > 0)
        {
            // 2B25F3..2B2604: add100 as signed32, multiply BEFORE dividing.
            value = (float)unchecked(input.DoctrineBonus + 100) * value;
            value /= parameters.PercentDenominator;
        }
        return value;
    }

    public static float ArmyWallAttack(BattleUnitWallInputs input,
        BattleUnitWallParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Effects);
        BattleUnitStatParameters effects = parameters.Effects;
        int coefficient = unchecked(input.FormationCoefficient + input.FormationBonus);
        float formation = (float)unchecked(coefficient * input.QueryPercent) / effects.PercentDenominator;
        float value = (float)input.EffectiveLeadership * formation;
        value /= parameters.LeadershipDenominator;
        value *= (float)unchecked(parameters.TroopOffset + input.Troops);
        value /= parameters.TroopDenominator;
        value *= EffectFactor(input.ArmyTypeOne, input.CommandEffect, input.StatEffect, input.SharedEffect, effects);
        value *= StatusFactor(input.ApplyModifiers, input.RawStatus, effects);
        // x86 MAXSS returns its second operand for NaN.
        return float.IsNaN(value) || value < effects.One ? effects.One : value;
    }

    /// <summary>Exact coefficient getter branch; encodedResult is required only
    /// when native (end-begin)&amp;~3 equals32. Static bytes are not capped.</summary>
    public static int FormationCoefficient(BattleUnitArmyStatKind kind,
        long NativeContainerByteCount, byte StaticFieldValue, byte? EncodedQueryResult)
    {
        int maximum = kind switch
        {
            BattleUnitArmyStatKind.FieldAttack => 45,
            BattleUnitArmyStatKind.SiegeTroopAttack => 70,
            BattleUnitArmyStatKind.Defense => 40,
            BattleUnitArmyStatKind.WallAttack => 60,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if ((NativeContainerByteCount & ~3L) != 32) return StaticFieldValue;
        if (EncodedQueryResult is not byte value)
            throw new InvalidDataException("Reached native Formation slot48 coefficient query is unresolved.");
        return value < 1 ? 1 : Math.Min(value, maximum);
    }

    /// <summary>SC2A945F..2A9489; requires actual selected native formation and
    /// actual Hex terrain code. It does not select a formation or classify a Core tile.</summary>
    public static float FormationTerrainFactor(int NativeFormationSlot, int NativeTerrainCode,
        int LongSnakePercent, int ForeignFormationPercent, float PercentDenominator)
    {
        bool terrain = NativeTerrainCode is 5 or 6 or >= 8 and <= 11;
        if (!terrain) return 1f;
        return NativeFormationSlot switch
        {
            6 => (float)LongSnakePercent / PercentDenominator,
            14 => (float)ForeignFormationPercent / PercentDenominator,
            _ => 1f
        };
    }

    public static int TruncateSingleToInt32(float value) =>
        float.IsNaN(value) || value >= 2147483648f || value < -2147483648f
            ? int.MinValue : (int)MathF.Truncate(value);

    private static int TruncateDoubleToInt32(double value) =>
        double.IsNaN(value) || value >= 2147483648d || value < -2147483648d
            ? int.MinValue : (int)Math.Truncate(value);

    private static float EffectFactor(bool typeOne, int command, int stat, int shared,
        BattleUnitStatParameters parameters)
    {
        int value = typeOne ? unchecked(command + stat + shared) : 0;
        int minimum = unchecked(-parameters.EffectLimit);
        value = value < minimum ? minimum : value > parameters.EffectLimit ? parameters.EffectLimit : value;
        return (float)unchecked(parameters.EffectStepPercent * value) /
            parameters.PercentDenominator + parameters.One;
    }

    private static float StatusFactor(bool apply, sbyte value, BattleUnitStatParameters parameters)
    {
        if (!apply) return parameters.One;
        float result = value;
        result *= (float)parameters.StatusStepPercent;
        result /= parameters.PercentDenominator;
        return result + parameters.One;
    }
}
