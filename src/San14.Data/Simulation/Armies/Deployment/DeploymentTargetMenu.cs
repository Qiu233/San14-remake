using San14.Core;

namespace San14.Data;

public sealed record DeploymentTargetPointer(bool? Allowed, string? UnresolvedInput);

/// <summary>Ordinary mode0 kind0 packets. Officer order is the actual current
/// selection; no Army exists yet. TargetHexId is the normalized physical Hex,
/// independently from an option's typed Base/Army command receiver.</summary>
public sealed record DeploymentTargetMenu(IReadOnlyList<int> OfficerIds, int ClickedHexId,
    int? TargetHexId, bool? PointerAllowed, bool? CanOpen, UnitTargetMenuKind? Kind,
    UnitTargetMenuRelation? Relation, IReadOnlyList<UnitTargetMenuOption> Options,
    string? UnresolvedInput);

/// <summary>A page-owned current read context. Create once on opening and
/// replace when the page accepts another World (including saved defaults).
/// Selection and route state are live call inputs. Pointer reads never capture
/// the World, enumerate build/area candidates, or find a route.</summary>
public sealed class DeploymentTargetMenuQuery
{
    private readonly Func<IReadOnlyList<DeploymentDraftRow>, int, DeploymentTargetPointer> pointer;
    private readonly Func<IReadOnlyList<DeploymentDraftRow>, int, UnitTargetRouteContext, DeploymentTargetMenu> menu;
    internal DeploymentTargetMenuQuery(
        Func<IReadOnlyList<DeploymentDraftRow>, int, DeploymentTargetPointer> pointer,
        Func<IReadOnlyList<DeploymentDraftRow>, int, UnitTargetRouteContext, DeploymentTargetMenu> menu)
    { this.pointer = pointer; this.menu = menu; }

    public DeploymentTargetPointer QueryPointer(IReadOnlyList<DeploymentDraftRow> selectedRows, int clickedHexId)
    { ArgumentNullException.ThrowIfNull(selectedRows); return pointer(selectedRows, clickedHexId); }

    public DeploymentTargetMenu QueryMenu(IReadOnlyList<DeploymentDraftRow> selectedRows,
        int clickedHexId, UnitTargetRouteContext route)
    {
        ArgumentNullException.ThrowIfNull(selectedRows); ArgumentNullException.ThrowIfNull(route);
        return menu(selectedRows, clickedHexId, route);
    }
}
