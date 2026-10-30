namespace San14.Core;

public sealed partial record GameState
{
    /// <summary>Current doctrine-panel activation and suppression belonging to
    /// the unassigned Force receiver. Null preserves unknown legacy knowledge;
    /// it does not mean that all effects are inactive.</summary>
    public ForcePolicyEffects? UnassignedForcePolicyEffects { get; init; }
}

internal static class UnassignedForcePolicyEffectsValidation
{
    // The source owns two independent eighty-bit sets. This check is confined
    // to explicit world validation; current rule lookups use the immutable sets.
    internal static void Validate(ForcePolicyEffects? effects)
    {
        if (effects is null) return;
        if (effects.ActiveEffectIds?.Any(id => id is < 1 or > 80) == true ||
            effects.SuppressedEffectIds?.Any(id => id is < 1 or > 80) == true)
            throw new InvalidDataException("Unassigned Force policy effect identity is invalid.");
    }
}
