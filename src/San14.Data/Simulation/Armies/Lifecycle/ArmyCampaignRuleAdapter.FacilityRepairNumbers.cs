using San14.Core;

namespace San14.Data;

internal sealed record FacilityRepairNumberResult(GameState State, MapObjectReceiverId Receiver,
    int Physical, int HexId, int DefinitionIdentity, byte Category, int RawIncrement,
    int ClippedIncrement, ushort StoredDurability, bool ReachedCapacity, bool FirstCompletion);

public sealed partial class ArmyCampaignRuleAdapter
{
    private FacilityRepairNumberResult ApplyFacilityRepairNumbers(GameState state, SourceWorldContext current,
        ObjectReference obj, ObjectDefinitionReference definition, int raw)
    {
        int physical = RepairNeed(obj.PhysicalSlot, "number_object_pointer");
        ushort before = RepairNeed(current.ReadObjectRawField14(obj), "number_before");
        int capacity = RepairNeed(FacilityRepairCapacity.ReadCapacity(state, current, obj, _baseCapacityInputs), "number_first_capacity");
        int selected = Math.Min(unchecked(before + raw), capacity);
        int clipped = unchecked(selected - before);
        current.WriteObjectPostLoadField14(obj, selected);
        ushort stored = RepairNeed(current.ReadObjectRawField14(obj), "number_stored");
        // Publish only this canonical entity. An unrelated unknown structure
        // is not an input of this setter; the operation shadow already changed.
        var directory = current.CurrentMapObjectDirectory ?? throw RepairMissing("number_source_directory");
        var receiver = directory.ReceiverAtPhysical(physical);
        var subject = state.MapObjects?.Find(receiver)?.Subject ?? throw RepairMissing("number_subject_owner");
        if (subject.Category == MapObjectCategory.City && subject.LocationId is int city)
            state = state with { Cities = new(state.Cities) { [city] = state.Cities[city] with { Durability = stored } } };
        else if (subject.Category == MapObjectCategory.Gate && subject.LocationId is int gate)
            state = state with { Gates = new(state.Gates) { [gate] = state.Gates[gate] with { Durability = stored } } };
        else if (subject.StructureId is int structure && state.Structures is { } structures)
            state = state with { Structures = new(structures) { [structure] = structures[structure] with { Durability = stored, MaxDurability = capacity, Completed = stored == capacity } } };
        else throw RepairMissing("number_canonical_durability_owner");
        // The optional EnsureRepairLoop intent precedes completion intents.
        // Read back the real store and make a second current capacity query.
        ushort reread = RepairNeed(current.ReadObjectRawField14(obj), "number_reread");
        int nextCap = RepairNeed(FacilityRepairCapacity.ReadCapacity(state, current, obj, _baseCapacityInputs), "number_second_capacity");
        bool reached = reread == nextCap;
        bool first = false;
        if (reached)
        {
            bool enabled = RepairNeed(FacilityActivationCurrent.ReadEffectsEnabled(current, obj), "number_activation_word");
            first = !enabled;
            if (first) state = FacilityActivationCurrent.EnableAfterRepairCompletion(state, current, obj);
        }
        return new(state, receiver, physical, RepairNeed(current.ReadObjectHexIndex(obj), "number_object_hex"),
            RepairNeed(definition.PhysicalSlot, "number_definition_identity"),
            RepairNeed(current.ReadObjectDefinitionRaw2C(definition), "number_category"), raw, clipped, stored, reached, first);
    }
}
