using System.Diagnostics;

namespace San14.Client.Gameplay;

/// <summary>Operation-owned Progress stage0 modulo gate and per-App-frame work
/// budget. The caller supplies an actually admitted source frame; elapsed wall
/// time never creates callbacks or simulation cycles.</summary>
internal sealed class CampaignProgressFrameAdmission
{
    // Offline-extracted CE TC/SC data: period-speed160/DATA160.json.
    private static ReadOnlySpan<int> SpeedFrames => [8, 2, 1];
    private int _counter;
    private long _startedAt;
    private int _budgetMilliseconds;
    private bool _hasWorkFrame;

    internal bool TryEnter(byte stage, int speed, int framesPerSecond, long startedAt, long now)
    {
        if ((uint)speed > 2) throw new ArgumentOutOfRangeException(nameof(speed));
        if (framesPerSecond is not (30 or 60)) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        _hasWorkFrame = false;
        if (stage == 0)
        {
            int interval = Math.Max(1, SpeedFrames[speed] * 2 * (framesPerSecond / 30));
            int remainder = _counter % interval;
            _counter++;
            if (remainder != 0) return false;
            _counter %= interval;
        }
        _startedAt = startedAt;
        _budgetMilliseconds = framesPerSecond == 60 ? 11 : 22;
        _hasWorkFrame = WithinBudget(now);
        return _hasWorkFrame;
    }

    internal bool CanContinue(long now) => _hasWorkFrame && WithinBudget(now);

    // The source compares integer elapsed milliseconds with >= 11/22.
    private bool WithinBudget(long now)
        => (long)Stopwatch.GetElapsedTime(_startedAt, now).TotalMilliseconds < _budgetMilliseconds;
}
