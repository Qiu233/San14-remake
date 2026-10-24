using San14.Core;

namespace San14.Data;

/// <summary>Finite265250 personnel leaves in the actual saved manager100
/// order. A successful free Person is not passed through provisional resident
/// migration. Other source branches retain their existing state and report.</summary>
public sealed class PlayableLastCityPersonnelSettlement(ArmyCampaignRuleAdapter package,
    LoadedScenario scenario) : IPlayableLastCityPersonnelRules
{
    private readonly PersonFreedomSource freedom = new(package, scenario);
    public PlayableLastCityPersonnelResolution Settle(GameState postCapture, CityState beforeCaptureCity,
        int defendingForceId, int capturingForceId)
    {
        var resolved = new List<int>(); var disposedArmies = new List<int>(); var reports = new List<PeriodReportEntry>();
        var working = postCapture;
        var arena = postCapture.ReadArmyLifecycle();
        if (arena?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Officer) is not { } order)
            return new(postCapture, [], []);
        foreach (var entity in order)
        {
            if (entity.Kind != WorldEntityKind.Officer || entity.Id is not int id || !postCapture.Officers.TryGetValue(id, out var before)) continue;
            if (before.ForceId != defendingForceId || before.LocationId != beforeCaptureCity.Id) continue;
            //Earlier actual callbacks can appoint a later Person Governor.
            //Keep original manager order, but consume current role/busy fields.
            var actor = working.Officers[before.Id];
            bool busy = PlayableOfficerAvailability.IsSourceBusy(working, actor);
            var result = actor.Deployed
                ? actor.ServiceStatus?.KindId switch
                {
                    OfficerServiceKinds.Governor => freedom.TryApplyDeployedGovernor(working, actor.Id, beforeCaptureCity),
                    OfficerServiceKinds.ServingOfficer => freedom.TryApplyDeployedResident(working, actor.Id, beforeCaptureCity),
                    _ => freedom.TryApplyDeployedRuler(working, actor.Id, beforeCaptureCity)
                }
                : actor.ServiceStatus?.KindId switch
                {
                    OfficerServiceKinds.Ruler => busy ? freedom.TryApplyReturning(working, actor.Id, beforeCaptureCity)
                        : freedom.TryApplyRulerResident(working, actor.Id, beforeCaptureCity),
                    OfficerServiceKinds.Governor => busy ? freedom.TryApplyReturningGovernor(working, actor.Id, beforeCaptureCity)
                        : freedom.TryApplyGovernorResident(working, actor.Id, beforeCaptureCity),
                    _ => freedom.TryApplyResident(working, actor.Id, beforeCaptureCity)
                };
            if (result.State is { } next)
            {
                working = next; resolved.Add(before.Id);
                if (result.Receipt?.ArmyCleanup is { ResourceStageCompleted: true } cleanup &&
                    !working.Units.ContainsKey(cleanup.UnitId) &&
                    working.ReadArmyLifecycle()?.Receivers.FindUnit(cleanup.UnitId) is null)
                    disposedArmies.Add(cleanup.UnitId);
                reports.Add(new("capture.last_city_person_freed", defendingForceId, beforeCaptureCity.Id, before.Id,
                    result.Receipt!.NewHome, null, null) { RuleId = PersonFreedomSource.Revision });
            }
            else
                reports.Add(new("capture.last_city_personnel_unresolved", defendingForceId, beforeCaptureCity.Id, before.Id,
                    0, null, null) { RuleId = result.UnresolvedOwner });
        }
        working = ClearReleasedAppointments(working, resolved);
        //2851B0's reached265250 prefix precedes293BD0. Its27A6B0
        //player-exit gate must run here, before ordinary unregister callbacks.
        var terminalSource = new PlayableTerminalSource(package);
        var exit = terminalSource.TryApplyForceDefeat(working, defendingForceId);
        if (exit.Terminal is not null && exit.State is { } stopped)
        {
            reports.Add(new("campaign_player_defeated", exit.Terminal.PlayerForcePhysical, beforeCaptureCity.Id)
                { RuleId = exit.Terminal.RuleId });
            return new(stopped, resolved.ToArray(), reports.ToArray());
        }
        if (exit.State is null)
        {
            reports.Add(new("capture.last_city_force_unresolved", defendingForceId, beforeCaptureCity.Id)
                { RuleId = exit.UnresolvedOwner });
            return new(working, resolved.ToArray(), reports.ToArray()) { ResourceDisposedArmyIds = disposedArmies.ToArray() };
        }
        var unregister = new ForceUnregisterSource(package).TryApplyCurrent(working, defendingForceId, capturingForceId, reached => working = reached);
        if (unregister.State is { } removed)
        {
            var unified = terminalSource.TryApplyOrdinaryUnification(removed);
            if (unified.Terminal is not null && unified.State is { } ending)
            {
                removed = ending;
                reports.Add(new("campaign_unified", unified.Terminal.PlayerForcePhysical, beforeCaptureCity.Id)
                    { RuleId = unified.Terminal.RuleId });
            }
            else if (unified.State is null)
                reports.Add(new("campaign_unification_unresolved", capturingForceId, beforeCaptureCity.Id)
                    { RuleId = unified.UnresolvedOwner });
            return new(removed, resolved.ToArray(), reports.ToArray())
                { NativeUnregisterRuleId = ForceUnregisterSource.Revision, ResourceDisposedArmyIds = disposedArmies.ToArray() };
        }
        reports.Add(new("capture.last_city_force_unresolved", defendingForceId, beforeCaptureCity.Id)
            { RuleId = unregister.UnresolvedOwner });
        return new(working, resolved.ToArray(), reports.ToArray()) { ResourceDisposedArmyIds = disposedArmies.ToArray() };
    }

    private static GameState ClearReleasedAppointments(GameState state, List<int> released)
    {
        // Keep the defeated district's identity available to all personnel
        // callbacks. Only retire its appointment after the manager pass ends.
        var districtUpdates = state.Districts.Values.Where(d => d.LeaderId is int leader &&
            released.Contains(leader) && state.Officers[leader].ForceId != d.ForceId).ToArray();
        if (districtUpdates.Length == 0) return state;
        var districts = new Dictionary<int, DistrictState>(state.Districts);
        var native = state.NativeForceUnregisterCurrent;
        var windows = native is null ? null : new Dictionary<int, byte?[]>(native.DistrictWindows10);
        foreach (var district in districtUpdates)
        {
            districts[district.Id] = district with { LeaderId = null };
            if (windows?.TryGetValue(district.Id, out var old) == true)
            {
                var fields = old.ToArray(); fields[2] = 0; fields[3] = 0;
                windows[district.Id] = fields;
            }
        }
        return state with { Districts = districts, NativeForceUnregisterCurrent = native is null ? null :
            native with { DistrictWindows10 = windows! } };
    }

}

public sealed partial class ArmyCampaignRuleAdapter
{
    public IPlayableLastCityPersonnelRules CreateLastCityPersonnelRules() => new PlayableLastCityPersonnelSettlement(this, _scenario);
}
