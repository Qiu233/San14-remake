using San14.Core;

namespace San14.Client.Gameplay;

public sealed record TacticTargetChoice(int Id, string OfficerName);
public sealed record TacticQueueView(int TacticId, int TargetUnitId, string Name);

/// <summary>Detached display values; target eligibility is queried from the current session on selection.</summary>
public sealed class TacticCommandView
{
    public static TacticCommandView Empty { get; } = Create(null, null, null, UnresolvedTacticRules.Instance, false);
    public int? ForceId { get; }
    public int? UnitId { get; }
    public bool CanIssue { get; }
    public bool CanQueryTargets { get; }
    public TacticQueueView? Pending { get; }
    public IReadOnlyList<TacticCommandAvailability> Choices { get; }
    private readonly Dictionary<PlayableTacticTargetRelation, TargetSet> _targets;
    private sealed record TargetSet(IReadOnlyList<TacticTargetChoice> Choices, int? SelectedHexTarget);

    private TacticCommandView(int? forceId, int? unitId, bool canIssue, bool canQueryTargets,
        TacticQueueView? pending, TacticCommandAvailability[] choices,
        Dictionary<PlayableTacticTargetRelation, TargetSet> targets)
    {
        ForceId = forceId;
        UnitId = unitId;
        CanIssue = canIssue;
        CanQueryTargets = canQueryTargets;
        Pending = pending;
        Choices = Array.AsReadOnly(choices);
        _targets = targets;
    }

    public IReadOnlyList<TacticTargetChoice> Targets(PlayableTacticTargetRelation? relation) =>
        relation is { } kind && _targets.TryGetValue(kind, out TargetSet? set) ? set.Choices : Array.Empty<TacticTargetChoice>();

    public int? SelectedHexTarget(PlayableTacticTargetRelation? relation) =>
        relation is { } kind && _targets.TryGetValue(kind, out TargetSet? set) ? set.SelectedHexTarget : null;

    public static TacticCommandView Create(GameState? world, int? unitId, int? forceId,
        IPlayableTacticRules profiles, bool canQueryTargets, int? selectedHex = null)
    {
        if (world is null || unitId is not int id || !world.Units.TryGetValue(id, out UnitState? caster) ||
            caster.ForceId != forceId)
            return new(forceId, null, false, canQueryTargets, null, [], []);

        bool canIssue = world.Phase == TurnPhase.Strategy && forceId is int force &&
            world.Forces.TryGetValue(force, out ForceState? owner) && owner.IsActive &&
            PlayableOutcome.Evaluate(world, force) == PlayableResult.Ongoing;
        TacticCommandAvailability[] choices = PlayableTacticAvailability.ForUnit(world, caster, profiles);
        var targets = new Dictionary<PlayableTacticTargetRelation, TargetSet>();
        foreach (PlayableTacticTargetRelation relation in choices.Where(choice => choice.TargetRelation is not null)
                     .Select(choice => choice.TargetRelation!.Value).Distinct())
        {
            UnitState[] candidates = world.Units.Values.Where(unit => relation switch
            {
                PlayableTacticTargetRelation.Self => unit.Id == caster.Id,
                PlayableTacticTargetRelation.Friendly => unit.ForceId == caster.ForceId ||
                    PlayableDiplomacy.AreAllied(world, caster.ForceId, unit.ForceId),
                PlayableTacticTargetRelation.Hostile => PlayableDiplomacy.IsHostile(world, caster.ForceId, unit.ForceId),
                _ => false
            }).ToArray();
            TacticTargetChoice[] rows = candidates.OrderBy(unit => unit.Id).Select(unit =>
                new TacticTargetChoice(unit.Id, world.Officers.TryGetValue(unit.OfficerId, out OfficerState? officer)
                    ? officer.Name : unit.OfficerId.ToString())).ToArray();
            targets.Add(relation, new(Array.AsReadOnly(rows), selectedHex is int hex
                ? candidates.FirstOrDefault(unit => unit.HexId == hex)?.Id : null));
        }
        TacticQueueView? pending = caster.TacticRuntime?.Pending is { } queued
            ? new(queued.TacticId, queued.TargetUnitId,
                world.Tactics.GetValueOrDefault(queued.TacticId)?.Name ?? queued.TacticId.ToString()) : null;
        return new(forceId, caster.Id, canIssue, canQueryTargets, pending, choices, targets);
    }
}

/// <summary>Queries only the selected pair; neither the panel nor its view retains the world or rules.</summary>
public static class TacticCommandQuery
{
    public static TacticUseEligibility ForTarget(GameState? world, int forceId, int unitId, int tacticId, int targetUnitId,
        IPlayableTacticRules profiles, PlayableLandRoutes? routes)
    {
        if (world is null || routes is null) return new(null, "tactic.target_missing");
        return PlayableTacticAvailability.ForTarget(world, unitId, forceId, tacticId, targetUnitId, profiles, routes);
    }
}
