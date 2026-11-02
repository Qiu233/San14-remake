namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Fork an already admitted session without loading or revalidating
    /// a save. Committed worlds are borrowed read-only; the ordinary command or
    /// advancement entry makes its private transaction before any write.</summary>
    public GameSimulation Fork(CampaignAppRandomState? appRandomSnapshot = null, GameState? preparedCandidate = null) =>
        new(this, appRandomSnapshot, preparedCandidate);

    private GameSimulation(GameSimulation source, CampaignAppRandomState? appRandomSnapshot, GameState? preparedCandidate)
    {
        //A suspended normal period already owns the stream consumed by its
        //prefix. The visible host still displays the committed world's older
        //stream; it cannot rebase that private continuation on resume.
        if (preparedCandidate is not null && !source.HasPendingOperation)
        {
            CheckAuxiliaryTransition(source.State, preparedCandidate);
            State = appRandomSnapshot is null ? preparedCandidate :
                preparedCandidate with { CampaignAppRandom32 = appRandomSnapshot };
        }
        else State = appRandomSnapshot is null || source._normalPeriod is not null || source.State.CampaignEventCommandPlayback is not null ? source.State
            : source.State with { CampaignAppRandom32 = appRandomSnapshot };
        _progress = source._progress is { } pending
            ? new(pending.Story?.Copy(), pending.ManualTactic?.Copy(), pending.ArmyProgress?.Copy()) : null;
        //The candidate is borrowed until command/advance takes its one private
        //transaction copy. A worker never mutates the visible session's prefix.
        _normalPeriod = source._normalPeriod;
        LastStorySequence = source.LastStorySequence;
    }
}
