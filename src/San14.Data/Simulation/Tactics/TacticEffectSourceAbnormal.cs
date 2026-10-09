using San14.Core;

namespace San14.Data;

/// <summary>The reached current branch inputs of Army2EEC50. Null fields are
/// allowed only when the corresponding earlier branch does not read them.</summary>
public sealed record TacticAbnormalSourceInput(int NativeIndex, int RequestedStrength,
    byte CurrentCounter, bool NativeArmyValid, bool? GeneralImmunity, bool? SpecificImmunity,
    int? TargetTrait9, int? TargetSpecificTrait, int? SourceTrait98,
    int? CurrentGeneralMultiplier, int? CurrentSpecificMultiplier, int? CurrentSourceMultiplier,
    int? CurrentUpperBound, TacticCapturedOwnerSource? CapturedOwner);

/// <summary>Fresh16E880 lookup at the captured source Hex. An empty current Hex
/// is explicit and preserves the former owner; it cannot be inferred from caster ID.</summary>
public sealed record TacticCapturedOwnerSource(bool HasEntity,
    NativeTacticOwnerReference? Owner)
{
    /// <summary>Actual type/physical identity read from16E880's current entity.
    /// Null for an empty Hex means that the source does not write this pair.</summary>
    public ArmyOverrideTargetState? OverrideTarget { get; init; }
    public ArmyOverrideTargetSourceResidue? SourceResidue { get; init; }
}

public sealed record TacticAbnormalSourceResult(byte CounterAfter,
    NativeTacticOwnerReference? OwnerAfter, int AdjustedStrength, int UnclampedSum,
    int CauseCode, bool CounterWritten, bool IncreasingNotification);

public sealed record TacticAbnormalSourceAdjustment(int AdjustedStrength,
    int CauseCode, bool CounterWritten, bool HasTraitAdjustment);

public sealed record TacticEffectQueryResult<T>(T Value, ulong RandomState);

public sealed record TacticAbnormalStrengthSourceInput(
    TacticEffectScaleSourceInput Scale, bool NativeEffectGateAllowed,
    int ResolvedSpecialPercent);

/// <summary>Literal arithmetic in the paired SC2EEC50/TC2EED60 bodies. These
/// operations use no random sampler; reached query/notification RNG is supplied
/// separately by the owned current context.</summary>
public static class TacticEffectSourceAbnormal
{
    public const float AdjustmentEpsilon = 1.1920928955078125e-05f;

    public static TacticAbnormalSourceResult? Resolve(
        TacticAbnormalSourceInput input, NativeTacticOwnerReference? formerOwner)
    {
        TacticAbnormalSourceAdjustment? adjustedInput = ResolveAdjustment(input);
        if (adjustedInput is null) return null;
        if (!adjustedInput.CounterWritten)
            return new(input.CurrentCounter, formerOwner, 0, input.CurrentCounter,
                adjustedInput.CauseCode, false, false);
        if (input.CurrentUpperBound is not int limit) return null;
        int adjusted = adjustedInput.AdjustedStrength;
        int sum = unchecked(input.CurrentCounter + adjusted);
        // The positive branch does not clamp a negative current limit to zero.
        byte after = unchecked((byte)(sum < 0 ? 0 : Math.Min(sum, limit)));
        NativeTacticOwnerReference? owner = formerOwner;
        if (input.NativeIndex == 2 && adjusted > 0)
        {
            if (input.CapturedOwner is not { } captured) return null;
            if (captured.HasEntity)
                owner = captured.Owner ?? throw new InvalidDataException("Captured native source entity has no typed owner.");
            else if (captured.Owner is not null)
                throw new InvalidDataException("Empty captured native source Hex has an owner.");
        }
        return new(after, owner, adjusted, sum, adjustedInput.CauseCode, true, sum > input.CurrentCounter);
    }

    /// <summary>Arithmetic before the reached trait-notification callback and
    /// fresh counter-cap query. No duration or captured-owner query is read here.</summary>
    public static TacticAbnormalSourceAdjustment? ResolveAdjustment(TacticAbnormalSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.NativeIndex is < 0 or > 2)
            throw new InvalidDataException("Native Army abnormal index is invalid.");
        if (!input.NativeArmyValid)
            return new(0, 0, false, false);
        if (input.GeneralImmunity is null) return null;
        if (input.GeneralImmunity.Value)
            return new(0, 21, false, false);
        if (input.SpecificImmunity is null) return null;
        if (input.SpecificImmunity.Value)
            return new(0, 22 + input.NativeIndex, false, false);
        if (input.TargetTrait9 is not int general || input.TargetSpecificTrait is not int specific ||
            input.SourceTrait98 is not int source || input.CurrentGeneralMultiplier is not int generalRate ||
            input.CurrentSpecificMultiplier is not int specificRate ||
            input.CurrentSourceMultiplier is not int sourceRate)
            return null;
        int generalContribution = unchecked(general * generalRate);
        int specificContribution = unchecked(specific * specificRate);
        int sourceContribution = unchecked(source * sourceRate);
        int total = unchecked(unchecked(generalContribution + specificContribution) + sourceContribution);
        float adjustment = (float)unchecked(total * input.RequestedStrength);
        adjustment = adjustment / 100f;
        int adjusted = input.RequestedStrength;
        int cause = 0;
        bool hasAdjustment = MathF.Abs(adjustment) >= AdjustmentEpsilon;
        if (hasAdjustment)
        {
            int subtraction = Truncate(adjustment);
            if (subtraction == 0) subtraction = adjustment < 0 ? -1 : 1;
            adjusted = unchecked(adjusted - subtraction);
            // Native reporting chooses the first sign-matching nonzero trait.
            if (SignMatches(sourceContribution, adjustment)) cause = 98;
            else if (SignMatches(generalContribution, adjustment)) cause = 9;
            else if (SignMatches(specificContribution, adjustment)) cause = 10 + input.NativeIndex;
        }
        return new(adjusted, cause, true, hasAdjustment);
    }

    private static bool SignMatches(int contribution, float adjustment) =>
        contribution != 0 && (contribution > 0) == (adjustment > 0);

    private static int Truncate(float value) => !float.IsFinite(value) ||
        value >= 2147483648f || value < -2147483648f ? int.MinValue : (int)value;
}
