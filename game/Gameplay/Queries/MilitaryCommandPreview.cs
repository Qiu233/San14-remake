using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>The current UI selection, independent of controls and display indices.</summary>
public readonly record struct MilitarySelection(int? SourceId, int? OfficerId,
    int? FormationId, int? UnitId, int? StructureKindId, int? TargetHexId);

public readonly record struct DeploymentPreview(bool Deploy, bool Occupy, bool Build);
public readonly record struct StructureCommandPreview(bool Build, bool Repair, bool Demolish);

/// <summary>
/// Presentation affordances for military commands. These are previews of the
/// current selection, not admission decisions; issuing orders uses session rules.
/// No world or mutable entity is retained by a returned preview.
/// </summary>
public static class MilitaryCommandPreview
{
    public static DeploymentPreview Deployment(GameState? world, int? player,
        MilitarySelection selection, PlayableLandRoutes? routes)
    {
        bool deploy = world?.RulesetId == PlayableSkeleton.RulesetId && CanCommand(world, player) &&
            selection.SourceId is not null && selection.OfficerId is not null &&
            selection.FormationId is not null && selection.TargetHexId is not null;
        bool occupy = deploy && world!.Territory.TryGetValue(selection.TargetHexId!.Value,
            out TerritoryState? land) && land.AreaId is not null;
        bool build = deploy && selection.StructureKindId is not null && routes is not null &&
            PlayableConstructionRules.CanBuildAt(world!, routes, player!.Value,
                selection.TargetHexId!.Value, world!.Structures ?? []);
        return new(deploy, occupy, build);
    }

    public static bool Withdraw(GameState? world, int? player, MilitarySelection selection) =>
        CanCommand(world, player) && SelectedUnit(world!, player!.Value, selection.UnitId) is
            { Mission: not (null or { Kind: UnitMissionKind.Transport }) };

    public static bool Occupy(GameState? world, int? player, MilitarySelection selection) =>
        CanCommand(world, player) && selection.TargetHexId is int target &&
        SelectedUnit(world!, player!.Value, selection.UnitId) is
            { Mission: not { Kind: UnitMissionKind.Transport } } &&
        world!.Territory.TryGetValue(target, out TerritoryState? land) && land.AreaId is not null;

    public static bool Disband(GameState? world, int? player, MilitarySelection selection) =>
        CanCommand(world, player) && selection.SourceId is int source &&
        SelectedUnit(world!, player!.Value, selection.UnitId) is { } unit &&
        world!.Cities.TryGetValue(source, out CityState? city) && city.ForceId == player &&
        city.HexId == unit.HexId;

    public static StructureCommandPreview Structures(GameState? world, int? player,
        MilitarySelection selection)
    {
        bool strategy = CanCommand(world, player);
        bool build = strategy && selection.StructureKindId is not null &&
            selection.TargetHexId is int buildTarget &&
            SelectedUnit(world!, player!.Value, selection.UnitId) is
                { Mission: not (null or { Kind: UnitMissionKind.Transport or UnitMissionKind.Retreat }) } &&
            world!.Territory.TryGetValue(buildTarget, out TerritoryState? land) && land.ForceId == player;
        StructureState? selected = world is not null && selection.TargetHexId is int target
            ? world.Structures?.Values.FirstOrDefault(item => item.HexId == target) : null;
        bool targetOwned = strategy &&
            SelectedUnit(world!, player!.Value, selection.UnitId) is
                { Mission: not (null or { Kind: UnitMissionKind.Transport or UnitMissionKind.Retreat }) } &&
            selected?.ForceId == player;
        return new(build, targetOwned && selected!.Completed && selected.MaxDurability is int maximum && selected.Durability < maximum,
            targetOwned);
    }

    private static bool CanCommand(GameState? world, int? player) =>
        world is { Phase: TurnPhase.Strategy } && player is int force &&
        PlayableOutcome.Evaluate(world, force) == PlayableResult.Ongoing;

    private static UnitState? SelectedUnit(GameState world, int player, int? selected) =>
        selected is int id && world.Units.TryGetValue(id, out UnitState? unit) && unit.ForceId == player
            ? unit : null;
}
