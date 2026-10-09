namespace San14.Core;

/// <summary>
/// Protects active diplomatic envoys at the common playable command boundary.
/// Each command still performs its own eligibility and accounting checks.
/// </summary>
public sealed class PlayableDiplomaticOfficerGuard(IOrderRules next) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context)
    {
        IEnumerable<int> requested = order switch
        {
            PrepareOfficerMoveOrder move => [move.OfficerId],
            PrepareTalentSearchOrder search => [search.ExecutorOfficerId],
            PrepareTalentRecruitOrder recruit => [recruit.ExecutorOfficerId],
            SetCityDutyOrder { OfficerId: int officer } => [officer],
            DispatchTransportOrder transport => [transport.OfficerId],
            PrepareDeploymentOrder { Units: not null } deploy => deploy.Units
                .Where(unit => unit is not null).Select(unit => unit.OfficerId),
            SetAreaAdministrationOrder { Changes: not null } administration => administration.Changes
                .Where(change => change is not null && change.OverseerId is not null)
                .Select(change => change.OverseerId!.Value),
            _ => []
        };
        if (requested.Any(id => PlayableDiplomacy.IsOfficerOnMission(state, id)))
            return CommandResult.Reject("Officer is serving as a diplomatic envoy.", "diplomacy.executor_busy");
        return next.Apply(state, force, order, context);
    }
}
