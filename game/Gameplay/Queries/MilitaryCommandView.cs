using System.Runtime.CompilerServices;
using San14.Core;

namespace San14.Client.Gameplay;

public sealed record MilitaryRosterUnit(int Id, int OfficerId, int HexId, string OfficerName,
    int Troops, bool HasUnfinishedPlan);
public sealed record MilitaryPlanView(DeploymentAction Action, int NextWaypointIndex, bool Completed,
    IReadOnlyList<int> Waypoints, string TargetName);
public sealed record MilitaryWaitingView(int Primary, int Second, int Third, long? ReadyOrdinal, bool LogisticsSet);
public sealed record MilitaryDepartureView(int Id, string Name, int? Supplies, bool IsCity);
public sealed record MilitaryUnitView(int Id, int OfficerId, int HexId, string OfficerName, int Troops,
    int Morale, int? NativeMorale, string? FormationName, bool IsTransport, bool HasCombatMission,
    DeploymentBehavior Behavior, MilitaryPlanView? Plan, bool SourceCreated, MilitaryWaitingView? Waiting,
    int? SourceLocationId, MilitaryDepartureView? Departure);
public sealed record MilitaryLocationChoice(int Id, int HexId, string Name, bool IsCity);
public sealed record MilitaryAttackChoice(DeploymentTarget Target, int HexId, string Name);

/// <summary>One military editor publication. All rows and draft seed values are detached from the world.</summary>
public sealed class MilitaryCommandView
{
    public static MilitaryCommandView Empty { get; } = new(null, false, 0, null, 0, null, [], [], []);
    public int? ForceId { get; }
    public bool CanIssue { get; }
    public int MapSize { get; }
    public long? CurrentOrdinal { get; }
    public long BindingRevision { get; }
    public MilitaryUnitView? FocusedUnit { get; }
    public IReadOnlyList<MilitaryRosterUnit> CombatUnits { get; }
    public IReadOnlyList<MilitaryAttackChoice> AttackTargets { get; }
    public IReadOnlyList<MilitaryLocationChoice> EntryTargets { get; }
    private readonly MilitaryLocationChoice[] _entryMapOrder;
    private readonly Dictionary<int, MilitaryRosterUnit> _combatUnits;

    internal MilitaryCommandView(int? forceId, bool canIssue, int mapSize, long? currentOrdinal,
        long bindingRevision, MilitaryUnitView? focused, MilitaryRosterUnit[] combat,
        MilitaryAttackChoice[] attacks, MilitaryLocationChoice[] entryMapOrder)
    {
        ForceId = forceId;
        CanIssue = canIssue;
        MapSize = mapSize;
        CurrentOrdinal = currentOrdinal;
        BindingRevision = bindingRevision;
        FocusedUnit = focused;
        CombatUnits = Array.AsReadOnly(combat);
        _combatUnits = combat.ToDictionary(unit => unit.Id);
        AttackTargets = Array.AsReadOnly(attacks);
        _entryMapOrder = entryMapOrder;
        EntryTargets = Array.AsReadOnly(entryMapOrder.Where(location => location.IsCity).OrderBy(location => location.Id)
            .Concat(entryMapOrder.Where(location => !location.IsCity).OrderBy(location => location.Id)).ToArray());
    }

    public MilitaryRosterUnit? CombatUnit(int id) => _combatUnits.GetValueOrDefault(id);
    public int? EntryAtHex(int hex) => _entryMapOrder.FirstOrDefault(location => location.HexId == hex)?.Id;
}

/// <summary>Session-local projection. Weak source identities preserve the editor's existing refresh/reset contract.</summary>
public sealed class MilitaryCommandViews
{
    private sealed record Binding(long Revision);
    private readonly ConditionalWeakTable<UnitState, Binding> _bindings = new();
    private long _nextRevision;

