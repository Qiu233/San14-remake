using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary26DA10 State tail, with the caller's actual
    /// param6 and event-playback presence. Keeps all mutations and reports
    /// inside the existing capture/transfer operation.</summary>
    internal GameState ApplyCityCaptureStateAdvantageCurrent(GameState state,
        SourceWorldContext current, CityCaptureAreaScope scope, NativeMapSeasonDate? date,
        List<CampaignEffectRequest> effects, bool suppressPrimary = false, bool eventPlaybackPresent = false)
    {
        var transition = CityCaptureTerritorySource.RequireNoSpecialStateAdvantageTransition(state, current, scope);
        if (transition is null || !transition.Gained && !transition.Lost) return state;
        //The immediate flags and journey work above has already run. Record
        //the actual invocation before a missing/suppressed special effect can
        //return; ordinary province notifications do not require that effect.
        if (eventPlaybackPresent)
        {
            var oldForce = current.ReadForceTableReference(scope.OldForcePhysical is >= 0 and <= 51 ? scope.OldForcePhysical : 0)
                ?? throw StateAdvantageMissing("story_old_Force_pointer");
            if (transition.Gained || transition.Lost && StateAdvantageNeed(
                current.ReadForceNativeValidity(oldForce), "story_old_Force_validity"))
            {
                var newForce = current.ReadForceTableReference(scope.NewForcePhysical is >= 0 and <= 51 ? scope.NewForcePhysical : 0)
                    ?? throw StateAdvantageMissing("story_new_Force_pointer");
                int? gain = transition.Gained || !StateAdvantageNeed(current.ReadForceNativeValidity(newForce),
                    "story_new_Force_validity") && transition.RequiredCities == 1
                    ? StateAdvantageNeed(current.ReadForcePhysicalSlot(newForce), "story_new_Force_identity") : null;
                int? loss = transition.Lost
                    ? StateAdvantageNeed(current.ReadForcePhysicalSlot(oldForce), "story_old_Force_identity") : null;
                state = RecordCityCaptureStoryStateControlCurrent(state, transition.StatePhysical, gain, loss);
            }
        }
        var special = CityCaptureTerritorySource.ReadStateAdvantageSpecialForce(state, current, transition);
        if (special.Force is null || special.Suppressed) return state;
        var pool = current.ReadOwnedCurrentListPool() ?? throw StateAdvantageMissing("current_pool");
        var working = state;
        int family = StateAdvantageNeed(current.ReadForcePhysicalSlot(special.Force), "special_Force_identity");
        if (transition.Lost)
        {
            var people = current.CreateTemporaryHexList() ?? throw StateAdvantageMissing("recall_Person_descriptor");
            try
            {
                current.ClearTemporaryHexList(people);
                //2B0290(special,people,0): native lookup order and actual
                //pointed-to identities, not current-force grouping.
                if (family is >= 46 and <= 50)
                    for (int lookup = 4101; lookup <= 4200; lookup++)
                    {
                        if ((lookup - 4101) % 20 < 10) continue;
                        int person = StateAdvantageNeed(current.ReadPersonLookupReference(lookup)?.PhysicalSlot, "recall_Person_lookup");
                        if (!StateAdvantageNeed(current.ReadPersonNativeValidity(person), "recall_Person_validity") ||
                            StateAdvantageNeed(current.ReadPersonStatus(person), "recall_PersonE6") is not (>= 1 and <= 4)) continue;
                        int identity = StateAdvantageNeed(current.ReadPersonIdentity(person), "recall_Person10");
                        int actual = identity is >= 4001 and <= 4030 ? 46 + (identity - 4001) / 6 :
                            identity is >= 4101 and <= 4200 ? 46 + (identity - 4101) / 20 : 0;
                        if (actual == family) current.AppendPersonReturnTemporary(people, person);
                    }
                working = ApplySpecialPersonnelRecallCurrent(working, current, pool, people,
                    conditional: false, requireValidOriginalForce: !eventPlaybackPresent, suppressPrimary, date, effects);
            }
            finally { current.ClearTemporaryHexList(people); current.ReleaseTemporaryHexList(people); }
        }
        if (transition.Gained)
        {
            var ordinary = current.ReadForceTableReference(scope.NewForcePhysical is >= 0 and <= 51 ? scope.NewForcePhysical : 0)
                ?? throw StateAdvantageMissing("gained_Force_pointer");
            if (StateAdvantageNeed(current.ReadForceNativeValidity(ordinary), "gained_Force_validity"))
            {
                int level = ReadDiplomacyRelationCurrent(working, current, scope.NewForcePhysical, family).Level;
                if (level > 3)
                    working = new MonthlyCampaignMaintenanceSource(this, working, current, pool, borrowedEffects: effects)
                        .ApplyCaptureStateAdvantagePersonnel(ordinary, special.Force, level);
            }
        }
        return working;
    }

    private GameState RecordCityCaptureStoryStateControlCurrent(GameState state, int province, int? gain, int? loss)
    {
        var turn = state.NativeNormalTurnContinuation;
        var command = state.CampaignEventCommandPlayback;
        var invocation = turn?.StoryPlayback ?? command?.Invocation;
        if (turn?.StoryPlayback is not null && command is not null)
            throw StateAdvantageMissing("story_conflicting_invocations");
        if (invocation is null)
        {
            if (state.EditedEventPlayback is { } edited)
            {
                if (edited.Phase != EditedEventPlaybackPhase.Results ||
                    edited.Source.ResourceUri != "san14://user/edited-events" || edited.Story.Source != edited.Source)
                    throw StateAdvantageMissing("edited_story_invocation");
                var control = edited.StateControl ?? throw StateAdvantageMissing("edited_story_state_control");
                if (control.IsSourceTimeUnavailable)
                    throw StateAdvantageMissing("edited_story_state_control_source_time_unavailable");
                return state with { EditedEventPlayback = edited with { StateControl = control.Record(province, gain, loss) } };
            }
            throw StateAdvantageMissing("story_invocation");
        }
        RequireStorySource(invocation.Story);
        var progress = invocation.StateControl ?? throw StateAdvantageMissing("story_state_control");
        if (progress.IsSourceTimeUnavailable)
            throw StateAdvantageMissing("story_state_control_source_time_unavailable");
        var source = invocation.Story.Source;
        long sequence = invocation.Story.Sequence;
        NormalTurnStoryPlayback Updated(NormalTurnStoryPlayback actual)
        {
            if (actual.Story.Source != source || actual.Story.Sequence != sequence)
                throw StateAdvantageMissing("story_invocation_changed");
            return actual with { StateControl = progress.Record(province, gain, loss) };
        }
        if (turn?.StoryPlayback is { } normal)
            return state with { NativeNormalTurnContinuation = turn with { StoryPlayback = Updated(normal) } };
        if (command is not null)
            return state with { CampaignEventCommandPlayback = command with { Invocation = Updated(command.Invocation) } };
        throw StateAdvantageMissing("story_invocation");
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        internal GameState ApplyCaptureStateAdvantagePersonnel(ForceReference ordinary, ForceReference special, int level)
        {
            ForceRelationChangeCurrent.ApplyInitialSpecialPersonnel(() => state, current, ordinary, special,
                level, P, ConsumeSpecialPersonnel, package.ReadApplicationGameplay(),
                count => Narration(Format(0x324C, new(Force0: Physical(special), Integers: [count]))),
                removedPeople: RecallSpecialPersonnel);
            return state;
        }
    }

    private static T StateAdvantageNeed<T>(T? value, string field) where T : struct => value ?? throw StateAdvantageMissing(field);
    private static InvalidDataException StateAdvantageMissing(string field) => new("city-capture.state-advantage." + field);
}
