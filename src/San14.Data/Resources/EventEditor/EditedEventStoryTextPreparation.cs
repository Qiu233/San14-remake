using System.Collections.Immutable;
using System.Text;
using San14.Core;

namespace San14.Data;

/// <summary>The editor's plain article formatter. It counts UTF-16 source
/// characters, retaining at most three lines of twenty full-width cells.</summary>
public static class EditedEventArticleText
{
    public static string Format(string text)
        => FormatCore(text, insertAutomaticNewLines: true);

    /// <summary>642C50's actual article-confirmation value: first sixty
    /// UTF-16 characters, followed by the source formatter's mode zero.</summary>
    public static string NormalizeForAuthoring(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return FormatCore(text.Length > 60 ? text[..60] : text, insertAutomaticNewLines: false);
    }

    private static string FormatCore(string text, bool insertAutomaticNewLines)
    {
        ArgumentNullException.ThrowIfNull(text);
        var output = new StringBuilder();
        int line = 0, halfCells = 0;
        foreach (char character in text)
        {
            if (character == '\0') break;
            int width = character is >= '\x20' and <= '\x7e' or >= '\uff61' and <= '\uff9f' ? 1 : 2;
            halfCells += width;
            if (character == '\n')
            {
                halfCells = 0;
                if (++line >= 3) break;
            }
            else if (halfCells > 40)
            {
                if (++line >= 3) break;
                if (insertAutomaticNewLines) output.Append('\n');
                halfCells = width;
            }
            output.Append(character);
        }
        return output.ToString();
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Prepare one actual edited article. This is independent of the
    /// normal Game-message VM and does not consume application randomness.</summary>
    public CampaignStoryPreparationResult PrepareEditedEventStoryStep(GameState candidate,
        CampaignStoryRequest story, EditedEventLibrary library)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(library);
        try
        {
            if ((uint)story.NextStep >= (uint)story.Steps.Length)
                throw new InvalidDataException("edited_event.text.step");
            if (story.PreparedStep is { } cached && cached.Step == story.NextStep)
                return new(cached, candidate.CampaignAppRandom32, null);
            var step = story.Steps[story.NextStep];
            if (step is not (StoryMessage or StoryNarration))
                return new(new(story.NextStep, null), candidate.CampaignAppRandom32, null);
            int slot = step is StoryMessage message ? message.MessageId : ((StoryNarration)step).MessageId;
            //39E250 returns an empty source string for an absent/invalid slot.
            string text = EditedEventArticleText.Format(library.FindMessage(slot)?.Text ?? string.Empty);
            TalentOfficerPresentationContext? speaker = null;
            string name = string.Empty;
            if (step is StoryMessage personMessage)
            {
                var current = CaptureDeploymentPersonSelectionCurrent(candidate);
                var pointer = current.ReadPersonLookupReference(personMessage.Speaker)
                    ?? throw new InvalidDataException("edited_event.text.speaker_lookup");
                int physical = pointer.PhysicalSlot
                    ?? throw new InvalidDataException("edited_event.text.speaker_pointer");
                int identity = current.ReadPersonIdentity(physical)
                    ?? throw new InvalidDataException("edited_event.text.speaker_identity");
                var person = candidate.Officers.GetValueOrDefault(identity)
                    ?? throw new InvalidDataException("edited_event.text.speaker_owner");
                name = (person.CurrentSurname ?? throw new InvalidDataException("edited_event.text.surname")) +
                    (person.CurrentGivenName ?? throw new InvalidDataException("edited_event.text.given_name"));
                speaker = new(personMessage.Speaker, name, current.ReadPersonPortraitAppearance(physical));
            }
            var prepared = new CampaignStoryPreparedText(name, text, ImmutableArray.Create(text), speaker, []);
            return new(new(story.NextStep, prepared), candidate.CampaignAppRandom32, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}
