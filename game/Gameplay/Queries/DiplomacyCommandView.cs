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

    public static DiplomacyCommandView Create(GameState? world, int? playerForceId, int? sourceCityId,
        IReadOnlyList<int>? executorOfficerIds = null, IReadOnlyList<int>? targetForceIds = null)
    {
        bool strategy = world?.Phase == TurnPhase.Strategy;
        bool known = world?.Diplomacy is not null;
        int missions = playerForceId is int missionOwner
            ? (world?.Diplomacy?.Missions.Values.Count(value => value.ForceId == missionOwner) ?? 0) +
                (world?.PersonnelCommands?.Diplomacies?.Values.Count(value => value.ForceId == missionOwner && !value.IsRetiring) ?? 0)
            : 0;
        int requests = playerForceId is int requestOwner
            ? world?.Diplomacy?.AttackRequests.Values.Count(value =>
                value.RequestingForceId == requestOwner || value.RequestedForceId == requestOwner) ?? 0 : 0;
        if (world is null || playerForceId is not int player || sourceCityId is not int city ||
            !world.Cities.TryGetValue(city, out CityState? source) || source.ForceId != player)
            return new(playerForceId, sourceCityId, strategy, known, missions, requests, [], [], []);

        // Candidate identity and order come from the current command query.
        // The view supplies names and relationship display, not eligibility.
        var executors = (executorOfficerIds ?? []).Select(id =>
            new NamedCommandChoice(id, world.Officers[id].Name)).ToArray();
        var forces = (targetForceIds ?? []).Select(id => world.Forces[id]).Select(value =>
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
