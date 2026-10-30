namespace San14.Data;

/// <summary>The actual Common22A0+18 allocation cursor used by D78900 and
/// BB85A0. Its finite capacity comes from the owning current arena, separately
/// from the eleven pointer-list capacities and the compact work record7C.</summary>
public sealed class DeferredEarlyShadowCurrentTupleArena
{
    private readonly object allocationGate = new();
    public DeferredEarlyShadowCurrentTupleArena(int actualCapacityBytes,
        int actualUsedBytes, string actualCurrentWriterEvidence)
    {
        if (actualCapacityBytes < 0 || actualUsedBytes < 0 || actualUsedBytes > actualCapacityBytes)
            throw new ArgumentOutOfRangeException(nameof(actualUsedBytes));
        ArgumentException.ThrowIfNullOrWhiteSpace(actualCurrentWriterEvidence);
        CapacityBytes = actualCapacityBytes; UsedBytes = actualUsedBytes;
        CurrentWriterEvidence = actualCurrentWriterEvidence;
    }
    public int CapacityBytes { get; }
    public int UsedBytes { get; private set; }
    public string CurrentWriterEvidence { get; }

    /// <summary>The source cursor reservation permits an exact arena fit.
    /// A refused reservation does not change the cursor.</summary>
    internal bool TryReserve(int bytes, out int offset)
    {
        lock (allocationGate)
        {
            offset = UsedBytes;
            if (bytes < 0 || bytes > CapacityBytes - UsedBytes) return false;
            UsedBytes += bytes; return true;
        }
    }
}

/// <summary>An existing same-category Main tuple. ActualTupleIdentity is the
/// original owned tuple object, not a new identity derived from a draw name.</summary>
public sealed record DeferredEarlyShadowActualMainTuple(int SourceDrawIndex,
    object ActualTupleIdentity, ulong ExistingKey8);

/// <summary>One shared 16-byte source tuple's owned representation. Pointer
/// identity and key are fixed by its first allocator; each pass list borrows
/// the same object. Existing Main tuple objects are retained without cloning.</summary>
public sealed class DeferredEarlyShadowCurrentPrimitiveTuple
{
    internal DeferredEarlyShadowCurrentPrimitiveTuple(
        DeferredEarlyShadowCurrentModelCandidate model, int category, int draw,
        ulong key, object? mainIdentity, int? offset)
    {
        Model = model; Category = category; SourceDrawIndex = draw; Key8 = key;
        RetainedMainTupleIdentity = mainIdentity; ArenaByteOffset = offset;
    }
    public DeferredEarlyShadowCurrentModelCandidate Model { get; }
    public int Category { get; }
    public int SourceDrawIndex { get; }
    public ulong Key8 { get; }
    public object? RetainedMainTupleIdentity { get; }
    public object TupleIdentity => RetainedMainTupleIdentity ?? this;
    public int? ArenaByteOffset { get; }
}

public sealed record DeferredEarlyShadowCategoryAppendResult(int CounterIndex,
    int Category, ulong CalculatedCasterKey, bool SourcePublisherReturnedTrue,
    bool CounterAcceptedCompleteSpan, bool AllocatedTupleSpan,
    IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> SharedTuples)
{
    public int AppendedCount => CounterAcceptedCompleteSpan ? SharedTuples.Count : 0;
}

/// <summary>The actual eleven-entry View19A0 allocation and finite BB85A0 /
/// BB8680 publication. Collection order and independent caster admission are
/// caller inputs. Allocating this owner does not populate counters6..10 or
/// certify a complete queue19 draw list.</summary>
public sealed class DeferredEarlyShadowCurrentTupleBank
{
    private readonly DeferredEarlyShadowCurrentPrimitiveTuple?[][] lists;
    private readonly int[] encodedCounts;
    private readonly Dictionary<(DeferredEarlyShadowCurrentModelCandidate Model, int Category),
        IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple>> spans = [];

