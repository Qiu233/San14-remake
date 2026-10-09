using San14.Core;

namespace San14.Data;

public sealed partial class ForceUnregisterSource
{
    private static void RequireEmptyPeopleAndArmies(GameState state, SourceWorldContext current, int force)
    {
        foreach (int? at in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("manager100"))
        {
            int person = at ?? throw Missing("Person_payload");
            if (Need(current.ReadPersonOldForceSlot(person), "Person_current_Force") == force)
                throw Missing("branch.nonempty_Force_Person_after_265250");
        }
        foreach (int army in Chain(current, RegistryKind.Army))
        {
            ushort id = Need(current.ReadArmyUnitCommanderId(new(army)), "Army12");
            int person = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?.PhysicalSlot ?? throw Missing("Army_Commander_pointer");
            if (Need(current.ReadPersonOldForceSlot(person), "Army_current_Force") == force &&
                !Known(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, army), "Army233560"))
                throw Missing("branch.nonempty_Force_Army_after_265250");
        }
    }

    private static void RequireNoTroopsDistrict(GameState state, SourceWorldContext current, int district)
    {
        var arena = state.NativeArmyLifecycle ?? throw Missing("CTroops_current_lifecycle");
        var groups = arena.ArmyGroups ?? throw Missing("CTroops_current_groups");
        var primary = ArmyGroupSourceAdapter.Roster(arena, current);
        var excluded = ArmyGroupSourceAdapter.Excluded(state, arena, current, primary);
        //Actual CTroops virtual validity is derived from the first eligible
        //Army member's current Commander District, not an old Core roster.
        for (int group = 1; group <= 500; group++)
        {
            var currentDistrict = ArmyGroupRules.GetDistrict(groups, group, primary, excluded,
                id => ArmyGroupSourceAdapter.Commander(arena, current, id))
                ?? throw Missing("CTroops_current_district");
            if (currentDistrict.IsRecognized && currentDistrict.DistrictId == district)
                throw Missing("branch.293490_valid_CTrops_in_District");
        }
    }

    private static int[] SettleHexes(GameState state, SourceWorldContext current, int victim,
        HexListHandle objectTemporary)
    {
        var changed = new List<int>();
        //No original scenery-layout receiver is constructed in this numeric
        //campaign. Its presentation-dirty tail has a known null receiver.
        current.CurrentSceneryLayout = new(null);
        //The complete campaign already supplies this same known-absent
        //570850 mode owner for ordinary personnel services.
        current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
        for (int hex = 0; hex < 48400; hex++)
        {
            var reference = new HexReference(hex);
            if (Need(current.ReadHexForceSlot(reference), "Hex14") != victim) continue;
            ushort link = Need(current.ReadHexObjectLink(reference), "Hex1A");
            var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw Missing("Hex_Object_pointer");
            int receiver = 0;
            if (Known(current.ReadObjectNativeValidity(obj), "Hex_Object_validity"))
            {
                byte own = Need(current.ReadObjectForceSlot13(obj), "Hex_Object13");
                //The global owned-Object gate above proves this temporary
                //has no pointer append/destruction in the finite leaf.
                if (own == victim) throw Missing("branch.Hex_owned_Object");
                receiver = own <= 51 ? own : 0;
            }
            ushort rawArmy = Need(current.ReadHexRaw1C(reference), "Hex1C");
            int army = rawArmy < 501 ? rawArmy : 0;
            if (Known(current.ReadArmyUnitNativeValidity(new(army)), "Hex_Army_validity") &&
                !Known(current.ReadForceNativeValidity(new(receiver)), "Hex_receiver_validity"))
            {
                ushort id = Need(current.ReadArmyUnitCommanderId(new(army)), "Hex_Army12");
                int person = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                    ?.PhysicalSlot ?? throw Missing("Hex_Army_Commander");
                receiver = Need(current.ReadPersonOldForceSlot(person), "Hex_Army_current_Force");
            }
            int requested = Known(current.ReadForceNativeValidity(new(receiver)), "Hex_final_Force_validity") ? receiver : 0;
            current.AssignHexForce(reference, requested); changed.Add(hex);
        }
        return changed.ToArray();
    }

    private static void ClearTargetAssociations(SourceWorldContext current, int victim)
    {
        foreach (int force in Chain(current, RegistryKind.Force))
        {
            if (force == victim || !Known(current.ReadForceNativeValidity(new(force)), "association_Force_validity")) continue;
            for (int slot = 0; slot < 3; slot++)
            {
                if (Need(current.ReadForceAssociationTargetSlot(new(force), slot), "association19C") != victim) continue;
                ushort id = Need(current.ReadForceAssociationPersonId(new(force), slot), "association196");
                int person = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                    ?.PhysicalSlot ?? throw Missing("association_Person");
                if (Known(current.ReadPersonNativeValidity(person), "association_Person_validity"))
                {
                    current.WriteForceAssociationTargetSlot(new(force), slot, 0);
                    current.WriteForceAssociationPersonId(new(force), slot, 0);
                }
                break; //2298B0 selects the first matching target only.
            }
        }
    }

    private static GameState PublishGovernor(GameState state, SourceWorldContext current, ArmyReturnGovernorPacket packet)
    {
        if (packet.Base.Kind != BaseKind.City || packet.Base.PhysicalSlot is not int cityId)
            throw Missing("branch.District_Governor_non_City");
        var people = new Dictionary<int, OfficerState>(state.Officers);
        foreach (var write in packet.PersonWrites)
        {
            ushort id = Need(current.ReadPersonIdentity(write.PersonPhysical), "Governor_ID");
            if (!people.TryGetValue(id, out var person)) throw Missing("Governor_playable_Person");
            people[id] = person with { Role = write.StatusAfter == 3 ? OfficerRole.Governor : OfficerRole.Officer,
                NativeAbilityRuntime = person.NativeAbilityRuntime! with { Status11E = write.StatusAfter } };
        }
        var city = state.Cities[cityId];
        return state with { Officers = people, Cities = new(state.Cities) { [cityId] = city with
            { GovernorId = packet.GovernorWord2EAfter == 0 ? null : packet.GovernorWord2EAfter,
                NativeReturnRuntime = city.NativeReturnRuntime! with { Governor2E = packet.GovernorWord2EAfter } } } };
    }

    public static byte?[] ForceResetImage()
    {
        var image = new byte?[0x20E];
        Zero(0x10, 3); Zero(0x47, 2); Zero(0x4A, 2); Zero(0x52, 2); Zero(0x54, 0x96);
        Zero(0x120, 0x3E); Zero(0x160, 8); Zero(0x168, 1); Zero(0x16A, 4);
        Zero(0x170, 8); Zero(0x178, 2); Put(0x15E, 0xB, 2);
        Zero(0x17A, 5); Zero(0x17F, 5); Zero(0x184, 10); Zero(0x18E, 5); Zero(0x193, 2);
        Zero(0x196, 6); Zero(0x19C, 3); Zero(0x1B8, 4); Zero(0x1BC, 8);
        Zero(0x1C5, 6); Zero(0x1CB, 10); Zero(0x1D5, 10); Zero(0x1DF, 10); Zero(0x20D, 1);
        //33C080(4C,0) writes only five numeric date bytes, not51.
        Put(0x4C, 0, 2); image[0x4E] = 1; image[0x4F] = 1; image[0x50] = 0;
        //31B250's explicit zero date copied to1B0: full six bytes.
        Put(0x1B0, 0, 2); image[0x1B2] = 1; image[0x1B3] = 1; image[0x1B4] = 0; image[0x1B5] = 0;
        return image;
        void Zero(int offset, int count) { for (int i = 0; i < count; i++) image[offset + i] = 0; }
        void Put(int offset, uint value, int width) { for (int i = 0; i < width; i++) image[offset + i] = (byte)(value >> (i * 8)); }
    }

    private static GameState ResetForce(GameState state, SourceWorldContext current, int victim,
        Dictionary<int, byte?[]> resetImages)
    {
        var module = state.NativeForceUnregisterCurrent!; var native = state.NativeArmySimulation!;
        var forces = new Dictionary<int, NativeArmyForceState>(native.Forces);
        var relation13 = forces.ToDictionary(x => x.Key, x => x.Value.NormalAftermath?.Relations13?.ToArray() ?? throw Missing("Force13"));
        var relationEA = forces.ToDictionary(x => x.Key, x => x.Value.RelationsEA?.ToArray() ?? throw Missing("ForceEA"));
        relation13[0] = module.PlaceholderRelations13.ToArray(); relationEA[0] = module.PlaceholderRelationsEA.ToArray();
        int player = current.ReadCurrentPlayerForce()?.PhysicalSlot ?? throw Missing("relation_player");
        for (int other = 0; other < 52; other++)
        {
            if (!relation13.ContainsKey(other) || !relationEA.ContainsKey(other)) throw Missing("complete_Force_relation_tables");
            relation13[victim][other] = 0; relation13[other][victim] = 0;
            relationEA[victim][other] &= 0x80; relationEA[other][victim] &= 0x80;
            if (victim == player) relationEA[victim][other] &= 0x7F;
            else if (other == player) relationEA[other][victim] &= 0x7F;
        }
        foreach (int id in forces.Keys.ToArray())
            forces[id] = forces[id] with { RelationsEA = relationEA[id],
                NormalAftermath = forces[id].NormalAftermath! with { Relations13 = relation13[id] } };
        forces[victim] = forces[victim] with { Flags12 = 0, Flags193 = 0, Flags194 = 0,
            ActivePolicyBitmap1CB = new byte[10], SuppressedPolicyBitmap1D5 = new byte[10],
            NormalAftermath = forces[victim].NormalAftermath! with { Development1BE = 0 } };
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (int field in new[] { 0x10, 0x52, 0x16C }) values[BattleRemakeCurrentForcePolicies.ForceFieldKey(victim, field)] = 0;
        values[BattleRemakeCurrentForcePolicies.ForceFieldKey(victim, 0x15E)] = 0xB;
        values[OwnedArmyLifecycleDetachedInputs.Key("Force", victim, "10")] = 0;
        values[OwnedForce164Inputs.ForceValueKey(victim)] = 0;
        for (int group = 0; group < 5; group++)
        {
            for (int ordinal = 0; ordinal < 7; ordinal++)
            { values[BattleRemakeCurrentForcePolicies.DepartmentPersonKey(victim, group, ordinal)] = 0;
                values[BattleRemakeCurrentForcePolicies.DepartmentFilterKey(victim, group, ordinal)] = 0; }
            values[BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(victim, group)] = 0;
        }
        for (int slot = 0; slot < 3; slot++)
        {
            current.WriteForceAssociationPersonId(new(victim), slot, 0); current.WriteForceAssociationTargetSlot(new(victim), slot, 0);
            values[PersonAssignmentPrecallInputs.AssociationPersonKey(victim, slot)] = 0;
            values[PersonAssignmentPrecallInputs.AssociationTargetKey(victim, slot)] = 0;
        }
        current.SetOwnedForceRulerSlot10(victim, 0); current.SetAssignmentForceAdvisor52(victim, 0); current.SetForceRankSlot47(victim, 0);
        var rankings = new Dictionary<int, NativeForceRankingCurrent>(module.ForceRankings)
            { [victim] = module.ForceRankings[victim] with { Word1BC = 0 } };
        return state with { Forces = new(state.Forces) { [victim] = state.Forces[victim] with
                { IsActive = false, CombatObjective = ForceCombatObjective.Inactive } },
            NativeArmySimulation = native with { Forces = forces, Parameters = native.Parameters with { Int32Values = values } },
            NativeForceUnregisterCurrent = module with { ForceResetImages = resetImages, ForceRankings = rankings,
                PlaceholderRelations13 = relation13[0], PlaceholderRelationsEA = relationEA[0] } };
    }
}
