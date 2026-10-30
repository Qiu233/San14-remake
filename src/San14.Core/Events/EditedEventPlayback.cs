using System.Text.Json.Serialization;

namespace San14.Core;

public enum EditedEventPlaybackPhase { Presentation, Results, Summary, PostResult }

/// <summary>Prepared edited-definition result page, captured after the actual
/// business transaction. Restoring it never opens the title library.</summary>
public sealed record EditedEventResultSummary(string Locale, string Title, string Body, string Hint);

/// <summary>A manual Strategy event owns its real Story cursor independently
/// from a normal-period continuation. Source identifies the selected title
/// library revision, rather than copying a live archive into the world.</summary>
public sealed record EditedEventPlayback(int Slot, EventSourceReference Source,
    CampaignStoryRequest Story, EditedEventPlaybackPhase Phase)
{
    public EditedEventResultSummary? Summary { get; init; }
    [JsonRequired] public CampaignStoryStateControlProgress StateControl { get; init; } = null!;
    public EditedEventPlayback Copy() => this with { Story = Story.Copy() };
}

public sealed partial record GameState
{
    public EditedEventPlayback? EditedEventPlayback { get; init; }
}

internal static class EditedEventPlaybackValidation
{
    internal static void Validate(GameState state)
    {
        if (state.EditedEventPlayback is not { } playback) return;
        if (state.Phase != TurnPhase.Strategy || state.NativePlayableTerminal is not null ||
            state.CampaignTurnStop is not null || state.EndingProgress is not null ||
            state.NativePeriodContinuation is not null || state.NativeNormalTurnContinuation is not null ||
            state.ManualTacticExecutionContinuation is not null)
            throw new InvalidDataException("Manual edited-event playback has a conflicting campaign owner.");
        Validate(playback);
        if (playback.Phase == EditedEventPlaybackPhase.PostResult && state.EditedEventResultCursor is not null)
            throw new InvalidDataException("Edited-event post-Result work still has a business evaluator cursor.");
    }

    internal static void Validate(EditedEventPlayback playback)
    {
        if (playback.Slot is < 0 or >= 100 || playback.Source is null || playback.Story is null ||
            !Enum.IsDefined(playback.Phase) || playback.Source.ResourceUri != "san14://user/edited-events" ||
            playback.Source.DirectoryIndex != playback.Slot || playback.Source.EventId != playback.Slot ||
            playback.Story.Source != playback.Source)
            throw new InvalidDataException("Manual edited-event source is incomplete or inconsistent.");
        PlayableEventStateValidation.ValidateSource(playback.Source);
        playback.Story.Validate(allowEmpty: true);
        if (playback.StateControl is null)
            throw new InvalidDataException("Manual edited-event province-control knowledge is missing.");
        playback.StateControl.Validate();
        if (playback.Phase != EditedEventPlaybackPhase.PostResult &&
            (playback.StateControl.NextOperation != 0 || playback.StateControl.PendingNotice is not null ||
             playback.StateControl.AwaitingPresentationBatch is not null))
            throw new InvalidDataException("Edited-event province-control work started before summary confirmation.");
        bool finished = playback.Story.NextStep == playback.Story.Steps.Length;
        if (finished != (playback.Phase != EditedEventPlaybackPhase.Presentation) ||
            (playback.Phase is EditedEventPlaybackPhase.Summary or EditedEventPlaybackPhase.PostResult) != (playback.Summary is not null))
            throw new InvalidDataException("Manual edited-event phase does not match its actual Story cursor.");
        if (playback.Summary is { } summary) ValidateSummary(summary);
    }

    internal static void ValidateSummary(EditedEventResultSummary summary)
    {
        if (summary.Locale is not ("TC" or "SC") ||
            summary.Title is null || summary.Body is null || summary.Hint is null ||
            summary.Title.Contains('\0') || summary.Body.Contains('\0') || summary.Hint.Contains('\0'))
            throw new InvalidDataException("Saved edited-event result summary is incomplete.");
    }
}
