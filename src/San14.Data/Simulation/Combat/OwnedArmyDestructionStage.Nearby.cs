using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    private static bool NearbyProtection(GameState state, SourceWorldContext current,
        ArmyUnitReference victim, ForceReference victimForce, NativeMapSeasonDate? sourceDate)
    {
        var temporary = current.CreateNearbyTemporary();
        try
        {
            NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, victim,
                Known(ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius), "nearby_radius"), temporary);
            var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw Missing("current.protector_Army_head");
            while (node.Identity.HasValue)
            {
                var pointer = current.ReadRegistryNodePayload(node) ?? throw Missing("current.protector_Army_pointer");
                if (pointer.Kind != RegistryKind.Army) throw Missing("current.protector_Army_type");
                var candidate = new ArmyUnitReference(pointer.PhysicalSlot);
                if (Known(current.ReadArmyUnitNativeValidity(candidate), "protector_Army_validity") &&
                    current.ReadArmyUnitForceReference(candidate).PhysicalSlot == victimForce.PhysicalSlot)
                {
                    int person = CommanderPhysical(current, candidate);
                    var trait = FirstPersonTrait(current, person, 0x35);
                    if (trait.PhysicalSlot is not null && Known(current.ReadSkillPersonalityValidity(trait), "protector_Personality_validity"))
                    {
                        var date = sourceDate ?? throw Missing("current.protector_Position_World34_date");
                        var first = PersonReturnCommands.ResolvePersonPosition(state, current, person, date);
                        var second = PersonReturnCommands.ResolvePersonPosition(state, current, CommanderPhysical(current, victim), date);
                        int distance = NativeDistance(Known(second.PhysicalSlot, "victim_Hex"), Known(first.PhysicalSlot, "protector_Hex"));
                        if (Known(current.ReadPersonalityByte(trait, 0xA4), "protector_Personality_A4") == 9 && distance <= Radius(state, current, trait))
                            return true;
                    }
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.protector_Army_next");
            }
            return false;
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
    }

    private static void NearbyPersonalityTail(GameState state, SourceWorldContext current,
        ArmyUnitReference victim, ForceReference victimForce, ForceReference winnerForce)
    {
        var temporary = current.CreateNearbyTemporary();
        try
        {
            NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, victim,
                Known(ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius), "nearby_radius"), temporary);
            var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw Missing("current.personality_tail_head");
            while (node.Identity.HasValue)
            {
                var pointer = current.ReadRegistryNodePayload(node) ?? throw Missing("current.personality_tail_Army");
                if (pointer.Kind != RegistryKind.Army) throw Missing("current.personality_tail_pointer_type");
                var army = new ArmyUnitReference(pointer.PhysicalSlot);
                int person = CommanderPhysical(current, army);
                if (Known(current.ReadPersonNativeValidity(person), "tail_Commander_validity"))
                {
                    var force = current.ReadArmyUnitForceReference(army);
                    if (Known(current.ReadForceNativeValidity(victimForce), "tail_victimForce_validity") &&
                        force.PhysicalSlot == victimForce.PhysicalSlot)
                        ApplyCategory(army, person, 0x27, false);
                    if (Known(current.ReadForceNativeValidity(winnerForce), "tail_winnerForce_validity") && force.PhysicalSlot == winnerForce.PhysicalSlot)
                    {
                        bool hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                            Known(force.PhysicalSlot, "tail_Force"), Known(victimForce.PhysicalSlot, "tail_victimForce"))
                            ?? throw Missing("current.tail_2CA650");
                        if (hostile) ApplyCategory(army, person, 0x28, true);
                    }
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.personality_tail_next");
            }
        }
        finally { current.ClearNearbyTemporary(temporary, true); }

        void ApplyCategory(ArmyUnitReference army, int person, uint category, bool winnerCategory)
        {
            var list = current.CreatePersonInjuryTemporary();
            try
            {
                var append = PersonCategoryTemporarySource.Append(current, person, category, list);
                if (append.NativeReturn is null) throw Missing(append.UnresolvedOwner ?? "current.2C56D0");
                var at = current.ReadSkillTemporaryHead(list) ?? throw Missing("current.tail_Personality_head");
                while (at.Identity.HasValue)
                {
                    var trait = current.ReadSkillTemporaryPayload(at) ?? throw Missing("current.tail_Personality_payload");
                    //The source applies BOTH signed values. It does not use
                    //the effect-code matching query22C760 for these callbacks.
                    foreach (int offset in new[] { 0xB8, 0xBC })
                    {
                        int value = Math.Clamp((int)unchecked((short)Known(current.ReadPersonalityWord(trait, offset), "tail_Personality_value")), -100, 2000);
                        if (value < 0)
                        {
                            var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("current.tail_Army_resource");
                            int cap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
                            if (winnerCategory || Math.Min(Known(fields.Morale1A, "tail_Army1A"), cap) > 0)
                            {
                                var result = ArmyMoraleNonzeroCurrent.Apply(state, current, army, value);
                                if (result.NativeReturn is null) throw Missing(result.UnresolvedOwner ?? "current.tail_2709A0");
                            }
                        }
                        else if (value > 0)
                        {
                            byte kind = winnerCategory ? Known(current.ReadPersonalityByte(trait, 0xAC), "tail_Personality_AC") : (byte)1;
                            if (kind == 1) AddMorale(current, army, value);
                            if (kind == 0xB) throw Missing("branch.tail_Personality_AC11_223990_neighbors");
                        }
                    }
                    at = current.ReadHexListNodeNext(at) ?? throw Missing("current.tail_Personality_next");
                }
            }
            finally { current.ClearPersonInjuryTemporary(list, true); }
        }
    }

    private static void AddMorale(SourceWorldContext current, ArmyUnitReference army, int amount)
    {
        var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("current.morale_Army_fields");
        int limit = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        int oldLimit = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        int request = Math.Min(unchecked(Math.Min(Known(fields.Morale1A, "morale_Army1A"), oldLimit) + amount), limit);
        int setter = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        current.WriteArmyReturnMorale(Known(army.PhysicalSlot, "morale_Army_physical"),
            ArmyMoraleSource.SetByte2419A0(request, setter));
    }
    private static PersonalityReference FirstPersonTrait(SourceWorldContext current, int person, byte effect)
    {
        for (int index = 0; index < 9; index++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, index) ?? throw Missing("current.2AB720_selected_trait");
            if (!Known(current.ReadSkillPersonalityValidity(trait), "2AB720_trait_validity")) continue;
            if (Known(current.ReadPersonalityByte(trait, 0xB6), "2AB720_B6") == effect ||
                Known(current.ReadPersonalityByte(trait, 0xBA), "2AB720_BA") == effect) return trait;
        }
        return new(null);
    }
    private static int CommanderPhysical(SourceWorldContext current, ArmyUnitReference army)
    {
        ushort id = Known(current.ReadArmyUnitCommanderId(army), "nearby_Commander12");
        return (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw Missing("current.nearby_Commander_pointer");
    }
    //TC2AAC90 over the native220-wide column-offset layout. Unlike a Core
    //pathfinder this only implements the actual integer comparison query.
    private static int NativeDistance(int first, int second)
    {
        int horizontal = Math.Abs(second % 220 - first % 220);
        int doubledVertical = (second / 220 - first / 220) * 2 + (second % 220 % 2 == 0 ? 1 : 0) - (first % 220 % 2 == 0 ? 1 : 0);
        int vertical = Math.Abs(doubledVertical);
        return horizontal <= vertical ? (vertical - horizontal) / 2 + horizontal : horizontal;
    }
}
