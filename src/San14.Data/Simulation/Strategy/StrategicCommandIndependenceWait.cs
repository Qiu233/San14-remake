using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState StartStrategicIndependenceWaitCurrent(GameState state, SourceWorldContext current,
        PersonnelStrategicTask previous, NativeMapSeasonDate date)
    {
        var manager = current.ReadOwnedPersonCommandManager() ?? throw StrategyMissing("wait_creation_manager");
        if (manager.Independences is null) throw StrategyMissing("wait_creation_owner");
        if (manager.AvailableDispatchSlots.Length == 0) throw StrategyMissing("wait_creation_dispatch_allocation");
        int executorId = previous.MemberIds[0];
        int executor = current.ResolveOfficerSource(executorId) ?? throw StrategyMissing("wait_creation_executor");
        int captured = previous.TargetGovernorId ?? throw StrategyMissing("wait_creation_captured_governor");
        var city = current.ReadCityTableReference(previous.Target.Id <= 51 ? previous.Target.Id : 0)
            ?? throw StrategyMissing("wait_creation_City");
        int cityHex = current.ReadBaseCurrentHexIndex(city);
        var hex = current.ReadHexTableReference(cityHex < 48400 ? cityHex : 0) ?? throw StrategyMissing("wait_creation_Hex");
        int at = StrategyNeed(hex.PhysicalSlot, "wait_creation_Hex_identity");
        int location = StrategyNeed(current.ReadPersonFirstLocation(executor), "wait_creation_executor_E2");
        var source = BaseLookup.Find(location, current);
        int district = StrategyNeed(current.ReadBaseDistrictSlot(source), "wait_creation_source_District");
        int targetForce = StrategyNeed(current.ReadBaseForceSlot(city), "wait_creation_target_Force");
        int sourceForce = StrategyNeed(current.ReadBaseForceSlot(source), "wait_creation_source_Force");
        int id = manager.NextTaskId, slot = manager.AvailableDispatchSlots[0];
        var task = new PersonnelIndependenceTask(id, slot, [], previous.ForceId,
            location, district, previous.Target.Id, captured, targetForce, sourceForce, at, at,
            date, PersonReturnCommands.DateFromOrdinal(unchecked(PersonReturnCommands.CalendarOrdinal(date) +
                previous.IndependenceChoice)), previous.SavedResult, previous.MilitaryIntelligence, false, false)
            { ReportToPlayer = previous.ReportToPlayer };
        manager = manager with { NextTaskId = checked(id + 1), AvailableDispatchSlots = manager.AvailableDispatchSlots[1..],
            Independences = new(manager.Independences) { [id] = task } };
        var descriptor = current.OwnedIndexes?.AssociatedTaskHandle ?? throw StrategyMissing("wait_creation_association");
        if (!current.AppendPersonReturnAssociated(new(descriptor.Identity), id)) throw StrategyMissing("wait_creation_registration");
        current.SetOwnedPersonCommandManager(manager);
        current.SetLinkedEntityRawState58(id, slot); current.SetLinkedEntityMemberIds(id, []);
        current.SetAssociatedPayloadPostLoadCallback(id, PostLoadCallback.NoOp);
        ushort flags = StrategyNeed(current.ReadArmyReturnPersonFlags196(executor), "wait_creation_flags15E");
        current.WriteArmyReturnPersonFlags196(executor, (ushort)(flags | 1));
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, executor, date);
        ushort area = StrategyNeed(current.ReadHexAreaSlot(position), "wait_creation_position_Area");
        current.WritePersonSecondLocation(executor, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
        manager = current.ReadOwnedPersonCommandManager() ?? throw StrategyMissing("wait_creation_current_manager");
        task = task with { MemberIds = [executorId] };
        manager = manager with { Independences = new(manager.Independences!) { [id] = task } };
        current.SetOwnedPersonCommandManager(manager);
        current.SetLinkedEntityMemberIds(id, [(uint?)StrategyNeed(current.ReadPersonIdentity(executor), "wait_creation_member_identity")]);
        var working = state with { PersonnelCommands = manager };
        if (StrategyNeed(current.ReadArmyReturnBaseGovernor2E(source), "wait_creation_source_governor") == executorId)
            _ = ApplyPersonFreedomGovernorCurrent(working, current, source);
        working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        return AppendStrategicWaitCreationReportsCurrent(working, current, task, previous, date);
    }

    PersonnelStrategicAdvanceResult IPersonnelStrategicExecution.AdvanceIndependenceCurrent(GameState state,
        SourceWorldContext current, PersonnelIndependenceTask task, NativeMapSeasonDate date)
    {
        if (date.Hour != 8) return new(state, false);
        task = task with { Started = true };
        var manager = current.ReadOwnedPersonCommandManager() ?? throw StrategyMissing("wait_manager");
        manager = manager with { Independences = new(manager.Independences ?? throw StrategyMissing("wait_owner"))
            { [task.Id] = task } };
        current.SetOwnedPersonCommandManager(manager); state = state with { PersonnelCommands = manager };
        var view = StrategicWaitJourneyView(task);
        var continuation = new StrategicJourneyCurrent(this, state, current, view);
        var first = continuation.Check(false);
        if (first != StrategicJourneyDisposition.Continue) return Cancel(first, false);
        int remaining = Math.Max(0, unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) -
            PersonReturnCommands.CalendarOrdinal(date)));
        //1DE160 checks the timeout only at remaining<=1, but releases the
        //wait immediately when its governor becomes available before then.
        if (remaining <= 1)
        {
            var last = continuation.Check(true);
            if (last != StrategicJourneyDisposition.Continue) return Cancel(last, true);
            if (continuation.NeedsIndependenceWait()) return Cancel(StrategicJourneyDisposition.ReportCancellation, true);
        }
        if (continuation.NeedsIndependenceWait()) return new(state, false);
        var result = ResolveStrategicIndependenceWaitCurrent(state, current, task);
        state = result.State;
        int executor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw StrategyMissing("wait_reward_executor");
        state = ApplyStrategicCommandRewardsCurrent(state, current, executor, 11, result.ResultCode == 0 ? 2 : 1);
        return Complete(state);

        PersonnelStrategicAdvanceResult Cancel(StrategicJourneyDisposition reason, bool timeout)
        {
            if (reason == StrategicJourneyDisposition.ReportCancellation)
                state = AppendStrategicWaitCancellationCurrent(state, current, task, timeout, date);
            //The wait's virtual90 is1F2290→2351B0. It has no military refund.
            return Complete(state);
        }
        PersonnelStrategicAdvanceResult Complete(GameState working) => new(
            PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(working, current, task.Id, task.MemberIds,
                date, IsTalentPlayerPrimaryDistrict(working, current,
                    current.ReadDistrictTableReference(task.SourceDistrictId <= 51 ? task.SourceDistrictId : 0)
                    ?? throw StrategyMissing("wait_return_District"))), true);
    }

    //A read-only participant projection shares the exact continuation rules;
    //it is never registered or saved as a seven-kind strategic task.
    private static PersonnelStrategicTask StrategicWaitJourneyView(PersonnelIndependenceTask task) => new(
        task.Id, task.DispatchSlot, task.MemberIds, 11, task.ForceId, task.SourceLocationId, task.SourceDistrictId,
        new(StrategicCommandTargetKind.City, task.TargetCityId), null, task.CapturedGovernorId,
        task.TargetOriginalForceId, task.SourceForceId, task.StartHexId, task.EndHexId, task.StartDate,
        task.DueDate, task.SavedResult, task.MilitaryIntelligence, 0, task.Started, task.Cancelled)
        { IsAssociated = task.IsAssociated, IsRetiring = task.IsRetiring, ReportToPlayer = task.ReportToPlayer };
}
