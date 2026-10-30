using San14.Core;

namespace San14.Data;

internal sealed record StoryPersonHealthMutationResult(GameState State, int NativeReturn,
    IReadOnlyList<CampaignStoryPersonStatus> OrderedStatusChanges);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The five ordinary illness/recovery callers share the actual
    /// health owner, not the editor's additional Person eligibility rules.</summary>
    internal StoryPersonHealthMutationResult ApplyStoryPersonHealthCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, int? targetPerson,
        ForceReference? recoveryForce, IReadOnlyList<int> people)
    {
        if (arguments.Count != 8) throw StoryHealthMissing("arguments");
        if (code == 168)
        {
            var force = recoveryForce ?? throw StoryHealthMissing("recovery_Force");
            if (!Need(current.ReadForceNativeValidity(force), "recovery_Force_validity")) return new(state, 1, []);
            int forceId = Need(current.ReadForcePhysicalSlot(force), "recovery_Force_identity");
            if (forceId is < 1 or > 51) return new(state, 1, []);
            //2349D0(filter0) is the real registered-person chain with raw
            //Force membership then service status1..4, as in office commands.
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw StoryHealthMissing("recovery_Person_registry"))
            {
                int person = Need(entry, "recovery_Person_pointer");
                if (Need(current.ReadPersonOldForceSlot(person), "recovery_Person_Force") != forceId) continue;
                byte status = Need(current.ReadPersonStatus(person), "recovery_Person_status");
                if (status is < 1 or > 4) continue;
                if (Need(OfficerHealthSourceAdapter.ReadByte(current, person), "recovery_Person_health") != 0)
                    state = WritePersonHealthCurrent(state, current, person, 0);
            }
            return new(state, 1, []);
        }
        if (code == 260)
        {
            int requested = arguments[0];
            foreach (int person in people)
            {
                //The optional worsening-only comparison precedes the shared
                //writer's validity/value/environment/personality gates.
                if (arguments[1] == 1 && Need(OfficerHealthSourceAdapter.ReadByte(current, person), "list_Person_health") > requested)
                    continue;
                state = ApplyPersonIllnessCurrent(state, current, person, requested);
            }
            return new(state, 1, []);
        }
        if (code is not (259 or 449 or 450)) throw StoryHealthMissing("opcode");
        int target = targetPerson ?? throw StoryHealthMissing("selected_Person");
        state = ApplyPersonIllnessCurrent(state, current, target, arguments[1], skipPersonality: code == 450);
        if (code != 449) return new(state, 1, []);
        bool manager = Need(current.ReadEventManagerPresent(), "event_manager_presence");
        //Notification follows the attempted write even after a legitimate
        //no-op. Its payload is the original requested int, never final health.
        bool notify = manager && Need(current.ReadPersonNativeValidity(target), "summary_Person_validity") &&
            (Need(current.ReadOwnedReturnPersonFlags(target), "summary_Person_flags") & 0x4000) == 0;
        return new(state, 1, notify ? new[] { new CampaignStoryPersonStatus(target, arguments[1]) } : []);

        static T Need<T>(T? value, string owner) where T : struct => value ?? throw StoryHealthMissing(owner);
    }

    private static InvalidDataException StoryHealthMissing(string owner) => new("story.person_health.current." + owner);
}
