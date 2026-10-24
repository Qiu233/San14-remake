using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal HistoryTextResult FormatTalentReportCurrent(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
        => FormatReportMessage(state, current, root, operands, ReportMessageOrigin.Talent, null);

    private static T TalentNeed<T>(T? value, string field) where T : struct
        => value ?? throw new InvalidDataException("talent.current_" + field);

    internal bool TalentForceIsPlayerCurrent(GameState state, SourceWorldContext current, ForceReference force)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw new InvalidDataException("talent.current_player_force");
        int ordinal = TalentNeed(current.ReadForcePhysicalSlot(player), "player_ordinal");
        return ordinal != 0 && TalentNeed(current.ReadForcePhysicalSlot(force), "force_ordinal") == ordinal;
    }

    private int TalentRulerCurrent(SourceWorldContext current, ForceReference force)
    {
        ushort raw = TalentNeed(current.ReadForceRecord(TalentNeed(current.ReadForcePhysicalSlot(force), "ruler_force"))?.RawRuler10, "ruler10");
        var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw new InvalidDataException("talent.current_ruler_lookup");
        return TalentNeed(current.ReadPersonIdentity(TalentNeed(person.PhysicalSlot, "ruler_pointer")), "ruler_ID10");
    }

    private ushort TalentAreaBaseCurrent(SourceWorldContext current, AreaReference area)
    {
        byte raw = TalentNeed(current.ReadAreaCitySlot(area), "Area35");
        var city = current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw new InvalidDataException("talent.current_Area_parent_City");
        return TalentNeed(current.ReadBaseObjectLink(city), "parent_City4E");
    }

    internal GameState ApplyTalentSearchDiscoveryNoticeCurrent(GameState state, SourceWorldContext current,
        int executor, int found, AreaReference area, List<CampaignEffectRequest> effects)
    {
        var force = current.ReadPersonForce(executor) ?? throw new InvalidDataException("talent.current_discovery_Force");
        if (!TalentForceIsPlayerCurrent(state, current, force)) return state;
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) });
        var text = FormatTalentReportCurrent(state, current, 0xC365, new(
            Person0: TalentNeed(current.ReadPersonIdentity(found), "found_ID"),
            Person1: TalentNeed(current.ReadPersonIdentity(executor), "executor_ID"),
            Area0: TalentNeed(area.PhysicalSlot, "Area_pointer"), Base0: TalentAreaBaseCurrent(current, area)));
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
        return text.State;
    }

    internal GameState ApplyTalentSearchPersonReportCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, int executor, int found, bool joined,
        List<CampaignEffectRequest> effects)
    {
        var force = current.ReadPersonForce(executor) ?? throw new InvalidDataException("talent.current_person_report_Force");
        if (!TalentNeed(current.ReadForceNativeValidity(force), "person_report_Force_validity")) return state;
        if (!TalentForceIsPlayerCurrent(state, current, force)) return state;
        var area = TalentSearchAreaCurrent(current, task);
        return AppendTalentSearchHistoryCurrent(state, current, task, date, executor,
            joined ? 0xC391 : 0xC392, joined ? 0x51A9 : 0x51B3,
            new(Person0: TalentNeed(current.ReadPersonIdentity(executor), "executor_ID"),
                Person1: TalentRulerCurrent(current, force), Person2: TalentNeed(current.ReadPersonIdentity(found), "found_ID"),
                Area0: TalentNeed(area.PhysicalSlot, "Area_pointer"), Base0: TalentAreaBaseCurrent(current, area)),
            effects, 1, joined ? 1 : 0,
            mainOperands: new(Person0: TalentNeed(current.ReadPersonIdentity(executor), "executor_ID"),
                Person1: TalentNeed(current.ReadPersonIdentity(found), "found_ID")));
    }

    internal static AreaReference TalentSearchAreaCurrent(SourceWorldContext current, PersonnelTalentTask task)
    {
        var hex = current.ReadHexTableReference(task.TargetHexId ?? throw new InvalidDataException("talent.current_target_Hex"))
            ?? throw new InvalidDataException("talent.current_target_Hex_lookup");
        ushort raw = TalentNeed(current.ReadHexAreaSlot(hex), "target_Hex_Area");
        return current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw new InvalidDataException("talent.current_Area_lookup");
    }

    internal GameState AppendTalentSearchHistoryCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, int executor, int mainRoot, int? supplementalRoot,
        HistoryMessageOperands operands, List<CampaignEffectRequest> effects, int mark0, int mark4,
        int? hexOverride = null, HistoryMessageOperands? mainOperands = null)
    {
        var main = FormatTalentReportCurrent(state, current, mainRoot, mainOperands ?? operands);
        var working = main.State;
        string? supplemental;
        if (supplementalRoot is int root)
        {
            var attachment = FormatTalentReportCurrent(working, current, root, operands);
            working = attachment.State; supplemental = attachment.Text;
        }
        else supplemental = CampaignHistoryWriter.Supplemental(working);
        int officer = TalentNeed(current.ReadPersonIdentity(executor), "history_Person10");
        int day = (ReadArmyDestructionSampler(working, current) ?? throw new InvalidDataException("talent.current_history_day450")).Day450;
        int hex = hexOverride ?? TalentNeed(TalentTravelSource.ResolvePosition(working, current, task, date).PhysicalSlot, "history_position");
        byte district = TalentNeed(current.ReadPersonDistrictSlot(executor), "history_DistrictE0");
        return WriteHistory(working, current, new(2, district, hex, day, main.Text, supplemental,
            officer, MarkBit0: mark0, MarkBit4: mark4), effects);
    }

    //271C20 param7=1: retained old Force/player gate has already succeeded.
    //The speaker selection precedes the fresh old-Force validity branch;
    //formatting retains source App32 mutations and then emits one history.
    private GameState ApplyRecruitOldForceHistoryCurrent(GameState state, SourceWorldContext current,
        int person, ForceReference oldForce, DistrictReference destination,
        Func<int> readDay450, List<CampaignEffectRequest> effects)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw TalentCommandMissing("join_history_pool");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var selection = new PersonDeathHistorySelection(state, current, pool, abilities, _governorInputs,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect));
        var primary = ReadGrowthPlayerPrimaryDistrict(state, current);
        var speaker = selection.Select(primary);
        int speakerPhysical = TalentNeed(speaker.PhysicalSlot, "join_history_speaker");
        int speakerId = TalentNeed(current.ReadPersonIdentity(speakerPhysical), "join_history_speaker10");
        bool valid = TalentNeed(current.ReadForceNativeValidity(oldForce), "join_history_old_Force_validity");
        int target = TalentNeed(current.ReadPersonIdentity(person), "join_history_Person10");
        int oldOrdinal = TalentNeed(current.ReadForcePhysicalSlot(oldForce), "join_history_old_Force60");
        int NewForceOrdinal()
        {
            byte raw = TalentNeed(current.ReadDistrictForceId(TalentNeed(destination.PhysicalSlot, "join_history_District")), "join_history_District10");
            var force = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw TalentCommandMissing("join_history_new_Force");
            return TalentNeed(current.ReadForcePhysicalSlot(force), "join_history_new_Force60");
        }
        var main = FormatTalentReportCurrent(state, current, valid ? 0x298F : 0x2990,
            valid ? new(Person0: target, Force0: oldOrdinal, Force1: NewForceOrdinal())
                : new(Person0: target, Force0: NewForceOrdinal()));
        var working = main.State;
        int payload = valid && target == TalentNeed(current.ReadPersonIdentity(speakerPhysical), "join_history_same_speaker10")
            ? 5012 : speakerId;
        //The invalid-Force branch binds the actual selected Person again.
        int argument0 = valid ? payload : TalentNeed(current.ReadPersonIdentity(speakerPhysical), "join_history_invalid_speaker10");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw TalentCommandMissing("join_history_player");
        int ruler = TalentRulerCurrent(current, player);
        var supplemental = FormatTalentReportCurrent(working, current, valid ? 0x535B : 0x5360,
            valid ? new(Person0: argument0, Person1: ruler, Person2: TalentNeed(current.ReadPersonIdentity(person), "join_history_late_Person10"),
                Force0: oldOrdinal, Force1: NewForceOrdinal())
                : new(Person0: argument0, Person1: ruler, Person2: TalentNeed(current.ReadPersonIdentity(person), "join_history_late_Person10"),
                    Force0: NewForceOrdinal()));
        working = supplemental.State;
        int day = readDay450();
        byte district = TalentNeed(current.ReadPersonDistrictSlot(speakerPhysical), "join_history_speaker_E0");
        return WriteHistory(working, current, new(2, district, 0, day, main.Text, supplemental.Text,
            payload, MarkBit0: 1, MarkBit4: 0), effects);
    }
}