    public MilitaryCommandView Create(GameState? world, int? selectedUnitId, int? forceId)
    {
        int mapSize = world is null ? 0 : checked(world.MapWidth * world.MapHeight);
        bool canIssue = world?.Phase == TurnPhase.Strategy && forceId is int player &&
            world.Forces.TryGetValue(player, out ForceState? owner) && owner.IsActive &&
            PlayableOutcome.Evaluate(world, player) == PlayableResult.Ongoing;
        if (world is null) return new(forceId, canIssue, mapSize, null, 0, null, [], [], []);

        UnitState? focused = selectedUnitId is int id && world.Units.TryGetValue(id, out UnitState? unit) &&
            unit.ForceId == forceId ? unit : null;
        long binding = focused is null ? 0 : _bindings.GetValue(focused, _ => new(++_nextRevision)).Revision;
        MilitaryUnitView? selected = focused is null ? null : UnitView(world, focused);
        MilitaryRosterUnit[] combat = world.Units.Values.Where(unit => unit.ForceId == forceId &&
                unit.Mission is not null and not { Kind: UnitMissionKind.Transport }).OrderBy(unit => unit.Id)
            .Select(unit => new MilitaryRosterUnit(unit.Id, unit.OfficerId, unit.HexId,
                world.Officers.GetValueOrDefault(unit.OfficerId)?.Name ?? unit.OfficerId.ToString(), unit.Troops,
                unit.MilitaryPlan is { Completed: false })).ToArray();
        var attacks = new List<MilitaryAttackChoice>();
        var entries = new List<MilitaryLocationChoice>();
        if (forceId is int force)
        {
            foreach (UnitState enemy in world.Units.Values.Where(unit => PlayableDiplomacy.IsHostile(world, force, unit.ForceId)).OrderBy(unit => unit.Id))
                attacks.Add(new(new(DeploymentTargetKind.Unit, enemy.Id), enemy.HexId,
                    world.Officers.TryGetValue(enemy.OfficerId, out OfficerState? officer) ? officer.Name : enemy.OfficerId.ToString()));
            foreach (CityState city in world.Cities.Values.OrderBy(city => city.Id))
                if (city.HexId is int hex && PlayableDiplomacy.IsHostile(world, force, city.ForceId))
                    attacks.Add(new(new(DeploymentTargetKind.Location, city.Id), hex, city.Name));
            foreach (GateState gate in world.Gates.Values.OrderBy(gate => gate.Id))
                if (gate.HexId is int hex && PlayableDiplomacy.IsHostile(world, force, gate.ForceId))
                    attacks.Add(new(new(DeploymentTargetKind.Location, gate.Id), hex, gate.Name));
            // Map selection uses insertion order, cities before gates; menu order is independent.
            entries.AddRange(world.Cities.Values.Where(city => city.ForceId == force && city.HexId is not null)
                .Select(city => new MilitaryLocationChoice(city.Id, city.HexId!.Value, city.Name, true)));
            entries.AddRange(world.Gates.Values.Where(gate => gate.ForceId == force && gate.HexId is not null)
                .Select(gate => new MilitaryLocationChoice(gate.Id, gate.HexId!.Value, gate.Name, false)));
        }
        return new(forceId, canIssue, mapSize, world.NativeClock?.UpdateOrdinal, binding, selected,
            combat, attacks.ToArray(), entries.ToArray());
    }

    private static MilitaryUnitView UnitView(GameState world, UnitState unit)
    {
        MilitaryPlanView? plan = unit.MilitaryPlan is { } military ? new(military.Action,
            military.NextWaypointIndex, military.Completed, Array.AsReadOnly(military.WaypointHexIds.ToArray()),
            TargetName(world, military.Action.Target)) : null;
        MilitaryWaitingView? waiting = unit.NativeTacticRuntime is { } runtime ? new(runtime.Waiting20,
            runtime.Waiting21, runtime.Waiting22, runtime.PrimaryWaitingReadyAtUpdateOrdinal, (runtime.Flags26 & 2) != 0) : null;
        MilitaryDepartureView? departure = unit.SourceLocationId is int location && world.Cities.TryGetValue(location, out CityState? city)
            ? new(location, city.Name, city.Supplies, true)
            : unit.SourceLocationId is int gateId && world.Gates.TryGetValue(gateId, out GateState? gate)
                ? new(gateId, gate.Name, null, false) : null;
        return new(unit.Id, unit.OfficerId, unit.HexId,
            world.Officers.GetValueOrDefault(unit.OfficerId)?.Name ?? unit.OfficerId.ToString(),
            unit.Troops, unit.MoraleState.CurrentValue ?? unit.Morale, unit.MoraleState.CurrentValue,
            unit.FormationId is int formation ? world.Formations.GetValueOrDefault(formation)?.Name : null,
            unit.Mission?.Kind == UnitMissionKind.Transport,
            unit.Mission is not null and not { Kind: UnitMissionKind.Transport },
            unit.Behavior ?? new(true, true, false, DeploymentAfterAction.Stay), plan, unit.NativeArmyOrigin is not null,
            waiting, unit.SourceLocationId, departure);
    }

    private static string TargetName(GameState world, DeploymentTarget target) => target.Kind switch
    {
        DeploymentTargetKind.Unit => world.Units.TryGetValue(target.Id, out UnitState? unit) &&
            world.Officers.TryGetValue(unit.OfficerId, out OfficerState? officer) ? officer.Name : target.Id.ToString(),
        DeploymentTargetKind.Location => world.Cities.GetValueOrDefault(target.Id)?.Name ??
            world.Gates.GetValueOrDefault(target.Id)?.Name ?? target.Id.ToString(),
        _ => target.Id.ToString()
    };
}
