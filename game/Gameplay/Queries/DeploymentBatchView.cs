using San14.Core;

namespace San14.Client.Gameplay;

public sealed record DeploymentTargetChoice(DeploymentTarget Target, int HexId, string Name);

/// <summary>One detached publication for editing a local deployment draft.</summary>
public sealed class DeploymentBatchView
{
    public static DeploymentBatchView Empty { get; } = Create(null, null, null, null);
    public string? ScenarioKey { get; }
    public int? PeriodNumber { get; }
    public bool InStrategyPhase { get; }
    public bool CanCompose { get; }
    public int? SelectedHex { get; }
    public int? ActionHex { get; }
    public int? OccupationAreaId { get; }
    public long? AvailableTroops { get; }
    public long? AvailableBooks { get; }
    public IReadOnlyList<DeploymentTargetChoice> AttackTargets { get; }
    public IReadOnlyList<DeploymentTargetChoice> EntryTargets { get; }
    private readonly Dictionary<int, string> _officers, _formations, _tactics;

    private DeploymentBatchView(GameState? world, bool canCompose, int? selectedHex, int? actionHex,
        int? area, long? troops, long? books, DeploymentTargetChoice[] attacks, DeploymentTargetChoice[] entries)
    {
        ScenarioKey = world?.ScenarioKey;
        PeriodNumber = world?.PeriodNumber;
        InStrategyPhase = world?.Phase == TurnPhase.Strategy;
        CanCompose = canCompose;
        SelectedHex = selectedHex;
        ActionHex = actionHex;
        OccupationAreaId = area;
        AvailableTroops = troops;
        AvailableBooks = books;
        AttackTargets = Array.AsReadOnly(attacks);
        EntryTargets = Array.AsReadOnly(entries);
        _officers = world?.Officers.Values.ToDictionary(value => value.Id, value => value.Name) ?? [];
        _formations = world?.Formations.Values.ToDictionary(value => value.Id, value => value.Name) ?? [];
        _tactics = world?.Tactics.Values.ToDictionary(value => value.Id, value => value.Name) ?? [];
    }

    public string OfficerName(int id) => _officers.GetValueOrDefault(id) ?? id.ToString();
    public string FormationName(int id) => _formations.GetValueOrDefault(id) ?? id.ToString();
    public string TacticName(int id) => _tactics.GetValueOrDefault(id) ?? id.ToString();

    public static bool CanComposeAt(GameState? world, int? forceId, int? sourceId)
    {
        if (world is not { RulesetId: PlayableSkeleton.RulesetId, Phase: TurnPhase.Strategy } ||
            forceId is not int player || !world.Forces.TryGetValue(player, out var owner) || !owner.IsActive ||
            PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing || sourceId is not int source) return false;
        int? districtId = world.Cities.TryGetValue(source, out var city) && city.ForceId == player
            ? city.DistrictId
            : world.Gates.TryGetValue(source, out var gate) && gate.ForceId == player ? gate.DistrictId : null;
        return districtId is int district && world.Districts.TryGetValue(district, out var commandDistrict) &&
            commandDistrict.Number == 1;
    }

    public static DeploymentBatchView Create(GameState? world, int? forceId, int? sourceId, int? selectedHex)
    {
        CityState? city = sourceId is int source ? world?.Cities.GetValueOrDefault(source) : null;
        GateState? gate = sourceId is int location ? world?.Gates.GetValueOrDefault(location) : null;
        bool canCompose = CanComposeAt(world, forceId, sourceId);
        int? actionHex = canCompose && selectedHex is int hex && hex >= 0 && hex < checked(world!.MapWidth * world.MapHeight) ? hex : null;
        int? area = actionHex is int cell ? world!.Territory.GetValueOrDefault(cell)?.AreaId : null;
        long? troops = world is not null && sourceId is int baseId && (city is not null || gate is not null)
            ? (city?.Troops ?? gate!.Troops) - DeploymentAvailability.ReservedTroops(world, baseId) : null;
        long? books = world is not null && (city?.DistrictId ?? gate?.DistrictId) is int bookDistrict &&
            world.Districts[bookDistrict].OrdersRemaining is int remaining
            ? remaining - CommandBookAvailability.Reserved(world, bookDistrict) : null;
        var attacks = new List<DeploymentTargetChoice>();
        var entries = new List<DeploymentTargetChoice>();
        if (world is not null && forceId is int force)
        {
            foreach (UnitState unit in world.Units.Values.Where(value => PlayableDiplomacy.IsHostile(world, force, value.ForceId)).OrderBy(value => value.Id))
                attacks.Add(new(new(DeploymentTargetKind.Unit, unit.Id), unit.HexId,
                    world.Officers.GetValueOrDefault(unit.OfficerId)?.Name ?? unit.OfficerId.ToString()));
            foreach (CityState target in world.Cities.Values.OrderBy(value => value.Id)) Add(target.Id, target.HexId, target.ForceId, target.Name);
            foreach (GateState target in world.Gates.Values.OrderBy(value => value.Id)) Add(target.Id, target.HexId, target.ForceId, target.Name);

            void Add(int id, int? targetHex, int? targetForce, string name)
            {
                if (targetHex is not int position) return;
                var choice = new DeploymentTargetChoice(new(DeploymentTargetKind.Location, id), position, name);
                if (targetForce == force) entries.Add(choice);
                if (PlayableDiplomacy.IsHostile(world, force, targetForce)) attacks.Add(choice);
            }
        }
        return new(world, canCompose, selectedHex, actionHex, area, troops, books, attacks.ToArray(), entries.ToArray());
    }
}
