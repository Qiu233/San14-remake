using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>7E17B0 mode0, optionally with the actual popup site. All ordinary retained
    /// rows are required for source preselection; only Selected rows supply
    /// the real Person list. The nullable tutorial input belongs to the
    /// actual application controller, not a default inferred from Phase.</summary>
    public DeploymentFacilitySelectionQuery CreateDeploymentFacilitySelectionQuery(GameState state,
        IReadOnlyList<DeploymentFacilityDraftRow> pageRows, bool? tutorialRestrictsFacilities,
        int? capturedSiteHexId = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(pageRows);
        // The page retains these ordinary rows while the child dialog and
        // construction state run. Copy only its small input list, not World.
        var rows = pageRows.ToArray();
        SourceWorldContext? current = null;
        string? captureMissing = null;
        try { current = CaptureFacilityConstructionCurrent(state); }
        catch (InvalidDataException error) { captureMissing = error.Message; }
        var selected = rows.Where(row => row.Selected).Select(row => row.Row).ToArray();
        int?[]? people = null;
        string? peopleMissing = null;
        if (current is not null)
        {
            try
            {
                people = selected.Select(row =>
                {
                    int id = row.Request.OfficerId;
                    if (id < 0) throw Missing("draft_facility_Person_lookup");
                    var reference = current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                        ?? throw Missing("draft_facility_Person_pointer");
                    int physical = K(reference.PhysicalSlot, "draft_facility_Person_identity");
                    //7E17B0 inserts null for a known-invalid receiver. It is
                    //not replaced by PersonTable0 (which could be an alias).
                    return K(current.ReadPersonNativeValidity(physical), "draft_facility_Person_validity")
                        ? (int?)physical : null;
                }).ToArray();
            }
            catch (InvalidDataException error) { peopleMissing = error.Message; }
        }
        return CreateConstructionPeopleSelectionQuery(state,current,captureMissing,people,peopleMissing,selected.Select(row=>(int?)row.Request.Troops).ToArray(),
            rows.Select(row => row.RetainedConstructionDefinitionPhysical).ToArray(),tutorialRestrictsFacilities,capturedSiteHexId);
    }
}
