using San14.Core;

namespace San14.Data.AI.Original;

internal sealed record CreateOriginalAiDepartureTroopsOrder(int ForceId, IReadOnlyList<int> ArmyUnitIds,
    int Purpose, OriginalAiEntity? Target, OriginalAiHex? Hex) : GameOrder(ForceId);

internal sealed class OriginalAiDepartureTroopsRules(ArmyCampaignRuleAdapter adapter) : IOrderRules
{
    internal int NativeReturn { get; private set; }
    internal OriginalAiTroopsReference? CreatedGroup { get; private set; }

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not CreateOriginalAiDepartureTroopsOrder create || create.ForceId != force.Id ||
            state.Phase != TurnPhase.Strategy)
            return CommandResult.Reject("Military-group creation is outside its current Strategy scope.", "original-ai.departure-troops.scope");
        try
        {
            //planning.Apply supplies normal force authority. As with ordinary
            //Army instructions, a delegated actor may use only its own units.
            foreach (int id in create.ArmyUnitIds)
            {
                if (id == 0) continue; //The known native table-zero placeholder has no owned Unit.
                var unit = adapter.ReadOriginalAiDepartureArmyImage(state, id);
                if (unit.ForceId != force.Id || context.DelegatedDistrictId is int district &&
                    state.Officers.GetValueOrDefault(unit.OfficerId)?.DistrictId != district)
                    return CommandResult.Reject("A selected Army is outside the authorized Force or District.", "original-ai.departure-troops.Army-scope");
            }
            var current = adapter.CaptureOriginalAiDomesticCurrent(state);
            if (!adapter.CanCreateOriginalAiDepartureTroopsCurrent(state, create.ArmyUnitIds))
            { NativeReturn = 0; return CommandResult.Reject("The Army list is empty or contains an expedition member.", "original-ai.departure-troops.source-gate"); }
            var target = adapter.PrepareOriginalAiDepartureTroopsTargetCurrent(state, current,
                create.Purpose, create.Target, create.Hex);
            var application = adapter.ApplyOriginalAiDepartureTroopsCurrent(state, current,
                create.ArmyUnitIds, create.Purpose, target);
            NativeReturn = application.NativeReturn; CreatedGroup = application.Group;
            return CommandResult.Accept(application.State);
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "Current military-group creation inputs are unavailable."); }
    }
}
