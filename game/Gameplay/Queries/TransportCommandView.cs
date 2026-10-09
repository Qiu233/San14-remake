using San14.Core;

namespace San14.Client.Gameplay;

public sealed record TransportUnitView(int Id, int OfficerId, string OfficerName, int Troops, int Supplies,
    int CargoGold, int? SourceLocationId, string? SourceCityName, int DestinationCityId, string? DestinationCityName,
    bool Returning, TransportArrivalDisposition Arrival, bool SourceCityOwned, int? CurrentCityId);

/// <summary>A convoy's detached display and selection values for one panel refresh.</summary>
public sealed class TransportCommandView
{
    public static TransportCommandView Empty { get; } = Create(null, null, null);
    public int? ForceId { get; }
    public bool CanIssue { get; }
    public TransportUnitView? Unit { get; }
    public IReadOnlyList<NamedCommandChoice> Destinations { get; }

    private TransportCommandView(int? force, bool canIssue, TransportUnitView? unit, NamedCommandChoice[] destinations)
    {
        ForceId = force;
        CanIssue = canIssue;
        Unit = unit;
        Destinations = Array.AsReadOnly(destinations);
    }

    public static TransportCommandView Create(GameState? world, int? selectedUnitId, int? forceId)
    {
        bool canIssue = world is { Phase: TurnPhase.Strategy } && forceId is int force &&
            world.Forces.TryGetValue(force, out ForceState? owner) && owner.IsActive &&
            PlayableOutcome.Evaluate(world, force) == PlayableResult.Ongoing;
        if (world is null || selectedUnitId is not int id || !world.Units.TryGetValue(id, out UnitState? unit) ||
            unit.ForceId != forceId || unit.Mission is not { Kind: UnitMissionKind.Transport } mission)
            return new(forceId, canIssue, null, []);

        int destination = mission.DestinationCityId!.Value;
        CityState? source = unit.SourceLocationId is int sourceId ? world.Cities.GetValueOrDefault(sourceId) : null;
        var current = new TransportUnitView(unit.Id, unit.OfficerId,
            world.Officers.GetValueOrDefault(unit.OfficerId)?.Name ?? unit.OfficerId.ToString(),
            unit.Troops, unit.Supplies, mission.CargoGold, unit.SourceLocationId, source?.Name, destination,
            world.Cities.GetValueOrDefault(destination)?.Name, mission.Returning, mission.Arrival,
            source is not null && source.ForceId == forceId,
            world.Cities.Values.FirstOrDefault(city => city.ForceId == forceId && city.HexId == unit.HexId)?.Id);
        var destinations = world.Cities.Values.Where(city => city.ForceId == forceId && city.HexId is not null)
            .OrderBy(city => city.Id).Select(city => new NamedCommandChoice(city.Id, city.Name)).ToArray();
        return new(forceId, canIssue, current, destinations);
    }
}
