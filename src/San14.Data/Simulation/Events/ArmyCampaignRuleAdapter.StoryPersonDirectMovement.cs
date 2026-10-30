using San14.Core;

namespace San14.Data;

internal sealed record StoryPersonDirectMovementResult(GameState State, int NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary153..156 callers of the shared person movement kernels.
    /// The invocation resolves operands in source order; editor eligibility,
    /// command admission and the editor's additional post-move tail do not apply.</summary>
    internal StoryPersonDirectMovementResult ApplyStoryPersonDirectMovementCurrent(GameState state,
        SourceWorldContext current, int code, int? firstPerson = null, int? secondPerson = null,
        BaseReference? firstBase = null, BaseReference? secondBase = null,
        ForceReference? targetForce = null, Action<ForceReference, int>? recordJoined = null)
        => new EventPersonMovementCurrent(this, state, current,
            new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)))
            .ApplyStoryDirect(code, firstPerson, secondPerson, firstBase, secondBase, targetForce, recordJoined);

    private sealed partial class EventPersonMovementCurrent
    {
        internal StoryPersonDirectMovementResult ApplyStoryDirect(int code, int? firstPerson,
            int? secondPerson, BaseReference? firstBase, BaseReference? secondBase,
            ForceReference? targetForce, Action<ForceReference, int>? recordJoined)
        {
            storyImmediateRetirement = true;
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            try
            {
                switch (code)
                {
                    case 153:
                        ChangeStoryDirectForce(firstPerson ?? throw Missing("story_direct_Person"),
                            targetForce ?? throw Missing("story_direct_Force"), recordJoined);
                        break;
                    case 154:
                    {
                        var target = firstBase ?? throw Missing("story_direct_Base");
                        int person = firstPerson ?? throw Missing("story_direct_Person");
                        if (Need(current.ReadBaseNativeValidity(target), "story_direct_Base_validity") &&
                            Need(current.ReadPersonNativeValidity(person), "story_direct_Person_validity"))
                            MoveStoryDirectPeople([person], target);
                        break;
                    }
                    case 155:
                    {
                        var origin = firstBase ?? throw Missing("story_direct_origin_Base");
                        var target = secondBase ?? throw Missing("story_direct_target_Base");
                        if (Need(current.ReadBaseNativeValidity(origin), "story_direct_origin_validity") &&
                            Need(current.ReadBaseNativeValidity(target), "story_direct_target_validity"))
                            MoveStoryDirectPeople(ReadStoryDirectHomePeople(origin), target);
                        break;
                    }
                    case 156:
                    {
                        int person = firstPerson ?? throw Missing("story_direct_Person");
                        int other = secondPerson ?? throw Missing("story_direct_other_Person");
                        if (!Need(current.ReadPersonNativeValidity(person), "story_direct_Person_validity") ||
                            !Need(current.ReadPersonNativeValidity(other), "story_direct_other_validity")) break;
                        var target = BaseLookup.Find(Need(current.ReadPersonFirstLocation(other),
                            "story_direct_other_E2"), current);
                        if (Need(current.ReadBaseNativeValidity(target), "story_direct_other_Home_validity"))
                            MoveStoryDirectPeople([person], target);
                        break;
                    }
                    default: throw Missing("story_direct_opcode");
                }
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw Missing("effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("effect_calendar"), effects.ToArray()) };
                return new(latest, 1);
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }

        private void ChangeStoryDirectForce(int person, ForceReference target,
            Action<ForceReference, int>? recordJoined)
        {
            BindInputs();
            byte status = Need(current.ReadPersonStatus(person), "story_direct_force_PersonE6");
            if (status is 5 or 7 or 8)
            {
                //The ordinary caller invokes29E080 directly, without the
                //editor's3D8050 or extra employed-person destruction tail.
                latest = rules.JoinNormalTurnSelfRecommendationCurrent(latest, current, person,
                    CurrentGateMapQueries.FindCity(ForceHome(target), current));
            }
            else
            {
                //Both26C580 and26CAF0 have their own native Person gate.
                //3ACC20 itself deliberately has no such outer gate.
                if (Need(current.ReadPersonNativeValidity(person), "story_direct_split_Person_validity"))
                    SplitTravelGroup(person);
                var home = ForceHome(target);
                var district = ForcePrimaryDistrictSource.Resolve(current, target);
                var joined = rules.ApplyRecruitJoinCurrent(latest, current, person, district,
                    home, Date, Day, effects);
                latest = joined.State;
                if (Need(current.ReadPersonStatus(person), "story_direct_force_fresh_E6") is >= 1 and <= 4)
                    SnapIdlePosition(person);
            }
            if (Need(current.ReadEventManagerPresent(), "story_direct_event_manager_presence"))
                (recordJoined ?? throw Missing("story_direct_Force_change_recorder"))(target, person);
            Publish();
        }

        private IEnumerable<int> ReadStoryDirectHomePeople(BaseReference origin)
        {
            int home = Need(current.ReadBaseObjectLink(origin), "story_direct_origin_Base4E");
            if (home is < 1 or > 61) yield break;
            //234920/3346B0 completes the ordinary1..4 pass before its captive6
            //pass. Registration order and duplicate payloads are significant.
            for (int mode = 0; mode <= 1; mode++)
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw Missing("story_direct_Person100"))
                {
                    int person = Need(entry, "story_direct_Person100_payload");
                    if (Need(current.ReadPersonFirstLocation(person), "story_direct_iterator_E2") != home) continue;
                    byte status = Need(current.ReadPersonStatus(person), "story_direct_iterator_E6");
                    if (mode == 0 ? status is >= 1 and <= 4 : status == 6) yield return person;
                }
        }

        private void MoveStoryDirectPeople(IEnumerable<int> people, BaseReference target)
        {
            //The source owns this actual temporary until the complete3D7A40
            //passes finish; a failed node allocation omits only that payload.
            var caller = current.CreateTemporaryHexList() ?? throw Missing("story_direct_caller_descriptor");
            if (!caller.Identity.HasValue) throw Missing("story_direct_caller_allocation");
            try
            {
                var captured = new List<int>();
                foreach (int person in people)
                    if (current.AppendPersonReturnTemporary(caller, person)) captured.Add(person);
                _ = ChangeStoryHome(captured, target);
            }
            finally { Release(caller); }
        }
    }
}
