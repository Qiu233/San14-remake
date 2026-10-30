using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The editor death instruction, using the enclosing result
    /// operation's current and live pool. The ordinary death writer owns jobs,
    /// Army destruction, items, relations, governor selection and registration.
    /// This operation does not write event occurrence or a Story receipt.</summary>
    internal (GameState State, int NativeReturn) ApplyEditedEventPersonDeathCurrent(
        GameState state, SourceWorldContext current, IReadOnlyList<int> arguments,
        EditedEventResultSelectors selectors)
    {
        if (arguments.Count != 8) throw EditedDeathMissing("statement_arguments");
        int person = selectors.Person(arguments[0]);
        if (!EditedDeathNeed(current.ReadPersonNativeValidity(person), "Person_validity")) return (state, 0);
        ushort id = EditedDeathNeed(current.ReadPersonIdentity(person), "Person10");
        if (unchecked((ushort)(id - 4001)) <= 1099) return (state, 0);
        //232530 and233240(...,0) have different Home/special-ID gates.
        ushort home = EditedDeathNeed(current.ReadPersonFirstLocation(person), "PersonE2");
        if ((home is < 1 or > 61) && DuelAftermathForce(current, person) is >= 46 and <= 50)
            return (state, 0);
        if (DuelAftermathSpecialPerson(current, person)) return (state, 0);
        if ((EditedDeathNeed(current.ReadOwnedReturnPersonFlags(person), "Person15E") & 0x4000) != 0)
            return (state, 0);
        if (EditedDeathNeed(current.ReadPersonStatus(person), "PersonE6") is 9 or 1)
            return (state, 0);

        var death = ApplyEditedEventPersonDeathTailCurrent(state, current, person);
        //405 forbids a ruler before the first write. Any suspension is a
        //genuine unsupported lifecycle path, never a completed event result.
        if (death.NativeReturn is null || death.State.PersonDeathContinuation is not null)
            throw EditedDeathMissing("unexpected_death_continuation");
        if (death.NativeReturn == 0) return (death.State, 0);
        var working = MaintainEditedDeathArmyCommandsCurrent(death.State, current);
        working = PublishNormalTurnCurrent(working, current,
            current.OwnedIndexes ?? throw EditedDeathMissing("completion_pool"));
        working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
        return (working, 1);
    }

    /// <summary>3C3380, shared by the editor death and ruler-change callers.
    /// The caller owns its additional gates and274D70 command-maintenance tail.
    /// A null return preserves the actual successor choice and completed
    /// prefix; its caller must suspend rather than advancing the result cursor.</summary>
    internal (GameState State, int? NativeReturn) ApplyEditedEventPersonDeathTailCurrent(
        GameState state, SourceWorldContext current, int person)
    {
        if (!EditedDeathNeed(current.ReadPersonNativeValidity(person), "tail_Person_validity")) return (state, 0);
        var pool = current.OwnedIndexes ?? throw EditedDeathMissing("current_pool");
        //Borrowing binds the existing ordinary lifecycle consumers. It does
        //not capture another world or rebuild the live registration lists.
        _ = BorrowOwnedArmyReturnWorld(state, current, pool);
        var manager = state.CampaignEventEligibility ?? throw EditedDeathMissing("event_manager");
        var priorBlocker = current.ReportBlockerState;
        bool returned = false;
        GameState working;
        try
        {
            current.ReportBlockerState = new(true, 1);
            working = state with { CampaignEventEligibility = manager with { HistoryRecordingSuppressed = true } };
            //3C3380 ->295EE0(Person,null,0,1,1,0,0,1,0).
            //Argument8 preserves chronology/kin journals;1B0 suppresses the
            //separate persistent report ring, not the actual death scenes.
            var death = new PersonDeathSource(this, working, current, pool)
                .ApplyCombatDeath(person, new(null), 0, destroyAssociatedArmy: true,
                    report: true, history: true, suppressReports: false);
            working = death.State;
            if (death.NativeReturn is null)
            {
                if (working.PersonDeathContinuation is null)
                    throw EditedDeathMissing("death_continuation_owner");
                working = PublishNormalTurnCurrent(working, current, pool);
                working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
                returned = true;
                return (working, null);
            }
            working = ConsumeArmyLifecyclePendingCurrent(working, current, pool)
                ?? throw EditedDeathMissing("Army_pending_cleanup");
            //The native writer clears1B0, rather than restoring its entry
            //value. On failure no candidate is returned to the caller.
            working = working with { CampaignEventEligibility =
                (working.CampaignEventEligibility ?? throw EditedDeathMissing("event_manager_after_death"))
                    with { HistoryRecordingSuppressed = false } };
            current.ReportBlockerState = new(true, 0);

            //3D5120 invalidates native event-manager Person selections and
            //actor classification caches. This implementation constructs a
            //fresh condition runtime for every evaluation and has no retained
            //native Person cache. Subsequent evaluations read this changed
            //world; the existing source identities of finished Story steps
            //remain immutable. No parallel address-indexed cache is required.
            working = PublishNormalTurnCurrent(working, current, pool);
            working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
            returned = true;
            return (working, 1);
        }
        finally
        {
            //A successful native operation leaves1B0 cleared; an operation
            //suspended for a real choice leaves the recording gate set.
            if (!returned) current.ReportBlockerState = priorBlocker;
        }
    }

    private GameState MaintainEditedDeathArmyCommandsCurrent(GameState state, SourceWorldContext current)
    {
        //274D70 traverses the live Army60 registration list after28E6D0.
        var order = current.ReadRegistryReferences(RegistryKind.Army)
            ?? throw EditedDeathMissing("Army_registry");
        if (order.Count == 0) return state;
        var season = state.NativeMapSeason ?? throw EditedDeathMissing("calendar");
        var operation = new FacilityOperation(() => season.ThirdDate, season.AdvancedCalendarSteps,
            latest => (ReadArmyDestructionSampler(latest, current)
                ?? throw EditedDeathMissing("campaign_day450")).Day450);
        foreach (var entry in order)
        {
            int physical = EditedDeathNeed(entry?.PhysicalSlot, "registered_Army_pointer");
            var image = NotificationArmy(state, physical);
            current.SetOwnedPersonCommandManager(state.PersonnelCommands);
            state = MaintainOrdinaryArmyCommand(state, image, operation, null, current);
            var maintained = NotificationArmy(state, physical);
            if (!ReferenceEquals(image.OverrideTarget, maintained.OverrideTarget))
                current.SetArmyCommandOverride(physical, ArmyOverrideTargetSourceAdapter.Read(state, maintained));
        }
        return state;
    }

    private static T EditedDeathNeed<T>(T? value, string field) where T : struct =>
        value ?? throw EditedDeathMissing(field);
    private static InvalidDataException EditedDeathMissing(string field) =>
        new("edited_event.results.person_death." + field);
}