    private DeferredEarlyShadowCurrentTupleBank(long frame, string view,
        DeferredEarlyShadowCurrentViewCounterCapacities capacities,
        DeferredEarlyShadowViewCounterAllocationLayout layout,
        DeferredEarlyShadowCurrentTupleArena arena, int offset)
    {
        FrameNumber = frame; ViewIdentity = view; Capacities = capacities;
        Layout = layout; Arena = arena; BankArenaByteOffset = offset;
        lists = layout.Capacities.Select(capacity =>
            new DeferredEarlyShadowCurrentPrimitiveTuple?[checked((int)capacity)]).ToArray();
        encodedCounts = Enumerable.Repeat(-1, 11).ToArray();
    }
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public DeferredEarlyShadowCurrentViewCounterCapacities Capacities { get; }
    public DeferredEarlyShadowViewCounterAllocationLayout Layout { get; }
    public DeferredEarlyShadowCurrentTupleArena Arena { get; }
    public int BankArenaByteOffset { get; }
    public string SourceEvidence => Capacities.SourceEvidence + ";" + Arena.CurrentWriterEvidence +
        ";TC:D78900-Common18-eleven-entry-allocation;C394F0-category0/1;BB85A0-shared-span/BB8680-strict-capacity;not-complete-caster-order";

