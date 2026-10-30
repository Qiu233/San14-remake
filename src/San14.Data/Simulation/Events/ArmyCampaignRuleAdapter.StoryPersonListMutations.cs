using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record StoryPersonListMutationResult(GameState State, int? NativeReturn,
    CampaignStoryPersonListContinuation? Continuation = null, IReadOnlyList<int>? OrderedPeople = null);

public sealed partial class ArmyCampaignRuleAdapter
{
    private StoryPersonListMutationResult ApplyStoryPersonListMutationCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, IReadOnlyList<int> people,
        int? targetPerson, BaseReference? targetBase, CampaignStoryPersonListContinuation? continuation,
        StoryPersonMutationClassification classification)
    {
        switch (code)
        {
            case 232:
            case 233:
                foreach (int person in people)
                {
                    ushort flags = StoryListNeed(current.ReadOwnedReturnPersonFlags(person), "loyalty_flags");
                    if ((flags & 0x4000) != 0) continue;
                    byte after;
                    if (code == 232)
                    {
                        byte before = StoryListNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person,
                            _governorInputs), "loyalty_current");
                        if (before >= 101) continue;
                        after = (byte)Math.Clamp(unchecked(before + arguments[0]), 0, 100);
                    }
                    else after = unchecked((byte)arguments[0]);
                    if ((flags & 0x2000) != 0) after = byte.MaxValue;
                    current.SetPersonDestructionLoyaltyE8(person, after);
                    state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
                }
                return new(state, 1);
            case 236:
                int target = targetPerson ?? throw StoryListMissing("employment_target");
                if (!StoryListNeed(current.ReadPersonNativeValidity(target), "employment_target_validity")) return new(state, 1);
                foreach (int person in people)
                {
                    int id = StoryListNeed(current.ReadPersonIdentity(person), "employment_Person_identity");
                    int targetId = Math.Min((int)StoryListNeed(current.ReadPersonIdentity(target), "employment_target_identity"), 6000);
                    var before = current.ReadOfficerAftermath(person) ?? throw StoryListMissing("employment_resentment_owner");
                    //Both source stores are unconditional; only the independent
                    //disposition and compatibility knowledge are retained.
                    current.SetOfficerAftermath(person, before with
                    {
                        Resentment = new(targetId, Math.Clamp(arguments[1], 0, 255)),
                        SourceKnowledge = before.SourceKnowledge is { } knowledge
                            ? knowledge with { UnmappedResentmentTarget = null } : null
                    });
                    state = current.PublishOfficerAftermath(state, id);
                }
                return new(state, 1);
            case 234:
            case 235:
            case 238:
                if (continuation is not null) throw StoryListMissing("unexpected_movement_continuation");
                var movement = ApplyStoryPersonListMovementCurrent(state, current, code, people, targetPerson, targetBase,
                    classification.RecordJoined, classification.RecordFreed);
                return new(movement.State, 1, OrderedPeople: movement.OrderedPeople);
            case 237:
                return ApplyStoryPersonListDeathsCurrent(state, current, people, continuation, classification);
            default: throw StoryListMissing("opcode");
        }
    }

    private StoryPersonListMutationResult ApplyStoryPersonListDeathsCurrent(GameState state,
        SourceWorldContext current, IReadOnlyList<int> people, CampaignStoryPersonListContinuation? continuation,
        StoryPersonMutationClassification classification)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw StoryListMissing("death_shared_pool");
        int next = continuation?.NextPersonIndex ?? 0;
        if (next < 0 || next > people.Count) throw StoryListMissing("death_person_index");
        bool manager = StoryListNeed(current.ReadEventManagerPresent(), "death_manager_presence");
        if (continuation?.AwaitingDeathCompletion == true)
        {
            if (state.PersonDeathContinuation is not null) return new(state, null, continuation);
            if (next == 0) throw StoryListMissing("death_completed_person_index");
            if (manager)
            {
                state = ClearRecordingGate(state, current);
                classification.RecordDeath(people[next - 1]);
            }
        }
        _ = BorrowOwnedArmyReturnWorld(state, current, pool);
        for (int index = next; index < people.Count; index++)
        {
            if (manager)
            {
                current.ReportBlockerState = new(true, 1);
                state = state with { CampaignEventEligibility =
                    (state.CampaignEventEligibility ?? throw StoryListMissing("death_manager_owner"))
                        with { HistoryRecordingSuppressed = true } };
            }
            var death = new PersonDeathSource(this, state, current, pool)
                .ApplyCombatDeath(people[index], new(null), 0, destroyAssociatedArmy: true,
                    report: true, history: true, suppressReports: false);
            state = death.State;
            if (death.NativeReturn is null)
            {
                if (state.PersonDeathContinuation is null) throw StoryListMissing("death_succession_owner");
                state = PublishNormalTurnCurrent(state, current, pool);
                state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
                return new(state, null, new(index + 1, true));
            }
            if (manager)
            {
                state = ClearRecordingGate(state, current);
                classification.RecordDeath(people[index]);
            }
        }
        //237 drains once after the complete ordered list, not once per person.
        state = ConsumeArmyLifecyclePendingCurrent(state, current, pool)
            ?? throw StoryListMissing("death_pending_cleanup");
        state = PublishNormalTurnCurrent(state, current, pool);
        state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
        return new(state, 1);

        static GameState ClearRecordingGate(GameState value, SourceWorldContext owner)
        {
            owner.ReportBlockerState = new(true, 0);
            return value with { CampaignEventEligibility =
                (value.CampaignEventEligibility ?? throw StoryListMissing("death_manager_after_owner"))
                    with { HistoryRecordingSuppressed = false } };
        }
    }

    private static T StoryListNeed<T>(T? value, string input) where T : struct
        => value ?? throw StoryListMissing(input);
    private static InvalidDataException StoryListMissing(string input) => new("story.person_list.current." + input);

    //3D7170 clears this manager before the whole invocation. Its occurrences
    //then survive every presentation slice until the actual result page.
    private sealed class StoryPersonMutationClassification(SourceWorldContext current, CampaignStoryOutcomes? previous)
    {
        internal CampaignStoryOutcomes? Current { get; private set; } = previous;

        private bool Eligible(int person) => StoryListNeed(current.ReadPersonNativeValidity(person), "classification_validity") &&
            (StoryListNeed(current.ReadOwnedReturnPersonFlags(person), "classification_flags") & 0x4000) == 0;
        private static ImmutableArray<int> Without(ImmutableArray<int> values, int person)
            => values.Where(value => value != person).ToImmutableArray();
        private static ImmutableArray<CampaignStoryForcePeople> Without(ImmutableArray<CampaignStoryForcePeople> groups, int person)
            => groups.Select(group => group with { PersonPhysicalSlots = Without(group.PersonPhysicalSlots, person) }).ToImmutableArray();
        private CampaignStoryOutcomes RemoveOther(CampaignStoryOutcomes value, int person) => value with
        {
            FreePersonSlots = Without(value.FreePersonSlots, person),
            CapturedByForce = Without(value.CapturedByForce, person),
            JoinedByForce = Without(value.JoinedByForce, person),
            MeritPersonSlots = Without(value.MeritPersonSlots, person)
        };
        //3DFDC0 clears these six Person occurrence collections only. Status,
        //item gains, alliances and Base occurrences retain their original entries.
        internal void RemovePersonResults(int person)
        {
            if (!Eligible(person) || Current is not { } value) return;
            Current = RemoveOther(value, person) with
            { DeathPersonSlots = Without(value.DeathPersonSlots, person),
                RaisedArmyPersonSlots = Without(value.RaisedArmyPersonSlots, person) };
        }
        internal void RecordCurrentPlayerCityIncrease(int kind)
        {
            if (Current is { } value) Current = value with { CurrentPlayerCityIncreaseKind = kind };
        }
        internal void ClearAllianceResults()
        {
            if (Current is { } value) Current = value with
            { AlliancesCreated = [], AlliancesExtended = [], AlliancesCancelled = [] };
        }
        internal void ClearJoinedResultsForForce(ForceReference force)
        {
            if (Current is not { } value) return;
            int slot = StoryListNeed(force.PhysicalSlot, "clear_joined_Force_pointer");
            Current = value with { JoinedByForce = value.JoinedByForce.Select(group =>
                group.ForcePhysicalSlot == slot ? group with { PersonPhysicalSlots = [] } : group).ToImmutableArray() };
        }
        internal void RecordCaptured(ForceReference force, int person)
        {
            if (!StoryListNeed(current.ReadForceNativeValidity(force), "capture_Force_validity") ||
                !Eligible(person) || Current is not { } value) return;
            int slot = StoryListNeed(force.PhysicalSlot, "capture_Force_pointer");
            //3D65A0 does not erase death, raised-army or merit occurrences.
            var changed = value with { FreePersonSlots = Without(value.FreePersonSlots, person),
                CapturedByForce = Without(value.CapturedByForce, person),
                JoinedByForce = Without(value.JoinedByForce, person) };
            var groups = changed.CapturedByForce.ToBuilder();
            int at = 0;
            while (at < groups.Count && groups[at].ForcePhysicalSlot < slot) at++;
            if (at < groups.Count && groups[at].ForcePhysicalSlot == slot)
                groups[at] = groups[at] with { PersonPhysicalSlots = groups[at].PersonPhysicalSlots.Add(person) };
            else groups.Insert(at, new(slot, [person]));
            Current = changed with { CapturedByForce = groups.ToImmutable() };
        }
        internal void RecordDeath(int person)
        {
            if (!Eligible(person) || Current is not { } value) return;
            int identity = StoryListNeed(current.ReadPersonIdentity(person), "classification_death_identity");
            var status = value.StatusChanges.Select(change =>
                StoryListNeed(current.ReadPersonNativeValidity(change.PersonPhysicalSlot), "classification_status_validity") &&
                StoryListNeed(current.ReadPersonIdentity(change.PersonPhysicalSlot), "classification_status_identity") == identity
                    ? change with { Status = 255 } : change).ToImmutableArray();
            Current = RemoveOther(value, person) with
            { DeathPersonSlots = Without(value.DeathPersonSlots, person).Add(person), StatusChanges = status };
        }
        internal void RecordFreed(int person)
        {
            if (!Eligible(person) || Current is not { } value) return;
            var changed = RemoveOther(value, person);
            Current = changed with { FreePersonSlots = changed.FreePersonSlots.Add(person) };
        }
        internal void RecordJoined(ForceReference force, int person)
        {
            if (!StoryListNeed(current.ReadForceNativeValidity(force), "classification_Force_validity") ||
                !Eligible(person) || Current is not { } value) return;
            int slot = StoryListNeed(force.PhysicalSlot, "classification_Force_pointer");
            var changed = RemoveOther(value, person);
            var groups = changed.JoinedByForce.ToBuilder();
            int index = -1;
            for (int i = 0; i < groups.Count; i++) if (groups[i].ForcePhysicalSlot == slot) { index = i; break; }
            if (index >= 0) groups[index] = groups[index] with { PersonPhysicalSlots = groups[index].PersonPhysicalSlots.Add(person) };
            else
            {
                //Keep deterministic receiver-slot ordering. Equivalence with
                //the original heap-pointer map order remains a display check;
                //no displayed Force name or logical identity is used here.
                int at = 0;
                while (at < groups.Count && groups[at].ForcePhysicalSlot < slot) at++;
                groups.Insert(at, new(slot, [person]));
            }
            Current = changed with { JoinedByForce = groups.ToImmutable() };
        }
        internal void RecordAlliance(ForceReference first, ForceReference second, StoryForceRelationOutcome outcome)
        {
            if (Current is not { } value) return;
            var pair = new CampaignStoryForcePair(StoryListNeed(first.PhysicalSlot, "alliance_first_pointer"),
                StoryListNeed(second.PhysicalSlot, "alliance_second_pointer"));
            value = value with
            {
                AlliancesCreated = RemoveFirst(value.AlliancesCreated, pair),
                AlliancesExtended = RemoveFirst(value.AlliancesExtended, pair),
                AlliancesCancelled = RemoveFirst(value.AlliancesCancelled, pair)
            };
            Current = outcome switch
            {
                StoryForceRelationOutcome.AllianceCreated => value with { AlliancesCreated = value.AlliancesCreated.Add(pair) },
                StoryForceRelationOutcome.AllianceExtended => value with { AlliancesExtended = value.AlliancesExtended.Add(pair) },
                StoryForceRelationOutcome.AllianceDestroyed => value with { AlliancesCancelled = value.AlliancesCancelled.Add(pair) },
                _ => throw StoryListMissing("alliance_classification")
            };
        }
        internal void RecordTroopIncrease(BaseReference receiver)
        {
            if (Current is not { } value) return;
            int physical = StoryListNeed(receiver.PhysicalSlot, "troop_increase_Base_pointer");
            Current = value with { TroopIncreasedBases = value.TroopIncreasedBases.Add(new(receiver.Kind == BaseKind.Gate, physical)) };
        }
        internal void RecordItemGain(int person, int item)
        {
            if (Current is not { } value) return;
            var groups = value.ItemsGainedByPerson.ToBuilder();
            int index = -1;
            for (int i = 0; i < groups.Count; i++) if (groups[i].PersonPhysicalSlot == person) { index = i; break; }
            if (index >= 0) groups[index] = groups[index] with { ItemPhysicalSlots = groups[index].ItemPhysicalSlots.Add(item) };
            else
            {
                int at = 0;
                while (at < groups.Count && groups[at].PersonPhysicalSlot < person) at++;
                groups.Insert(at, new(person, [item]));
            }
            Current = value with { ItemsGainedByPerson = groups.ToImmutable() };
        }
        internal void RecordStatus(int person, int status)
        {
            if (Current is not { } value) return;
            var changes = value.StatusChanges.ToBuilder();
            int at = 0;
            while (at < changes.Count && changes[at].PersonPhysicalSlot < person) at++;
            if (at < changes.Count && changes[at].PersonPhysicalSlot == person)
            {
                //3D4C30 retains the first nonzero value. Zero is still a
                //real entry and can be assigned by a later notification.
                if (changes[at].Status != 0) return;
                changes[at] = changes[at] with { Status = status };
            }
            else changes.Insert(at, new(person, status));
            Current = value with { StatusChanges = changes.ToImmutable() };
        }
        internal void RecordRaisedArmy(int person)
        {
            if (!Eligible(person) || Current is not { } value) return;
            //3D6540 clears this Person only from merit, then appends a real
            //raising-army occurrence. The other result categories are retained.
            Current = value with { MeritPersonSlots = Without(value.MeritPersonSlots, person),
                RaisedArmyPersonSlots = value.RaisedArmyPersonSlots.Add(person) };
        }
        internal void RecordMerit(int person)
        {
            if (Current is { } value) Current = value with { MeritPersonSlots = value.MeritPersonSlots.Add(person) };
        }
        private static ImmutableArray<CampaignStoryForcePair> RemoveFirst(ImmutableArray<CampaignStoryForcePair> values,
            CampaignStoryForcePair pair)
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] == pair || values[i].FirstForcePhysicalSlot == pair.SecondForcePhysicalSlot &&
                    values[i].SecondForcePhysicalSlot == pair.FirstForcePhysicalSlot) return values.RemoveAt(i);
            return values;
        }
    }
}
