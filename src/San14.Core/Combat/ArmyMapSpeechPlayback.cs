using System.Collections.Immutable;

namespace San14.Core;

public sealed record ArmyMapSpeechDisplay(long Sequence, int ArmyId, int OfficerId,
    int HexId, string Text, bool Leaving)
{
    public int LayoutStyle { get; init; }
}

/// <summary>The application-owned speech clock. Only enqueue, start and removal
/// publish a campaign state; ordinary display frames change this small owner.</summary>
public sealed class ArmyMapSpeechPlayback
{
    private sealed class Playing(ArmyMapSpeechEntry entry)
    {
        internal ArmyMapSpeechEntry Entry = entry;
        internal byte Delay = entry.DelayFrames;
        internal double Elapsed = entry.ElapsedSeconds;
    }
    private ArmyMapSpeechState source;
    private readonly List<Playing> queue = [];
    private readonly List<long> ready = [], finished = [];
    private readonly List<ArmyMapSpeechDisplay> visible = [];

    public ArmyMapSpeechPlayback(ArmyMapSpeechState snapshot)
    {
        source = snapshot;
        foreach (var entry in snapshot.Entries) queue.Add(new(entry));
        RefreshVisible();
    }
    public IReadOnlyList<long> ReadyToStart => ready;
    public IReadOnlyList<long> Finished => finished;
    public IReadOnlyList<ArmyMapSpeechDisplay> Visible => visible;
    public void Synchronize(ArmyMapSpeechState snapshot)
    {
        if (ReferenceEquals(source, snapshot)) return;
        var existing = queue.ToDictionary(item => item.Entry.Sequence);
        queue.Clear();
        foreach (var entry in snapshot.Entries)
        {
            if (existing.TryGetValue(entry.Sequence, out var retained))
            {
                //A source packet replacement retains the existing delay and
                //clock. A genuine new start installs its once-selected text.
                retained.Entry = entry; queue.Add(retained);
            }
            else queue.Add(new(entry));
        }
        source = snapshot; RefreshVisible();
    }
    public void Advance(double elapsedSeconds, bool paused)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        ready.Clear(); finished.Clear();
        foreach (var item in queue)
        {
            if (item.Entry.TextAtSource is not null)
            {
                item.Elapsed += elapsedSeconds;
                if (item.Elapsed >= 3.5) finished.Add(item.Entry.Sequence);
                continue;
            }
            //1BD960 pauses delay/start, while a displayed balloon continues
            //its actual three-second hold and half-second departure clock.
            if (paused) continue;
            if (item.Delay != 0) { item.Delay--; continue; }
            //The actual start transaction checks the shared speaker exclusion
            //before acquiring a layout or selecting text, just like1BD960.
            ready.Add(item.Entry.Sequence);
        }
        RefreshVisible();
    }
    public ArmyMapSpeechState CaptureSnapshot() => new(source.NextSequence,
        queue.Select(item => item.Entry with { DelayFrames = item.Delay, ElapsedSeconds = item.Elapsed }).ToImmutableArray());

    private void RefreshVisible()
    {
        visible.Clear();
        foreach (var item in queue)
            if (item.Entry.TextAtSource is { } text && item.Elapsed < 3.5)
                visible.Add(new(item.Entry.Sequence, item.Entry.ArmyId, item.Entry.OfficerId,
                    item.Entry.HexId, text, item.Elapsed >= 3) { LayoutStyle = item.Entry.ActionCode == 25 ? 1 : 0 });
    }
}
