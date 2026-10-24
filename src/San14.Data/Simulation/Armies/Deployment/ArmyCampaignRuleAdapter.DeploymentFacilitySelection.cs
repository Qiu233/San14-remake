using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>7E17B0 mode0, with no captured site. All ordinary retained
    /// rows are required for source preselection; only Selected rows supply
    /// the real Person list. The nullable tutorial input belongs to the
    /// actual application controller, not a default inferred from Phase.</summary>
    public DeploymentFacilitySelectionQuery CreateDeploymentFacilitySelectionQuery(GameState state,
        IReadOnlyList<DeploymentFacilityDraftRow> pageRows, bool? tutorialRestrictsFacilities)
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
        var directory = current is null
            ? new UnitFacilityDirectory([], captureMissing, "facility.directory.original_column_descriptors",
                "facility.directory.original_sort_descriptor")
            : ReadFacilityDirectory(current, tutorialRestrictsFacilities, Eligibility);
        IReadOnlyList<string>? preselected = null;
        string? preselectionMissing = null;
        try
        {
            // The original starts at the second retained row, regardless of
            // selection. A known nonzero suppresses the seed even if another
            // row's retained field is unknown.
            var retained = rows.Skip(1).Select(row => row.RetainedConstructionDefinitionPhysical).ToArray();
            if (retained.Any(value => value.HasValue && value.Value != 0)) preselected = [];
            else
            {
                if (retained.Any(value => value is null)) throw Missing("draft_facility_retained_definition");
                if (current is null) throw new InvalidDataException(captureMissing);
                int seed = K(current.ReadObjectDefinitionReference(0)?.PhysicalSlot, "draft_facility_seed_definition_pointer");
                if (directory.Rows.Any(row => row.DefinitionPhysicalSlot is null ||
                    row.DefinitionPhysicalSlot == seed && row.OriginalIncluded is null))
                    throw Missing("draft_facility_seed_visible_pointer_matches");
                var matches = directory.Rows.Where(row => row.OriginalIncluded == true &&
                    row.DefinitionPhysicalSlot == seed).ToArray();
                //7E8100 stops at the first matching pointer in the actual
                //sorted visible list. Lookup order cannot pick that row when
                //aliases match the same pointer and the sort is unresolved.
                if (matches.Length > 1)
                    throw Missing("draft_facility_seed_alias_original_sorted_first");
                preselected = Array.AsReadOnly(matches.Select(row => row.DefinitionKey).ToArray());
            }
        }
        catch (InvalidDataException error) { preselectionMissing = error.Message; }
        return new(directory, preselected, preselectionMissing, Placement);

        (bool? Allowed, string? Reason, string? Missing) Eligibility(ObjectDefinitionReference reference,
            FacilityConstructionDefinition? definition, bool tutorialDefinition)
        {
            if (people is null) return (null, null, peopleMissing ?? captureMissing);
            if (people.Length == 0) return (false, "structure.selection.commander", null);
            try
            {
                if (definition is null) throw Missing("draft_facility_definition_fields");
                //7E17B0 first requires an intrinsic2C5830 match for +32,
                //unless the real tutorial shortcut applies. This is not the
                //later2307D0 collected-personalities qualification.
                if (definition.RequiredTrait != 0 && !(tutorialDefinition && tutorialRestrictsFacilities == true))
                {
                    bool? intrinsic = AnyPerson(person => TacticRemakeCurrentTraits.CommanderHasEffect(
                        current!, person, definition.RequiredTrait));
                    if (intrinsic != true)
                    {
                        if (tutorialDefinition && tutorialRestrictsFacilities is null)
                            throw Missing("draft_facility_tutorial_owner");
                        return intrinsic is false ? (false, "structure.selection.trait", null)
                            : (null, null, Missing("draft_facility_selected_intrinsic_trait").Message);
                    }
                }
                string? missing = null;
                bool? allowed = AnyPerson(person =>
                {
                    try { return Qualified(person, definition, tutorialDefinition); }
                    catch (InvalidDataException error) { missing ??= error.Message; return null; }
                });
                return allowed is true ? (true, null, null)
                    : allowed is false ? (false, "structure.selection.commander_or_policy", null)
                    : (null, null, missing ?? Missing("draft_facility_qualification").Message);
            }
            catch (InvalidDataException error) { return (null, null, error.Message); }
        }
        bool? AnyPerson(Func<int, bool?> predicate)
        {
            bool unknown = false;
            foreach (int? person in people!)
            {
                if (!person.HasValue) continue;
                bool? value = predicate(person.Value);
                if (value == true) return true;
                unknown |= !value.HasValue;
            }
            return unknown ? null : false;
        }
        bool Qualified(int person, FacilityConstructionDefinition definition, bool tutorialDefinition)
        {
            var forceReference = current!.ReadPersonForce(person) ?? throw Missing("draft_facility_Person_Force_pointer");
            if (!K(current.ReadForceNativeValidity(forceReference), "draft_facility_Person_Force_validity")) return false;
            if (tutorialDefinition && tutorialRestrictsFacilities == true) return true;
            if (definition.RequiredPolicyEffect != 0)
            {
                if (!state.PolicyEffects.ContainsKey(definition.RequiredPolicyEffect)) throw Missing("draft_facility_policy_definition");
                int forcePhysical = K(forceReference.PhysicalSlot, "draft_facility_Force_identity");
                if (!state.Forces.TryGetValue(forcePhysical, out var force)) throw Missing("draft_facility_Force_owner");
                int level = K(BattleRemakeCurrentForcePolicies.ReadForceLevel(state, force,
                    definition.RequiredPolicyEffect), "draft_facility_policy_current_level");
                if (level < definition.RequiredPolicyLevel)
                {
                    if (tutorialDefinition && tutorialRestrictsFacilities is null) throw Missing("draft_facility_tutorial_owner");
                    return false;
                }
            }
            // A true intrinsic precheck cannot substitute for this collector.
            if (definition.RequiredTrait != 0) throw Missing("draft_facility_required_trait_collector");
            return true;
        }
        DeploymentFacilityPlacement Placement(string key, int clicked)
        {
            int? physical = null; bool? allowed = null; DeploymentAction? action = null;
            int? cost = null; string? reason = null; string? missing = null; string? costMissing = null;
            string? commandMissing = null;
            try
            {
                if (clicked is < 0 or >= 48400 || !FacilityConstructionIntent.IsDefinitionKey(key))
                { allowed = false; reason = "structure.selection.target_or_definition"; return Result(); }
                if (current is null) throw new InvalidDataException(captureMissing);
                if (people is null) throw new InvalidDataException(peopleMissing);
                if (people.Length == 0 || !people[0].HasValue)
                { allowed = false; reason = "structure.site.selected_person_or_hex"; return Result(); }
                int lookup = MapObjectSourceDirectory.DefinitionSlot(key);
                var row = directory.Rows.FirstOrDefault(entry => entry.DefinitionLookupId == lookup);
                if (row is null || row.OriginalIncluded == false)
                { allowed = false; reason = "structure.definition"; return Result(); }
                if (row.OriginalIncluded is null) throw Missing("draft_facility_tutorial_owner");
                var reference = current.ReadObjectDefinitionReference(lookup) ?? throw Missing("draft_facility_definition_pointer");
                physical = K(reference.PhysicalSlot, "draft_facility_definition_identity");
                // Existing site leaf lacks2793F0's tutorial short circuit.
                //15..17 have no implemented construction consumer either.
                if (physical is >= 15 and <= 17 && tutorialRestrictsFacilities != false)
                    throw Missing("draft_facility_tutorial_site_predicate");
                var site = new HexReference(clicked); //already physical map input
                var list = current.CreateNearbyTemporary();
                try
                {
                    foreach (int? person in people)
                        if (person.HasValue) current.AppendConstructionPersonTemporary(list, person.Value);
                    allowed = FacilityConstructionLegality.Check(state, current, list, site, reference,
                        rejected => reason = rejected);
                }
                finally { current.ClearNearbyTemporary(list, true); }
                if (allowed != true) return Result();
                var definition = Read(current, reference);
                if (IsSupported(physical) && definition.Category != 3)
                    action = new("build", new(DeploymentTargetKind.Hex, clicked),
                        MapObjectSourceDirectory.DefinitionKey(definition.Physical));
                else commandMissing = definition.Category == 3
                    ? "deployment.facility.category3_multi_placement" : "deployment.facility.definition_consumer";
                try
                {
                    var force = current.ReadPersonForce(people[0]!.Value) ?? throw Missing("draft_facility_cost_Force_pointer");
                    cost = FacilityConstructionCost.Read(state, current, force, definition, _baseCapacityInputs);
                }
                catch (InvalidDataException error) { costMissing = error.Message; }
            }
            catch (InvalidDataException error) { missing = error.Message; }
            return Result();
            DeploymentFacilityPlacement Result() => new(clicked, key, physical, allowed, action, cost,
                reason, missing, costMissing, commandMissing);
        }
    }
}
