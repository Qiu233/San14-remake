using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

internal sealed partial class TrapEffectApplication
{
    //The ordinary facility caller already owns the two collected channels.
    //Borrow those descriptors; no mid-operation capture or export is legal.
    internal void ApplyOrdinaryFacility(GameState latest, ObjectReference facility, int kind,
        HexListHandle[] channels)
    {
        State = latest;
        var reference = Definition(facility);
        if (!Need(Current.ReadObjectDefinitionValidity(reference), "ordinary_definition_validity")) return;
        if (Need(Current.ReadObjectDefinitionRaw2C(reference), "ordinary_definition_category") != 2 || kind != 5)
            throw Missing("ordinary_definition_category_kind");
        PrepareFacilityProjectiles(facility, channels);
        victimForces = Current.CreateNearbyTemporary();
        try
        {
            if (Need(Current.ReadObjectNativeValidity(facility), "ordinary_ForceList_Object_validity"))
                Current.ClearNearbyTemporary(victimForces, false);
            for (int channel = 0; channel < 2; channel++)
            {
                if (Need(Current.ReadTemporaryHexListCount(channels[channel]), "ordinary_dispatch_count") == 0) continue;
                var copy = Current.CreateNearbyTemporary();
                try
                {
                    foreach (var hex in FacilityEffectTargets.ReadHexes(Current, channels[channel]))
                        Current.AppendNearbyHexTemporary(copy, hex);
                    if (!Need(Current.ReadObjectNativeValidity(facility), "ordinary_dispatch_Object_validity")) continue;
                    var dispatchReference = Definition(facility);
                    if (!Need(Current.ReadObjectDefinitionValidity(dispatchReference), "ordinary_dispatch_definition_validity")) continue;
                    var definition = Current.ReadFacilityEffectDefinition(dispatchReference)
                        ?? throw Missing("ordinary_dispatch_definition_fields");
                    if (definition.Channel(channel).Effect != 1)
                        throw Missing("ordinary_effect_" + definition.Channel(channel).Effect);
                    ApplyDamage(facility, Need(dispatchReference.PhysicalSlot, "ordinary_dispatch_definition_identity"),
                        channel, copy, category: 2);
                }
                finally { Current.ClearNearbyTemporary(copy, true); }
            }
            ApplyRelations(facility, category: 2);
        }
        finally { Current.ClearNearbyTemporary(victimForces, true); victimForces = null; }
    }

    private void PrepareFacilityProjectiles(ObjectReference facility, HexListHandle[] channels)
    {
        if (!Need(Current.ReadObjectNativeValidity(facility), "projectile_Object_validity")) return;
        byte kind = (Current.ReadPhysicalObject(Need(facility.PhysicalSlot, "projectile_Object_pointer"))
            ?? throw Missing("projectile_Object_record")).RawField12;
        if (kind != 5) throw Missing("projectile_Object_kind");
        foreach (var channel in channels)
            foreach (var hex in FacilityEffectTargets.ReadHexes(Current, channel))
            {
                int identity = Need(Current.ReadHexPhysicalSlot(hex), "projectile_Hex_identity");
                var indexed = Current.ReadHexTableReference(identity < 48400 ? identity : 0)
                    ?? throw Missing("projectile_indexed_Hex_pointer");
                if (!indexed.PhysicalSlot.HasValue) continue;
                int selected = unchecked((short)Need(Current.ReadHexRaw1C(indexed), "projectile_indexed_signed1C"));
                if (selected is < 1 or > 500) continue;
                //231660 independently reads the original Hex after2C6D20.
                //Neither call tests the Army's native validity or active Unit.
                int raw = unchecked((short)Need(Current.ReadHexRaw1C(hex), "projectile_original_signed1C"));
                if (raw < 0) throw Missing("projectile_negative_Army_pointer");
                var army = Current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0)
                    ?? throw Missing("projectile_Army_pointer");
                var arena = State.ReadArmyLifecycle() ?? throw Missing("projectile_Army_lifecycle");
                int physical = Need(army.PhysicalSlot, "projectile_Army_physical");
                var receiver = arena.Slots[physical].ReceiverId;
                Feedback.Requests.Add(new(null, null) { FacilityProjectile = new(Feedback.Receiver(facility), receiver,
                    Need(Current.ReadObjectHexIndex(facility), "projectile_source_Hex10"),
                    Need(Current.ReadArmyUnitRawHex2A(army), "projectile_target_Hex2A")) });
            }
    }

    internal GameState CompleteOrdinaryFacilityEffects(GameState latest)
    {
        State = latest;
        //18E800 calls18FAC0 after all190F40 candidates, before their destructor.
        Aggregate();
        return State;
    }

    internal GameState FinishOrdinaryFacilities(GameState latest)
    {
        State = latest;
        State = ArmyReturnImagePublication.Publish(State, Current, []);
        PublishPeople();
        State = Feedback.PublishCurrentState(State);
        State = OwnedArmyLifecycleTargets.PublishCurrent(State.WithArmyLifecycle(
            OwnedArmyDestructorFlags.PublishCurrent(State.ReadArmyLifecycle()!, Current)), Current);
        return State with { ArmyMapOccupancy = Current.ExportArmyMapOccupancy(),
            PendingCombatSettlements = Current.OwnedDestructionQueue ?? throw Missing("ordinary_manager140_publication") };
    }
}
