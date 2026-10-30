using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelIntelligenceExecution
{
    PersonnelIntelligenceAdvanceResult IPersonnelIntelligenceExecution.AdvanceIntelligenceCurrent(GameState state,
        SourceWorldContext current, PersonnelIntelligenceTask task, NativeMapSeasonDate date)
    {
        if (date.Hour != 8) return new(state, false);
        if (task.NarrationProgress is null) throw IntelligenceMissing("narration_source_time_unavailable");
        if (task.NarrationProgress.Pending is not null)
            return ResumeIntelligenceNarrationCurrent(state, current, task, date);
        task = task with { Started = true };
        state = StoreIntelligenceTaskCurrent(state, current, task);
        //Inspect's128 result accessor is an actual independent guard.
        if (task.Kind == PersonnelIntelligenceKind.Inspect && task.SavedResult != -1) return new(state, true);
        var first = CheckIntelligenceJourneyCurrent(state, current, task);
        if (first != IntelligenceJourneyDisposition.Continue) return Cancel(first);
        int remaining = Math.Max(0, unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) -
            PersonReturnCommands.CalendarOrdinal(date)));
        if (remaining > 1) return new(state, false);
        var last = CheckIntelligenceJourneyCurrent(state, current, task);
        if (last != IntelligenceJourneyDisposition.Continue) return Cancel(last);
        int actor = ReadIntelligenceJourneyActorCurrent(current, task) ?? throw IntelligenceMissing("journey_actor");
        return task.Kind == PersonnelIntelligenceKind.Away
            ? BeginIntelligenceAwayNarrationCurrent(state, current, task, actor, date)
            : BeginIntelligenceInspectNarrationCurrent(state, current, task, actor, date);

        PersonnelIntelligenceAdvanceResult Cancel(IntelligenceJourneyDisposition reason)
        {
            if (reason == IntelligenceJourneyDisposition.ReportCancellation)
            {
                state = BeginIntelligenceCancellationNarrationCurrent(state, current, task,
                    ReadIntelligenceJourneyActorCurrent(current, task) ?? throw IntelligenceMissing("cancel_actor"), date);
                if (ReadIntelligenceNarrationTaskCurrent(current, task.Id).NarrationProgress?.Pending is not null)
                    return new(state, false);
            }
            return FinishIntelligenceReturnCurrent(state, current, task, date);
        }
    }

    private enum IntelligenceJourneyDisposition { Continue, QuietCancellation, ReportCancellation }
    private IntelligenceJourneyDisposition CheckIntelligenceJourneyCurrent(GameState state,
        SourceWorldContext current, PersonnelIntelligenceTask task)
    {
        int? first = ReadIntelligenceJourneyActorCurrent(current, task);
        if (first is not int person) return IntelligenceJourneyDisposition.QuietCancellation;
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(person), "journey_Person_validity"))
            return IntelligenceJourneyDisposition.QuietCancellation;
        var force = current.ReadPersonForce(person) ?? throw IntelligenceMissing("journey_Force");
        if (!IntelligenceNeed(current.ReadForceNativeValidity(force), "journey_Force_validity") ||
            IntelligenceNeed(current.ReadForcePhysicalSlot(force), "journey_Force_identity") != task.ForceId)
            return IntelligenceJourneyDisposition.QuietCancellation;
        int raw = IntelligenceNeed(current.ReadPersonDistrictSlot(person), "journey_E0");
        var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw IntelligenceMissing("journey_District");
        if (task.Kind == PersonnelIntelligenceKind.Away)
        {
            //233080 independently requires a native-valid ruler as District leader.
            int leaderId = IntelligenceNeed(current.ReadDistrictLeader(IntelligenceNeed(district.PhysicalSlot,
                "journey_District_pointer")), "journey_District_leader");
            int leader = IntelligenceNeed((leaderId <= 6000 ? current.ReadPersonLookupReference(leaderId)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "journey_District_leader_pointer");
            if (!IntelligenceNeed(current.ReadPersonNativeValidity(leader), "journey_District_leader_validity") ||
                IntelligenceNeed(current.ReadPersonStatus(leader), "journey_District_leader_status") != 1)
                return IntelligenceJourneyDisposition.QuietCancellation;
        }
        else if (task.Kind != PersonnelIntelligenceKind.Inspect) throw IntelligenceMissing("journey_kind");
        if (!IntelligenceNeed(current.ReadDistrictNativeValidity(district), "journey_District_validity") ||
            IntelligenceNeed(district.PhysicalSlot, "journey_District_identity") != task.SourceDistrictId)
            return IntelligenceJourneyDisposition.QuietCancellation;
        var home = BaseLookup.Find(checked((ushort)task.SourceLocationId), current);
        int currentHome = IntelligenceNeed(current.ReadBaseObjectLink(home), "journey_current_Base4E");
        if (IntelligenceNeed(current.ReadPersonFirstLocation(person), "journey_E2") != currentHome)
            return IntelligenceJourneyDisposition.QuietCancellation;
        if (task.Kind == PersonnelIntelligenceKind.Away)
        {
            var target = current.ReadForceTableReference(task.TargetId <= 51 ? task.TargetId : 0)
                ?? throw IntelligenceMissing("journey_target_Force");
            if (!IntelligenceNeed(current.ReadForceNativeValidity(target), "journey_target_validity") ||
                IntelligenceNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, target, force), "journey_target_relation"))
                return IntelligenceJourneyDisposition.ReportCancellation;
        }
        else
        {
            var target = current.ReadCityTableReference(task.TargetId <= 51 ? task.TargetId : 0)
                ?? throw IntelligenceMissing("journey_target_City");
            if (!IntelligenceNeed(current.ReadBaseNativeValidity(target), "journey_target_City_validity") ||
                IntelligenceNeed(current.ReadBaseForceSlot(target), "journey_target_City_Force") != task.ForceId)
                return IntelligenceJourneyDisposition.ReportCancellation;
        }
        return task.Cancelled ? IntelligenceJourneyDisposition.QuietCancellation : IntelligenceJourneyDisposition.Continue;
    }

    private static GameState StoreIntelligenceTaskCurrent(GameState state, SourceWorldContext current, PersonnelIntelligenceTask task)
    {
        var manager = current.ReadOwnedPersonCommandManager() ?? throw IntelligenceMissing("task_manager");
        manager = manager with { Intelligences = new(manager.Intelligences ?? throw IntelligenceMissing("task_owner")) { [task.Id] = task } };
        current.SetOwnedPersonCommandManager(manager);
        return state with { PersonnelCommands = manager };
    }

    //3D8050 cancels the actual associated command, then resolves its newly
    //associated Return and settles its ordered members at the person's Home.
    //Both v110(1) calls retire immediately; this is not another return order.
    internal GameState ClearTutorialSpyAssignmentCurrent(GameState state, SourceWorldContext current, int physical)
    {
        var linked = current.ReadPersonLinkedEntity(physical) ?? throw IntelligenceMissing("clear_associated");
        if (linked.Identity is not int commandId) return state;
        var home = BaseLookup.Find(IntelligenceNeed(current.ReadPersonFirstLocation(physical), "clear_HomeE2"), current);
        if (!IntelligenceNeed(current.ReadBaseNativeValidity(home), "clear_Home_validity")) return state;
        var date = state.NativeMapSeason?.ThirdDate ?? throw IntelligenceMissing("clear_World34_date");
        state = PersonReturnCommands.CancelAssociatedCommandCurrent(state, current, commandId, date, this);
        state = PersonReturnCommands.RetireAssociatedCommandImmediatelyCurrent(state, current, commandId);
        linked = current.ReadPersonLinkedEntity(physical) ?? throw IntelligenceMissing("clear_return_associated");
        if (linked.Identity is not int returnId) return PersonReturnCommands.PublishCurrent(state, current);
        var owner = current.ReadOwnedPersonCommandManager() ?? throw IntelligenceMissing("clear_return_owner");
        //32B190 only consumes native kinds15,1,18. Cancellation's normal
        //235730/2247F0 path has just installed kind15 here.
        if (!owner.Returns.ContainsKey(returnId) && owner.Caravans?.GetValueOrDefault(returnId)?.NativeKind != 18)
            throw IntelligenceMissing("clear_arrival_command_kind");
        var pool = current.ReadOwnedCurrentListPool() ?? throw IntelligenceMissing("clear_current_pool");
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        var arrival = ApplyPersonReturnArrivalCurrent(state, current, pool, returnId, home,
            new PersonReturnGovernorQueries(state, _governorInputs, current), false, date);
        state = arrival.State ?? throw IntelligenceMissing(arrival.UnresolvedOwner ?? "clear_Home_arrival");
        state = PersonReturnCommands.RetireAssociatedCommandImmediatelyCurrent(state, current, returnId);
        return PersonReturnCommands.PublishCurrent(state, current);
    }
}
