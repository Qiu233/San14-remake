using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Shared7E17B0 selection body. Both real producers supply selected People,
    // retained definition fields and the same captured current; no draft packets
    // are synthesized for existing Armies.
    private DeploymentFacilitySelectionQuery CreateConstructionPeopleSelectionQuery(GameState state,
        SourceWorldContext? current, string? captureMissing, int?[]? people, string? peopleMissing,int?[]? selectionTroops,
        IReadOnlyList<int?>? retainedDefinitions, bool? tutorialRestrictsFacilities, int? capturedSiteHexId = null)
    {
        var directory = current is null
            ? new UnitFacilityDirectory([], captureMissing, "facility.directory.original_column_descriptors",
                "facility.directory.original_sort_descriptor")
            : ReadFacilityDirectory(state,current,tutorialRestrictsFacilities,Eligibility,people,selectionTroops);
        IReadOnlyList<string>? preselected = null;
        string? preselectionMissing = null;
        try
        {
            // The original starts at the second retained row, regardless of
            // selection. A known nonzero suppresses the seed even if another
            // row's retained field is unknown.
            if (retainedDefinitions is null) throw Missing("facility_retained_packet_vector");
            var retained = retainedDefinitions.Skip(1).ToArray();
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
                //The Facility-specific initial sorter uses selected-pointer
                //membership. All aliases of this same pointer tie and retain
                //their native candidate order;7E8100 selects the first one.
                if(matches.Length!=0)directory.Pages?.PrioritizeSelectedDefinition(seed);
                preselected=Array.AsReadOnly(matches.Take(1).Select(row=>row.DefinitionKey).ToArray());
            }
        }
        catch (InvalidDataException error) { preselectionMissing = error.Message; }
        return new(directory, preselected, preselectionMissing, Placement,ContinuousPlacement);

        DeploymentFacilityPlacement ContinuousPlacement(string key,int first,int last)
        {
            var initial=Placement(key,first);
            if(initial.OriginalAllowed!=true)return initial;
            var final=Placement(key,last);
            if(final.OriginalAllowed!=true)return final;
            try
            {
                var reference=current!.ReadObjectDefinitionReference(MapObjectSourceDirectory.DefinitionSlot(key))
                    ??throw Missing("draft_continuous_definition");
                bool allowed=ContinuousConstructionAllows(state,current,first,last,reference,hex=>
                {
                    var result=Placement(key,K(hex.PhysicalSlot,"draft_continuous_Hex"));
                    return result.OriginalAllowed??throw new InvalidDataException(result.UnresolvedInput??"draft_continuous_site");
                });
                return initial with{OriginalAllowed=allowed,OriginalReason=allowed?null:"structure.continuation.unavailable"};
            }
            catch(InvalidDataException error){return initial with{OriginalAllowed=null,UnresolvedInput=error.Message};}
        }

        (bool? Allowed, string? Reason, string? Missing) Eligibility(ObjectDefinitionReference reference,
            FacilityConstructionDefinition? definition, bool tutorialDefinition)
        {
            if (people is null) return (null, null, peopleMissing ?? captureMissing);
            if (people.Length == 0) return (false, "structure.selection.commander", null);
            try
            {
                if (capturedSiteHexId is int capturedSite)
                {
                    if (capturedSite is < 0 or >= 48400) return (false, "structure.selection.target", null);
                    var list = current!.CreateNearbyTemporary();
                    try
                    {
                        foreach (int? person in people) current.AppendConstructionPersonTemporary(list, person);
                        string? rejected = null;
                        bool siteAllowed = FacilityConstructionLegality.Check(state, current, list,
                            new HexReference(capturedSite), reference, reason => rejected = reason);
                        return (siteAllowed, siteAllowed ? null : rejected ?? "structure.site.unavailable", null);
                    }
                    finally { current.ClearNearbyTemporary(list, true); }
                }
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
            bool qualified=FacilityConstructionQualification.PersonQualifies(state,current!,person,definition,
                tutorialDefinition && tutorialRestrictsFacilities==true);
            if(!qualified && tutorialDefinition && tutorialRestrictsFacilities is null)throw Missing("draft_facility_tutorial_owner");
            return qualified;
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
                if (IsSupported(physical))
                    action = new("build", new(DeploymentTargetKind.Hex, clicked),
                        MapObjectSourceDirectory.DefinitionKey(definition.Physical));
                else commandMissing = "deployment.facility.definition_consumer";
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
