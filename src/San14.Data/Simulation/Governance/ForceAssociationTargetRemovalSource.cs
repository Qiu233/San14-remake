using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //278090, shared by the ordinary Force budget and Force-retirement callers.
    //Only its first matching target slot is cleared, including duplicates.
    internal GameState CancelForceAssociationTargetCurrent(GameState state, SourceWorldContext current,
        ForceReference own, ForceReference other, int messageRoot, bool writeHistory,
        bool eventPlaybackPresent, List<CampaignEffectRequest> effects)
    {
        if (!K(current.ReadForceNativeValidity(own), "own_validity") ||
            !K(current.ReadForceNativeValidity(other), "other_validity") || own == other) return state;
        int target = K(current.ReadForcePhysicalSlot(other), "target60"), selected = -1;
        for (int slot = 0; slot < 3; slot++)
            if (K(current.ReadForceAssociationTargetSlot(own, slot), "target19C") == target) { selected = slot; break; }
        if (selected < 0) return state;
        int id = K(current.ReadForceAssociationPersonId(own, selected), "actor196");
        int person = K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "actor_lookup");
        if (!K(current.ReadPersonNativeValidity(person), "actor_validity")) return state;
        current.WriteForceAssociationTargetSlot(own, selected, 0);
        current.WriteForceAssociationPersonId(own, selected, 0);
        state = PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, [], [],
            [(K(own.PhysicalSlot, "own_pointer"), selected)]);
        if (!TalentForceIsPlayerCurrent(state, current, own)) return state;
        id = K(current.ReadPersonIdentity(person), "actor10");
        if (writeHistory)
        {
            var main = FormatNormalTurnReport(state, current, 0x3452, new(Person0: id, Force0: target)); state = main.State;
            int day = (ReadArmyDestructionSampler(state, current) ?? throw Missing("World450")).Day450;
            var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(person), "actorE2"), current);
            int rawDistrict = K(current.ReadPersonDistrictSlot(person), "actorE0");
            var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0) ?? throw Missing("District_pointer");
            var hex = current.ReadReportBaseHex(home) ?? throw Missing("Base_Hex");
            state = WriteHistory(state, current, new(0, K(current.ReadDistrictPhysicalSlot(district), "District_ordinal"),
                K(current.ReadReportMapCellPhysicalSlot(hex), "Hex_ordinal"), day, main.Text,
                CampaignHistoryWriter.Supplemental(state), 5012), effects);
        }
        if (messageRoot != 0 && !eventPlaybackPresent)
        {
            var source = state;
            var line = FormatNormalTurnReport(state, current, messageRoot,
                new(Person0: id, Person1: K(current.ReadMoraleWaveRuler22EAC0(own), "ruler22EAC0"), Force0: target));
            state = line.State;
            effects.Add(CreateStrategicCommandPresentation(current, source, id, line));
        }
        return state;
        static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("force-association-target-removal." + field);
    }
}
