using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed record UnitFacilitySelectionChoice(string DefinitionKey, string Name,
    bool? Allowed, string? Reason, string? UnresolvedInput, byte? Category = null);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Definition selection before choosing a construction site.
    /// Shares the implemented construction consumers, but performs no
    /// Hex, scenery, area-capacity, radius, cost or command evaluation.</summary>
    public IReadOnlyList<UnitFacilitySelectionChoice> QueryUnitFacilitySelection(GameState state, int unitId, int? targetHexId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var choices = QueryFacilityConstructionChoices();
        if (!state.Units.TryGetValue(unitId, out var unit))
            return All(false, "structure.selection.army", null);
        if (unit.NativeTacticRuntime is not { } runtime)
            return All(null, null, Missing("selection_Army_runtime").Message);
        if (runtime.Status10 != 1)
            return All(false, "structure.selection.ordinary_army", null);
        if (targetHexId is int target && (uint)target >= (uint)(state.MapWidth * state.MapHeight))
            return All(false, "structure.selection.target", null);

        SourceWorldContext current;
        int person;
        ForceState force;
        try
        {
            // This existing capture imports the saved current registry and
            // fixed definitions. The extra site/scenery binding used by the
            // construction quote is deliberately not performed here.
            current = targetHexId.HasValue ? CaptureFacilityConstructionCurrent(state) : CaptureWritableTransportRouteCurrent(state);
            int physical = unit.NativeArmyOrigin?.NativeSlotIndex
                ?? throw Missing("selection_Army_receiver");
            var army = new ArmyUnitReference(physical);
            if (!K(current.ReadArmyUnitNativeValidity(army), "selection_Army_validity"))
                return All(false, "structure.selection.army", null);
            if (K(current.ReadArmyUnitCommanderId(army), "selection_Army_commander") != unit.OfficerId)
                throw Missing("selection_Army_commander_identity");
            person = current.ResolveOfficerSource(unit.OfficerId) ?? throw Missing("selection_Person_pointer");
            if (!K(current.ReadPersonNativeValidity(person), "selection_Person_validity"))
                return All(false, "structure.selection.commander", null);
            var personForce = current.ReadPersonForce(person) ?? throw Missing("selection_Person_Force_pointer");
            if (!K(current.ReadForceNativeValidity(personForce), "selection_Person_Force_validity"))
                return All(false, "structure.selection.force", null);
            int forcePhysical = K(personForce.PhysicalSlot, "selection_Force_identity");
            if (!state.Forces.TryGetValue(forcePhysical, out var ownedForce))
                throw Missing("selection_Force_owner");
            force = ownedForce;
        }
        catch (InvalidDataException error) { return All(null, null, error.Message); }

        return Array.AsReadOnly(choices.Select(ReadChoice).ToArray());

        UnitFacilitySelectionChoice ReadChoice(FacilityConstructionChoice choice)
        {
            byte? category = null;
            try
            {
                int slot = MapObjectSourceDirectory.DefinitionSlot(choice.DefinitionKey);
                if (!IsSupported(slot)) throw Missing("selection_definition_outside_supported_consumers");
                var reference = current.ReadObjectDefinitionReference(slot)
                    ?? throw Missing("selection_definition_pointer");
                if (!K(current.ReadObjectDefinitionValidity(reference), "selection_definition_validity"))
                    return Result(false, "structure.definition", null);
                var definition = Read(current, reference);
                category = definition.Category;
                if (definition.Category is not (2 or 3 or 4))
                    return Result(false, "structure.definition", null);
                if (targetHexId is int target)
                {
                    var site = current.ReadHexTableReference(target) ?? throw Missing("selection_site_pointer");
                    string? rejected = null;
                    if (!FacilityConstructionLegality.CheckCommander(state, current, person, site, reference, reason => rejected = reason))
                        return Result(false, rejected ?? "structure.site.unavailable", null);
                    return Result(true, null, null);
                }

                if (slot is >= 15 and <= 17 && K(ReadConstructionTutorialQualification(state), "selection_tutorial_owner"))
                    return Result(true, null, null);

                if(definition.RequiredTrait!=0 && !K(TacticRemakeCurrentTraits.CommanderHasEffect(
                    current,person,definition.RequiredTrait),"selection_intrinsic_trait"))
                    return Result(false,"structure.selection.trait",null);
                if(!FacilityConstructionQualification.PersonQualifies(state,current,person,definition))
                    return Result(false,"structure.selection.commander_or_policy",null);
                return Result(true, null, null);
            }
            catch (InvalidDataException error) { return Result(null, null, error.Message); }

            UnitFacilitySelectionChoice Result(bool? allowed, string? reason, string? missing) =>
                new(choice.DefinitionKey, choice.Name, allowed, reason, missing, category);
        }

        IReadOnlyList<UnitFacilitySelectionChoice> All(bool? allowed, string? reason, string? missing) =>
            Array.AsReadOnly(choices.Select(choice => new UnitFacilitySelectionChoice(
                choice.DefinitionKey, choice.Name, allowed, reason, missing)).ToArray());
    }
}
