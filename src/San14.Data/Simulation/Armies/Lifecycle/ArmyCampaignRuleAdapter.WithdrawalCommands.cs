using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyWithdrawalCommandRules
{
    public ArmyWithdrawalDestination ReadWithdrawalDestination(GameState state, UnitState army)
    {
        byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, army);
        if (action is null) return new(true, null, null);
        if (action is not (11 or 12)) return new(false, null, null);
        var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(army.Id);
        var target = receiver is null ? null : OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId);
        if (target?.Identity3A is not ushort identity ||
            action == 11 && target.Kind38 != 5 || action == 12 && target.Kind38 != 6)
            return new(true, null, null);
        int location = action == 12 ? identity + 51 : identity;
        int? hex = action == 11 ? state.Cities.GetValueOrDefault(location)?.HexId : state.Gates.GetValueOrDefault(location)?.HexId;
        int? force = action == 11 ? state.Cities.GetValueOrDefault(location)?.ForceId : state.Gates.GetValueOrDefault(location)?.ForceId;
        return identity != 0 && hex is >= 0 and < 48400 && force == army.ForceId
            ? new(true, location, hex) : new(true, null, null);
    }

    public CommandResult IssueWithdrawal(GameState state, UnitState army)
    {
        if (army.Mission?.Kind == UnitMissionKind.Transport)
            return IssueTransportUnitWithdrawal(state, army);
        return IssueOrdinaryArmiesWithdrawal(state, [army]);
    }
}
