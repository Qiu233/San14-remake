using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>29EC80 for an actual generic Object62..3000 and a known-null
    /// actor. Base capture/reward logic is outside this finite entry. The
    /// caller owns publication of the complete environment operation and sink.</summary>
    internal GameState ApplyFireDamageFieldObjectTarget(GameState state, SourceWorldContext current,
        ObjectReference target, int requestedLoss, List<CampaignEffectRequest> sink) =>
        ApplyFireDamageObjectTarget(state, current, target, requestedLoss, sink, FireDamageObjectTargetProfile.Generic);

    /// <summary>The Base ObjectHP part of29EC80, independently selected by
    /// the actual Object allocation class. The caller's selectedBase belongs
    /// to43D8E0's earlier branch and cannot replace this fresh pointer lookup.</summary>
    internal GameState ApplyFireDamageBaseObjectTarget(GameState state, SourceWorldContext current,
        ObjectReference target, BaseReference selectedBase, int requestedLoss, List<CampaignEffectRequest> sink) =>
        ApplyFireDamageObjectTarget(state, current, target, requestedLoss, sink, FireDamageObjectTargetProfile.Base);

    private enum FireDamageObjectTargetProfile { Generic, Base }

    private GameState ApplyFireDamageObjectTarget(GameState state, SourceWorldContext current,
        ObjectReference target, int requestedLoss, List<CampaignEffectRequest> sink, FireDamageObjectTargetProfile profile)
    {
        if (!FireObjectNeed(current.ReadObjectNativeValidity(target), "validity")) return state;
        //This independent simulation explicitly owns570850 Absent. A present
        //challenge mode is not a guessed successful498020 result.
        if (current.ArmyReturnTransferMode is not { Present: false }) throw FireObjectMissing("challenge_570850");
        int physical = FireObjectNeed(target.PhysicalSlot, "physical_receiver");
        if (profile == FireDamageObjectTargetProfile.Generic ? physical is < 62 or > 3000 : physical is < 1 or > 61)
            throw FireObjectMissing(profile == FireDamageObjectTargetProfile.Generic ? "generic_receiver62_3000" : "Base_receiver1_61");
        var pending = current.ReadOwnedCurrentListPool()?.PendingObjectRetirements ?? throw FireObjectMissing("pending_F0_owner");
        var directory = current.CurrentMapObjectDirectory ?? throw FireObjectMissing("source_directory");
        var receiver = directory.ReceiverAtPhysical(physical);
        //2336B0 compares node payload pointers; a known null pointer is a
        //normal nonmatch. Do not dereference it or invent unknown ownership.
        foreach (var entry in pending)
            if (entry == receiver) return state;
        int before = FireObjectNeed(current.ReadObjectRawField14(target), "current_HP14");
        int applied = requestedLoss < before ? requestedLoss : before;
        int intermediate = unchecked(before - applied);
        current.WriteObjectPostLoadField14(target, intermediate);
        ushort stored = FireObjectNeed(current.ReadObjectRawField14(target), "stored_HP14");
        state = PublishCurrentObjectDurability(state, current, target, stored);
        if (profile == FireDamageObjectTargetProfile.Base)
        {
            //232780/232D00 classify the actual Object physical block. The
            //following22DFF0 result is an ordinal in its respective Base table,
            //which may itself have aliases; do not fabricate a direct BaseRef.
            var parsed = physical <= 51 ? current.ReadCityTableReference(physical)
                : current.ReadGateTableReference(physical - 51);
            var basePointer = parsed ?? throw FireObjectMissing("Base_Object_class_pointer");
            byte flag = FireObjectNeed(current.ReadBasePostLoadFlag66(basePointer), "Base_raw66");
            if (flag != 3)
            {
                //2E3320 checks Base validity before the known-null actor.
                //Neither its flag66 comparison/write nor temporary allocation
                //is reached on this real null-actor branch.
                if (FireObjectNeed(current.ReadBaseNativeValidity(basePointer), "Base_null_actor_update_validity") &&
                    FireObjectNeed(current.ReadArmyUnitNativeValidity(new ArmyUnitReference(null)), "Base_known_null_actor_validity"))
                    throw FireObjectMissing("Base_known_null_actor_contract");
            }
            if (intermediate > 0) return state;
            //29EFC8's Force0 branch returns0 without capture admission. A
            //nonzero Force reaches the independent Base capture queue, whose
            //known-null winner contract remains unclosed for this consumer.
            if (FireObjectNeed(current.ReadObjectForceSlot13(target), "Base_fatal_Object13") == 0) return state;
            throw FireObjectMissing("Base_fatal29F0B2_known_null_capture");
        }
        //The branch uses the saved signed intermediate, not the clamped u16
        //setter result. Native signed wrapping can therefore reach removal.
        return intermediate > 0 ? state : RemoveMapObjectWithoutActor(state, current, target, sink);
    }

    /// <summary>Publish precisely the canonical entity whose sole setter just
    /// wrote current Object14. Do not rescan other receivers or rebind current.</summary>
    private static GameState PublishCurrentObjectDurability(GameState state, SourceWorldContext current,
        ObjectReference target, ushort stored)
    {
        var directory = current.CurrentMapObjectDirectory ?? throw FireObjectMissing("durability_source_directory");
        var receiver = directory.ReceiverAtPhysical(FireObjectNeed(target.PhysicalSlot, "durability_physical_receiver"));
        var subject = state.MapObjects?.Find(receiver)?.Subject ?? throw FireObjectMissing("durability_subject_owner");
        if (subject.Category == MapObjectCategory.City && subject.LocationId is int city && state.Cities.TryGetValue(city, out var cityState))
            return state with { Cities = new(state.Cities) { [city] = cityState with { Durability = stored } } };
        if (subject.Category == MapObjectCategory.Gate && subject.LocationId is int gate && state.Gates.TryGetValue(gate, out var gateState))
            return state with { Gates = new(state.Gates) { [gate] = gateState with { Durability = stored } } };
        if (subject.StructureId is int structure && state.Structures is { } structures && structures.TryGetValue(structure, out var structureState))
            return state with { Structures = new(structures) { [structure] = structureState with { Durability = stored } } };
        throw FireObjectMissing("canonical_durability_owner");
    }

    private static T FireObjectNeed<T>(T? value, string field) where T : struct => value ?? throw FireObjectMissing(field);
    private static InvalidDataException FireObjectMissing(string field) => new("army.progress.fire-damage.Object." + field);
}
