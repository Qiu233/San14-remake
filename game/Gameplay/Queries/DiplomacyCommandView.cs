using San14.Core;

namespace San14.Client.Gameplay;

public sealed record DiplomaticForceChoice(int Id, string Name, DiplomaticRelation? Relation,
    DiplomaticTreatyKind? TreatyKind, int? TreatyRemainingPeriods);

/// <summary>Detached display values for one diplomacy panel refresh.</summary>
public sealed class DiplomacyCommandView
{
    public static DiplomacyCommandView Empty { get; } = Create(null, null, null);
    public int? PlayerForceId { get; }
    public int? SourceCityId { get; }
    public bool InStrategyPhase { get; }
    public bool DiplomacyKnown { get; }
    public int PendingMissions { get; }
    public int AttackRequests { get; }
    public IReadOnlyList<NamedCommandChoice> Executors { get; }
    public IReadOnlyList<DiplomaticForceChoice> Forces { get; }
    public IReadOnlyList<NamedCommandChoice> TargetCities { get; }

    private DiplomacyCommandView(int? player, int? sourceCity, bool strategy, bool diplomacyKnown,
        int pendingMissions, int attackRequests, NamedCommandChoice[] executors,
        DiplomaticForceChoice[] forces, NamedCommandChoice[] targets)
    {
        PlayerForceId = player;
        SourceCityId = sourceCity;
        InStrategyPhase = strategy;
        DiplomacyKnown = diplomacyKnown;
        PendingMissions = pendingMissions;
        AttackRequests = attackRequests;
        Executors = Array.AsReadOnly(executors);
        Forces = Array.AsReadOnly(forces);
        TargetCities = Array.AsReadOnly(targets);
    }

    public DiplomaticForceChoice? FindForce(int id) => Forces.FirstOrDefault(value => value.Id == id);

    public static DiplomacyCommandView Create(GameState? world, int? playerForceId, int? sourceCityId)
    {
        bool strategy = world?.Phase == TurnPhase.Strategy;
        bool known = world?.Diplomacy is not null;
        int missions = playerForceId is int missionOwner
            ? world?.Diplomacy?.Missions.Values.Count(value => value.ForceId == missionOwner) ?? 0 : 0;
        int requests = playerForceId is int requestOwner
            ? world?.Diplomacy?.AttackRequests.Values.Count(value =>
                value.RequestingForceId == requestOwner || value.RequestedForceId == requestOwner) ?? 0 : 0;
        if (world is null || playerForceId is not int player || sourceCityId is not int city ||
            !world.Cities.TryGetValue(city, out CityState? source) || source.ForceId != player)
            return new(playerForceId, sourceCityId, strategy, known, missions, requests, [], [], []);

        // These are selection candidates. Busy tasks, mission rules, fees and
        // relationship requirements belong to the simulation's command policy.
        var executors = world.Officers.Values.Where(value => value.ForceId == player && value.LocationId == city &&
                value.Presence == OfficerPresence.Present && !value.Deployed).OrderBy(value => value.Id)
            .Select(value => new NamedCommandChoice(value.Id, value.Name)).ToArray();
        var forces = world.Forces.Values.Where(value => value.Id != player && value.IsActive).OrderBy(value => value.Id)
            .Select(value =>
            {
                DiplomaticRelation? relation = PlayableDiplomacy.TryGetRelation(world, player, value.Id, out var level) ? level : null;
                DiplomaticTreaty? treaty = world.Diplomacy?.Treaties.GetValueOrDefault(PlayableDiplomacy.PairKey(player, value.Id));
                return new DiplomaticForceChoice(value.Id, value.Name, relation, treaty?.Kind,
                    treaty is not null ? Math.Max(0, treaty.ExpiresPeriod - world.PeriodNumber) : null);
            }).ToArray();
        var targets = world.Cities.Values.Where(value => value.ForceId is not null && value.ForceId != player)
            .OrderBy(value => value.Id).Select(value => new NamedCommandChoice(value.Id, value.Name)).ToArray();
        return new(playerForceId, sourceCityId, strategy, known, missions, requests, executors, forces, targets);
    }
}
