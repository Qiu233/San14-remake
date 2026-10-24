using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2006F0 /200780: a valid target may be busy, or away from the captured
    //base. A null/invalid base does not itself make the home comparison true.
    private bool TalentRecruitTargetStillAwayCurrent(SourceWorldContext current, int target, BaseReference home)
    {
        if (!TalentNeed(current.ReadPersonNativeValidity(target), "wait_target_validity")) return false;
        if (PersonCurrentBusySource.Read(current, target, TalentCommandMissing)) return true;
        return TalentNeed(current.ReadBaseNativeValidity(home), "wait_target_Base_validity") &&
            TalentNeed(current.ReadPersonFirstLocation(target), "wait_target_E2") !=
            TalentNeed(current.ReadBaseObjectLink(home), "wait_target_Base4E");
    }

    //1DB9E0: every evening is a retry opportunity, including evenings before
    //the deadline. Only a still unavailable target at the second guard times out.
    private PersonnelTalentAdvanceResult AdvanceTalentRecruitWaitCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        var cancellation = ReadTalentCancellation(state, current, task, date);
        if (cancellation != TalentCancellation.None)
            return new(CancelTalentCurrent(state, current, task, date, cancellation, effects), true);
        int remaining = Math.Max(0, unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) -
            PersonReturnCommands.CalendarOrdinal(date)));
        if (remaining <= 1)
        {
            //1E8E20's second call changes only the busy/home rejection gate.
            cancellation = ReadTalentCancellation(state, current, task, date, rejectRecruitWaitUnavailable: true);
            if (cancellation == TalentCancellation.RecruitWaitExpired)
                return new(CancelTalentRecruitWaitTimeoutCurrent(state, current, task, date, effects), true);
            if (cancellation != TalentCancellation.None)
                return new(CancelTalentCurrent(state, current, task, date, cancellation, effects), true);
        }
        int physical = current.ResolveOfficerSource(task.TargetOfficerId!.Value)
            ?? throw TalentCommandMissing("wait_target_source");
        var home = BaseLookup.Find(task.TargetOriginalHomeLocationId
            ?? throw TalentCommandMissing("wait_Base78"), current);
        if (TalentRecruitTargetStillAwayCurrent(current, physical, home)) return new(state, false);
        return new(CompleteTalentRecruitCurrent(state, current, task, date, effects), true);
    }

    //1EC330(22) ->1F63B0. The original Employ command remains associated
    //until the manager's normal retirement flush; this is a new receiver.
    private GameState StartTalentRecruitWaitCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask original, NativeMapSeasonDate date, int executor, int target,
        BaseReference targetHome, List<CampaignEffectRequest> effects)
    {
        state = WriteTalentRecruitWaitNoticeCurrent(state, current, original, date, executor, target,
            targetHome, false, effects);
        var owner = current.ReadOwnedPersonCommandManager() ?? throw TalentCommandMissing("wait_manager");
        if (owner.Talents is null) throw TalentCommandMissing("wait_saved_Talents");
        if (owner.AvailableDispatchSlots.Length == 0) throw TalentCommandMissing("wait_dispatch_allocation1DE290");
        int id = owner.NextTaskId, slot = owner.AvailableDispatchSlots[0];
        var task = original with { Id = id, DispatchSlot = slot, MemberIds = [], IsRecruitWait = true,
            Started = false, Cancelled = false, IsRetiring = false, IsAssociated = true, NeedsRefresh = false };
        owner = owner with { NextTaskId = checked(id + 1), AvailableDispatchSlots = owner.AvailableDispatchSlots[1..],
            Talents = new(owner.Talents) { [id] = task } };
        var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw TalentCommandMissing("wait_association_index");
        if (!current.AppendPersonReturnAssociated(new(handle.Identity), id))
            throw TalentCommandMissing("wait_association_registration");
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityRawState58(id, slot);
        current.SetLinkedEntityMemberIds(id, []);
        current.SetAssociatedPayloadPostLoadCallback(id, PostLoadCallback.NoOp);
        //The source allocates before validating the initializer. An unknown
        //initializer remains an unpublished failure, not an invented empty task.
        if (!TalentNeed(current.ReadPersonNativeValidity(executor), "wait_executor_validity") ||
            !TalentNeed(current.ReadPersonNativeValidity(target), "wait_target_validity") ||
            !TalentNeed(current.ReadBaseNativeValidity(targetHome), "wait_Base_validity"))
            throw TalentCommandMissing("wait_invalid_initializer1EFFB0");
        if (original.RecruitMode != 10 && original.RecruitMode != 30 &&
            original.RecruitMode != TalentRecruitParameters.Read(state, "employ-wait-duration-third"))
            throw TalentCommandMissing("wait_initializer_duration1EFFB0");
        ushort flags = TalentNeed(current.ReadArmyReturnPersonFlags196(executor), "wait_executor15E");
        current.WriteArmyReturnPersonFlags196(executor, (ushort)(flags | 1));
        ushort sourceLocation = TalentNeed(current.ReadPersonFirstLocation(executor), "wait_sourceE2");
        var sourceHome = BaseLookup.Find(sourceLocation, current);
        int district = TalentNeed(current.ReadBaseDistrictSlot(sourceHome), "wait_source_District");
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, executor, date);
        ushort area = TalentNeed(current.ReadHexAreaSlot(position), "wait_executor_position_Area");
        current.WritePersonSecondLocation(executor, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
        int personId = TalentNeed(current.ReadPersonIdentity(executor), "wait_executor10");
        var targetForce = current.ReadPersonForce(target) ?? throw TalentCommandMissing("wait_target_Force");
        int targetForceId = TalentNeed(current.ReadForcePhysicalSlot(targetForce), "wait_target_Force60");
        int ownForce = TalentNeed(current.ReadBaseForceSlot(sourceHome), "wait_source_Force");
        ushort baseLocation = unchecked((byte)TalentNeed(current.ReadBaseObjectLink(targetHome), "wait_Base4E"));
        ushort rawHex = current.ReadBaseCurrentHexIndex(targetHome);
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw TalentCommandMissing("wait_Base_Hex_pointer");
        int hexId = TalentNeed(hex.PhysicalSlot, "wait_Base_Hex_identity");
        int ordinal = unchecked(PersonReturnCommands.CalendarOrdinal(date) + original.RecruitMode);
        short year = unchecked((short)(ordinal / 360));
        int remainder = Math.Max(0, unchecked(ordinal - year * 360));
        var due = new NativeMapSeasonDate(year, unchecked((sbyte)(remainder / 30 + 1)),
            unchecked((sbyte)(remainder % 30 + 1)), 0);
        task = task with { MemberIds = [personId], ForceId = ownForce, SourceCityId = sourceLocation,
            SourceDistrictId = district, TargetOriginalForceId = targetForceId,
            TargetOriginalHomeLocationId = baseLocation, StartDate = date, DueDate = due,
            StartHexId = hexId, EndHexId = hexId };
        owner = current.ReadOwnedPersonCommandManager() ?? throw TalentCommandMissing("wait_initialized_manager");
        owner = owner with { Talents = new(owner.Talents!) { [id] = task } };
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityMemberIds(id, [(uint?)personId]);
        var working = state with { PersonnelCommands = owner };
        if (TalentNeed(current.ReadArmyReturnBaseGovernor2E(sourceHome), "wait_source_Governor2E") == personId)
        {
            ApplyPersonFreedomGovernorCurrent(working, current, sourceHome);
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        }
        var officer = working.Officers.GetValueOrDefault(personId) ?? throw TalentCommandMissing("wait_executor_domain");
        working = working with { Officers = new(working.Officers) { [personId] = officer with
            { Behavior = current.ReadPersonBehavior(executor) } } };
        return OfficerLocationSourceAdapter.PublishPosition(working, personId, current);
    }

    private GameState CancelTalentRecruitWaitTimeoutCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        int executor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw TalentCommandMissing("wait_timeout_executor");
        int target = current.ResolveOfficerSource(task.TargetOfficerId!.Value) ?? throw TalentCommandMissing("wait_timeout_target");
        var home = BaseLookup.Find(task.TargetOriginalHomeLocationId ?? throw TalentCommandMissing("wait_timeout_Base78"), current);
        var district = current.ReadDistrictTableReference(task.SourceDistrictId <= 51 ? task.SourceDistrictId : 0)
            ?? throw TalentCommandMissing("wait_timeout_District");
        var force = ReadTalentCommandForce(current, district);
        if (TalentNeed(current.ReadForceNativeValidity(force), "wait_timeout_command_Force_validity") &&
            (TalentNeed(current.ReadWorldTransferServiceFlags(), "wait_timeout_World16A8") & 0x100) == 0)
            state = WriteTalentRecruitWaitNoticeCurrent(state, current, task, date, executor, target, home, true, effects);
        return ScheduleTalentCompletionReturnCurrent(state, current, task.Id, task.MemberIds, date,
            IsTalentPlayerPrimaryDistrict(state, current, district));
    }

    //C97C/C981 on creation; C982 plus C415/C416 on timeout. These are normal
    //resource formatter calls and persisted presentation intents, never ACKs.
    private GameState WriteTalentRecruitWaitNoticeCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, int executor, int target,
        BaseReference home, bool timeout, List<CampaignEffectRequest> effects)
    {
        int actorId = TalentNeed(current.ReadPersonIdentity(executor), "wait_notice_executor10");
        int targetId = TalentNeed(current.ReadPersonIdentity(target), "wait_notice_target10");
        var force = current.ReadPersonForce(executor) ?? throw TalentCommandMissing("wait_notice_Force");
        var district = current.ReadDistrictTableReference(TalentNeed(current.ReadPersonDistrictSlot(executor), "wait_notice_DistrictE0"))
            ?? throw TalentCommandMissing("wait_notice_District_pointer");
        var operands = new HistoryMessageOperands(Person0: actorId, Person1: TalentRulerCurrent(current, force),
            Person2: targetId, Base0: TalentNeed(current.ReadBaseObjectLink(home), "wait_notice_Base4E"));
        int root = timeout ? 0xC982 : 0xC97C;
        if (TalentDistrictIsPlayerRulerCurrent(state, current, district))
        {
            var text = FormatTalentReportCurrent(state, current, root, operands); state = text.State;
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText,
                null, actorId, null, null, text.Text) });
            if (timeout)
            {
                text = FormatTalentReportCurrent(state, current, 0xC415, new(Person0: actorId)); state = text.State;
                effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement,
                    null, null, null, null, text.Text) });
            }
        }
        if (TalentNeed(current.ReadForceNativeValidity(force), "wait_notice_Force_validity") &&
            TalentForceIsPlayerCurrent(state, current, force))
            state = AppendTalentSearchHistoryCurrent(state, current, task, date, executor,
                timeout ? 0xC416 : 0xC981, root, operands, effects, 0, 0,
                mainOperands: new(Person0: actorId, Person1: targetId,
                    Base0: timeout ? null : TalentNeed(current.ReadBaseObjectLink(home), "wait_history_Base4E")));
        return state;
    }
}
