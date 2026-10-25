using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelStrategicExecution
{
    PersonnelStrategicAdvanceResult IPersonnelStrategicExecution.AdvanceStrategicCurrent(GameState state,
        SourceWorldContext current, PersonnelStrategicTask task, NativeMapSeasonDate date)
    {
        if (date.Hour != 8) return new(state, false);
        task = task with { Started = true };
        var manager = current.ReadOwnedPersonCommandManager() ?? throw StrategyMissing("journey_manager");
        manager = manager with { Strategies = new(manager.Strategies ?? throw StrategyMissing("journey_strategy_owner"))
            { [task.Id] = task } };
        current.SetOwnedPersonCommandManager(manager);
        state = state with { PersonnelCommands = manager };
        var continuation = new StrategicJourneyCurrent(this, state, current, task);
        var first = continuation.Check(false);
        if (first != StrategicJourneyDisposition.Continue) return Cancel(first);
        int remaining = Math.Max(0, unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) -
            PersonReturnCommands.CalendarOrdinal(date)));
        if (remaining > 1) return new(state, false);
        var last = continuation.Check(true);
        if (last != StrategicJourneyDisposition.Continue) return Cancel(last);
        if (task.Kind == 11 && task.IndependenceChoice > 0 && continuation.NeedsIndependenceWait())
            return new(StartStrategicIndependenceWaitCurrent(state, current, task, date), true);
        var result = ResolveStrategicCommandCurrent(state, current, task);
        state = result.State;
        int executor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw StrategyMissing("journey_reward_executor");
        state = ApplyStrategicCommandRewardsCurrent(state, current, executor, task.Kind,
            result.ResultCode == 0 ? 2 : result.ResultCode == 2 ? 6 : 1);
        return Complete(state);

        PersonnelStrategicAdvanceResult Cancel(StrategicJourneyDisposition reason)
        {
            if (reason == StrategicJourneyDisposition.ReportCancellation)
                state = AppendStrategicCancellationCurrent(state, current, task);
            state = PersonReturnCommands.RefundStrategicMilitaryCurrent(state, current, task);
            return Complete(state);
        }
        PersonnelStrategicAdvanceResult Complete(GameState working) => new(
            PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(working, current, task.Id,
                task.MemberIds, date, IsTalentPlayerPrimaryDistrict(working, current,
                    current.ReadDistrictTableReference(task.SourceDistrictId <= 51 ? task.SourceDistrictId : 0)
                    ?? throw StrategyMissing("journey_return_District"))), true);
    }

    private enum StrategicJourneyDisposition { Continue, QuietCancellation, ReportCancellation }

    //1E9F50's continuation predicate is distinct from both initial selector
    //qualification and the probability immunity predicates at the actual trial.
    private sealed class StrategicJourneyCurrent(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, PersonnelStrategicTask task)
    {
        private int Executor => current.ResolveOfficerSource(task.MemberIds[0]) ?? throw StrategyMissing("journey_executor");
        private ForceReference ActorForce => current.ReadPersonForce(Executor) ?? throw StrategyMissing("journey_actor_Force");
        private int TargetPerson => current.ResolveOfficerSource(task.Target.Id) ?? throw StrategyMissing("journey_target_Person");
        private ForceReference ForceAt(int raw) => current.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw StrategyMissing("journey_Force_pointer");
        private int ForceId(ForceReference force) => StrategyNeed(current.ReadForcePhysicalSlot(force), "journey_Force_identity");
        private bool ValidForce(ForceReference force) => StrategyNeed(current.ReadForceNativeValidity(force), "journey_Force_validity");
        private bool Protected(ForceReference first, ForceReference second) =>
            new DiplomacyCommandSource(state, current).HasTreaty(ForceId(first), ForceId(second));
        private BaseReference City => current.ReadCityTableReference(task.Target.Id <= 51 ? task.Target.Id : 0)
            ?? throw StrategyMissing("journey_City_pointer");

        internal StrategicJourneyDisposition Check(bool arrived)
        {
            if (task.MemberIds.Length == 0) return StrategicJourneyDisposition.QuietCancellation;
            int actor = Executor;
            if (!StrategyNeed(current.ReadPersonNativeValidity(actor), "journey_actor_validity") ||
                !ValidForce(ActorForce) || ForceId(ActorForce) != task.SourceForceId)
                return StrategicJourneyDisposition.QuietCancellation;
            int districtId = StrategyNeed(current.ReadPersonDistrictSlot(actor), "journey_actor_E0");
            var district = current.ReadDistrictTableReference(districtId < 52 ? districtId : 0)
                ?? throw StrategyMissing("journey_actor_District");
            if (!StrategyNeed(current.ReadDistrictNativeValidity(district), "journey_actor_District_validity") ||
                district.PhysicalSlot != task.SourceDistrictId ||
                StrategyNeed(current.ReadPersonFirstLocation(actor), "journey_actor_E2") != task.SourceLocationId)
                return StrategicJourneyDisposition.QuietCancellation;
            bool valid;
            switch (task.Kind)
            {
                case 10:
                    var first = ForceAt(task.Target.Id);
                    var second = ForceAt(task.SecondForceId ?? throw StrategyMissing("journey_second_Force"));
                    valid = ValidForce(first) && ValidForce(second) && !Protected(first, ActorForce) && !Protected(second, ActorForce);
                    break;
                case 11:
                    var force = ForceAt(StrategyNeed(current.ReadBaseForceSlot(City), "journey_City_Force"));
                    int governor = current.ResolveOfficerSource(task.TargetGovernorId ?? throw StrategyMissing("journey_captured_governor"))
                        ?? throw StrategyMissing("journey_captured_governor_pointer");
                    valid = ValidForce(force) && ForceId(force) == task.TargetOriginalForceId && !Protected(force, ActorForce) &&
                        ForceId(current.ReadPersonForce(governor) ?? throw StrategyMissing("journey_governor_Force")) == ForceId(force) &&
                        (!arrived || task.IndependenceChoice > 0 || !NeedsIndependenceWait());
                    break;
                case 12:
                    int person = TargetPerson;
                    if (!StrategyNeed(current.ReadPersonNativeValidity(person), "journey_separation_validity"))
                    { valid = false; break; }
                    byte status = StrategyNeed(current.ReadPersonStatus(person), "journey_separation_status");
                    var owner = current.ReadPersonForce(person) ?? throw StrategyMissing("journey_separation_Force");
                    valid = status is 2 or 3 or 4 or 6 && ForceId(owner) == task.TargetOriginalForceId && !Protected(owner, ActorForce);
                    break;
                case 24:
                    valid = package.ReadStrategicRumorContinuationCurrent(state, current, TargetPerson,
                        ActorForce, task.TargetOriginalForceId);
                    break;
                case 13 or 14:
                    var area = new AreaReference(task.Target.Id);
                    valid = StrategyNeed(current.ReadAreaNativeValidity(area), "journey_Area_validity");
                    if (valid && arrived)
                    {
                        var areaForce = ForceAt(StrategyNeed(current.ReadAreaCurrentForceSlot(area), "journey_Area_Force"));
                        valid = !Protected(areaForce, ActorForce);
                        if (valid && task.Kind == 14) valid = ValidForce(areaForce);
                        if (valid && task.Kind == 13) valid = package.ReadStrategicPoisonContinuationCurrent(current, area);
                    }
                    break;
                case 20:
                    var target = new AreaReference(task.Target.Id);
                    valid = StrategyNeed(current.ReadAreaNativeValidity(target), "journey_sabotage_Area_validity");
                    if (valid)
                    {
                        var targetForce = ForceAt(StrategyNeed(current.ReadAreaCurrentForceSlot(target), "journey_sabotage_Force"));
                        valid = (targetForce == ActorForce || !Protected(ActorForce, targetForce)) &&
                            package.HasStrategicSabotageTargetCurrent(state, current, task.Target.Id, ForceId(ActorForce));
                    }
                    break;
                default: throw StrategyMissing("journey_kind");
            }
            return !valid ? StrategicJourneyDisposition.ReportCancellation : task.Cancelled
                ? StrategicJourneyDisposition.QuietCancellation : StrategicJourneyDisposition.Continue;
        }

        internal bool NeedsIndependenceWait()
        {
            if (task.Kind != 11) return false;
            int captured = task.TargetGovernorId ?? throw StrategyMissing("wait_captured_governor");
            int governor = current.ResolveOfficerSource(captured) ?? throw StrategyMissing("wait_captured_Person");
            ushort currentGovernor = StrategyNeed(current.ReadArmyReturnBaseGovernor2E(City), "wait_City_governor");
            var force = ForceAt(StrategyNeed(current.ReadBaseForceSlot(City), "wait_City_Force"));
            var capital = new DiplomacyCommandSource(state, current).Capital(ForceId(force));
            ushort capitalLocation = StrategyNeed(current.ReadBaseObjectLink(capital), "wait_capital_location");
            ushort targetLocation = StrategyNeed(current.ReadBaseObjectLink(City), "wait_City_location");
            //22ACF0 supplies the City's current valid governor. The original
            //captured receiver remains separately associated by this task.
            int currentPerson = (currentGovernor <= 6000 ? current.ReadPersonLookupReference(currentGovernor)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot ?? throw StrategyMissing("wait_current_governor_pointer");
            if (!StrategyNeed(current.ReadPersonNativeValidity(currentPerson), "wait_governor_validity") ||
                currentGovernor != captured || capitalLocation == targetLocation) return true;
            if (capitalLocation is >= 52 and <= 61)
            {
                var capitalCity = CurrentGateMapQueries.FindCity(capital, current);
                if (StrategyNeed(current.ReadBaseNativeValidity(capitalCity), "wait_capital_City_validity") &&
                    StrategyNeed(current.ReadBaseObjectLink(capitalCity), "wait_capital_City_location") == targetLocation) return true;
            }
            return PersonCurrentBusySource.Read(current, governor, StrategyMissing);
        }
    }
}
