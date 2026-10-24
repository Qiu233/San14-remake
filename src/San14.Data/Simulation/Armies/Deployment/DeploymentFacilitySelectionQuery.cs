using San14.Core;

namespace San14.Data;

/// <summary>One ordinary row in the target page's retained order. The retained
/// definition is source row+60, independent of the row's current action: a
/// later Move does not clear a previous Build selection. A fresh normal row
/// supplies known zero. Null means the caller does not own that field.</summary>
public sealed record DeploymentFacilityDraftRow(DeploymentDraftRow Row, bool Selected,
    int? RetainedConstructionDefinitionPhysical);

/// <summary>Actual construction-state click result, separate from definition
/// inclusion and coarse qualification. This query neither submits an order
/// nor searches a route. CommandUnresolvedInput records an absent initial
/// order consumer; it must not be treated as a successful command receipt.</summary>
public sealed record DeploymentFacilityPlacement(int ClickedHexId, string DefinitionKey,
    int? DefinitionPhysicalSlot, bool? OriginalAllowed, DeploymentAction? Action,
    int? GoldCost, string? OriginalReason, string? UnresolvedInput,
    string? CostUnresolvedInput, string? CommandUnresolvedInput);

/// <summary>One definition-dialog and subsequent placement owner. Capture at
/// opening, and replace after an actual World or selection replacement. Its
/// methods are used for actual clicks, never to quote every hovered cell.</summary>
public sealed class DeploymentFacilitySelectionQuery
{
    private readonly Func<string, int, DeploymentFacilityPlacement> placement;
    internal DeploymentFacilitySelectionQuery(UnitFacilityDirectory directory,
        IReadOnlyList<string>? preselectedDefinitionKeys, string? preselectionUnresolvedInput,
        Func<string, int, DeploymentFacilityPlacement> placement)
    {
        Directory = directory; PreselectedDefinitionKeys = preselectedDefinitionKeys;
        PreselectionUnresolvedInput = preselectionUnresolvedInput; this.placement = placement;
    }
    public UnitFacilityDirectory Directory { get; }
    /// <summary>Pointer matches in the visible list. Empty means known no
    /// preselection; null means unknown. Disabled entries are not removed and
    /// are not replaced with the first allowed definition.</summary>
    public IReadOnlyList<string>? PreselectedDefinitionKeys { get; }
    public string? PreselectionUnresolvedInput { get; }
    public DeploymentFacilityPlacement QueryPlacement(string definitionKey, int clickedHexId)
    {
        ArgumentNullException.ThrowIfNull(definitionKey);
        return placement(definitionKey, clickedHexId);
    }
}