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
    Func<GameState, GameState>? endTutorial = null,
    CampaignProgressFrameAdmission? admission = null)
{
    private int _paused;
    private int _speed = 1;
    private Exception? _presentationFailure;
    private readonly object _frameLock = new();
    private TaskCompletionSource<SourceFrame> _nextSourceFrame = NewSourceFrame();
    private readonly CampaignProgressFrameAdmission _admission = admission ?? new();
    internal CampaignProgressFrameAdmission Admission => _admission;
    private ArmyTraitSpeechViews? _armyTraitSpeechViews;
    internal void ConfigureArmyTraitSpeechViews(ArmyTraitSpeechViews? views) => _armyTraitSpeechViews = views;
    internal void DiscardInitializedArmySpeechViews() => _armyTraitSpeechViews?.DiscardInitializedViews();
    internal bool PublishInitializedArmySpeechView(GameState expectedOwner, ArmyTraitSpeechViews.View view)
        => ReferenceEquals(Candidate, expectedOwner) &&
            _armyTraitSpeechViews?.PublishInitializedView(expectedOwner, view) == true;
    private NativeArmyProgressStageMoment? _borrowedAdmission;
    private sealed record SourceFrame(int FramesPerSecond, long StartedAt);
    private static TaskCompletionSource<SourceFrame> NewSourceFrame()
        // This signal is fired on the same UI context that owns Checkpoint.
        // Inline that continuation so its frame budget is not deferred by a
        // Godot context.Post to the following process pump. Worker completion
        // below remains asynchronous and never executes simulation on the UI.
        => new();

    // Called once by the existing source App-frame pacer, independently of
    // image submission, camera movement and whether a candidate is borrowed.
    internal void OnSourceFrame(int framesPerSecond, long startedAt)
    {
        TaskCompletionSource<SourceFrame> reached;
        lock (_frameLock) { reached = _nextSourceFrame; _nextSourceFrame = NewSourceFrame(); }
        reached.TrySetResult(new(framesPerSecond, startedAt));
    }

    private Task<SourceFrame> NextSourceFrame()
    {
        lock (_frameLock) return _nextSourceFrame.Task.WaitAsync(cancellation);
    }

    private void RequireActive()
    {
        cancellation.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _presentationFailure) is { } failure) throw failure;
    }

    // The worker owns this state while waiting. A menu request borrows it
    // through the existing checkpoint and resumes from the returned state.
    // With no request, admission retains the original source-frame path.
    internal GameState AdmitStage(GameState owned, NativeArmyProgressStageMoment moment)
    {
        RequireActive();
        owned = BorrowMenuIfRequested(owned, moment);
        // Stage1's accepted domain result must reach the real audio host
        // before another stage runs, rather than waiting until period commit.
        if (owned.PendingMapMusicIntent is not null || ArmyViewPresentationPlaybackRules.Next(owned) is not null)
            owned = Checkpoint(owned, moment);
        if (_borrowedAdmission == moment)
        {
            _borrowedAdmission = null;
            if (!IsPaused && _admission.CanContinue(Stopwatch.GetTimestamp())) return owned;
        }
        if (moment.SchedulerStageIndex != 0 && !IsPaused &&
            _admission.CanContinue(Stopwatch.GetTimestamp())) return owned;
        while (true)
        {
            var frame = NextSourceFrame().GetAwaiter().GetResult();
            RequireActive();
            // A paused worker can already be inside this loop when a menu is
            // requested. Check each real frame before admitting simulation.
            if (Volatile.Read(ref _menuBorrowRequested) != 0)
            {
                owned = BorrowMenuIfRequested(owned, moment);
                if (_borrowedAdmission == moment)
                {
                    _borrowedAdmission = null;
                    if (!IsPaused && _admission.CanContinue(Stopwatch.GetTimestamp())) return owned;
                }
                // Checkpoint consumed its own source frame. Do not reuse the
                // frame captured before that borrow or charge admission twice.
                continue;
            }
            if (!IsPaused && _admission.TryEnter(moment.SchedulerStageIndex, Speed,
                frame.FramesPerSecond, frame.StartedAt, Stopwatch.GetTimestamp())) return owned;
        }
    }

    private int _menuBorrowRequested;
    internal void RequestMenuBorrow() => Interlocked.Exchange(ref _menuBorrowRequested, 1);
    internal void CancelMenuBorrow() => Interlocked.Exchange(ref _menuBorrowRequested, 0);
    private GameState BorrowMenuIfRequested(GameState owned, NativeArmyProgressStageMoment moment)
        => Interlocked.Exchange(ref _menuBorrowRequested, 0) != 0 ? Checkpoint(owned, moment) : owned;

    internal bool MenuPresentationOpen { get; set; }
    internal bool TryAdoptMenuPresentationWorld(GameState expectedOwner, GameState replacement)
    {
        if (!MenuPresentationOpen || !ReferenceEquals(Candidate, expectedOwner)) return false;
        Candidate = replacement;
        return true;
    }
    internal GameState? Candidate { get; private set; }
    internal bool ManualCommandSelectionOpen { get; set; }
    internal bool OccupationPresentationOpen { get; set; }
    internal bool DuelPresentationOpen { get; set; }
    internal bool DuelCaptureAnnouncementOpen { get; set; }
    internal bool MapMusicPresentationOpen { get; set; }
    internal bool TryCompletePendingMapMusicIntent(GameState expectedOwner, MapMusicIntent expectedIntent)
    {
        if (Candidate is not { } owner ||
            !MapMusicIntentCompletion.TryComplete(owner, expectedOwner, expectedIntent, out var completed)) return false;
        Candidate = completed;
        return true;
    }
    internal bool TryCompleteArmyViewPresentation(GameState expectedOwner, ArmyViewPresentationHead head)
    {
        if (Candidate is not { } owner || !ReferenceEquals(owner, expectedOwner)) return false;
        GameState completed;
        bool accepted = head.Request.ArmyDeparture is not null
            ? ArmyViewPresentationPlaybackRules.TryCompleteDeparture(owner, head.BatchSequence, head.RequestIndex, out completed)
            : ArmyViewPresentationPlaybackRules.TryCompleteTrait(owner, head.Source, head.BatchSequence, head.RequestIndex, out completed);
        if (accepted) Candidate = completed;
        return accepted;
    }
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
    internal void FailPresentation(Exception failure)
    {
        if (Interlocked.CompareExchange(ref _presentationFailure, failure, null) is not null) return;
        lock (_frameLock) _nextSourceFrame.TrySetException(failure);
    }
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
    public void TogglePause() => SetPaused(!IsPaused);
    public void SetPaused(bool paused) => Interlocked.Exchange(ref _paused, paused ? 1 : 0);
    public void SetSpeed(int value)
    {
        if ((uint)value > 2) throw new ArgumentOutOfRangeException(nameof(value));
        Volatile.Write(ref _speed, value);
    }

    public GameState Checkpoint(GameState candidate, NativeArmyProgressStageMoment? nextStage)
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
                    // Initial/resumed and completed-block checkpoints are display
                    // borrows, not elapsed simulation cycles. A real following
                    // stage may consume its source frame here, while the existing
                    // manual-command and presentation leases still own the world.
                    while (true)
                    {
                        var frame = await NextSourceFrame();
                        RequireActive();
                        bool tutorialHold = false;
                        if (updateTutorial is not null && !MenuPresentationOpen && !ManualCommandSelectionOpen && !DuelPresentationOpen && !DuelCaptureAnnouncementOpen && !MapMusicPresentationOpen)
                        {
                            var update = updateTutorial(Candidate!);
                            Candidate = update.State; tutorialHold = update.Hold;
                        }
                        bool inputHold = MenuPresentationOpen || ManualCommandSelectionOpen || OccupationPresentationOpen || DuelPresentationOpen || DuelCaptureAnnouncementOpen || MapMusicPresentationOpen;
                        if (IsPaused || tutorialHold || inputHold ||
                            Candidate?.ManualTacticIntervention?.Directions.Length is > 0) continue;
                        if (nextStage is null) break;
                        if (!_admission.TryEnter(nextStage.SchedulerStageIndex, Speed,
                            frame.FramesPerSecond, frame.StartedAt, Stopwatch.GetTimestamp())) continue;
                        _borrowedAdmission = nextStage;
                        break;
                    }
                }
                finally
                {
                    try { presented = endTutorial?.Invoke(Candidate!) ?? Candidate!; }
                    finally { Candidate = null; MenuPresentationOpen = false; ManualCommandSelectionOpen = false; OccupationPresentationOpen=false; DuelPresentationOpen=false; DuelCaptureAnnouncementOpen=false; MapMusicPresentationOpen=false; release(); }
                }
                completed.TrySetResult(presented);
            }
            catch (OperationCanceledException) { completed.TrySetCanceled(cancellation); }
            catch (Exception error) { completed.TrySetException(error); }
        }, null);
        // Cancellation wakes the source-frame wait. Its finally releases the
        // borrowed candidate before this worker is permitted to unwind.
        return completed.Task.GetAwaiter().GetResult();
    }
}
