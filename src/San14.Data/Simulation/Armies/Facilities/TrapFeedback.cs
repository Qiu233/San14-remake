using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

/// <summary>One operation's ordered intentions and lazy personnel owner.
/// Numerical callbacks may create real tasks before the first speech getter.</summary>
internal sealed class TrapFeedback
{
    private readonly ArmyCampaignRuleAdapter package;
    private readonly Func<GameState> state;
    private readonly Action<GameState> replace;
    private readonly SourceWorldContext current;
    private readonly NativeArmyProgressStageMoment moment;
    private readonly GameResourceRepository repository;
    private readonly LoadedScenario scenario;
    private readonly Func<int> campaignDay;
    private bool personnelBound;
    internal List<CampaignEffectRequest> Requests { get; }
    internal ArmyTraitFeedback Traits { get; }

    internal TrapFeedback(ArmyCampaignRuleAdapter package, Func<GameState> state, Action<GameState> replace, SourceWorldContext current,
        NativeArmyProgressStageMoment moment, GameResourceRepository repository, LoadedScenario scenario, Func<int> campaignDay, List<CampaignEffectRequest>? requests = null)
    {
        this.package = package; this.state = state; this.replace = replace; this.current = current; this.moment = moment;
        this.repository = repository; this.scenario = scenario;
        this.campaignDay = campaignDay;
        Requests = requests ?? [];
        Traits = new(state(), current, moment, Requests, state, EnsurePersonnel);
    }

    internal void EnsurePersonnel()
    {
        if (personnelBound) return;
        //An earlier immediate defeat/return callback may already own a newer
        //manager here. Never overwrite it with the operation's initial state.
        if (current.ReadOwnedPersonCommandManager() is null)
        {
            var latest = state();
            if (latest.PersonnelCommands is null) throw Missing("feedback_personnel_owner");
            PersonReturnCommands.BindCurrent(latest, current);
        }
        personnelBound = true;
    }

    internal void Speech(ArmyUnitReference army, CombatSpeechEvent action,
        ObjectReference facility, int? definitionId = null)
    {
        if (!Need(current.ReadArmyUnitNativeValidity(army), "speech_Army_validity")) return;
        EnsurePersonnel();
        ushort raw = Need(current.ReadArmyUnitCommanderId(army), "speech_Commander12");
        int person = Person(raw);
        int position = PersonReturnCommands.ResolvePersonPosition(state(), current, person,
            moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
        int officer = Need(current.ReadPersonIdentity(person), "speech_Person_identity");
        Requests.Add(new(new(CombatParticipantKind.Army, ArmyId(army), officer, action, position,
            CombatParticipantKind.FieldObject, Receiver(facility).Value, null, null, null)
            { TrapDefinitionId = action == CombatSpeechEvent.ArmyUnderFacilityAttack ? null : definitionId,
                FacilityDefinitionId = action == CombatSpeechEvent.ArmyUnderFacilityAttack ? definitionId : null }, null));
    }

    internal int ArmyId(ArmyUnitReference army)
    {
        int physical = Need(army.PhysicalSlot, "Army_pointer");
        var arena = state().ReadArmyLifecycle() ?? throw Missing("feedback_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw Missing("feedback_Army_receiver");
        return receiver.ActiveUnitId ?? receiver.PendingImage?.Id ?? throw Missing("feedback_Army_identity");
    }
    internal MapObjectReceiverId Receiver(ObjectReference facility) =>
        (current.CurrentMapObjectDirectory ?? throw Missing("feedback_Object_directory"))
            .ReceiverAtPhysical(Need(facility.PhysicalSlot, "feedback_Object_pointer"));
    internal string DefinitionName(int definitionId) =>
        repository.LoadObjectDefinitions(scenario.FixedDataSource.SourcePath).Catalog.Definitions[definitionId].Name;
    internal int Person(int raw) =>
        (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw Missing("feedback_Person_pointer");
    internal string ArmyName(ArmyUnitReference army)
    {
        if (!Need(current.ReadArmyUnitNativeValidity(army), "name_Army_validity")) return "";
        int person = Person(Need(current.ReadArmyUnitCommanderId(army), "name_Commander12"));
        if (!Need(current.ReadPersonNativeValidity(person), "name_Person_validity")) return "";
        int id = Need(current.ReadPersonIdentity(person), "name_Person_identity");
        return state().Officers.GetValueOrDefault(id)?.Name ?? throw Missing("name_current_full_Name");
    }

    internal byte District(ArmyUnitReference army)
        => checked((byte)Need(current.ReadDistrictPhysicalSlot(ArmyDistrict(army)), "district_identity"));
    private DistrictReference ArmyDistrict(ArmyUnitReference army)
    {
        int person = Person(Need(current.ReadArmyUnitCommanderId(army), "district_Commander12"));
        byte raw = !Need(current.ReadPersonNativeValidity(person), "district_Person_validity") ? (byte)0 :
            Need(current.ReadPersonDistrictSlot(person), "district_PersonE0");
        return current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw Missing("district_pointer");
    }
    internal bool PlayerDistrict(ArmyUnitReference army)
    {
        //2EED60 uses233A90. Counter18F3D0 has its separate233880 owner.
        var district = ArmyDistrict(army);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state(), current) ?? throw Missing("report_player_pointer");
        if (!Need(current.ReadForceNativeValidity(player), "report_player_validity")) return false;
        if (Need(current.ReadDistrictForceSlot(district), "report_District_Force") !=
            Need(current.ReadForcePhysicalSlot(player), "report_player_identity")) return false;
        return RulerDistrict(district);
    }
    internal bool PlayerForce(ForceReference force)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state(), current) ?? throw Missing("player_Force_pointer");
        int identity = Need(current.ReadForcePhysicalSlot(player), "player_Force_identity");
        return identity != 0 && identity == Need(current.ReadForcePhysicalSlot(force), "trap_Force_identity");
    }

