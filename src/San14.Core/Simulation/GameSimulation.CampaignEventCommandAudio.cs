namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Accepts only the actual main-thread audio owner's captured
    /// return values after successful invocation entry. No business runs here.</summary>
    public bool TryBeginCampaignEventCommandAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback?.Invocation is not { } playback ||
            playback.Phase != NormalTurnStoryResultPhase.Entry || playback.Story.Sequence != sequence ||
            playback.AudioReturn is not null || playback.ExecutionCursor is not null || playback.Story.Steps.Length != 0)
            return false;
        StoreEventCommandInvocation(playback with { AudioReturn = snapshot, Phase = NormalTurnStoryResultPhase.Entered });
        return true;
    }

    /// <summary>Receives the real exit/restore completion from the same audio
    /// return owner. The normal worker still owns occurrence/profile completion.</summary>
    public bool TryCompleteCampaignEventCommandAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn expectedSnapshot)
    {
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback?.Invocation is not { } playback ||
            playback.Phase != NormalTurnStoryResultPhase.Exiting || playback.Story.Sequence != sequence ||
            !ReferenceEquals(playback.AudioReturn, expectedSnapshot) || expectedSnapshot is null ||
            playback.Story.NextStep != playback.Story.Steps.Length || playback.ExecutionCursor is { IsCompleted: false })
            return false;
        StoreEventCommandInvocation(playback with { Phase = NormalTurnStoryResultPhase.Exited });
        return true;
    }
}
