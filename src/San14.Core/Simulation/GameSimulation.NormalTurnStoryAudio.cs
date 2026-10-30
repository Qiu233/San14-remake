namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Accepts only the actual main-thread audio owner's captured
    /// return values after successful invocation entry. No business runs here.</summary>
    public bool TryBeginNormalTurnStoryAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        if (_progress is not null || _normalPeriod is not { } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Candidate.NativeNormalTurnContinuation is not { NextAction: 48, StoryPlayback: { } playback } turn ||
            playback.Phase != NormalTurnStoryResultPhase.Entry || playback.Story.Sequence != sequence ||
            playback.AudioReturn is not null || playback.ExecutionCursor is not null || playback.Story.Steps.Length != 0)
            return false;
        _normalPeriod = pending with { Candidate = pending.Candidate with
        {
            NativeNormalTurnContinuation = turn with
            { StoryPlayback = playback with { AudioReturn = snapshot, Phase = NormalTurnStoryResultPhase.Entered } }
        } };
        return true;
    }

    /// <summary>Receives the real exit/restore completion from the same audio
    /// return owner. The normal worker still owns occurrence/profile completion.</summary>
    public bool TryCompleteNormalTurnStoryAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn expectedSnapshot)
    {
        if (_progress is not null || _normalPeriod is not { } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Candidate.NativeNormalTurnContinuation is not { NextAction: 48, StoryPlayback: { } playback } turn ||
            playback.Phase != NormalTurnStoryResultPhase.Exiting || playback.Story.Sequence != sequence ||
            !ReferenceEquals(playback.AudioReturn, expectedSnapshot) || expectedSnapshot is null ||
            playback.Story.NextStep != playback.Story.Steps.Length || playback.ExecutionCursor is { IsCompleted: false })
            return false;
        _normalPeriod = pending with { Candidate = pending.Candidate with
        {
            NativeNormalTurnContinuation = turn with
            { StoryPlayback = playback with { Phase = NormalTurnStoryResultPhase.Exited } }
        } };
        return true;
    }
}
