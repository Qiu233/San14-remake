using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private LoadedArmyFlagPaneNames? rankPromotionNames;

    //2B5ED0 after the actual rank, loyalty, security and experience writers.
    //Action58 and the coalition king caller are NPC paths. Their permanent
    //chronology entries are independent of the report notification queue.
    private GameState ApplyNormalTurnRankPromotionReportsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force,
        int rank, int emperor, bool selfProclaimed)
    {
        int speaker = KingPromotionLookup(current, 5012); //Actual World9DF8 lookup.
        if (!KingPromotionNeed(current.ReadPersonNativeValidity(speaker), "history_speaker_validity")) return state;
        int speakerId = Identity(speaker);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw KingPromotionMissing("history_player_Force");
        int rulerId = TalentRulerCurrent(current, force);
        int forceId = KingPromotionNeed(current.ReadForcePhysicalSlot(force), "history_Force60");
        int playerRuler = TalentRulerCurrent(current, player);
        var effects = new List<CampaignEffectRequest>();

        //The granted imperial chronology is inserted before either formatter;
        //the self-proclamation entry follows both. The low-tendency granted
        //branch still formats2C7F for its final history, not its raw2C80 label.
        if (rank == 1 && !selfProclaimed)
        {
            short tendency = KingPromotionNeed(RulerCaptureReleaseInputs.ReadCurrentWord(state,
                KingPromotionLookup(current, rulerId), 0x17A), "history_ruler17A");
            //TC1A4A994: immutable file-backed.data i32=11 (offset1A49194).
            int threshold = StrategicParameter(state, "rank-promotion.imperial-tendency-threshold", 11);
            Chronicle(tendency > threshold ? 4 : 0x123, tendency > threshold ? 2u : 0x22u);
        }

        int mainRoot = rank == 1 ? selfProclaimed ? 0x2C81 : 0x2C7F : selfProclaimed ? 0x2C69 : 0x2C68;
        var mainOperands = !selfProclaimed && rank != 1
            ? new HistoryMessageOperands(Person0: Identity(emperor), Force0: forceId, Force1: forceId)
            : new HistoryMessageOperands(Force0: forceId, Force1: forceId);
        var main = FormatNormalTurnReport(state, current, mainRoot, mainOperands); state = main.State;
        int detailRoot = rank == 1 ? selfProclaimed ? 0x539C : 0x538C : selfProclaimed ? 0x537C : 0x5376;
        var detailOperands = selfProclaimed && rank != 1
            ? new HistoryMessageOperands(Person0: speakerId, Person1: playerRuler,
                Person2: rulerId, Force0: forceId)
            : new HistoryMessageOperands(Person0: speakerId, Person1: playerRuler,
                Person2: Identity(emperor), Person3: rulerId, Force0: forceId);
        var detail = FormatNormalTurnReport(state, current, detailRoot, detailOperands); state = detail.State;
        if (rank == 1 && selfProclaimed) Chronicle(5, 3);
        if (rank != 1)
        {
            //The native left operand reads+C before its second player test.
            //NPCs then skip the player-only rank chronology entry.
            _ = Suppression();
        }
        int day = Day();
        int district = KingPromotionNeed(current.ReadDistrictPhysicalSlot(
            ForcePrimaryDistrictSource.Resolve(current, force)), "history_primary_District60");
        state = WriteHistory(state, current, new(1, district, 0, day, main.Text, detail.Text,
            speakerId, MarkBit0: 1), effects);
        if (effects.Count == 0) return state;
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests
            ?? throw KingPromotionMissing("history_effect_owner")).Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeClock?.UpdateOrdinal ?? throw KingPromotionMissing("history_effect_revision"), effects) };

        int Identity(int physical) => KingPromotionNeed(current.ReadPersonIdentity(physical), "history_Person10");
        int Day() => (ReadArmyDestructionSampler(state, current)
            ?? throw KingPromotionMissing("history_World450")).Day450;
        int Suppression() => state.CampaignChronicle?.RecordingSuppression
            ?? throw KingPromotionMissing("chronicle_recording_suppression");
        void Chronicle(int eventCode, uint entryKind)
        {
            //1EF470 is CHistoryTableManager, not the story playback service.
            //Its+C is the actual saved chronology suppression control.
            if (Suppression() != 0) return;
            int day = Day();
            int nameIndex = KingPromotionNeed(current.ReadForceKingdomNameIndex(forceId), "chronicle_Force48");
            rankPromotionNames ??= ArmyFlagPaneNames.Load(_scenario);
            var country = rankPromotionNames.CustomNames.Definitions[nameIndex <= 100 ? nameIndex : 0];
            string rulerName = Name(state.Officers.GetValueOrDefault(rulerId)
                ?? throw KingPromotionMissing("chronicle_ruler_owner"), true);
            string forceName = HistoryWriter.CustomFirstName(state, current, force, Name);
            state = new CampaignChronicleWriter(HistoryWriter).Write(state, current,
                new(eventCode, 1, day, forceName,
                    KingPromotionNeed(current.ReadForceReportColor(force), "chronicle_Force49"), entryKind,
                    ImmutableArray.Create(rulerName, string.Empty, country.ShortName),
                    ImmutableArray.Create(48400, 48400, 48400, 48400)),
                ReadApplicationGameplay()?.UserSelectionServicePresent);
        }
        static string Name(OfficerState person, bool full)
        {
            string surname = Head(person.CurrentSurname ?? throw KingPromotionMissing("chronicle_surname"));
            return full ? surname + Head(person.CurrentGivenName ?? throw KingPromotionMissing("chronicle_given_name")) : surname;
        }
        static string Head(string value) => value.IndexOf('\0') is int end && end >= 0 ? value[..end] : value;
    }
}
