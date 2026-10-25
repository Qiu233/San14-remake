using System.Diagnostics;
using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>A host clock for the real private period operation. A candidate is
/// borrowed only while its worker is stopped. The UI captures render packets
/// and actual ordinary playback transitions, then returns that narrow state
/// to the worker. No intermediate candidate becomes the visible saved world.</summary>
internal sealed class CampaignPeriodPlayback(SynchronizationContext context,
    Action<GameState> publish, Action release, CancellationToken cancellation)
{
    private int _paused;
    private int _speed = 1;
    internal GameState? Candidate { get; private set; }
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
                    publish(candidate);
                    // Host playback cadence, independent of simulation time. Each
                    // interval displays one actual six-hour native update. Source
                    // animation and camera continue on their existing frame clocks.
                    var elapsed = Stopwatch.StartNew();
                    double activeMilliseconds = 0;
                    while (true)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        double before = elapsed.Elapsed.TotalMilliseconds;
                        await Task.Delay(16, cancellation);
                        if (!IsPaused) activeMilliseconds += elapsed.Elapsed.TotalMilliseconds - before;
                        double interval = Speed switch { 0 => 600, 1 => 300, _ => 150 };
                        if (!IsPaused && activeMilliseconds >= interval) break;
                    }
                    presented = Candidate!;
                }
                finally { Candidate = null; release(); }
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
