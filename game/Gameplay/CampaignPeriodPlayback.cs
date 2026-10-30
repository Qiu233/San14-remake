using System.Diagnostics;
using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>A host clock for the real private period operation. A candidate is
/// borrowed only while its worker is stopped. The UI captures render packets
/// and actual ordinary playback transitions, then returns that narrow state
/// to the worker. No intermediate candidate becomes the visible saved world.</summary>
internal sealed class CampaignPeriodPlayback(SynchronizationContext context,
    Action<GameState> publish, Action release, CancellationToken cancellation,
    Action<GameState>? beginTutorial = null,
    Func<GameState, (GameState State, bool Hold)>? updateTutorial = null,
    Func<GameState, GameState>? endTutorial = null)
{
    private int _paused;
    private int _speed = 1;
    private Exception? _presentationFailure;
    internal GameState? Candidate { get; private set; }
    internal bool ManualCommandSelectionOpen { get; set; }
    internal bool OccupationPresentationOpen { get; set; }
    internal bool DuelPresentationOpen { get; set; }
    internal bool DuelCaptureAnnouncementOpen { get; set; }
    internal bool TryCompleteDuelPresentation(GameState expectedOwner,long sequence,GameState preparedState)
    {
        if(!ReferenceEquals(Candidate,expectedOwner)||
            !CampaignDuelPresentationPlaybackRules.TryComplete(expectedOwner,sequence,out var next,preparedState))return false;
        Candidate=next;return true;
    }
    internal bool TryCompleteDuelCaptureAnnouncement(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, DuelCaptureAnnouncementRequest expectedRequest)
    {
        if (Candidate is not { } owner || !ReferenceEquals(owner, expectedOwner) ||
            !DuelCaptureAnnouncementPlaybackRules.TryComplete(owner, expectedSource,
                batchSequence, requestIndex, expectedRequest, out var next)) return false;
        Candidate = next; return true;
    }
    internal bool TryCompleteOccupationEffect(GameState expectedOwner,long sequence,int index,CampaignEffectRequest request)
    {
        if(!ReferenceEquals(Candidate,expectedOwner)||
            !RegionEncirclementPlaybackRules.TryCompleteOccupationEffect(expectedOwner,sequence,index,request,out var next))return false;
        Candidate=next;return true;
    }
    internal bool TryCompleteEncirclement(GameState expectedOwner,RegionEncirclementPresentation request)
    {
        if(!ReferenceEquals(Candidate,expectedOwner)||
            !RegionEncirclementPlaybackRules.TryComplete(expectedOwner,request,out var next))return false;
        Candidate=next;return true;
    }
    internal bool TryApplyManualTacticCommand(GameState expectedOwner,
        Func<GameState, CommandResult> apply, out CommandResult? result)
    {
        result = null;
        if (!ReferenceEquals(Candidate, expectedOwner)) return false;
        result = apply(expectedOwner);
        if (result.Accepted) Candidate = result.State!;
        return true;
    }
    internal void FailPresentation(Exception failure) => _presentationFailure ??= failure;
    internal bool TryCompleteUniqueDirection(GameState expectedOwner, ManualTacticInterventionState manager, long sequence)
    {
        if (Candidate is not { } owner || !ReferenceEquals(owner, expectedOwner) ||
            !UniqueDirectionPlaybackRules.TryComplete(owner, manager, sequence, out var next)) return false;
        Candidate = next; return true;
    }
    internal bool TryAdoptUniqueDirectionAudio(GameState expectedOwner, ManualTacticInterventionState manager,
        long sequence, GameState preparedState)
    {
        if (Candidate is not { } owner || !ReferenceEquals(owner, expectedOwner) ||
            !UniqueDirectionPlaybackRules.TryAdoptPreparedAudio(owner, manager, sequence, preparedState, out var next)) return false;
        Candidate = next; return true;
    }
    internal bool TryBeginOrdinaryTactic(GameState expectedOwner, ManualTacticInterventionState manager,
        long sequence, GameState? preparedState)
    {
        if (Candidate is not { } owner || !ReferenceEquals(owner, expectedOwner) ||
            !OrdinaryTacticPlaybackRules.TryBegin(owner, manager, sequence, preparedState, out var next)) return false;
        Candidate = next; return true;
    }

    internal bool TryCompleteOrdinaryTacticResults(GameState expectedOwner,
        ManualTacticInterventionState manager, long sequence)
    {
        if (Candidate is not { } owner || !ReferenceEquals(owner, expectedOwner) ||
            !OrdinaryTacticPlaybackRules.TryCompleteResults(owner, manager, sequence, out var next)) return false;
        Candidate = next; return true;
    }

    internal bool TryRetireOrdinaryTactic(GameState expectedOwner, ManualTacticInterventionState manager, long sequence)
    {
        if (Candidate is not { } owner || !ReferenceEquals(owner, expectedOwner) ||
            !OrdinaryTacticPlaybackRules.TryRetire(owner, manager, sequence, out var next)) return false;
        Candidate = next; return true;
    }
    public bool IsPaused => Volatile.Read(ref _paused) != 0;
    public int Speed => Volatile.Read(ref _speed);
    public void TogglePause() => Interlocked.Exchange(ref _paused, IsPaused ? 0 : 1);
    public void SetSpeed(int value)
    {
        if ((uint)value > 2) throw new ArgumentOutOfRangeException(nameof(value));
        Volatile.Write(ref _speed, value);
    }

    public GameState Checkpoint(GameState candidate)
    {
        cancellation.ThrowIfCancellationRequested();
        var completed = new TaskCompletionSource<GameState>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(async _ =>
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                GameState presented;
                try
                {
                    Candidate = candidate;
                    beginTutorial?.Invoke(candidate);
                    publish(candidate);
                    // Host playback cadence, independent of simulation time. Each
                    // interval displays one actual six-hour native update. Source
                    // animation and camera continue on their existing frame clocks.
                    var elapsed = Stopwatch.StartNew();
                    double activeMilliseconds = 0;
                    while (true)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (_presentationFailure is { } failure) throw failure;
                        double before = elapsed.Elapsed.TotalMilliseconds;
                        await Task.Delay(16, cancellation);
                        bool tutorialHold = false;
                        if (updateTutorial is not null && !ManualCommandSelectionOpen && !DuelPresentationOpen && !DuelCaptureAnnouncementOpen)
                        {
                            var update = updateTutorial(Candidate!);
                            Candidate = update.State; tutorialHold = update.Hold;
                        }
                        bool inputHold = ManualCommandSelectionOpen || OccupationPresentationOpen || DuelPresentationOpen || DuelCaptureAnnouncementOpen;
                        if (!IsPaused && !tutorialHold && !inputHold) activeMilliseconds += elapsed.Elapsed.TotalMilliseconds - before;
                        double interval = Speed switch { 0 => 600, 1 => 300, _ => 150 };
                        if (!IsPaused && !tutorialHold && !inputHold && activeMilliseconds >= interval &&
                            Candidate?.ManualTacticIntervention?.Directions.Length is not > 0) break;
                    }
                }
                finally
                {
                    try { presented = endTutorial?.Invoke(Candidate!) ?? Candidate!; }
                    finally { Candidate = null; ManualCommandSelectionOpen = false; OccupationPresentationOpen=false; DuelPresentationOpen=false; DuelCaptureAnnouncementOpen=false; release(); }
                }
                completed.TrySetResult(presented);
            }
            catch (OperationCanceledException) { completed.TrySetCanceled(cancellation); }
            catch (Exception error) { completed.TrySetException(error); }
        }, null);
        // Cancellation wakes the UI delay above. Its finally releases the
        // borrowed candidate before this worker is permitted to unwind.
        return completed.Task.GetAwaiter().GetResult();
    }
}
