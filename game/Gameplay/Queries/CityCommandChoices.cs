using San14.Core;

namespace San14.Client.Gameplay;

public sealed record PreparedArmyChoice(int Id, string CommanderName, int Troops, int? BuildTargetHexId);
public sealed record SummonOfficerChoice(int Id, string Name, string SourceCityName);
public sealed record CityDutyNames(string? Recruiter, string? Trainer, int PreparationPeriods);

/// <summary>
/// Preview values for one current base. Create returns detached read-only lists;
/// issuing a selected order still uses the simulation's authority and rules.
/// </summary>
public sealed record CityCommandChoices(
    bool IsCity,
    IReadOnlyList<NamedCommandChoice> DestinationCities,
    IReadOnlyList<PreparedArmyChoice> PreparedArmies,
    IReadOnlyList<NamedCommandChoice> Areas,
    IReadOnlyList<NamedCommandChoice> Officers,
    IReadOnlyList<NamedCommandChoice> TalentExecutors,
    IReadOnlyList<NamedCommandChoice> TalentTargets,
    IReadOnlyList<NamedCommandChoice> MoveOfficers,
    IReadOnlyList<SummonOfficerChoice> SummonOfficers,
    int DefaultCommanderId, int DefaultAdministratorId,
    int MaximumGold, int MaximumSupplies, int MaximumTroops,
    CityDutyNames? Duties)
{
    /// <summary>Reads the world synchronously, retaining only independent display values.</summary>
    public static CityCommandChoices? Create(GameState world, int player, int sourceId)
    {
        CityState? city = world.Cities.GetValueOrDefault(sourceId);
        GateState? gate = world.Gates.GetValueOrDefault(sourceId);
        if (city is null && gate is null) return null;

        var destinations = world.Cities.Values.Where(target => target.ForceId == player &&
                target.Id != sourceId && target.HexId is not null)
            .OrderBy(target => target.Id).Select(target => new NamedCommandChoice(target.Id, target.Name)).ToArray();
        var prepared = world.PreparedDeployments.Values.Where(plan => plan.ForceId == player &&
                plan.SourceLocationId == sourceId).OrderBy(plan => plan.Id).Select(plan =>
            {
                DeploymentAction last = plan.Actions.Last();
                return new PreparedArmyChoice(plan.Id, world.Officers[plan.OfficerId].Name,
                    plan.Troops, last.ActionKey == "build" ? last.Target.Id : null);
            }).ToArray();
        var areas = (city?.AreaIds ?? []).Where(id => world.Areas.TryGetValue(id, out var area) && area.ForceId == player)
            .Select(id => new NamedCommandChoice(id, world.Areas[id].Name)).ToArray();
        OfficerState[] available = world.Officers.Values.Where(officer => officer.ForceId == player &&
                officer.LocationId == sourceId && !PlayableOfficerAvailability.IsSourceBusy(world, officer) &&
                officer.Presence is null or OfficerPresence.Present)
            .OrderBy(officer => officer.Id).ToArray();
        var talentExecutors = available.Where(officer => city is not null &&
                officer.Presence == OfficerPresence.Present && !officer.Deployed &&
                world.PreparedTalentOrders?.ContainsKey(officer.Id) != true &&
                world.PreparedOfficerMoves?.ContainsKey(officer.Id) != true &&
                !world.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id) &&
                !world.Cities.Values.Any(active => active.Duties?.RecruiterId == officer.Id || active.Duties?.TrainerId == officer.Id) &&
                !world.Areas.Values.Any(area => area.Administration?.OverseerId == officer.Id)).Select(Choice).ToArray();
        var talentTargets = world.Officers.Values.Where(officer => officer.Role == OfficerRole.Free &&
                officer.Presence == OfficerPresence.Present && city is not null && officer.ForceId is null &&
                officer.LocationId == sourceId && world.PreparedTalentOrders?.Values.Any(plan => plan.TargetOfficerId == officer.Id) != true)
            .OrderBy(officer => officer.Id).Select(Choice).ToArray();

        bool Movable(OfficerState officer) => officer.Role == OfficerRole.Officer &&
            !PlayableOfficerAvailability.IsSourceBusy(world, officer) && officer.Presence == OfficerPresence.Present && !officer.Deployed &&
            world.PreparedOfficerMoves?.ContainsKey(officer.Id) != true &&
            !world.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id) &&
            !world.Cities.Values.Any(owned => owned.GovernorId == officer.Id || owned.Duties?.RecruiterId == officer.Id || owned.Duties?.TrainerId == officer.Id) &&
            !world.Areas.Values.Any(area => area.Administration?.OverseerId == officer.Id) &&
            !world.Districts.Values.Any(district => district.LeaderId == officer.Id);
        var moves = available.Where(officer => city is not null && Movable(officer)).Select(Choice).ToArray();
        var summons = world.Officers.Values.Where(officer => city is not null && officer.ForceId == player &&
                officer.LocationId != sourceId && officer.LocationId is int location && world.Cities.ContainsKey(location) && Movable(officer))
            .OrderBy(officer => officer.Id).Select(officer => new SummonOfficerChoice(officer.Id,
                officer.Name, world.Cities[officer.LocationId!.Value].Name)).ToArray();
        int commander = world.Forces[player].RulerId ?? -1;
        int administrator = available.FirstOrDefault(officer => officer.Id != commander && !officer.Deployed &&
                !world.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id))?.Id ??
            available.FirstOrDefault(officer => !officer.Deployed)?.Id ?? -1;
        string? OfficerName(int? id) => id is int person && world.Officers.TryGetValue(person, out var officer) ? officer.Name : null;
        var duties = city?.Duties is CityDutyState current ? new CityDutyNames(OfficerName(current.RecruiterId),
            OfficerName(current.TrainerId), current.PreparationPeriods) : null;
        return new(city is not null, Array.AsReadOnly(destinations), Array.AsReadOnly(prepared), Array.AsReadOnly(areas),
            Array.AsReadOnly(available.Select(Choice).ToArray()), Array.AsReadOnly(talentExecutors), Array.AsReadOnly(talentTargets),
            Array.AsReadOnly(moves), Array.AsReadOnly(summons), commander, administrator,
            Math.Max(0, city?.Gold ?? 0), Math.Max(0, city?.Supplies ?? 0), Math.Max(1, city?.Troops ?? gate!.Troops), duties);
    }

    private static NamedCommandChoice Choice(OfficerState officer) => new(officer.Id, officer.Name);
}