    /// <summary>D78900 publishes no bank when M=0 or the actual current
    /// arena cannot allocate its complete B0 header and all pointer lists.</summary>
    public static DeferredEarlyShadowCurrentTupleBank? BeginCurrent(long actualFrameNumber,
        string actualViewIdentity, DeferredEarlyShadowCurrentViewCounterCapacities actualCapacities,
        DeferredEarlyShadowCurrentTupleArena actualCommon18Arena)
    {
        ArgumentNullException.ThrowIfNull(actualCapacities); ArgumentNullException.ThrowIfNull(actualCommon18Arena);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualViewIdentity);
        if (actualFrameNumber <= 0) throw new ArgumentOutOfRangeException(nameof(actualFrameNumber));
        if (!actualCapacities.SourceAllocationRequested) return null;
        var layout = actualCapacities.RequireCompleteAllocationLayout();
        if (!actualCommon18Arena.TryReserve(checked((int)layout.ArenaByteLength), out int offset)) return null;
        return new(actualFrameNumber, actualViewIdentity, actualCapacities, layout, actualCommon18Arena, offset);
    }

    public int ReadEncodedCountC(int counterIndex) => encodedCounts[RequireCounter(counterIndex)];
    public int ReadCount(int counterIndex) => ~ReadEncodedCountC(counterIndex);
    public IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> ReadInsertionOrder(int counterIndex)
    {
        int index = RequireCounter(counterIndex), count = ~encodedCounts[index];
        var result = new DeferredEarlyShadowCurrentPrimitiveTuple[count];
        for (int i = 0; i < count; i++) result[i] = lists[index][i]!;
        return Array.AsReadOnly(result);
    }

    /// <summary>Borrow the genuinely allocated Main span at RBP-70/-80.
    /// The current arena cursor must already include that allocation. Merely
    /// having an inventory tuple receipt does not authorize this operation.</summary>
    public void RetainActualMainCategory(DeferredEarlyShadowCurrentModelCandidate actualModel,
        int actualCategory, IReadOnlyList<DeferredEarlyShadowActualMainTuple> actualAllocatedMainSpan)
    {
        var draws = RequireCategory(actualModel, actualCategory);
        ArgumentNullException.ThrowIfNull(actualAllocatedMainSpan);
        if (actualAllocatedMainSpan.Count != draws.Count || actualAllocatedMainSpan.Where((tuple, i) =>
                tuple.SourceDrawIndex != draws[i] || tuple.ActualTupleIdentity is null).Any())
            throw new InvalidDataException("Existing Main tuples must cover the same complete native category span in source order.");
        if (spans.ContainsKey((actualModel, actualCategory)))
            throw new InvalidOperationException("This category already has its original current tuple span.");
        var retained = actualAllocatedMainSpan.Select(tuple => new DeferredEarlyShadowCurrentPrimitiveTuple(
            actualModel, actualCategory, tuple.SourceDrawIndex, tuple.ExistingKey8, tuple.ActualTupleIdentity, null)).ToArray();
        spans.Add((actualModel, actualCategory), Array.AsReadOnly(retained));
    }

    /// <summary>Publish a retained Main span to View19A0[category], when its
    /// actual Main publication supplies that membership. The existing tuple
    /// allocation is neither charged again nor assigned a different key.</summary>
    public bool AppendRetainedMainCategory(DeferredEarlyShadowCurrentModelCandidate actualModel,
        int actualCategory)
    {
        RequireCategory(actualModel, actualCategory);
        if (!spans.TryGetValue((actualModel, actualCategory), out var tuples) ||
            tuples.Any(tuple => tuple.RetainedMainTupleIdentity is null))
            throw new InvalidOperationException("This Main counter has no genuinely allocated current Main category span.");
        return TryAppendCompleteSpan(actualCategory, tuples);
    }

    /// <summary>C3A071 onward: actual accepted passes0..3 publish to counters
    /// 2..5 in ascending order. A first allocation may survive a counter
    /// refusal: BB85A0 ignores BB8680's result and returns true after allocating
    /// or reusing a span. No per-pass clone or key rewrite occurs.</summary>
    public IReadOnlyList<DeferredEarlyShadowCategoryAppendResult> PublishFirstCasterCategory(
        DeferredEarlyShadowCurrentModelCandidate actualModel, int actualCategory,
        uint actualAcceptedPassMask, IReadOnlyList<float?> actualCurrentDistances400)
    {
        var draws = RequireCategory(actualModel, actualCategory);
        ArgumentNullException.ThrowIfNull(actualCurrentDistances400);
        if ((actualAcceptedPassMask & ~15u) != 0 || actualCurrentDistances400.Count is < 1 or > 4 ||
            (actualAcceptedPassMask >> actualCurrentDistances400.Count) != 0)
            throw new ArgumentException("First caster publication needs the actual one to four compact pass distances.");
        if (draws.Count == 0) return Array.Empty<DeferredEarlyShadowCategoryAppendResult>();
        var result = new List<DeferredEarlyShadowCategoryAppendResult>(4);
        for (int pass = 0; pass < actualCurrentDistances400.Count; pass++)
        {
            if ((actualAcceptedPassMask & (1u << pass)) == 0) continue;
            float distance = actualCurrentDistances400[pass] ??
                throw new InvalidDataException("An accepted current caster pass has no distance400 written by its culler.");
            ulong key = DeferredEarlyShadowCasterSource.PrepareFirstAcceptedDistanceKey(distance);
            bool allocated = false;
            if (!spans.TryGetValue((actualModel, actualCategory), out var tuples))
            {
                if (!Arena.TryReserve(checked(draws.Count * 16), out int offset))
                {
                    result.Add(new(pass + 2, actualCategory, key, false, false, false,
                        Array.Empty<DeferredEarlyShadowCurrentPrimitiveTuple>()));
                    continue;
                }
                var created = draws.Select((draw, i) => new DeferredEarlyShadowCurrentPrimitiveTuple(
                    actualModel, actualCategory, draw, key, null, checked(offset + i * 16))).ToArray();
                spans.Add((actualModel, actualCategory), tuples = Array.AsReadOnly(created)); allocated = true;
            }
            bool appended = TryAppendCompleteSpan(pass + 2, tuples);
            result.Add(new(pass + 2, actualCategory, key, true, appended, allocated, tuples));
        }
        return result.AsReadOnly();
    }

    /// <summary>C394F0 retains this same category pointer across Main,
    /// shadow and capture publication. A later destination must not rewrite
    /// the first allocator's key, even if its list previously refused the span.</summary>
    public IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple>? GetOrAllocateCurrentCategory(
        DeferredEarlyShadowCurrentModelCandidate actualModel, int actualCategory,
        ulong actualFirstAllocatorKey)
    {
        var draws = RequireCategory(actualModel, actualCategory);
        if (spans.TryGetValue((actualModel, actualCategory), out var retained)) return retained;
        if (!Arena.TryReserve(checked(draws.Count * 16), out int offset)) return null;
        var created = draws.Select((draw, i) => new DeferredEarlyShadowCurrentPrimitiveTuple(
            actualModel, actualCategory, draw, actualFirstAllocatorKey, null, checked(offset + i * 16))).ToArray();
        var result = Array.AsReadOnly(created);
        spans.Add((actualModel, actualCategory), result);
        return result;
    }

    private bool TryAppendCompleteSpan(int counter, IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> tuples)
    {
        int used = ~encodedCounts[counter];
        // BB86B5/B7 JGE: used+count == capacity is a refusal, not an exact fit.
        if (used < 0 || tuples.Count >= lists[counter].Length - used) return false;
        encodedCounts[counter] -= tuples.Count;
        for (int i = 0; i < tuples.Count; i++) lists[counter][used + i] = tuples[i];
        return true;
    }

    private static IReadOnlyList<int> RequireCategory(DeferredEarlyShadowCurrentModelCandidate model, int category)
    {
        ArgumentNullException.ThrowIfNull(model);
        return category switch
        {
            0 => model.Category0.SourceDrawIndices,
            1 => model.Category1.SourceDrawIndices,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }
    private static int RequireCounter(int counter)
        => (uint)counter < 11 ? counter : throw new ArgumentOutOfRangeException(nameof(counter));
}
