namespace San14.Core;

public enum CityDutyKind { Recruitment, Training }
public enum CityDomesticPolicy { Current, Commerce, Agriculture, Barracks, Delegated }
public enum CityDomesticSelectionKind { SelectReceiver, Dismiss }
public enum CityDomesticTerminal { Confirm, Cancel }

public sealed record SetCityDutyOrder(int ForceId, int CityId, CityDutyKind Kind,
    int? OfficerId) : GameOrder(ForceId);
public sealed record CityDomesticStageOperation(CityDutyKind Duty,
    CityDomesticSelectionKind Selection, int ReceiverPhysicalSlot, ushort ObservedRawIdentity);
public sealed record CompleteCityDomesticOrder(int ForceId, int CityId,
    IReadOnlyList<CityDomesticStageOperation> Operations, CityDomesticTerminal Terminal,
    CityDomesticPolicy Policy = CityDomesticPolicy.Current) : GameOrder(ForceId);

/// <summary>Owns source-backed page replay, restoration and atomic publication.
/// The legacy single-duty order enters the same execution path.</summary>
public interface ICityDomesticExecution
{
    CommandResult Complete(GameState state, ForceState force,
        CompleteCityDomesticOrder order, CommandContext context);
    CommandResult SetDuty(GameState state, ForceState force,
        SetCityDutyOrder order, CommandContext context);
}

/// <summary>Shared city authority and input-shape checks for player and AI commands.</summary>
public sealed class PlayableCityDutyOrders(IOrderRules otherCommands,
    ICityDomesticExecution? execution = null) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        int cityId;
        CompleteCityDomesticOrder? completion = null;
        SetCityDutyOrder? dutyOrder = null;
        if (order is CompleteCityDomesticOrder pageOrder)
        {
            if (!Enum.IsDefined(pageOrder.Terminal))
                return CommandResult.Reject("City page terminal is invalid.", "duty.terminal");
            if (pageOrder.Operations is null)
                return CommandResult.Reject("City page operations are missing.", "duty.operations_missing");
            if (pageOrder.Terminal == CityDomesticTerminal.Confirm && !Enum.IsDefined(pageOrder.Policy))
                return CommandResult.Reject("City policy is invalid.", "duty.policy");
            var operations = new List<CityDomesticStageOperation>();
            foreach (CityDomesticStageOperation operation in pageOrder.Operations)
            {
                if (operation is null || !Enum.IsDefined(operation.Duty) ||
                    !Enum.IsDefined(operation.Selection) || operation.ReceiverPhysicalSlot < 0)
                    return CommandResult.Reject("City page operation is invalid.", "duty.operation");
                operations.Add(operation);
            }
            completion = pageOrder with
            {
                Operations = operations.AsReadOnly(),
                Policy = pageOrder.Terminal == CityDomesticTerminal.Confirm
                    ? pageOrder.Policy : CityDomesticPolicy.Current
            };
            cityId = pageOrder.CityId;
        }
        else if (order is SetCityDutyOrder singleDuty)
        {
            if (!Enum.IsDefined(singleDuty.Kind))
                return CommandResult.Reject("City duty kind is invalid.", "duty.kind");
            dutyOrder = singleDuty;
            cityId = singleDuty.CityId;
        }
        else return otherCommands.Apply(state, force, order, context);

        if (!state.Cities.TryGetValue(cityId, out CityState? city) || city.ForceId != force.Id)
            return CommandResult.Reject("City is not controlled by this force.", "duty.city_control");
        if (city.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.RuleUnavailable("rules.city_district", "City district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("City is outside the authorized district.", "duty.district_scope");
        if (execution is null)
            return CommandResult.RuleUnavailable("rules.city_domestic_execution", "Current city page writer is unavailable.");
        return completion is not null
            ? execution.Complete(state, force, completion, context)
            : execution.SetDuty(state, force, dutyOrder!, context);
    }
}
