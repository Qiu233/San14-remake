using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Invocation-local threshold transitions. Slot zero is a real
/// source pointer identity, independently of a missing pointer.</summary>
public sealed record CampaignStoryStateControlPair(int? GainedForcePhysicalSlot, int? LostForcePhysicalSlot);

/// <summary>Text captured at the actual province-control notification boundary.</summary>
public sealed record CampaignStoryStateControlNotice(int Province, int Force, bool Gained, string TextAtSource);

/// <summary>The ordered gain, loss and special-personnel tail of one Story.
/// Missing source-time knowledge is retained explicitly across old saves.</summary>
public sealed record CampaignStoryStateControlProgress(ImmutableArray<CampaignStoryStateControlPair>? Pairs,
    bool? HasChanges, int NextOperation, CampaignStoryStateControlNotice? PendingNotice, long? AwaitingPresentationBatch)
{
    public static CampaignStoryStateControlProgress Fresh { get; } = new(
        Enumerable.Range(0, 23).Select(_ => new CampaignStoryStateControlPair(null, null)).ToImmutableArray(),
        false, 0, null, null);
    public static CampaignStoryStateControlProgress Unavailable { get; } = new(null, null, 0, null, null);

    [JsonIgnore] public bool IsSourceTimeUnavailable => Pairs is null && HasChanges is null;
    [JsonIgnore] public bool IsCompleted => !IsSourceTimeUnavailable && NextOperation == 69 &&
        PendingNotice is null && AwaitingPresentationBatch is null;

    public static bool IsAwaitingPresentation(GameState state)
    {
        long? sequence = state.NativeNormalTurnContinuation?.StoryPlayback?.StateControl?.AwaitingPresentationBatch
            ?? state.CampaignEventCommandPlayback?.Invocation.StateControl?.AwaitingPresentationBatch
            ?? state.EditedEventPlayback?.StateControl?.AwaitingPresentationBatch;
        if (sequence is null) return false;
        var batches = state.CampaignEffectRequests?.PendingBatches
            ?? throw new InvalidDataException("Story province-control presentation outbox is unavailable.");
        //Only the actual unconsumed FIFO batch blocks the continuation. Once
        //its requests retire, the normal consumer may advance its saved cursor.
        return batches.Any(batch => batch.Sequence == sequence && batch.Requests.Length > 0);
    }

    public CampaignStoryStateControlProgress Record(int province, int? incomingGain, int? incomingLoss)
    {
        if (Pairs is not { IsDefault: false, Length: 23 } pairs || HasChanges is null)
            throw new InvalidDataException("Story province-control source-time knowledge is unavailable.");
        if (province is < 0 or >= 23) throw new ArgumentOutOfRangeException(nameof(province));
        if (incomingGain is < 0 or > 51) throw new ArgumentOutOfRangeException(nameof(incomingGain));
        if (incomingLoss is < 0 or > 51) throw new ArgumentOutOfRangeException(nameof(incomingLoss));
        var previous = pairs[province];
        int? oldGain = previous.GainedForcePhysicalSlot, oldLoss = previous.LostForcePhysicalSlot;
        int? gain = oldGain, loss = oldLoss;
        //All comparisons use the pair before any of these four source writes.
        if (incomingLoss == oldGain) gain = null;
        if (incomingGain == oldLoss) loss = null;
        if (incomingLoss != oldGain) loss = incomingLoss;
        if (incomingGain != oldLoss) gain = incomingGain;
        return this with { Pairs = pairs.SetItem(province, new(gain, loss)), HasChanges = true };
    }

    internal void Validate()
    {
        if (Pairs is null || HasChanges is null)
        {
            if (Pairs is not null || HasChanges is not null || NextOperation != 0 ||
                PendingNotice is not null || AwaitingPresentationBatch is not null)
                throw new InvalidDataException("Story province-control unavailable knowledge has an invalid shape.");
            return;
        }
        var pairs = Pairs.Value;
        if (pairs.IsDefault || pairs.Length != 23 || pairs.Any(pair => pair is null ||
                pair.GainedForcePhysicalSlot is < 0 or > 51 || pair.LostForcePhysicalSlot is < 0 or > 51) ||
            NextOperation is < 0 or > 69 || AwaitingPresentationBatch is <= 0 ||
            HasChanges == false && pairs.Any(pair => pair.GainedForcePhysicalSlot is not null || pair.LostForcePhysicalSlot is not null))
            throw new InvalidDataException("Story province-control continuation is incomplete or invalid.");
        if (PendingNotice is { } notice && (AwaitingPresentationBatch is null || NextOperation >= 46 ||
            notice.Province != NextOperation % 23 || notice.Gained != (NextOperation < 23) ||
            notice.Force is < 0 or > 51 || notice.TextAtSource is null ||
            notice.Force != (notice.Gained ? pairs[notice.Province].GainedForcePhysicalSlot : pairs[notice.Province].LostForcePhysicalSlot)))
            throw new InvalidDataException("Story province-control notice does not belong to its pending operation.");
    }
}
