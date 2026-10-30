using San14.Core;

namespace San14.Data.AI.Original;

internal sealed record OriginalAiDepartureInput(int OfficerId, int RequestedTroops, int FormationId,
    IReadOnlyList<int> TacticIds, int Action, OriginalAiEntity? TargetEntity, OriginalAiHex? TargetHex,
    int Category, int Retreat, int Pursuit, int Choice, OriginalAiDefinitionReference? Facility,
    OriginalAiHex? Continuation);
internal sealed record OriginalAiDepartureApplication(GameState State, int NativeReturn, int? UnitId, bool WroteOutput);
internal sealed record ExecuteOriginalAiDepartureOrder(int ForceId, OriginalAiActor Actor,
    OriginalAiDepartureInput Input) : GameOrder(ForceId);

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus Departure(uint commander, uint troopsKey, uint formation, uint tacticList,
        int action, uint target, int category, int retreat, int pursuit, int choice, uint output,
        uint facilityKey, uint continuationKey)
    {
        if (!TryEntity(commander, out var person) || person.Kind != WorldEntityKind.Officer ||
            !TryPersonSkill(formation, out var selected) || selected.Kind != OriginalAiPersonSkillKind.Formation ||
            !TryTypedCandidates(tacticList, CandidateListKind.Tactic, out var tactics))
            return OriginalAiNodeStatus.Failure;
        // B27580's return is deliberately ignored by034E60 after a real
        // zero initialization. Other mandatory typed operands are not guessed.
        int count = TryInteger(troopsKey, out int requested) ? requested : 0;
        OriginalAiEntity? targetEntity = TryEntity(target, out var entity) ? entity : null;
        OriginalAiHex? targetHex = TryHex(target, out var hex) ? hex : null;
        OriginalAiDefinitionReference? facility = TryDefinition(facilityKey, out var definition) ? definition : null;
        OriginalAiHex? continuation = TryHex(continuationKey, out var last) ? last : null;
        var input = new OriginalAiDepartureInput(person.Id, count, selected.PhysicalSlot,
            tactics.SkillValues.Select(value => value.PhysicalSlot).ToArray(), action, targetEntity, targetHex,
            category, retreat, pursuit, choice, facility, continuation);
        var execution = new OriginalAiDepartureRules(Adapter);
        var result = planning.Apply(new ExecuteOriginalAiDepartureOrder(Actor.ForceId, Actor, input),
            execution, commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing) throw new InvalidDataException(missing + ": " + result.Error);
        if (!result.Accepted || execution.Application is not { } applied) return OriginalAiNodeStatus.Failure;
        if (applied.WroteOutput)
        {
            if (applied.UnitId is int unit) SetEntity(output, new(WorldEntityKind.Army, unit));
            else SetAncestorEntity(output, null); //Actual exhausted2D2CA0 returns its null pointer.
        }
        return applied.NativeReturn != 0 ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }
}

internal sealed class OriginalAiDepartureRules(ArmyCampaignRuleAdapter adapter) : IOrderRules
{
    internal OriginalAiDepartureApplication? Application { get; private set; }
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not ExecuteOriginalAiDepartureOrder departure || state.Phase != TurnPhase.Strategy ||
            departure.ForceId != force.Id || departure.Actor.ForceId != force.Id ||
            context.DelegatedDistrictId is int district && departure.Actor.DistrictId != district)
            return CommandResult.Reject("Departure requires its actual authorized Strategy actor.", "original_ai.departure.scope");
        try
        {
            Application = adapter.ApplyOriginalAiDepartureCurrent(state, departure.Actor, departure.Input);
            return CommandResult.Accept(Application.State);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }
}
