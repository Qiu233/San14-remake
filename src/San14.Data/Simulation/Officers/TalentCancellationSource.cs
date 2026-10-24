using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private enum TalentCancellation { None, Quiet, SourceChanged, SearchAreaChanged, RecruitTargetChanged, RecruitWaitExpired }

    private TalentCancellation ReadTalentCancellation(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, bool rejectRecruitWaitUnavailable = false)
    {
        if (task.MemberIds.Length == 0 || task.Kind == TalentOrderKind.Search && task.TargetHexId is null ||
            task.Kind == TalentOrderKind.Recruit && task.TargetOfficerId is null) return TalentCancellation.Quiet;
        int actor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw TalentCommandMissing("executor_source");
        var force = current.ReadPersonForce(actor) ?? throw TalentCommandMissing("executor_Force");
        byte districtRaw = TalentKnown(current.ReadPersonDistrictSlot(actor), "executor_DistrictE0");
        var district = current.ReadDistrictTableReference(districtRaw <= 51 ? districtRaw : 0)
            ?? throw TalentCommandMissing("executor_District_pointer");
        bool sourceGood = TalentKnown(current.ReadPersonNativeValidity(actor), "executor_validity") &&
            TalentKnown(current.ReadForceNativeValidity(force), "executor_Force_validity") &&
            TalentKnown(current.ReadForcePhysicalSlot(force), "executor_Force60") == task.ForceId &&
            TalentKnown(current.ReadDistrictNativeValidity(district), "executor_District_validity");
        if (!sourceGood) return task.Kind == TalentOrderKind.Search ? TalentCancellation.Quiet : TalentCancellation.SourceChanged;
        if (TalentKnown(current.ReadDistrictPhysicalSlot(district), "executor_District_identity") != task.SourceDistrictId)
            return task.Kind == TalentOrderKind.Search ? TalentCancellation.Quiet : TalentCancellation.SourceChanged;
        if (task.Kind == TalentOrderKind.Search)
        {
            var hex = current.ReadHexTableReference(task.TargetHexId!.Value) ?? throw TalentCommandMissing("search_target_Hex");
            int raw = TalentKnown(current.ReadHexAreaSlot(hex), "search_target_Area");
            var area = current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw TalentCommandMissing("search_Area_pointer");
            if (!TalentKnown(current.ReadAreaNativeValidity(area), "search_Area_validity") ||
                TalentKnown(current.ReadAreaCurrentForceSlot(area), "search_Area_Force") != task.ForceId)
                return TalentCancellation.SearchAreaChanged;
            return TalentKnown(current.ReadPersonFirstLocation(actor), "executor_Home") == task.SourceCityId && !task.Cancelled
                ? TalentCancellation.None : TalentCancellation.Quiet;
        }
        if (TalentKnown(current.ReadPersonFirstLocation(actor), "executor_Home") != task.SourceCityId)
            return TalentCancellation.SourceChanged;
        int target = current.ResolveOfficerSource(task.TargetOfficerId!.Value) ?? throw TalentCommandMissing("recruit_target_source");
        if (!TalentKnown(current.ReadPersonNativeValidity(target), "recruit_target_validity") ||
            TalentKnown(current.ReadPersonStatus(target), "recruit_target_status") == 9)
            return TalentCancellation.RecruitTargetChanged;
        var other = current.ReadPersonForce(target) ?? throw TalentCommandMissing("recruit_target_Force");
        byte status = TalentKnown(current.ReadPersonStatus(target), "recruit_target_status");
        if (TalentKnown(current.ReadForceNativeValidity(other), "recruit_target_Force_validity"))
        {
            int ordinal = TalentKnown(current.ReadForcePhysicalSlot(other), "recruit_target_Force60");
            if (ordinal == task.ForceId) return TalentCancellation.RecruitTargetChanged;
            bool busy = PersonCurrentBusySource.Read(current, target, TalentCommandMissing);
            if (task.IsRecruitWait && rejectRecruitWaitUnavailable &&
                (busy || TalentKnown(current.ReadPersonFirstLocation(target), "wait_guard_target_E2") !=
                    TalentKnown(current.ReadBaseObjectLink(BaseLookup.Find(task.TargetOriginalHomeLocationId
                        ?? throw TalentCommandMissing("wait_guard_Base78"), current)), "wait_guard_Base4E")))
                return TalentCancellation.RecruitWaitExpired;
            if (busy && task.RecruitMode == 0 ||
                ordinal != task.TargetOriginalForceId || ordinal is >= 46 and <= 51 || status == 1 ||
                TalentKnown(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, other, force), "recruit_target_relationship") && status != 6 ||
                status is not (>= 1 and <= 4) && status != 6)
                return TalentCancellation.RecruitTargetChanged;
            if (status == 6 && !CaptiveHomeMatches()) return TalentCancellation.RecruitTargetChanged;
        }
        else if (status == 5)
        {
            var at = PersonReturnCommands.ResolvePersonPosition(state, current, target, date);
            if (TalentKnown(current.ReadHexForceSlot(at), "free_target_Hex_Force") != task.ForceId)
                return TalentCancellation.RecruitTargetChanged;
        }
        else if (status == 6 && !CaptiveHomeMatches()) return TalentCancellation.RecruitTargetChanged;
        return task.Cancelled ? TalentCancellation.Quiet : TalentCancellation.None;

        bool CaptiveHomeMatches()
        {
            var home = BaseLookup.Find(TalentKnown(current.ReadPersonFirstLocation(target), "captive_HomeE2"), current);
            return TalentKnown(current.ReadBaseForceSlot(home), "captive_Home_Force") == task.ForceId;
        }
    }

    private GameState CancelTalentCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, TalentCancellation reason,
        List<CampaignEffectRequest> effects)
    {
        var district = current.ReadDistrictTableReference(task.SourceDistrictId <= 51 ? task.SourceDistrictId : 0)
            ?? throw TalentCommandMissing("cancel_District");
        var force = ReadTalentCommandForce(current, district);
        bool owned = TalentForceIsPlayerCurrent(state, current, force);
        if (reason != TalentCancellation.Quiet && owned &&
            (TalentKnown(current.ReadWorldTransferServiceFlags(), "cancel_World16A8") & 0x100) == 0)
            state = WriteTalentCancellationCurrent(state, current, task, district, force, reason, effects);
        return ScheduleTalentCompletionReturnCurrent(state, current, task.Id, task.MemberIds, date,
            IsTalentPlayerPrimaryDistrict(state, current, district));
    }

    private static ForceReference ReadTalentCommandForce(SourceWorldContext current, DistrictReference district)
    {
        int raw = TalentKnown(current.ReadDistrictNativeValidity(district), "command_District_validity")
            ? TalentKnown(current.ReadDistrictForceId(TalentKnown(district.PhysicalSlot, "command_District_physical")), "command_District10") : 0;
        return current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw TalentCommandMissing("command_Force_pointer");
    }

    private static bool IsTalentPlayerPrimaryDistrict(GameState state, SourceWorldContext current, DistrictReference district)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw TalentCommandMissing("command_player_Force");
        return TalentKnown(current.ReadForceNativeValidity(player), "command_player_validity") &&
            ForcePrimaryDistrictSource.Resolve(current, player) == district;
    }

    private GameState WriteTalentCancellationCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, DistrictReference district, ForceReference commandForce,
        TalentCancellation reason, List<CampaignEffectRequest> effects)
    {
        int actor = task.MemberIds.Length == 0 ? 0 : current.ResolveOfficerSource(task.MemberIds[0])
            ?? throw TalentCommandMissing("cancel_actor_source");
        int actorId = TalentKnown(current.ReadPersonIdentity(actor), "cancel_actor_ID");
        int Ruler() => TalentRulerCurrent(current, current.ReadPersonForce(actor) ?? throw TalentCommandMissing("cancel_actor_Force"));
        int target = 0, targetIdentity = 0;
        if (reason != TalentCancellation.SearchAreaChanged)
        {
            target = task.TargetOfficerId is int targetId ? current.ResolveOfficerSource(targetId)
                ?? throw TalentCommandMissing("cancel_target_source") : 0;
            targetIdentity = TalentKnown(current.ReadPersonIdentity(target), "cancel_target_ID");
        }
        int mainRoot, supplementalRoot, historyOfficer = actorId;
        HistoryMessageOperands main, supplemental;
        if (reason == TalentCancellation.SearchAreaChanged)
        {
            var area = TalentSearchAreaCurrent(current, task);
            var operands = new HistoryMessageOperands(Person0: actorId, Person1: Ruler(),
                Area0: TalentKnown(area.PhysicalSlot, "cancel_Area"), Base0: TalentAreaBaseCurrent(current, area));
            if (IsTalentPlayerPrimaryDistrict(state, current, district))
            {
                var text = FormatTalentReportCurrent(state, current, 0xC3AB, operands);
                state = text.State;
                effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText,
                    null, actorId, null, null, text.Text) });
            }
            mainRoot = 0xC3B4; supplementalRoot = 0x51A0; main = operands; supplemental = operands;
        }
        else if (reason == TalentCancellation.RecruitTargetChanged)
        {
            supplemental = new(Person0: actorId, Person1: Ruler(), Person2: targetIdentity);
            if (IsTalentPlayerPrimaryDistrict(state, current, district))
            {
                var text = FormatTalentReportCurrent(state, current, 0xC3F4, supplemental); state = text.State;
                effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText,
                    null, actorId, null, null, text.Text) });
                text = FormatTalentReportCurrent(state, current, 0xC415, new(Person0: actorId)); state = text.State;
                effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement,
                    null, null, null, null, text.Text) });
            }
            mainRoot = 0xC416; supplementalRoot = 0x51E2; main = new(Person0: actorId, Person1: targetIdentity);
        }
        else
        {
            mainRoot = 0xC417; supplementalRoot = 0x51EA; main = new(Person0: actorId, Person1: targetIdentity);
            historyOfficer = 5012;
            supplemental = new(Person0: 5012, Person1: TalentRulerCurrent(current,
                OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw TalentCommandMissing("cancel_player")),
                Person2: actorId, Person3: targetIdentity);
        }
        var message = FormatTalentReportCurrent(state, current, mainRoot, main);
        var attachment = FormatTalentReportCurrent(message.State, current, supplementalRoot, supplemental);
        string? supplementalText = attachment.Text;
        if (reason == TalentCancellation.RecruitTargetChanged &&
            current.ReadPersonForce(actor) == current.ReadPersonForce(target))
            supplementalText = CampaignHistoryWriter.Supplemental(attachment.State);
        int day = (ReadArmyDestructionSampler(attachment.State, current) ?? throw TalentCommandMissing("cancel_history_day450")).Day450;
        return WriteHistory(attachment.State, current, new(2, task.SourceDistrictId, 0, day,
            message.Text, supplementalText, historyOfficer, MarkBit0: reason == TalentCancellation.SourceChanged ? 1 : 0), effects);
    }
}
