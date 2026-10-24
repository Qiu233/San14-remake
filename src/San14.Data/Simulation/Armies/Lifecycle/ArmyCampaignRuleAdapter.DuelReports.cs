using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //379F60: the caller has already performed the real injury/recovery prefix.
    //These are real history writes; the resulting UI notification is not a
    //replacement for the stored ring record.
    private GameState AppendDuelMoraleNotice(GameState state, SourceWorldContext current,
        int kind, int personId, NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> effects)
    {
        int person = DuelAftermathPerson(current, personId);
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(person), "notice_Person_validity")) return state;
        var army = DuelReportArmy(current, person);
        if (!DuelAftermathNeed(current.ReadArmyUnitNativeValidity(army), "notice_Army_validity")) return state;
        int root = kind switch { 0 => 0xC95C, 1 => 0xC95B, 2 => 0xC95D,
            _ => throw DuelCampaignAftermathParameters.Missing("notice_kind") };
        var formatted = FormatDuelHistoryReport(state, current, root, new(Army0: army));
        state = formatted.State;
        int reporter = DuelAftermathNeed(current.ReadPersonIdentity(person), "notice_reporter");
        byte district = DuelAftermathNeed(current.ReadPersonDistrictSlot(person), "notice_District");
        int day = FSampler(state, moment).Day450;
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate);
        int hex = position.PhysicalSlot ?? throw DuelCampaignAftermathParameters.Missing("notice_current_position");
        return WriteHistory(state, current, new(5, district, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), reporter), effects);
    }

    //37A120: player-relative first/second participants, before the loser
    //morale/destruction writer. Summary and attached speech are separate
    //formatter contexts with their actual four Person operands.
    private GameState AppendDuelOutcomeHistory(GameState state, SourceWorldContext current,
        int playerOutcome, int aftermath, int playerPersonId, int otherPersonId,
        NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> effects)
    {
        int first = DuelAftermathPerson(current, playerPersonId), second = DuelAftermathPerson(current, otherPersonId);
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(first), "summary_first_validity") ||
            !DuelAftermathNeed(current.ReadPersonNativeValidity(second), "summary_second_validity")) return state;
        var firstArmy = DuelReportArmy(current, first); var secondArmy = DuelReportArmy(current, second);
        if (!DuelAftermathNeed(current.ReadArmyUnitNativeValidity(firstArmy), "summary_first_Army_validity") ||
            !DuelAftermathNeed(current.ReadArmyUnitNativeValidity(secondArmy), "summary_second_Army_validity")) return state;
        int category = 5, mark3 = 0, root;
        if (playerOutcome == 0)
        {
            if (aftermath == 2) root = DuelReportFlag14(current, second) ? 0xC954 : 0xC953;
            else root = aftermath != 1 && DuelReportMorale(current, secondArmy) -
                DuelCampaignAftermathParameters.Read(state, "loser-morale-loss") > 0 ? 0xC952 : 0xC954;
            //The C952 surviving-morale branch retains category5/mark3=0.
            if (root != 0xC952) { category = 1; mark3 = 1; }
        }
        else if (playerOutcome == 2) root = 0xC959;
        else if (playerOutcome == 1)
        {
            if (aftermath == 2)
            {
                root = DuelReportFlag14(current, first) ? 0xC957 : 0xC956;
                category = 1; mark3 = 1;
            }
            else if (aftermath == 1) { root = 0xC958; category = 1; mark3 = 1; }
            else root = DuelReportMorale(current, firstArmy) -
                DuelCampaignAftermathParameters.Read(state, "loser-morale-loss") > 0 ? 0xC955 : 0xC957;
        }
        else return state;
        int firstId = DuelAftermathNeed(current.ReadPersonIdentity(first), "summary_first_identity");
        int secondId = DuelAftermathNeed(current.ReadPersonIdentity(second), "summary_second_identity");
        var main = FormatDuelHistoryReport(state, current, root, new(Person0: firstId, Person1: secondId));
        state = main.State;
        if (playerOutcome == 1 && aftermath == 1)
            state = AppendDuelDeathChronicleIfRelated(state, current, first, second, moment);

        //The empty supplemental buffer is local and initialized to empty;
        //it is not the independent default history supplemental pointer.
        string attachment = "";
        var force = current.ReadPersonForce(first) ?? throw DuelCampaignAftermathParameters.Missing("summary_attachment_Force");
        _ = DuelAftermathNeed(current.ReadForceNativeValidity(force), "summary_attachment_Force_validity");
        int ruler = DuelAftermathNeed(current.ReadMoraleWaveRuler22EAC0(force), "summary_attachment_ruler");
        int? attachmentRoot = playerOutcome switch
        {
            0 when aftermath == 2 => DuelReportFlag14(current, second) ? 0x53A4 : 0x53A3,
            0 when aftermath == 1 => 0x53A4,
            0 when DuelReportMorale(current, secondArmy) - DuelCampaignAftermathParameters.Read(state, "loser-morale-loss") <= 0 => 0x53A4,
            1 when aftermath == 2 && !DuelReportFlag14(current, first) => 0x53A5,
            1 when aftermath == 1 => 0x53A6,
            _ => null
        };
        if (attachmentRoot is int selected)
        {
            var formatted = FormatDuelHistoryReport(state, current, selected,
                new(Person0: 5012, Person1: ruler, Person2: firstId, Person3: secondId));
            state = formatted.State; attachment = formatted.Text;
        }
        byte district = DuelAftermathNeed(current.ReadPersonDistrictSlot(first), "summary_District");
        int day = FSampler(state, moment).Day450;
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, first, moment.PreWriterThirdDate);
        int hex = position.PhysicalSlot ?? throw DuelCampaignAftermathParameters.Missing("summary_current_position");
        //ASM37A6A0 writes arg11(mark4)=1 independently of category/mark3.
        return WriteHistory(state, current, new(category, district, hex, day, main.Text, attachment,
            5012, MarkBit3: mark3, MarkBit4: 1), effects);
    }

    private GameState AppendDuelDeathChronicleIfRelated(GameState state, SourceWorldContext current,
        int deceased, int victor, NativeArmyProgressStageMoment moment)
    {
        var force = current.ReadPersonForce(deceased) ?? throw DuelCampaignAftermathParameters.Missing("journal_Force");
        int rulerId = DuelAftermathNeed(current.ReadForceNativeValidity(force), "journal_Force_validity")
            ? DuelAftermathNeed(current.ReadMoraleWaveRuler22EAC0(force), "journal_ruler") : 0;
        int ruler = DuelAftermathPerson(current, rulerId);
        int deceasedId = DuelAftermathNeed(current.ReadPersonIdentity(deceased), "journal_deceased_identity");
        int victorId = DuelAftermathNeed(current.ReadPersonIdentity(victor), "journal_victor_identity");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var people = new DeploymentNarrationCurrentPeople(state, current, abilities, rulerId, deceasedId,
            panelReader: (pointer, effect) => _baseCapacityInputs.ReadPanelModifier(state, pointer, effect));
        bool related = people.Relationship('A', rulerId, deceasedId) != 0 || people.Relationship('B', rulerId, deceasedId) != 0;
        if (!related && DuelAftermathNeed(current.ReadPersonNativeValidity(ruler), "journal_ruler_validity") &&
            DuelAftermathNeed(current.ReadPersonNativeValidity(deceased), "journal_deceased_validity"))
        {
            bool forceMatches = true;
            if (DuelAftermathNeed(current.ReadPersonStatus(ruler), "journal_ruler_status") is >= 1 and <= 4 &&
                DuelAftermathNeed(current.ReadPersonStatus(deceased), "journal_deceased_status") is >= 1 and <= 4)
                forceMatches = DuelAftermathNeed(current.ReadPersonOldForceSlot(ruler), "journal_ruler_old_Force") ==
                    DuelAftermathNeed(current.ReadPersonOldForceSlot(deceased), "journal_deceased_old_Force");
            if (forceMatches)
            {
                related = people.Relationship('C', rulerId, deceasedId) != 0 || people.Relationship('B', rulerId, deceasedId) != 0 ||
                    people.Relationship('D', rulerId, deceasedId) != 0 || people.Relationship('A', rulerId, deceasedId) != 0;
                if (!related)
                {
                    ushort blood = OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, ruler, 0xB2)
                        ?? throw DuelCampaignAftermathParameters.Missing("journal_clan");
                    related = blood != 0 && people.Relationship('H', rulerId, deceasedId) != 0;
                }
            }
        }
        if (!related && DuelAftermathNeed(current.ReadPersonNativeValidity(deceased), "journal_like_target_validity"))
            for (int index = 0; index < 8; index++)
                if ((OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, ruler, index)
                    ?? throw DuelCampaignAftermathParameters.Missing("journal_like")) == deceasedId) { related = true; break; }
        if (!related) return state;
        int day = FSampler(state, moment).Day450;
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, deceased, moment.PreWriterThirdDate);
        int hex = position.PhysicalSlot ?? throw DuelCampaignAftermathParameters.Missing("journal_Hex");
        string label = HistoryWriter.CustomFirstName(state, current, force, FullName);
        int color = DuelAftermathNeed(current.ReadForceReportColor(force), "journal_color");
        string secondName = Name(victorId), firstName = Name(deceasedId);
        RequireLabel(firstName); RequireLabel(secondName);
        return new CampaignChronicleWriter(HistoryWriter).Write(state, current,
            new(0xDA, 1, day, label, color, 26,
                ImmutableArray.Create(firstName, secondName, ""),
                ImmutableArray.Create(hex, 48400, 48400, 48400)));

        string Name(int id) => FullName(state.Officers.GetValueOrDefault(id)
            ?? throw DuelCampaignAftermathParameters.Missing("journal_Person_owner"), true);
        static string FullName(OfficerState person, bool full)
        {
            string surname = Head(person.CurrentSurname ?? throw DuelCampaignAftermathParameters.Missing("journal_surname"));
            return full ? surname + Head(person.CurrentGivenName ?? throw DuelCampaignAftermathParameters.Missing("journal_given_name")) : surname;
            static string Head(string value) => value.IndexOf('\0') is int index && index >= 0 ? value[..index] : value;
        }
        static void RequireLabel(string value)
        {
            //The existing chronology writer's complete nine-unit source
            //profile preserves the original terminator and old tail.
            if (value.Length > 8 || value.Any(character => character == '\0' || char.IsSurrogate(character)))
                throw DuelCampaignAftermathParameters.Missing("journal_complete_label_copy_profile");
        }
    }

    private static bool DuelReportFlag14(SourceWorldContext current, int person) =>
        (DuelAftermathNeed(current.ReadArmyReturnPersonFlags196(person), "report_Person_flags") & 0x4000) != 0;
    private static int DuelReportMorale(SourceWorldContext current, ArmyUnitReference army)
    {
        int cap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        byte raw = DuelAftermathNeed(current.ReadArmyReturnResourceFields(army)?.Morale1A, "report_Army_morale");
        return Math.Min((int)raw, cap);
    }
    private static ArmyUnitReference DuelReportArmy(SourceWorldContext current, int person)
    {
        ushort position = DuelAftermathNeed(current.ReadPersonSecondLocation(person), "report_Person_position");
        var army = current.ReadArmyUnitTableReference(position is >= 62 and <= 561 ? position - 61 : 0)
            ?? throw DuelCampaignAftermathParameters.Missing("report_Army_pointer");
        ushort id = DuelAftermathNeed(current.ReadPersonIdentity(person), "report_Person_identity");
        return DuelAftermathNeed(current.ReadArmyUnitCommanderId(army), "report_Army_commander") == id ? army :
            current.ReadArmyUnitTableReference(0) ?? throw DuelCampaignAftermathParameters.Missing("report_Army0_pointer");
    }
}
