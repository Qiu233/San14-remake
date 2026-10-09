namespace San14.Data;

/// <summary>All reached current inputs to SC176110/175D50. StageOrdinal is
/// frame+8; current parameters are explicit even when their image initial bytes are known.</summary>
public sealed record TacticEffectScaleSourceInput(byte EffectValue,
    float AbilityFactor, int StageOrdinal, int CurrentStageStepPercent,
    float SharedFactor);

public sealed record TacticMoraleIncreaseSourceInput(
    TacticEffectScaleSourceInput Scale, int CurrentMorale,
    int CurrentArmyMoraleMaximum, int TargetDefinitionMoraleCap,
    int TargetSetterMoraleCap);

public sealed record TacticHealingSourceInput(
    TacticEffectScaleSourceInput Scale, int CurrentTroops, int WoundedTroops,
    int CurrentCapacity, int CurrentRecoveryFloor, int CurrentWoundedWriteMaximum);

public sealed record TacticHealingSourceResult(int RequestedRecovery,
    int TroopsAfter, int WoundedAfter);

/// <summary>Pure arithmetic from the inspected native effects. Target selection,
/// permission, source identities and callback side effects belong to explicit contexts.</summary>
public static class TacticEffectSourceMath
{
    public static int Scale(TacticEffectScaleSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        float value = input.EffectValue;
        value = value * Finite(input.AbilityFactor);
        value = value * Stage(input);
        value = value * Finite(input.SharedFactor);
        return Truncate(value);
    }

    /// <summary>Army destination from opcode15, before the native byte setter.
    /// Includes both distinct current morale bounds in their native order.</summary>
    public static int ArmyMoraleAfter(TacticMoraleIncreaseSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CurrentMorale is < 0 or > byte.MaxValue ||
            input.CurrentArmyMoraleMaximum is < 0 or > byte.MaxValue ||
            input.TargetDefinitionMoraleCap is < 0 or > byte.MaxValue ||
            input.TargetSetterMoraleCap is < 0 or > byte.MaxValue)
            throw new InvalidDataException("Current native morale bytes or bounds are unsupported.");
        int amount = Scale(input.Scale);
        int current = Math.Min(input.CurrentMorale, input.CurrentArmyMoraleMaximum);
        int requested = Math.Min(input.TargetDefinitionMoraleCap, unchecked(current + amount));
        return requested < 0 ? 0 : Math.Min(input.TargetSetterMoraleCap, requested);
    }

    /// <summary>Opcode23's input strength to the Army2EAD80/Base2EB700 abnormal
    /// status resolvers. This value is not a troop-loss amount.</summary>
    public static int Opcode23RequestedStrength(TacticEffectScaleSourceInput input,
        int ResolvedSpecialPercent)
    {
        ArgumentNullException.ThrowIfNull(input);
        float value = input.EffectValue;
        value = value * Finite(input.AbilityFactor);
        value = value * Stage(input);
        value = value * Finite(input.SharedFactor);
        value = value * (float)ResolvedSpecialPercent;
        value = value / 100f;
        return Truncate(value);
    }

    /// <summary>Opcode20 Army transfer with an explicit current wounded write
    /// limit. A negative transfer is preserved; callers must resolve its eligibility.</summary>
    public static TacticHealingSourceResult ArmyHealing(
        TacticHealingSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CurrentTroops is < 0 or > ushort.MaxValue ||
            input.WoundedTroops is < 0 or > ushort.MaxValue ||
            input.CurrentCapacity < 0 || input.CurrentWoundedWriteMaximum < 0)
            throw new InvalidDataException("Current native Army healing input is unsupported.");
        float value = (float)input.WoundedTroops * Finite(input.Scale.AbilityFactor);
        value = value / (float)Math.Max(1, 20 - input.Scale.EffectValue);
        value = MathF.Max(value, (float)input.CurrentRecoveryFloor);
        value = value * Stage(input.Scale);
        value = value * Finite(input.Scale.SharedFactor);
        value = MathF.Min(value, (float)input.WoundedTroops);
        int recovered = Math.Min(Truncate(value),
            unchecked(input.CurrentCapacity - input.CurrentTroops));
        int troops = Math.Clamp(unchecked(input.CurrentTroops + recovered), 0, ushort.MaxValue);
        int wounded = Math.Clamp(unchecked(input.WoundedTroops - recovered),
            0, input.CurrentWoundedWriteMaximum);
        return new(recovered, troops, wounded);
    }

    private static float Stage(TacticEffectScaleSourceInput input)
    {
        float stage = unchecked(input.StageOrdinal * input.CurrentStageStepPercent);
        stage = stage / 100f;
        return stage + 1f;
    }

    private static float Finite(float value) => float.IsFinite(value) ? value :
        throw new InvalidDataException("Current native tactic factor is not finite.");

    private static int Truncate(float value)
    {
        if (!float.IsFinite(value) || value < int.MinValue || (double)value >= 2147483648d)
            throw new InvalidDataException("Native tactic scalar conversion is unsupported.");
        return (int)value;
    }
}
