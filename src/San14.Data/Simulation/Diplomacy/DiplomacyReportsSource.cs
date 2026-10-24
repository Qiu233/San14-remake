using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal HistoryTextResult FormatDiplomacyReportCurrent(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
        => FormatReportMessage(state, current, root, operands, ReportMessageOrigin.Diplomacy, null);

    //815D20/816470/816990/817310/817840/818240 set only Force0
    //before3507..3509. This is the real information dialog, not speech by
    //a guessed ruler or envoy. The host consumes the existing outbox once.
    private GameState WriteDiplomacyNegotiationResponseCurrent(GameState state,
        SourceWorldContext current, int root, int respondingForce, List<CampaignEffectRequest> effects)
    {
        var text = FormatDiplomacyReportCurrent(state, current, root, new(Force0: respondingForce));
        DiplomacyPresent(effects, TalentCommandPresentationOperation.Announcement, text.Text);
        return text.State;
    }

    //1DA870's introduction precedes both the player's negotiation and an
    //NPC result trial. A resumed negotiation must not replay this call.
    internal GameState WriteDiplomacyArrivalCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, List<CampaignEffectRequest> effects)
    {
        var actor = DiplomacyReportActor(current, task);
        var own = current.ReadPersonForce(actor) ?? throw DiplomacyReportMissing("arrival_own_Force");
        var target = DiplomacyForce(current, task.TargetForceId);
        int ownRuler = DiplomacyRulerId(current, own), targetRuler = DiplomacyRulerId(current, target);
        if (TalentForceIsPlayerCurrent(state, current, own))
        {
            var text = FormatDiplomacyReportCurrent(state, current, 0xC418,
                new(Person0: DiplomacyNeed(current.ReadPersonIdentity(actor), "arrival_executor10"),
                    Person1: ownRuler, Person2: targetRuler,
                    Force0: DiplomacyNeed(own.PhysicalSlot, "arrival_own_Force60"),
                    Force1: DiplomacyNeed(target.PhysicalSlot, "arrival_target_Force60")));
            DiplomacyPresent(effects, TalentCommandPresentationOperation.OfficerText, text.Text,
                officer: DiplomacyNeed(current.ReadPersonIdentity(actor), "arrival_executor10"));
            return text.State;
        }
        if (!TalentForceIsPlayerCurrent(state, current, target)) return state;
        //405A40 consumes the shared App32 sample before C42E's own message
        //choices. The audio theme and actual sample admission belong to App.
        var saved = state.CampaignAppRandom32 ?? throw DiplomacyReportMissing("arrival_audio_App32");
        var random = new SharedSourceRandom32(saved.State);
        int variant = random.Next(3);
        state = state with { CampaignAppRandom32 = new(random.State) };
        effects.Add(new(null, null) { DiplomacyArrivalAudio = new(variant) });
        var notice = FormatDiplomacyReportCurrent(state, current, 0xC42E,
            new(Person0: 5012, Person1: targetRuler, Person2: ownRuler,
                Force0: DiplomacyNeed(target.PhysicalSlot, "arrival_target_Force60")));
        DiplomacyPresent(effects, TalentCommandPresentationOperation.OfficerText, notice.Text, officer: 5012);
        return notice.State;
    }

    internal GameState WriteDiplomacyDistrictObjectiveNoticeCurrent(GameState state,
        SourceWorldContext current, List<CampaignEffectRequest> effects)
    {
        var text = FormatDiplomacyReportCurrent(state, current, 0x2D03, new());
        DiplomacyPresent(effects, TalentCommandPresentationOperation.Announcement, text.Text);
        return text.State;
    }

    //821BE0: the caller has already observed a real loyalty reduction and
    //the own-Force player gate.3174 has no operand setter or random choice.
    internal GameState WriteDiplomacyCancelLoyaltyNoticeCurrent(GameState state,
        SourceWorldContext current, List<CampaignEffectRequest> effects)
    {
        var text = FormatDiplomacyReportCurrent(state, current, 0x3174, new());
        DiplomacyPresent(effects, TalentCommandPresentationOperation.Announcement, text.Text);
        return text.State;
    }

    //2E98E0(param3=2): only the report tail. The caller retains15F/160
    //through this call and owns the later relationship mutation and clear.
    internal GameState WriteDiplomacyAttackRequestEndCurrent(GameState state, SourceWorldContext current,
        int receiverForceId, int requesterForceId, int targetLocationId, List<CampaignEffectRequest> effects)
    {
        var receiver = DiplomacyForce(current, receiverForceId);
        var requester = DiplomacyForce(current, requesterForceId);
        bool receiverValid = DiplomacyNeed(current.ReadForceNativeValidity(receiver), "request_end_receiver_validity");
        bool receiverPlayer = receiverValid && TalentForceIsPlayerCurrent(state, current, receiver);
        bool requesterPlayer = false;
        string? intro = null;
        //The signed byte is promoted into the real Base operand setter;
        //32C7D0 later consumes its low16 bits, not a guessed City index.
        ushort location = unchecked((ushort)(sbyte)targetLocationId);
        if (receiverPlayer)
        {
            var text = FormatDiplomacyReportCurrent(state, current, 0x320A,
                new(Person0: 5012, Person1: DiplomacyNeed(current.ReadMoraleWaveRuler22EAC0(receiver), "request_end_receiver_ruler"),
                    Force0: requesterForceId, Base0: location));
            state = text.State; intro = text.Text;
        }
        else if (receiverValid && DiplomacyNeed(current.ReadForceNativeValidity(requester), "request_end_requester_validity") &&
            TalentForceIsPlayerCurrent(state, current, requester))
        {
            requesterPlayer = true;
            var text = FormatDiplomacyReportCurrent(state, current, 0x320E,
                new(Person0: 5012, Person1: DiplomacyNeed(current.ReadMoraleWaveRuler22EAC0(requester), "request_end_requester_ruler"),
                    Force0: DiplomacyNeed(receiver.PhysicalSlot, "request_end_receiver_ordinal"), Base0: location));
            state = text.State; intro = text.Text;
        }
        //The main formatter runs even when neither participant has an intro.
        var main = FormatDiplomacyReportCurrent(state, current, 0x3212,
            new(Person0: DiplomacyNeed(current.ReadMoraleWaveRuler22EAC0(receiver), "request_end_receiver_ruler"),
                Force0: requesterForceId));
        state = main.State;
        var target = BaseLookup.Find(location, current);
        if (DiplomacyNeed(current.ReadBaseNativeValidity(target), "request_end_target_validity"))
        {
            ForceReference? historyForce = receiverPlayer ? receiver : null;
            if (historyForce is null &&
                DiplomacyNeed(current.ReadForceNativeValidity(requester), "request_end_history_requester_validity") &&
                (requesterPlayer || TalentForceIsPlayerCurrent(state, current, requester))) historyForce = requester;
            if (historyForce is not null)
            {
                int hex = ReadGrowthReportHex(current, target);
                int day = (ReadArmyDestructionSampler(state, current) ?? throw DiplomacyReportMissing("request_end_day450")).Day450;
                var primary = ForcePrimaryDistrictSource.Resolve(current, historyForce);
                state = WriteHistory(state, current, new(3,
                    DiplomacyNeed(primary.PhysicalSlot, "request_end_primary_District"), hex, day,
                    main.Text, string.IsNullOrEmpty(intro) ? CampaignHistoryWriter.Supplemental(state) : intro,
                    5012, MarkBit0: 1, MarkBit4: 1), effects);
            }
        }
        //A bad target skips only history; an existing intro still appears.
        if (!string.IsNullOrEmpty(intro))
            DiplomacyPresent(effects, TalentCommandPresentationOperation.OfficerText, intro, officer: 5012);
        return state;
    }

    //1E48C0 cancellation or81ECA0 settlement. These do not settle an offer,
    //draw a diplomatic result, retire a task, or schedule its return journey.
    internal GameState WriteDiplomacyHistoryCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, bool? accepted, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects, bool? hadAllianceBeforeResolution = null)
    {
        if (accepted is null) return WriteDiplomacyCancellationCurrent(state, current, task, effects);
        int actor = DiplomacyReportActor(current, task);
        var own = current.ReadPersonForce(actor) ?? throw DiplomacyReportMissing("history_own_Force");
        var target = DiplomacyForce(current, task.TargetForceId);
        int ownRuler = DiplomacyRulerId(current, own), targetRuler = DiplomacyRulerId(current, target);
        byte rawDistrict = DiplomacyNeed(current.ReadPersonDistrictSlot(actor), "history_executor_E0");
        var actorDistrict = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
            ?? throw DiplomacyReportMissing("history_executor_District");
        var targetPrimary = ForcePrimaryDistrictSource.Resolve(current, target);
        var pool = current.ReadOwnedCurrentListPool() ?? throw DiplomacyReportMissing("history_current_pool");
        var selection = new PersonDeathHistorySelection(state, current, pool,
            new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language)), _governorInputs,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect));
        //The original resolves the target's advice speaker before branching,
        //including when the eventual player-source report uses the executor.
        var targetSpeaker = selection.Select(targetPrimary);
        bool ownPlayer = TalentForceIsPlayerCurrent(state, current, own);
        bool targetPlayer = TalentForceIsPlayerCurrent(state, current, target);
        int actorId = DiplomacyNeed(current.ReadPersonIdentity(actor), "history_executor10");
        var pair = new HistoryMessageOperands(Person0: ownRuler, Person1: targetRuler);
        var detailed = new HistoryMessageOperands(Person0: actorId, Person1: ownRuler, Person2: targetRuler);
        var district = actorDistrict;
        int payload = actorId, mark3 = 0, category = 3;
        int announceRoot, mainRoot, detailRoot;
        bool success = accepted.Value;
        bool renewal = task.NativeKind == 5 ? hadAllianceBeforeResolution
            ?? throw DiplomacyReportMissing("history_prior_alliance_months") : false;
        int sound;
        switch (task.NativeKind)
        {
            case 4:
                if (!ownPlayer && !targetPlayer) return state;
                announceRoot = success ? 0x31E6 : 0x31F1;
                mainRoot = success ? 0x31E7 : 0x31F2;
                detailRoot = success ? 0x31E8 : 0x31F3;
                sound = success ? 4 : 6;
                break;
            case 6:
                //The native break-alliance branch has no accepted split.
                announceRoot = 0x3164; mainRoot = 0x3165; detailRoot = 0x3166;
                sound = ownPlayer ? 4 : targetPlayer ? 6 : 8;
                break;
            case 9:
                if (!ownPlayer && !targetPlayer) return state;
                ushort requested = DiplomacyNeed(current.ReadBaseObjectLink(BaseLookup.Find(task.TargetCityId
                    ?? throw DiplomacyReportMissing("history_requested_Base"), current)), "history_requested4E");
                pair = pair with { Base0 = requested }; detailed = detailed with { Base0 = requested };
                announceRoot = success ? 0x31B8 : 0x31C3;
                mainRoot = success ? 0x31B9 : 0x31C4;
                detailRoot = success ? 0x31BA : 0x31C5;
                sound = success ? 4 : 6;
                break;
            case 5:
                if (!success && !ownPlayer && !targetPlayer) return state;
                announceRoot = success ? renewal ? 0x318D : 0x318C : renewal ? 0x31A3 : 0x31A2;
                mainRoot = success ? renewal ? 0x318F : 0x318E : renewal ? 0x31A5 : 0x31A4;
                detailRoot = success ? renewal ? 0x3199 : 0x3190 : renewal ? 0x31AF : 0x31A6;
                sound = success ? ownPlayer || targetPlayer ? 4 : 8 : 6;
                break;
            default: throw DiplomacyReportMissing("history_native_kind");
        }
        var announcement = FormatDiplomacyReportCurrent(state, current, announceRoot, pair);
        state = announcement.State;
        DiplomacyPresent(effects, TalentCommandPresentationOperation.SoundCue, null, sound: sound);
        DiplomacyPresent(effects, TalentCommandPresentationOperation.Announcement, announcement.Text);
        var mainOperands = pair;
        if (task.NativeKind == 6 || task.NativeKind == 5 && !success)
            mainOperands = new(Person0: actorId, Person1: targetRuler);
        var main = FormatDiplomacyReportCurrent(state, current, mainRoot, mainOperands);
        state = main.State;
        string? supplemental;
        if (ownPlayer)
        {
            var detail = FormatDiplomacyReportCurrent(state, current, detailRoot, detailed);
            state = detail.State; supplemental = detail.Text;
        }
        else if (task.NativeKind == 6)
        {
            if (targetPlayer)
            {
                int speaker = DiplomacyNeed(current.ReadPersonIdentity(DiplomacyNeed(targetSpeaker.PhysicalSlot,
                    "history_target_speaker_pointer")), "history_target_speaker10");
                var detail = FormatDiplomacyReportCurrent(state, current, 0x316F,
                    new(Person0: speaker, Person1: targetRuler, Person2: ownRuler));
                state = detail.State; supplemental = detail.Text; payload = speaker; district = targetPrimary;
            }
            else
            {
                var playerPrimary = DiplomacyPlayerPrimary(state, current);
                if (!DiplomacyNeed(current.ReadDistrictNativeValidity(playerPrimary), "history_observer_District_validity")) return state;
                _ = selection.Select(playerPrimary);
                var detail = FormatDiplomacyReportCurrent(state, current, 0x3164, pair);
                state = detail.State; supplemental = detail.Text; payload = 5012;
            }
        }
        else if (task.NativeKind == 5 && success)
        {
            var playerPrimary = DiplomacyPlayerPrimary(state, current);
            if (!DiplomacyNeed(current.ReadDistrictNativeValidity(playerPrimary), "history_observer_District_validity")) return state;
            int speaker = DiplomacyNeed(current.ReadPersonIdentity(DiplomacyNeed(selection.Select(playerPrimary).PhysicalSlot,
                "history_observer_speaker_pointer")), "history_observer_speaker10");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DiplomacyReportMissing("history_player_Force");
            var detail = FormatDiplomacyReportCurrent(state, current,
                targetPlayer ? renewal ? 0x50E4 : 0x50DD : renewal ? 0x50D6 : 0x50CF,
                new(Person0: speaker, Person1: DiplomacyRulerId(current, player),
                    Force0: DiplomacyNeed(own.PhysicalSlot, "history_own_Force60"),
                    Force1: targetPlayer ? null : DiplomacyNeed(target.PhysicalSlot, "history_target_Force60")));
            state = detail.State; supplemental = detail.Text; payload = speaker;
            if (targetPlayer) district = targetPrimary;
            else { category = 1; mark3 = 1; }
        }
        else
        {
            supplemental = CampaignHistoryWriter.Supplemental(state);
            payload = DiplomacyNeed(current.ReadPersonIdentity(DiplomacyNeed(targetSpeaker.PhysicalSlot,
                "history_target_speaker_pointer")), "history_target_speaker10");
            district = targetPrimary;
        }
        int targetPerson = DiplomacyLookupPerson(current, targetRuler);
        var home = BaseLookup.Find(DiplomacyNeed(current.ReadPersonFirstLocation(targetPerson), "history_target_ruler_E2"), current);
        int hex = ReadGrowthReportHex(current, home);
        int day = (ReadArmyDestructionSampler(state, current) ?? throw DiplomacyReportMissing("history_day450")).Day450;
        return WriteHistory(state, current, new(category, DiplomacyNeed(district.PhysicalSlot, "history_District60"), hex,
            day, main.Text, supplemental, payload, MarkBit0: 1, MarkBit3: mark3, MarkBit4: 1), effects);
    }

    private GameState WriteDiplomacyCancellationCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, List<CampaignEffectRequest> effects)
    {
        var primary = DiplomacyPlayerPrimary(state, current);
        if (!DiplomacyNeed(current.ReadDistrictNativeValidity(primary), "cancel_player_District_validity") ||
            DiplomacyNeed(primary.PhysicalSlot, "cancel_player_District") != task.SourceDistrictId) return state;
        if ((DiplomacyNeed(current.ReadWorldTransferServiceFlags(), "cancel_World16A8") & 0x100) != 0) return state;
        int actor = DiplomacyReportActor(current, task);
        var own = current.ReadPersonForce(actor) ?? throw DiplomacyReportMissing("cancel_own_Force");
        var target = DiplomacyForce(current, task.TargetForceId);
        int actorId = DiplomacyNeed(current.ReadPersonIdentity(actor), "cancel_executor10");
        int ownRuler = DiplomacyRulerId(current, own), targetRuler = DiplomacyRulerId(current, target);
        int detailedRoot = task.NativeKind switch { 4 => 0x305E, 5 => 0x3067, 6 => 0x3070, 9 => 0x308B,
            _ => throw DiplomacyReportMissing("cancel_kind") };
        int shortRoot = task.NativeKind switch { 4 => 0x309D, 5 => 0x309E, 6 => 0x309F, 9 => 0x30A2,
            _ => throw DiplomacyReportMissing("cancel_kind") };
        var detailed = FormatDiplomacyReportCurrent(state, current, detailedRoot,
            new(Person0: actorId, Person1: ownRuler, Person2: targetRuler,
                Force0: DiplomacyNeed(target.PhysicalSlot, "cancel_target_Force60")));
        var main = FormatDiplomacyReportCurrent(detailed.State, current, shortRoot,
            new(Person0: targetRuler, Force0: DiplomacyNeed(target.PhysicalSlot, "cancel_target_Force60")));
        int day = (ReadArmyDestructionSampler(main.State, current) ?? throw DiplomacyReportMissing("cancel_day450")).Day450;
        state = WriteHistory(main.State, current, new(3,
            DiplomacyNeed(current.ReadPersonDistrictSlot(actor), "cancel_executor_E0"), 0, day,
            main.Text, detailed.Text, actorId, MarkBit0: 1), effects);
        DiplomacyPresent(effects, TalentCommandPresentationOperation.OfficerText, detailed.Text, officer: actorId);
        return state;
    }

    private static DistrictReference DiplomacyPlayerPrimary(GameState state, SourceWorldContext current)
        => ForcePrimaryDistrictSource.Resolve(current, OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw DiplomacyReportMissing("player_Force"));
    private static int DiplomacyReportActor(SourceWorldContext current, PersonnelDiplomacyTask task)
        => task.MemberIds.Length != 0 ? DiplomacyLookupPerson(current, task.MemberIds[0]) : throw DiplomacyReportMissing("executor_member");
    private static int DiplomacyRulerId(SourceWorldContext current, ForceReference force)
    {
        ushort raw = DiplomacyNeed(current.ReadForceRecord(DiplomacyNeed(force.PhysicalSlot, "ruler_Force"))?.RawRuler10, "ruler10");
        return DiplomacyNeed(current.ReadPersonIdentity(DiplomacyLookupPerson(current, raw)), "ruler_Person10");
    }
    private static void DiplomacyPresent(List<CampaignEffectRequest> effects,
        TalentCommandPresentationOperation operation, string? text, int? officer = null, int? sound = null)
        => effects.Add(new(null, null) { TalentCommandPresentation = new(operation, sound, officer, null, null, text) });
}
