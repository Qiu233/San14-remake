using San14.Core;

namespace San14.Data;

internal sealed record StoryPersonListMovementResult(GameState State, IReadOnlyList<int>? OrderedPeople);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal StoryPersonListMovementResult ApplyStoryPersonListMovementCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> orderedPhysicals,
        int? targetPersonPhysical, BaseReference? targetBase,
        Action<ForceReference, int>? recordJoined = null, Action<int>? recordFreed = null)
        => new EventPersonMovementCurrent(this, state, current,
            new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)))
            .ApplyStory(code, orderedPhysicals, targetPersonPhysical, targetBase, recordJoined, recordFreed);

    private sealed partial class EventPersonMovementCurrent
    {
        private bool storyImmediateRetirement;
        private Action<ForceReference, int>? storyRecordJoined;
        private Action<int>? storyRecordFreed;

        internal StoryPersonListMovementResult ApplyStory(int code, IReadOnlyList<int> people,
            int? targetPerson, BaseReference? targetBase,
            Action<ForceReference, int>? recordJoined, Action<int>? recordFreed)
        {
            storyImmediateRetirement = true;
            storyRecordJoined = recordJoined;
            storyRecordFreed = recordFreed;
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            try
            {
                IReadOnlyList<int>? sorted = code switch
                {
                    234 => ChangeStoryForce(people, targetPerson ?? throw Missing("story_target_Person")),
                    235 => ChangeStoryHome(people, targetBase ?? throw Missing("story_target_Base")),
                    238 => FreeStoryPeople(people),
                    _ => throw Missing("story_unexpected_opcode")
                };
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw Missing("effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("effect_calendar"), effects.ToArray()) };
                return new(latest, sorted);
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }

        private IReadOnlyList<int>? ChangeStoryForce(IReadOnlyList<int> people, int target)
        {
            if (!Need(current.ReadPersonNativeValidity(target), "story_target_Person_validity")) return null;
            byte rawDistrict = Need(current.ReadPersonDistrictSlot(target), "story_target_PersonE0");
            var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
                ?? throw Missing("story_target_District");
            var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(target), "story_target_PersonE2"), current);
            if (people.Count == 0) return null;
            BindInputs();
            foreach (int person in people)
            {
                if (!Need(current.ReadPersonNativeValidity(person), "story_changed_Person_validity") ||
                    DuelAftermathSpecialPerson(current, person)) continue;
                SplitTravelGroup(person); //26C580 then26CAF0, even before the flag14/status5 skip.
                ushort flags = Need(current.ReadArmyReturnPersonFlags196(person), "story_changed_Person15E");
                if ((flags & 0x4000) == 0)
                {
                    var joined = rules.ApplyRecruitJoinCurrent(latest, current, person, district, home, Date, Day, effects);
                    latest = joined.State; //Native return0 still retains any genuine prefix stores.
                }
                else
                {
                    if (Need(current.ReadPersonStatus(person), "story_appendix_PersonE6") == 5) continue;
                    FreeStoryPerson(person);
                }
                if (Need(current.ReadPersonStatus(person), "story_changed_fresh_PersonE6") is >= 1 and <= 4)
                    SnapIdlePosition(person);
                if (Need(current.ReadEventManagerPresent(), "story_event_manager_presence"))
                {
                    int districtPhysical = Need(district.PhysicalSlot, "story_target_District_pointer");
                    byte forceSlot = Need(current.ReadDistrictForceId(districtPhysical), "story_target_District10");
                    var force = current.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0)
                        ?? throw Missing("story_target_Force_pointer");
                    (storyRecordJoined ?? throw Missing("story_event_manager_Force_change_recorder"))(force, person);
                }
            }
            Publish();
            return null;
        }

        private IReadOnlyList<int>? ChangeStoryHome(IReadOnlyList<int> people, BaseReference target)
        {
            if (!Need(current.ReadBaseNativeValidity(target), "story_target_Base_validity")) return null;
            BindInputs();
            //3D7A40 completes each entire pass before starting the next one.
            foreach (int person in people)
            {
                var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                    ?? throw Missing("story_home_commander_Army");
                if (Need(current.ReadArmyUnitNativeValidity(army), "story_home_Army_validity"))
                    ReturnArmy(army, target, 1);
            }
            ConsumePending();
            foreach (int person in people) ResetAssociated(person);
            var sorted = MovePeople(people, target);
            Publish();
            return sorted;
        }

        private IReadOnlyList<int>? FreeStoryPeople(IReadOnlyList<int> people)
        {
            if (people.Count == 0) return null;
            BindInputs();
            foreach (int person in people)
            {
                //238 deliberately allows an invalid Person to reach265250.
                //Only a valid, conditionally protected band person is skipped.
                if (Need(current.ReadPersonNativeValidity(person), "story_free_Person_validity") &&
                    DuelAftermathSpecialPerson(current, person)) continue;
                SplitForeignTravelGroup(person); //26C580 only; no26CAF0 Return retirement.
                ushort flags = Need(current.ReadArmyReturnPersonFlags196(person), "story_free_Person15E");
                if ((flags & 0x4000) != 0 && Need(current.ReadPersonStatus(person), "story_free_PersonE6") == 5) continue;
                FreeStoryPerson(person);
                if (Need(current.ReadEventManagerPresent(), "story_event_manager_presence"))
                    (storyRecordFreed ?? throw Missing("story_event_manager_freedom_recorder"))(person);
            }
            Publish();
            return null;
        }

        private void FreeStoryPerson(int person)
            => latest = rules.ApplyCaptiveFreedomCurrent(latest, current, Pool, person, Date, Day,
                () => rules.ReadArmyDestructionSampler(latest, current) ?? throw Missing("story_freedom_sampler"), effects);

        private void SplitForeignTravelGroup(int person)
        {
            if (!Need(current.ReadPersonNativeValidity(person), "story_split_Person_validity")) return;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("story_split_association");
            if (Need(current.ReadLinkedEntityNativeValidity(linked), "story_split_association_validity") && Kind(linked) is 17 or 18)
                SplitForeignTradeGroup(linked);
        }

        private IReadOnlyList<int> MovePeople(IReadOnlyList<int> people, BaseReference target)
        {
            var moved = PersonReturnArrivalSource.ApplyStoryPeopleCurrent(latest, current, Pool, people, target,
                new PersonReturnGovernorQueries(latest, rules._governorInputs, current),
                (state, person, assignment) => rules.ApplyPersonAssociationRemovalReports(state, current,
                    person, assignment, Day, effects), rules, Date);
            latest = moved.State ?? throw Missing(moved.UnresolvedOwner ?? "story_home_271320");
            //An empty input genuinely returns before allocating affected Bases.
            return moved.Receipt?.SortedPersonPhysicals ?? (people.Count == 0 ? [] :
                throw Missing("story_home_sorted_people"));
        }
    }
}
