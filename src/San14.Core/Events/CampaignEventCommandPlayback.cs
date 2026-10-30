using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>The ordinary Events command owns one selected normal-resource
/// invocation. It neither advances a period nor borrows its preparation owner.</summary>
public sealed record CampaignEventCommandPlayback(NormalTurnStoryPlayback Invocation, int Flags)
{
    [JsonIgnore] public CampaignStoryRequest? PendingStory => Invocation.Story.NextStep < Invocation.Story.Steps.Length ? Invocation.Story : null;
    [JsonIgnore] public NormalTurnStoryResultSummary? PendingResult => Invocation.Phase == NormalTurnStoryResultPhase.Result ? Invocation.ResultSummary : null;
    [JsonIgnore] public NormalTurnStoryPlayback? PendingAudioEntry => Invocation.Phase == NormalTurnStoryResultPhase.Entry ? Invocation : null;
    [JsonIgnore] public NormalTurnStoryPlayback? PendingAudioExit => Invocation.Phase == NormalTurnStoryResultPhase.Exiting ? Invocation : null;
    [JsonIgnore] public CampaignStoryForceRankContinuation? PendingForceRank => Invocation.ExecutionCursor?.ForceRankContinuation is { IsAwaitingInput: true } rank ? rank : null;
    public bool RequiresAutomaticContinuation(GameState owner) => PendingStory is null && PendingResult is null &&
        PendingAudioEntry is null && PendingAudioExit is null && PendingForceRank is null &&
        !CampaignStoryStateControlProgress.IsAwaitingPresentation(owner);
    internal CampaignEventCommandPlayback Copy() => this with { Invocation = Invocation.Copy() };
    internal void Validate()
    {
        if (Invocation is null || Invocation.IsReturningCamera || !Invocation.RemainingEventIds.IsEmpty ||
            Flags != (Invocation.Story.Source.EventId == 900 ? 2 : 3))
            throw new InvalidDataException("The ordinary event command invocation is incomplete.");
        Invocation.Validate();
        PlayableEventStateValidation.ValidateSource(Invocation.Story.Source);
    }
}

public sealed partial record GameState
{
    public CampaignEventCommandPlayback? CampaignEventCommandPlayback { get; init; }
}

internal static class CampaignEventCommandValidation
{
    internal static void Validate(GameState state)
    {
        if (state.CampaignEventCommandPlayback is not { } command) return;
        if (state.Phase != TurnPhase.Strategy || state.NativePeriodContinuation is not null ||
            state.NativeNormalTurnContinuation is not null || state.EditedEventPlayback is not null ||
            state.ManualTacticExecutionContinuation is not null || state.EndingProgress is not null)
            throw new InvalidDataException("Ordinary event command has a conflicting campaign owner.");
        command.Validate();
    }
}
