namespace San14.Core;

/// <summary>Source adapters can select distinct per-instruction target areas
/// after the shared primary-target permission check.</summary>
public interface IPlayableTacticTargetSource
{
    int[] ResolveTargetIds(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes);
}

/// <summary>Source definitions may depend on the acting Army's formation,
/// traits and current cooldown queries. Generic tactic identity has no caster.</summary>
public interface IPlayableTacticCasterSource
{
    PlayableTacticDefinition? ResolveForCaster(GameState state, UnitState caster, int tacticId);
}

/// <summary>A complete source program can interleave different target areas in
/// instruction order. The explicit steps retain that order and its RNG receipts.</summary>
public sealed record TacticSourceEffectStep(int TargetUnitId, PlayableTacticEffect[] Effects,
    ulong RandomState);
public sealed record TacticUseSourceResolution(TacticSourceEffectStep[] Steps, ulong RandomState);
public interface IPlayableTacticUseSource
{
    bool UsesSourceEffectOrder { get; }
    TacticUseEligibility CanUseTarget(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes);
    TacticUseSourceResolution? ResolveUse(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes);
}

/// <summary>The original source adapter resolves a complete program and its
/// current usage inputs. It returns null if any instruction remains unknown.</summary>
public interface ITacticRuleSource : IPlayableTacticTargetSource,
    IPlayableTacticCasterSource, IPlayableNativeSubdayRules
{
    PlayableTacticDefinition? Resolve(GameState state, int tacticId);
    TacticUseEligibility CanUse(GameState state, UnitState caster,
        PlayableTacticDefinition definition);
    TacticTargetResolution ResolveEffects(GameState state, UnitState caster,
        UnitState target, PlayableTacticDefinition definition);
}

/// <summary>Production tactic boundary with an explicit source adapter. There is
/// no default original balance, runtime parameter table or eligibility result.</summary>
public sealed class TacticRuleAdapter : IPlayableTacticRules,
    IPlayableTacticTargetSource, IPlayableTacticCasterSource, IPlayableNativeSubdayRules,
    IPlayableTacticUseSource, IPlayableNativeTacticActivationTailSource
{
    private readonly ITacticRuleSource _source;

    public TacticRuleAdapter(ITacticRuleSource source) =>
        _source = source ?? throw new ArgumentNullException(nameof(source));

    public PlayableTacticDefinition? Resolve(GameState state, int tacticId)
    {
        ArgumentNullException.ThrowIfNull(state);
        PlayableTacticDefinition? definition = _source.Resolve(state, tacticId);
        if (definition is null) return null;
        if (definition.TacticId != tacticId)
            throw new InvalidDataException("Original tactic adapter returned a different identity.");
        PlayableTacticStateValidation.ValidateDefinition(definition);
        return definition with { Effects = definition.Effects.ToArray() };
    }

    public TacticUseEligibility CanUse(GameState state, UnitState caster,
        PlayableTacticDefinition definition) => _source.CanUse(state, caster, definition);

    public PlayableTacticDefinition? ResolveForCaster(GameState state, UnitState caster, int tacticId)
    {
        PlayableTacticDefinition? definition = _source.ResolveForCaster(state, caster, tacticId);
        if (definition is null) return null;
        if (definition.TacticId != tacticId)
            throw new InvalidDataException("Original caster tactic adapter returned a different identity.");
        PlayableTacticStateValidation.ValidateDefinition(definition);
        return definition with { Effects = definition.Effects.ToArray() };
    }

    public int[] ResolveTargetIds(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes) =>
        _source.ResolveTargetIds(state, caster, primaryTarget, definition, routes);

    public NativeSubdayUnitResolution? AdvanceUnit(GameState state, UnitState unit,
        NativeSubdayMoment moment) => _source.AdvanceUnit(state, unit, moment);

    public bool UsesSourceEffectOrder => _source is IPlayableTacticUseSource { UsesSourceEffectOrder: true };
    public bool HasNativeActivationCommanderTail => _source is IPlayableNativeTacticActivationTailSource
        { HasNativeActivationCommanderTail: true };
    public OfficerState? ResolveNativeActivationCommanderTail(GameState state, UnitState caster,
        PlayableTacticDefinition definition) => _source is IPlayableNativeTacticActivationTailSource tail
            ? tail.ResolveNativeActivationCommanderTail(state, caster, definition) : null;

    public TacticUseEligibility CanUseTarget(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes) =>
        _source is IPlayableTacticUseSource source
            ? source.CanUseTarget(state, caster, primaryTarget, definition, routes)
            : _source.CanUse(state, caster, definition);

    public TacticUseSourceResolution? ResolveUse(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes)
    {
        if (_source is not IPlayableTacticUseSource source) return null;
        TacticUseSourceResolution? result = source.ResolveUse(state, caster, primaryTarget, definition, routes);
        if (result is null) return null;
        if (result.Steps is null) throw new InvalidDataException("Original ordered tactic steps are unresolved.");
        foreach (TacticSourceEffectStep step in result.Steps)
        {
            if (step is null || step.TargetUnitId < 0)
                throw new InvalidDataException("Original ordered tactic target is invalid.");
            PlayableTacticStateValidation.ValidateDefinition(definition with { Effects = step.Effects });
        }
        return result with { Steps = result.Steps.Select(step => step with
            { Effects = step.Effects.ToArray() }).ToArray() };
    }

    public TacticTargetResolution ResolveEffects(GameState state, UnitState caster,
        UnitState target, PlayableTacticDefinition definition)
    {
        TacticTargetResolution result = _source.ResolveEffects(state, caster, target, definition)
            ?? throw new InvalidDataException("Original tactic target inputs are unresolved.");
        PlayableTacticStateValidation.ValidateDefinition(definition with { Effects = result.Effects });
        return result with { Effects = result.Effects.ToArray() };
    }
}