    internal bool RulerDistrict(DistrictReference district)
    {
        int at = Need(district.PhysicalSlot, "leader_District_pointer");
        int person = Person(Need(current.ReadDistrictLeader(at), "leader_District12"));
        return Need(current.ReadPersonNativeValidity(person), "leader_Person_validity") &&
            Need(current.ReadPersonStatus(person), "leader_Person_status") == 1;
    }
    internal DistrictReference FacilityDistrict(HexReference hex)
    {
        ushort areaRaw = hex.PhysicalSlot.HasValue ? Need(current.ReadHexAreaSlot(hex), "caller_Hex12") : (ushort)0;
        var area = current.ReadAreaTableReference(areaRaw <= 500 ? areaRaw : 0) ?? throw Missing("caller_Area_pointer");
        //18E9C0 reads Area35 without its own Area-validity branch.
        byte cityRaw = Need(current.ReadAreaCitySlot(area), "caller_Area35");
        var city = current.ReadCityTableReference(cityRaw <= 51 ? cityRaw : 0) ?? throw Missing("caller_City_pointer");
        return !Need(current.ReadBaseNativeValidity(city), "caller_City_validity") ? new(null) :
            current.ReadCityDistrictReference(city) ?? throw Missing("caller_City_District");
    }
    private DistrictReference PrimaryDistrict(ForceReference force)
    {
        int identity = Need(current.ReadForcePhysicalSlot(force), "primary_Force_identity");
        if (identity is >= 1 and <= 51)
        {
            ushort ruler = Need(current.ReadForceRecord(identity)?.RawRuler10, "primary_Force10");
            foreach (var district in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("primary_District_registry"))
            {
                int at = Need(district?.PhysicalSlot, "primary_District_pointer");
                if (Need(current.ReadDistrictForceId(at), "primary_District10") != identity) continue;
                if (Need(current.ReadDistrictLeader(at), "primary_District12") == ruler ||
                    Need(current.ReadDistrictNumber(at), "primary_District11") == 1) return new(at);
            }
        }
        return current.ReadDistrictTableReference(0) ?? throw Missing("primary_District0");
    }
    internal void TriggeredReport(ObjectReference facility, int definition, ForceReference force, HexReference savedHex)
    {
        string trapName = ReportObjectName(facility);
        ushort areaArgument = Need(current.ReadHexAreaSlot(savedHex), "triggered_saved_Hex12");
        var formatted = package.FormatHistoryReport(state(), current, 0x5F80,
            new HistoryMessageOperands(String0: trapName, Area0: areaArgument));
        replace(formatted.State);
        int day = Day();
        int place = Need(current.ReadHexPhysicalSlot(savedHex), "triggered_saved_Hex_identity");
        var recipient = PrimaryDistrict(force);
        int district = Need(current.ReadDistrictPhysicalSlot(recipient), "triggered_recipient_identity");
        CommitHistory(5, district, place, day, 5012, formatted.Text);
    }
    private int Day() => campaignDay();
    private string TraitName(int trait) => current.ReadPersonalityName(new(trait));
    internal void CounterReport(ObjectReference facility, int definition, ArmyUnitReference army, int trait, HexReference context)
    {
        string traitName = TraitName(trait);
        byte argument = Need(current.ReadObjectForceSlot13(facility), "counter_report_Object13");
        string name = ReportObjectName(facility);
        var formatted = package.FormatHistoryReport(state(), current, 0xC96A,
            new HistoryMessageOperands(Army0: army, String0: traitName, String1: name, Force0: argument));
        replace(formatted.State);
        int person = Person(Need(current.ReadArmyUnitCommanderId(army), "counter_report_Commander12"));
        int officer = Need(current.ReadPersonIdentity(person), "counter_report_Person_identity");
        int day = Day();
        int position = Need(current.ReadHexPhysicalSlot(context), "counter_report_Hex_identity");
        byte district = RawReportDistrict(army);
        CommitHistory(0, district, position, day, officer, formatted.Text);
    }
    internal void AbnormalReport(ArmyUnitReference army, ObjectReference facility,
        int definition, byte index, int cause, int responseSign)
    {
        if (!PlayerDistrict(army)) return;
        int district = District(army);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state(), current) ?? throw Missing("abnormal_player_pointer");
        var primary = PrimaryDistrict(player);
        if (!Need(current.ReadDistrictNativeValidity(primary), "abnormal_primary_validity") ||
            district != Need(current.ReadDistrictPhysicalSlot(primary), "abnormal_primary_identity")) return;
        if (responseSign != 0) throw Missing("abnormal_report_nonnull_caster");
        int person = Person(Need(current.ReadArmyUnitCommanderId(army), "abnormal_report_Commander12"));
        if (cause is >= 21 and <= 24)
        {
            string? phrase = cause == 21 ? null : package.ReadHistoryDataText(1, cause switch
            { 22 => 0x25C, 23 => 0x260, 24 => 0x25E, _ => throw Missing("abnormal_immunity_phrase") });
            int? immunity = FirstTrait(person, cause);
            if (immunity is int trait) Add(FacilityTrapReportOperation.AbnormalImmunity, trait, null, phrase);
            return;
        }
        Add(FacilityTrapReportOperation.AbnormalApplied, null, null);
        if (cause is < 9 or > 12) return;
        var image = ArmyImage(army);
        var keys = ArmyTraitSourceAdapter.Encode(state(), image) ?? throw Missing("abnormal_report_Army36");
        int[] contributions = TacticRemakeCurrentTraits.ArmyContributions(current, person, keys)
            ?? throw Missing("abnormal_report_traits127");
        bool positive = contributions[cause] > 0;
        int? selected = null; int best = 0;
        foreach (ushort key in keys)
            Consider(current.ReadPersonalityTableReference(key <= 355 ? key : 0) ?? throw Missing("abnormal_cached_trait_pointer"), false, 0);
        for (int i = 0; i < 9; i++)
            Consider(TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, i)
                ?? throw Missing("abnormal_own_trait_pointer"), true, i);
        if (selected is int selectedTrait) Add(FacilityTrapReportOperation.AbnormalAttribution, selectedTrait, best > 0);

        void Consider(PersonalityReference trait, bool own, int slot)
        {
            if (!Need(current.ReadSkillPersonalityValidity(trait), "abnormal_attribution_trait_validity")) return;
            if (own && Need(current.ReadPersonalityWord(trait, 0xB4), "abnormal_attribution_B4") != ushort.MaxValue &&
                (Need(current.ReadPersonTraitMask1BC(person), "abnormal_attribution_mask") & (1 << slot)) == 0) return;
            int value = Contribution(trait, cause);
            if (value == 0 || (value > 0) != positive || NativeAbs(value) <= NativeAbs(best)) return;
            best = value; selected = Need(trait.PhysicalSlot, "abnormal_attribution_trait_identity");
        }
        void Add(FacilityTrapReportOperation operation, int? trait, bool? positiveContribution, string? immunityPhrase = null)
        {
            int root;
            HistoryMessageOperands operands;
            int category;
            if (operation == FacilityTrapReportOperation.AbnormalImmunity)
            {
                root = cause == 21 ? 0x3228 : 0x3229;
                operands = new(Army0: army, String0: TraitName(trait ?? throw Missing("abnormal_immunity_trait")), String1: immunityPhrase);
                category = 0;
            }
            else if (operation == FacilityTrapReportOperation.AbnormalApplied)
            {
                root = 0x3225;
                string sourceName = DefinitionName(definition);
                string phrase = AbnormalPhrase(index);
                operands = new(Army0: army, String0: sourceName, String1: phrase);
                category = 5;
            }
            else
            {
                root = 0x322D;
                string phrase = AbnormalPhrase(cause switch
                { 9 => index, 10 => 0, 11 => 1, 12 => 2, _ => throw Missing("abnormal_attribution_phrase") });
                string sign = package.ReadHistoryDataText(1, positiveContribution == true ? 0x4BA : 0x4B9);
                operands = new(Army0: army, String0: TraitName(trait ?? throw Missing("abnormal_attribution_trait")), String1: phrase, String2: sign);
                category = 0;
            }
            var formatted = package.FormatHistoryReport(state(), current, root, operands);
            replace(formatted.State);
            int position = Need(current.ReadArmyUnitRawHex2A(army), "abnormal_report_Army2A");
            int day = Day();
            CommitHistory(category, district, position, day, 5012, formatted.Text);
        }
    }

    private string AbnormalPhrase(int index) => package.ReadHistoryDataText(1, index switch
    { 0 => 0x25C, 1 => 0x260, 2 => 0x25E, _ => throw Missing("abnormal_report_index") });

    private string ReportObjectName(ObjectReference facility)
    {
        //Complete22EFD0: actual Object12 lookup, no extra Object-validity gate.
        var definition = FacilityConstructionLegality.DefinitionOf(current, facility);
        return Need(current.ReadObjectDefinitionValidity(definition), "report_Object_definition_validity")
            ? DefinitionName(Need(definition.PhysicalSlot, "report_Object_definition_pointer"))
            : CampaignHistoryWriter.Supplemental(state()) ?? throw Missing("report_Object_empty_text");
    }

    private byte RawReportDistrict(ArmyUnitReference army)
    {
        ushort raw = Need(current.ReadArmyUnitCommanderId(army), "report_recipient_Commander12");
        var reference = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("report_recipient_Person_lookup");
        if (reference.PhysicalSlot is not int person) return 0;
        return Need(current.ReadPersonNativeValidity(person), "report_recipient_Person_validity")
            ? Need(current.ReadPersonDistrictSlot(person), "report_recipient_E0") : (byte)0;
    }

    private void CommitHistory(int category, int district, int hex, int day, int person, string text)
    {
        var latest = state();
        replace(package.WriteHistory(latest, current, new ReportCommand(category, district, hex, day, text,
            CampaignHistoryWriter.Supplemental(latest), person, MarkBit4: 1), Requests));
    }
    private UnitState ArmyImage(ArmyUnitReference army)
    {
        int id = ArmyId(army);
        return state().Units.GetValueOrDefault(id) ?? state().ReadArmyLifecycle()?.Receivers.FindUnit(id)?.PendingImage
            ?? throw Missing("report_Army_image");
    }
    private int? FirstTrait(int person, int effect)
    {
        if (!Need(current.ReadPersonNativeValidity(person), "report_selected_Person_validity")) return null;
        for (int i = 0; i < 9; i++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, i) ?? throw Missing("report_selected_trait");
            if (!Need(current.ReadSkillPersonalityValidity(trait), "report_selected_validity")) continue;
            if (Need(current.ReadPersonalityByte(trait, 0xB6), "report_selected_B6") == effect ||
                Need(current.ReadPersonalityByte(trait, 0xBA), "report_selected_BA") == effect)
                return Need(trait.PhysicalSlot, "report_selected_identity");
        }
        return null;
    }
    private int Contribution(PersonalityReference trait, int effect)
    {
        for (int pair = 0; pair < 2; pair++)
            if (Need(current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "report_contribution_code") == effect)
                return Math.Clamp((int)unchecked((short)Need(current.ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC),
                    "report_contribution_value")), -100, 2000);
        return 0;
    }
    private static int NativeAbs(int value) => value < 0 ? unchecked(-value) : value;

    internal void FacilityVisual(FacilityTrapPresentationOperation operation, ObjectReference facility,
        int definitionId, int hex, float? range = null) => Requests.Add(new(null, null)
        { FacilityTrapPresentation = new(operation, Receiver(facility), definitionId, hex, range) });
    internal void ArmyVisual(ArmyTrapPresentationOperation operation, ArmyUnitReference army,
        ObjectReference facility, int definitionId, int hex) => Requests.Add(new(null, null)
        { ArmyTrapPresentation = new(operation, ArmyId(army), hex, Receiver(facility), definitionId) });
    internal void Damage(ArmyFacilityDamagePresentationOperation operation, ArmyUnitReference army,
        int hex, int gross, sbyte? modifier = null) => Requests.Add(new(null, null)
        { ArmyFacilityDamagePresentation = new(operation, ArmyId(army), hex, gross, modifier) });

    internal GameState PublishCurrentState(GameState value)
    {
        value = Traits.Publish(value);
        if (personnelBound) value = value with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw Missing("feedback_personnel_publication") };
        return value;
    }

    internal GameState Publish(GameState value)
    {
        value = PublishCurrentState(value);
        return Requests.Count == 0 ? value : value with { CampaignEffectRequests =
            (value.CampaignEffectRequests ?? throw Missing("feedback_pending_owner"))
                .Append(CampaignEffectSource.FacilityTraps, moment.CalendarCycleOrdinal, Requests) };
    }
}
