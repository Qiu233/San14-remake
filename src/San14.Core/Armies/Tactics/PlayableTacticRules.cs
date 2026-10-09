namespace San14.Core;

public readonly record struct TacticUseEligibility(bool? Allowed, string? Reason = null,
    string? MissingRuleKey = null);
public sealed record TacticTargetResolution(PlayableTacticEffect[] Effects, ulong RandomState)
{
    /// <summary>Immutable resource compatibility publication, committed with
    /// the effects. Ordinary game rules never interpret its source encoding.</summary>
    public ICampaignResourceState? ResourceStateAfter { get; init; }
}

/// <summary>Source or provisional adapters supply both resolved effects and final usage eligibility.</summary>
public interface IPlayableTacticRules
{
    PlayableTacticDefinition? Resolve(GameState state, int tacticId);
    TacticUseEligibility CanUse(GameState state, UnitState unit,
        PlayableTacticDefinition definition);
    // A source adapter can evaluate caster/target abilities and preserve deterministic random state.
    // A known miss returns an empty effect array; unresolved definitions must Resolve to null.
    TacticTargetResolution ResolveEffects(GameState state, UnitState caster, UnitState target,
        PlayableTacticDefinition definition) => new(definition.Effects.ToArray(), state.RandomState);
}

/// <summary>The production default explicitly has no inferred original effects.</summary>
public sealed class UnresolvedTacticRules : IPlayableTacticRules
{
    public static UnresolvedTacticRules Instance { get; } = new();
    private UnresolvedTacticRules() { }
    public PlayableTacticDefinition? Resolve(GameState state, int tacticId) => null;
    public TacticUseEligibility CanUse(GameState state, UnitState unit,
        PlayableTacticDefinition definition) => new(null, "Original tactic usage eligibility is unresolved.",
            "tactic.eligibility");
}

/// <summary>Explicit custom/provisional definitions; construction never supplies default balance values.</summary>
public sealed class ExplicitPlayableTacticRules : IPlayableTacticRules
{
    private readonly IReadOnlyDictionary<int, PlayableTacticDefinition> _definitions;
    private readonly Func<GameState, UnitState, PlayableTacticDefinition, TacticUseEligibility> _eligibility;

    public ExplicitPlayableTacticRules(IEnumerable<PlayableTacticDefinition> definitions,
        Func<GameState, UnitState, PlayableTacticDefinition, TacticUseEligibility> eligibility)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
        var owned = new Dictionary<int, PlayableTacticDefinition>();
        foreach (PlayableTacticDefinition definition in definitions)
        {
            PlayableTacticStateValidation.ValidateDefinition(definition);
            if (!owned.TryAdd(definition.TacticId, definition with { Effects = definition.Effects.ToArray() }))
                throw new InvalidDataException("Tactic profile contains a repeated definition.");
        }
        _definitions = owned;
    }

    public PlayableTacticDefinition? Resolve(GameState state, int tacticId) =>
        _definitions.TryGetValue(tacticId, out PlayableTacticDefinition? definition)
            ? definition with { Effects = definition.Effects.ToArray() } : null;
    public TacticUseEligibility CanUse(GameState state, UnitState unit,
        PlayableTacticDefinition definition) => _eligibility(state, unit, definition);
}

public static class PlayableTacticCombat
{
    public const string NativeFootstopMovementRuleId = "san14:ce-native-abnormal24-movement-veto";
    public const string NativeConfusionFieldAttackRuleId = "san14:ce-native-abnormal23-ordinary-field-veto";

    /// <summary>CE2C7350 excludes a positive current byte24 before reading
    /// march targets. This is a proved veto, not permission for the rest of
    /// the original march. Missing native state keeps the provisional route
    /// policy; it never becomes an invented native counter or duration.</summary>
    public static bool NativeFootstopBlocksMovement(UnitState unit) =>
        unit.NativeTacticRuntime?.Abnormal24 is > 0;

    public static bool CanMove(UnitState unit, int period) => !NativeFootstopBlocksMovement(unit) && !Has(unit,
        PlayableTacticEffectKind.Immobilized, period) && !Has(unit, PlayableTacticEffectKind.Confused, period);
    public static bool CanAttack(UnitState unit, int period) => !Has(unit,
        PlayableTacticEffectKind.Confused, period);

    /// <summary>170660 excludes an ordinary source-created Army actor with
    /// positive current23. The separate action16 branch and special forces
    /// remain outside this proved veto. This is only Army-to-Army normal
    /// field attack selection, not a tactic, siege or movement prohibition.</summary>
    public static bool NativeConfusionBlocksOrdinaryFieldAttack(UnitState unit) =>
        unit.NativeArmyOrigin is not null && unit.ForceId is >= 1 and <= 45 &&
        unit.NativeTacticRuntime is { Status10: > 0, Abnormal23: > 0 } &&
        unit.NativeArmyConfiguration?.Action37 is byte action && action != 0x10;

    /// <summary>Applies explicit percentage modifiers to an already resolved loss; not an original damage formula.</summary>
    public static int AdjustLoss(int resolvedLoss, UnitState attacker, UnitState defender, int period)
    {
        if (resolvedLoss < 0 || period < 0) throw new InvalidDataException("Tactic combat input is invalid.");
        if (!CanAttack(attacker, period)) return 0;
        long attack = Math.Max(0L, 100L + Sum(attacker, PlayableTacticEffectKind.AttackPercent, period));
        long defense = Math.Max(0L, 100L + Sum(defender, PlayableTacticEffectKind.DefensePercent, period));
        long result = checked((long)resolvedLoss * attack) / Math.Max(1L, defense);
        return checked((int)Math.Min(int.MaxValue, result));
    }

    public static bool Has(UnitState unit, PlayableTacticEffectKind kind, int period) =>
        unit.TacticRuntime?.Statuses.Any(status => status.Kind == kind && status.ExpiresAtPeriod > period) == true;

    private static long Sum(UnitState unit, PlayableTacticEffectKind kind, int period) =>
        unit.TacticRuntime?.Statuses.Where(status => status.Kind == kind && status.ExpiresAtPeriod > period)
            .Sum(status => (long)status.Amount) ?? 0;
}
