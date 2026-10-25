using San14.Core;

namespace San14.Data;

public sealed record StoryStandingRegistration(int Slot, int? OfficerId);
public sealed record StoryActorPublication(int Slot, TalentOfficerPresentationContext Speaker,
    AreaAdministrationQuery<DeploymentNarrationPortraitRoute> StandingPortrait,
    OfficerPortraitRoute? FacePortrait, string? UnresolvedOwner);
public sealed record StoryPresentationPublication(long Sequence, int Step,
    CampaignStoryPreparedText? Text, IReadOnlyList<StoryStandingRegistration> RegisteredCharacters,
    IReadOnlyList<StoryActorPublication> Actors, int? HighlightedSlot)
{
    /// <summary>False when Text is the retained, already consumed scene line.
    /// A non-text step must not present that line as a newly prepared message.</summary>
    public bool HasCurrentPreparedText { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One source snapshot for the story owner. Registration does
    /// not reveal actors, and publication does not acknowledge a displayed step.</summary>
    public AreaAdministrationQuery<StoryPresentationPublication> QueryStoryPresentation(
        GameState state, CampaignStoryRequest story)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(story);
        try
        {
            if (story.Scene is null || story.NextStep < 0 || story.NextStep > story.Steps.Length)
                throw StoryPortraitMissing("request_cursor");
            if (story.PreparedStep is { } stale && stale.Step != story.NextStep)
                throw StoryPortraitMissing("prepared_step_identity");
            CampaignStoryStep? step = story.NextStep < story.Steps.Length ? story.Steps[story.NextStep] : null;
            var prepared = story.PreparedStep?.Text;
            bool currentText = prepared is not null && step is StoryMessage or StoryNarration;
            var text = prepared ?? story.Scene.PreparedText;
            //106/107 only register. Their pending step is useful to the view
            //without predicting a world mutation or showing these characters.
            var standing = step as StoryStanding ?? story.Scene.Standing;
            var registrations = StoryPortraitRegistrations(standing);
            var actors = new SortedDictionary<int, TalentOfficerPresentationContext>();
            foreach (var pair in story.Scene.VisibleActors)
            {
                if (pair.Key is < 0 or > 4 || pair.Value is null) throw StoryPortraitMissing("scene_actor_slot");
                actors.Add(pair.Key, pair.Value);
            }
            int? highlighted = story.Scene.HighlightedSlot;
            if (currentText && step is StoryNarration) highlighted = null;
            else if (currentText && step is StoryMessage message && prepared?.Speaker is { } speaker)
            {
                //8457A0 uses the registered source Person operand. An
                //unregistered speaker occupies0, replacing only that actor.
                int slot = 0;
                foreach (var registration in registrations)
                    if (registration.OfficerId == message.Speaker) { slot = registration.Slot; break; }
                actors[slot] = speaker;
                highlighted = slot;
            }
            if (highlighted is < 0 or > 4) throw StoryPortraitMissing("highlighted_slot");

            SourceWorldContext? current = null;
            string? captureFailure = null;
            if (actors.Count != 0)
            {
                try
                {
                    current = CaptureDeploymentPersonSelectionCurrent(state);
                    current.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle() ?? throw StoryPortraitMissing("army_lifecycle"));
                    OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
                }
                catch (InvalidDataException error) { captureFailure = error.Message; current = null; }
            }
            var publication = actors.Select(pair => StoryPortraitActor(state, current, pair.Key,
                pair.Value, captureFailure)).ToArray();
            return AreaAdministrationQuery<StoryPresentationPublication>.Known(new(story.Sequence,
                story.NextStep, text, registrations, publication, highlighted) { HasCurrentPreparedText = currentText });
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<StoryPresentationPublication>.Unknown(error.Message); }
    }

    private static StoryStandingRegistration[] StoryPortraitRegistrations(StoryStanding? standing)
    {
        if (standing is null) return [];
        if (standing.Characters is not { Length: 2 or 3 }) throw StoryPortraitMissing("standing_registration_count");
        int[] slots = standing.Characters.Length == 2 ? [3, 4] : [1, 0, 2];
        var result = new StoryStandingRegistration[slots.Length];
        for (int index = 0; index < slots.Length; index++)
        {
            var character = standing.Characters[index];
            if (character is null || character.Available != character.CharacterId.HasValue)
                throw StoryPortraitMissing("standing_registration_identity");
            result[index] = new(slots[index], character.Available ? character.CharacterId : null);
        }
        return result;
    }

    private StoryActorPublication StoryPortraitActor(GameState state, SourceWorldContext? current,
        int slot, TalentOfficerPresentationContext speaker, string? captureFailure)
    {
        try
        {
            if (current is null) throw new InvalidDataException(captureFailure ?? "story.presentation.current");
            if (speaker.OfficerId is < 1 or > 6000 || speaker.NameAtSource is null)
                throw StoryPortraitMissing("source_speaker");
            ushort appearance = speaker.PortraitAppearanceIndex ?? throw StoryPortraitMissing("source_appearance");
            //8457A0→8433A0→842D70 always selects the three-part layout by
            //the source Person's A6, including Person5012. The separate serif
            //Face2 special case does not apply to story actors. Retain the
            //source-time appearance after world changes; animated dispatch
            //remains concretely unknown rather than substituting a face image.
            var portrait = ReadNarrationPortrait(state, current, _definitions, speaker.OfficerId,
                appearance, requireCurrentAppearance: false);
            return new(slot, speaker, AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Known(portrait), null, null);
        }
        catch (InvalidDataException error)
        {
            return new(slot, speaker, AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Unknown(error.Message), null, error.Message);
        }
    }

    private static InvalidDataException StoryPortraitMissing(string field) => new("story.presentation." + field);
}
