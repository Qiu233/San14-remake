using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual184..187 mutations over one invocation's ordered Force
    /// pointers. The ordinary caller publishes returned effects exactly once.</summary>
    internal (GameState State, int NativeReturn, IReadOnlyList<CampaignEffectRequest> Effects)
        ApplyStoryForceListRelationsCurrent(GameState state, SourceWorldContext current, int code,
            IReadOnlyList<int> arguments, ForceReference target, ForceReference? leader,
            IReadOnlyList<int> forcePhysicalSlots,
            Action<ForceReference, ForceReference, StoryForceRelationOutcome> recordOutcome)
    {
        if (arguments.Count != 8) throw StoryForceListMissing("arguments");
        ArgumentNullException.ThrowIfNull(recordOutcome);
        var effects = new List<CampaignEffectRequest>();
        _baseCapacityInputs.BindCurrent(state, current);
        WorldIndexContext Pool() => current.ReadOwnedCurrentListPool() ?? throw StoryForceListMissing("current_pool");
        bool Valid(ForceReference force) => StoryForceListNeed(current.ReadForceNativeValidity(force), "Force_validity");
        int Physical(ForceReference force) => StoryForceListNeed(current.ReadForcePhysicalSlot(force), "Force_identity");

        if (code == 186)
        {
            //288FC0 owns its real target/leader gates, optional leader prepend,
            //ordered member flags, pairwise friendship and map refresh intent.
            var head = leader ?? throw StoryForceListMissing("coalition_leader");
            if (Valid(target) && Valid(head))
                state = ApplyNormalTurnCoalitionActivationCurrent(state, current, Pool(), target, head,
                    forcePhysicalSlots, effects);
            return (state, 1, effects);
        }
        if (code is not (184 or 185 or 187)) throw StoryForceListMissing("opcode");
        if (Valid(target))
        {
            int targetId = Physical(target);
            foreach (int physical in forcePhysicalSlots)
            {
                var member = new ForceReference(physical);
                if (code is 184 or 185)
                {
                    if (physical == targetId) continue;
                    int amount = arguments[1];
                    if (code == 184)
                    {
                        int before = physical < 52 ? ReadEditedEventFriendship(state, current, target, physical) : 0;
                        amount = unchecked(amount - before);
                    }
                    //No added member-nativevalid or editor self/player gate:
                    //the real2704F0 kernel owns its own admission and tail.
                    state = ApplyNormalTurnForceRelationCurrent(state, current, Pool(), target, member, amount);
                }
                else if ((ReadReportForceRelationByte(state, member, targetId, StoryForceListMissing) & 127) != 0)
                {
                    state = new MonthlyCampaignMaintenanceSource(this, state, current, Pool())
                        .ApplyStoryAllianceEndCurrent(member, target);
                    //3D4990 is the original destroyed-alliance manager fact.
                    //It is separate from the successful world mutation.
                    if (StoryForceListNeed(current.ReadEventManagerPresent(), "event_manager_presence") &&
                        Valid(member) && Valid(target))
                        recordOutcome(member, target, StoryForceRelationOutcome.AllianceDestroyed);
                }
            }
        }
        if (code == 187)
        {
            //These are once-per-instruction tails, including invalid target
            //and empty list. Reusing pair opcode126 per member repeats them.
            state = RefreshEditedEventRegionStatistics(state, current);
            state = MaintainEditedEventAppointments(state, current);
            state = SynchronizeDiplomacyProjection(state);
            state = PublishNormalTurnCurrent(state, current, Pool());
        }
        else state = SynchronizeDiplomacyProjection(state);
        return (state, 1, effects);
    }

    private static T StoryForceListNeed<T>(T? value, string field) where T : struct =>
        value ?? throw StoryForceListMissing(field);
    private static InvalidDataException StoryForceListMissing(string field) => new("story.force_list." + field);
}
