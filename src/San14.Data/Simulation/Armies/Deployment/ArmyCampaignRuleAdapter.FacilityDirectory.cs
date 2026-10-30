using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read the complete ordinary facility definition directory once
    /// for a selection page. No order, quote, world copy, or persistent writer.
    /// The tutorial argument belongs to the actual application controller;
    /// null does not mean that the normal campaign controller is present.</summary>
    public UnitFacilityDirectory QueryUnitFacilityDirectory(GameState state, int unitId,
        int? targetHexId = null, bool? tutorialRestrictsFacilities = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        SourceWorldContext current;
        try
        {
            current = targetHexId.HasValue ? CaptureFacilityConstructionCurrent(state)
                : CaptureWritableTransportRouteCurrent(state);
        }
        catch (InvalidDataException error) { return new(Array.Empty<UnitFacilityDirectoryRow>(), error.Message, null, null); }

        var actor = ResolveActor();
        return ReadFacilityDirectory(state,current,tutorialRestrictsFacilities,Eligibility,
            actor.Allowed is null?null:[actor.Person],[state.Units.TryGetValue(unitId,out var selectedUnit)&&selectedUnit.NativeArmyOrigin?.NativeSlotIndex is int selectedSlot
                ?current.ReadArmyReturnResourceFields(new ArmyUnitReference(selectedSlot))?.Ready16:null]);

        (int? Person, ForceState? Force, bool? Allowed, string? Reason, string? Missing) ResolveActor()
        {
            if (!state.Units.TryGetValue(unitId, out var unit)) return (null, null, false, "structure.selection.army", null);
            if (unit.NativeTacticRuntime is not { } runtime) return (null, null, null, null, Missing("directory_Army_runtime").Message);
            if (runtime.Status10 != 1) return (null, null, false, "structure.selection.ordinary_army", null);
            if (targetHexId is int target && (uint)target >= (uint)(state.MapWidth * state.MapHeight))
                return (null, null, false, "structure.selection.target", null);
            try
            {
                int slot = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("directory_Army_receiver");
                var army = new ArmyUnitReference(slot);
                if (!K(current.ReadArmyUnitNativeValidity(army), "directory_Army_validity"))
                    return (null, null, false, "structure.selection.army", null);
                if (K(current.ReadArmyUnitCommanderId(army), "directory_Army_commander") != unit.OfficerId)
                    throw Missing("directory_Army_commander_identity");
                int person = current.ResolveOfficerSource(unit.OfficerId) ?? throw Missing("directory_Person_pointer");
                if (!K(current.ReadPersonNativeValidity(person), "directory_Person_validity"))
                    return (null, null, false, "structure.selection.commander", null);
                var forceReference = current.ReadPersonForce(person) ?? throw Missing("directory_Person_Force_pointer");
                if (!K(current.ReadForceNativeValidity(forceReference), "directory_Person_Force_validity"))
                    return (null, null, false, "structure.selection.force", null);
                int forcePhysical = K(forceReference.PhysicalSlot, "directory_Force_identity");
                if (!state.Forces.TryGetValue(forcePhysical, out var force)) throw Missing("directory_Force_owner");
                return (person, force, true, null, null);
            }
            catch (InvalidDataException error) { return (null, null, null, null, error.Message); }
        }

        (bool? Allowed, string? Reason, string? Missing) Eligibility(ObjectDefinitionReference reference,
            FacilityConstructionDefinition? definition, bool tutorialDefinition)
        {
            if (actor.Allowed != true) return (actor.Allowed, actor.Reason, actor.Missing);
            try
            {
                if (targetHexId is int target)
                {
                    var site = current.ReadHexTableReference(target) ?? throw Missing("directory_site_pointer");
                    string? rejected = null;
                    bool allowed = FacilityConstructionLegality.CheckCommander(state, current,
                        actor.Person!.Value, site, reference, reason => rejected = reason);
                    return (allowed, allowed ? null : rejected ?? "structure.site.unavailable", null);
                }
                //2C4E30's real tutorial shortcut precedes policy and trait.
                if (tutorialDefinition && tutorialRestrictsFacilities == true) return (true, null, null);
                if (definition is null) throw Missing("directory_definition_fields");
                if(definition.RequiredTrait!=0 && !K(TacticRemakeCurrentTraits.CommanderHasEffect(
                    current,actor.Person!.Value,definition.RequiredTrait),"directory_intrinsic_trait"))
                    return (false,"structure.selection.trait",null);
                if(!FacilityConstructionQualification.PersonQualifies(state,current,actor.Person!.Value,definition))
                {
                    if(tutorialDefinition && tutorialRestrictsFacilities is null)throw Missing("directory_tutorial_owner");
                    return (false,"structure.selection.commander_or_policy",null);
                }
                return (true, null, null);
            }
            catch (InvalidDataException error) { return (null, null, error.Message); }
        }


    }
    private UnitFacilityDirectory ReadFacilityDirectory(GameState state,SourceWorldContext current, bool? tutorialRestrictsFacilities,
        Func<ObjectDefinitionReference, FacilityConstructionDefinition?, bool,
            (bool? Allowed, string? Reason, string? Missing)> readEligibility,
        int?[]? selectionPeople=null,int?[]? selectionTroops=null)
    {
        // The accepted arrival-name consumer uses this same physical catalog
        // owner after resolving the definition lookup pointer.
        var catalog = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions;
        var rows = new List<UnitFacilityDirectoryRow>();
        for (int lookup = 0; lookup < 31; lookup++)
        {
            int? physical = null;
            string? name = null;
            byte? category = null;
            bool? included = null;
            try
            {
                var reference = current.ReadObjectDefinitionReference(lookup)
                    ?? throw Missing("directory_definition_pointer");
                if (!K(current.ReadObjectDefinitionValidity(reference), "directory_definition_validity")) continue;
                physical = K(reference.PhysicalSlot, "directory_definition_identity");
                if ((uint)physical.Value >= (uint)catalog.Count) throw Missing("directory_definition_catalog_identity");
                name = catalog[physical.Value].Name;
                bool tutorialDefinition = physical is >= 15 and <= 17;
                if (tutorialRestrictsFacilities == true && !tutorialDefinition) continue;
                category = K(current.ReadObjectDefinitionRaw2C(reference), "directory_definition_category");
                if (category is not (2 or 3 or 4)) continue;
                included = tutorialDefinition || tutorialRestrictsFacilities.HasValue ? true : null;
                string? inclusionMissing = included.HasValue ? null : Missing("directory_tutorial_owner").Message;
                var definition = current.ReadFacilityConstructionDefinition(reference);
                ushort? durability = current.ReadObjectDefinitionBaseDurability(reference);
                var eligibility = readEligibility(reference, definition, tutorialDefinition);
                rows.Add(new(lookup, MapObjectSourceDirectory.DefinitionKey(lookup), physical, name, category,
                    included, eligibility.Allowed, eligibility.Reason,
                    Join(inclusionMissing, eligibility.Missing),
                    IsSupported(physical))
                {
                    BaseDurability = durability,
                    BaseGoldCost = definition?.BaseGoldCost,
                    RequiredTrait = definition?.RequiredTrait,
                    RequiredPolicyEffect = definition?.RequiredPolicyEffect,
                    RequiredPolicyLevel = definition?.RequiredPolicyLevel,
                    ExclusionRadius = definition?.ExclusionRadius,
                    TerrainMask = definition?.TerrainMask,
                    DefinitionUnresolvedInput = Join(definition is null ? Missing("directory_definition_fields").Message : null,
                        durability is null
                            ? Missing("directory_definition_base_durability").Message : null)
                });
            }
            catch (InvalidDataException error)
            {
                // Keep the unresolved lookup instead of silently deleting a
                // potentially valid category2/3/4 definition from the catalog.
                rows.Add(new(lookup, MapObjectSourceDirectory.DefinitionKey(lookup), physical,
                    name, category, included, null, null, error.Message, false));
            }
        }
        var visible=rows.Where(row=>row.OriginalIncluded!=false).ToArray();
        return new(rows.AsReadOnly(),null,null,null)
            {Pages=PrepareFacilitySelectionPages(state,current,catalog,visible,selectionPeople,selectionTroops)};

        static string? Join(string? first, string? second) => first is null ? second
            : second is null || second == first ? first : first + "; " + second;
    }

}
