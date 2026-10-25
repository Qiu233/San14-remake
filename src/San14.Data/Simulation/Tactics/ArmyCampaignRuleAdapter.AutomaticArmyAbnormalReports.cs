using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual Army-source2F29B0, mode0. Call only after2EED60
    /// reaches an immunity report or a new positive status. Formatting and
    /// persistent history use the existing complete owners; pending UI work
    /// remains unacknowledged in the enclosing operation's ordered sink.</summary>
    internal GameState ApplyAutomaticArmyAbnormalReport(GameState state, SourceWorldContext current,
        UnitState? caster, UnitState target, int districtSlot, int nativeIndex, int causeCode,
        string actualTacticName, Func<int> readWorldTime450, List<CampaignEffectRequest> effects)
    {
        ArgumentNullException.ThrowIfNull(actualTacticName);
        ArgumentNullException.ThrowIfNull(readWorldTime450);
        if (nativeIndex is < 0 or > 2) return state;
        var targetArmy = current.ReadArmyUnitTableReference(K(target.NativeArmyOrigin?.NativeSlotIndex,
            "target_Army_physical")) ?? throw AbnormalReportMissing("target_Army_pointer");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw AbnormalReportMissing("current_player_Force_pointer");
        var primary = PrimaryDistrict(player);
        if (!K(current.ReadDistrictNativeValidity(primary), "current_player_primary_validity") ||
            districtSlot != K(current.ReadDistrictPhysicalSlot(primary), "current_player_primary_identity")) return state;
        int receivingPerson = Commander(target);
        var working = state;
        if (causeCode is >= 21 and <= 24)
        {
            int? trait = FirstImmunity(receivingPerson, causeCode);
            if (trait is not int selected) return working;
            string? phrase = causeCode == 21 ? null : Phrase(causeCode - 22);
            Add(causeCode == 21 ? 0x3228 : 0x3229, 0,
                new(Army0: targetArmy, String0: current.ReadPersonalityName(new(selected)), String1: phrase));
            return working;
        }
        //The source Army tactic's actual name pointer is supplied even if
        //its text is empty; do not replace it with an Object/facility name.
        Add(0x3225, 5, new(Army0: targetArmy, String0: actualTacticName, String1: Phrase(nativeIndex)));
        if (causeCode is not (9 or 10 or 11 or 12 or 98)) return working;
        PersonalityReference? selectedTrait = null;
        int selectedValue = 0;
        if (causeCode == 98)
        {
            //Source98 selection is NOT the target's absolute maximum rule.
            //151 and256..355 compete by strictly more-negative value;
            //157/163/204 select immediately in the source's ordered list.
            int actingPerson = Commander(caster ?? throw AbnormalReportMissing("source98_actual_caster"));
            for (int slot = 0; slot < 9; slot++)
            {
                int key = K(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,
                    actingPerson, slot), "source98_selected_key");
                if (key is 157 or 163 or 204)
                {
                    selectedTrait = current.ReadPersonalityTableReference(key)
                        ?? throw AbnormalReportMissing("source98_special_trait_pointer");
                    break;
                }
                if (key != 151 && key is not (>= 256 and <= 355)) continue;
                var trait = current.ReadPersonalityTableReference(key)
                    ?? throw AbnormalReportMissing("source98_trait_pointer");
                //22C760 has no personality-validity gate. The selected
                //result's native validity is tested only after the loop.
                int value = Contribution(trait, 98);
                if (value < selectedValue) { selectedValue = value; selectedTrait = trait; }
            }
            if (selectedTrait is null || !K(current.ReadSkillPersonalityValidity(selectedTrait), "source98_selected_validity"))
                return working;
            selectedValue = Contribution(selectedTrait, 98);
        }
        else
        {
            var keys = ArmyTraitSourceAdapter.Encode(working, target) ?? throw AbnormalReportMissing("target_Army36");
            var values = TacticRemakeCurrentTraits.ArmyContributions(current, receivingPerson, keys)
                ?? throw AbnormalReportMissing("target_contributions127");
            bool positive = values[causeCode] > 0;
            foreach (ushort key in keys)
                Consider(current.ReadPersonalityTableReference(key <= 355 ? key : 0)
                    ?? throw AbnormalReportMissing("target_cached_trait_pointer"), false, 0);
            for (int slot = 0; slot < 9; slot++)
                Consider(TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, receivingPerson, slot)
                    ?? throw AbnormalReportMissing("target_own_trait_pointer"), true, slot);

            void Consider(PersonalityReference trait, bool own, int slot)
            {
                if (!K(current.ReadSkillPersonalityValidity(trait), "target_attribution_validity")) return;
                if (own && K(current.ReadPersonalityWord(trait, 0xB4), "target_attribution_condition") != ushort.MaxValue &&
                    (K(current.ReadPersonTraitMask1BC(receivingPerson), "target_attribution_mask") & (1 << slot)) == 0) return;
                int value = Contribution(trait, causeCode);
                if (value == 0 || (value > 0) != positive || Abs(value) <= Abs(selectedValue)) return;
                selectedTrait = trait; selectedValue = value;
            }
            if (selectedTrait is null) return working;
        }
        string statusPhrase = Phrase(causeCode is 9 or 98 ? nativeIndex : causeCode - 10);
        string sign = ReadHistoryDataText(1, selectedValue > 0 ? 0x4BA : 0x4B9);
        Add(0x322D, 0, new(Army0: targetArmy,
            String0: current.ReadPersonalityName(selectedTrait), String1: statusPhrase, String2: sign));
        return working;

        void Add(int root, int category, HistoryMessageOperands operands)
        {
            var formatted = FormatHistoryReport(working, current, root, operands);
            working = formatted.State;
            int position = K(current.ReadArmyUnitRawHex2A(targetArmy), "target_report_Hex2A");
            int worldTime = readWorldTime450();
            working = WriteHistory(working, current, new(category, districtSlot, position,
                worldTime, formatted.Text, CampaignHistoryWriter.Supplemental(working), 5012, MarkBit4: 1), effects);
        }
        int Commander(UnitState army)
        {
            var pointer = current.ReadArmyUnitTableReference(K(army.NativeArmyOrigin?.NativeSlotIndex,
                "Commander_Army_physical")) ?? throw AbnormalReportMissing("Commander_Army_pointer");
            ushort raw = K(current.ReadArmyUnitCommanderId(pointer), "Commander_Army12");
            var person = raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0);
            return K(person?.PhysicalSlot, "Commander_Person_pointer");
        }
        int? FirstImmunity(int person, int effect)
        {
            if (!K(current.ReadPersonNativeValidity(person), "immunity_Person_validity")) return null;
            for (int slot = 0; slot < 9; slot++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, slot)
                    ?? throw AbnormalReportMissing("immunity_trait_pointer");
                if (!K(current.ReadSkillPersonalityValidity(trait), "immunity_trait_validity")) continue;
                if (K(current.ReadPersonalityByte(trait, 0xB6), "immunity_trait_B6") == effect ||
                    K(current.ReadPersonalityByte(trait, 0xBA), "immunity_trait_BA") == effect)
                    return K(trait.PhysicalSlot, "immunity_trait_physical");
            }
            return null;
        }
        int Contribution(PersonalityReference trait, int effect)
        {
            for (int pair = 0; pair < 2; pair++)
                if (K(current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "contribution_code") == effect)
                    return Math.Clamp((int)unchecked((short)K(current.ReadPersonalityWord(trait,
                        pair == 0 ? 0xB8 : 0xBC), "contribution_value")), -100, 2000);
            return 0;
        }
        DistrictReference PrimaryDistrict(ForceReference force)
        {
            int identity = K(current.ReadForcePhysicalSlot(force), "primary_Force_identity");
            if (identity is >= 1 and <= 51)
            {
                ushort ruler = K(current.ReadForceRecord(identity)?.RawRuler10, "primary_Force10");
                foreach (var district in current.ReadArmyReturnRegisteredDistricts()
                    ?? throw AbnormalReportMissing("primary_District_registry"))
                {
                    int at = K(district?.PhysicalSlot, "primary_District_pointer");
                    if (K(current.ReadDistrictForceId(at), "primary_District_Force") != identity) continue;
                    if (K(current.ReadDistrictLeader(at), "primary_District_leader") == ruler ||
                        K(current.ReadDistrictNumber(at), "primary_District_number") == 1) return new(at);
                }
            }
            return current.ReadDistrictTableReference(0) ?? throw AbnormalReportMissing("primary_District0");
        }
        string Phrase(int index) => ReadHistoryDataText(1, index switch
        { 0 => 0x25C, 1 => 0x260, 2 => 0x25E, _ => throw AbnormalReportMissing("status_phrase_index") });
        int Abs(int value) => value < 0 ? unchecked(-value) : value;
        T K<T>(T? value, string owner) where T : struct => value ?? throw AbnormalReportMissing(owner);
    }

    private static InvalidDataException AbnormalReportMissing(string owner) =>
        new("automatic_Army_abnormal_report." + owner);
}
