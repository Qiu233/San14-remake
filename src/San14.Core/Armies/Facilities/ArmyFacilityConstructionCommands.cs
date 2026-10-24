namespace San14.Core;

/// <summary>The selected current facility definition and its construction sites.
/// A null outer owner is unknown. A null DefinitionKey is known no selection;
/// site None and unknown site knowledge follow the existing march coordinate type.</summary>
public sealed record FacilityConstructionIntent(string? DefinitionKey,
    ArmyMarchDestination? InitialSite, ArmyMarchDestination? ContinuationSite)
{
    public static FacilityConstructionIntent None => new(null, ArmyMarchDestination.None, ArmyMarchDestination.None);
    public static bool IsDefinitionKey(string? key) => key is not null &&
        key.StartsWith("san14-object-definition:", StringComparison.Ordinal) &&
        int.TryParse(key.AsSpan("san14-object-definition:".Length), out int id) && id is >= 0 and <= 30;
}

/// <summary>Shared player/AI command capability. Acceptance writes current
/// action, target, route, construction selection and changed-list atomically.</summary>
public interface IPlayableArmyConstructionCommandRules
{
    CommandResult IssueFacilityConstruction(GameState state, UnitState army, int hexId,
        string definitionKey, IReadOnlyList<int> waypointHexIds);
    CommandResult IssueMovementAfterConstruction(GameState state, UnitState army,
        DeploymentAction action, IReadOnlyList<int> waypointHexIds);
}

/// <summary>Formal current-object demolition command, shared by player and AI.</summary>
public interface IPlayableArmyFacilityWorkCommandRules
{
    CommandResult IssueFacilityRepair(GameState state, UnitState army,
        int structureId, int approachHexId, IReadOnlyList<int> waypointHexIds);
    CommandResult IssueFacilityDemolition(GameState state, UnitState army,
        int structureId, int approachHexId, IReadOnlyList<int> waypointHexIds);
}
