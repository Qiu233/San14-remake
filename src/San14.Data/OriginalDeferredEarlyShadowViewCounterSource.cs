namespace San14.Data;

/// <summary>The eleven original View19A0 counter capacities. Unrepresented
/// prepared A98/106C inputs leave their own counters unknown; the first caster family
/// capacities2..5 remain independently available.</summary>
public sealed class OriginalDeferredEarlyShadowCurrentViewCounterCapacities
{
    internal OriginalDeferredEarlyShadowCurrentViewCounterCapacities(uint maximum,
        uint caster, uint? auxiliary, uint? last, string evidence)
    {
        MaximumPrimaryCapacity = maximum;
        Capacities = Array.AsReadOnly(new uint?[] { maximum, maximum, caster, caster, caster, caster,
            auxiliary, auxiliary, auxiliary, auxiliary, last });
        SourceEvidence = evidence;
    }
    public uint MaximumPrimaryCapacity { get; }
    public IReadOnlyList<uint?> Capacities { get; }
    public int SourceCounterCount => 11;
    public bool SourceAllocationRequested => MaximumPrimaryCapacity != 0;
    public string SourceEvidence { get; }
    public uint RequireFirstCasterPassCapacity(int actualPass)
    {
        if ((uint)actualPass >= 4) throw new ArgumentOutOfRangeException(nameof(actualPass));
        return Capacities[2 + actualPass]!.Value;
    }
    public OriginalDeferredEarlyShadowViewCounterAllocationLayout RequireCompleteAllocationLayout()
    {
        if (Capacities.Any(value => value is null))
            throw new NotSupportedException("The exact eleven-counter allocation requires the actual prepared Camera A98 and106C inputs.");
        uint[] capacities = Capacities.Select(value => value!.Value).ToArray();
        return OriginalDeferredEarlyShadowViewCounterSource.PrepareNumericAllocationLayout(capacities);
    }
}

public sealed record OriginalDeferredEarlyShadowViewCounterAllocationLayout(
    uint ArenaByteLength, IReadOnlyList<uint> PointerListByteOffsets, IReadOnlyList<uint> Capacities)
{
    public uint HeaderByteLength => 0xB0;
    public int InitialEncodedCountC => -1;
}

public readonly record struct OriginalDeferredEarlyShadowKind1CounterCapacityInput(
    uint CapacityFromPlainRecordTag, bool CurrentRecordShadowWrapper0Present);

public static class OriginalDeferredEarlyShadowViewCounterSource
{
    /// <summary>D78900 before original model submission, consuming the complete
    /// actual kind1 queue. Only records with null current shadow wrapper0
    /// contribute to the max; View1BD0 presence separately controls2..5.</summary>
    public static OriginalDeferredEarlyShadowCurrentViewCounterCapacities PrepareCurrent(
        OriginalDeferredEarlyShadowCurrentCasterLightRows actualLights,
        bool actualSelectedRecord1BD0Present, int? actualCameraSignedA98, int? actualCameraSigned106C)
    {
        ArgumentNullException.ThrowIfNull(actualLights);
        var numeric = PrepareNumericCapacities(actualLights.Rows.Select(row =>
            new OriginalDeferredEarlyShadowKind1CounterCapacityInput(checked((uint)row.Record.FirstCounterCapacity),
                row.RecordShadowWrapper0Present)).ToArray(), actualSelectedRecord1BD0Present, actualCameraSignedA98, actualCameraSigned106C);
        return new(numeric.MaximumPrimaryCapacity, numeric.RequireFirstCasterPassCapacity(0), numeric.Capacities[6],
            numeric.Capacities[10], actualLights.Queue.QueueOwnerIdentity + ";" + numeric.SourceEvidence);
    }

    /// <summary>The actual D78900 capacity/layout kernel with explicit complete
    /// kind1 rows. Numerical inputs alone are not a current queue publication.</summary>
    public static OriginalDeferredEarlyShadowCurrentViewCounterCapacities PrepareNumericCapacities(
        IReadOnlyList<OriginalDeferredEarlyShadowKind1CounterCapacityInput> actualCompleteKind1Rows,
        bool actualSelectedRecord1BD0Present, int? actualCameraSignedA98, int? actualCameraSigned106C)
    {
        ArgumentNullException.ThrowIfNull(actualCompleteKind1Rows);
        uint maximum = 0;
        foreach (var row in actualCompleteKind1Rows)
        {
            if (row.CapacityFromPlainRecordTag > 0xFFFFF)
                throw new NotSupportedException("D78900 reads the capacity from the actual lower20-bit plain-record tag.");
            if (!row.CurrentRecordShadowWrapper0Present) maximum = Math.Max(maximum, row.CapacityFromPlainRecordTag);
        }
        uint caster = actualSelectedRecord1BD0Present ? maximum : 0;
        uint? auxiliary = actualCameraSignedA98 is { } a98 ? a98 < 512 ? 0u : Math.Max(maximum >> 6, 64u) : null;
        uint? last = actualCameraSigned106C is { } camera106C ? camera106C > 0 ? maximum : 0u : null;
        return new(maximum, caster, auxiliary, last,
            "TC:D789FE:max0;D78B13:capacity-tag-lower20;D78BA8/D78BB3:current-record0-null-max;" +
            "D78C8B/D78C9B:1BD0->caster-capacity;C99980:A98-query;D78CB7:CMOVBE/max64;" +
            "D78CC0:RBP-FDC=header106C;D78D70..D78E4C:11-counters");
    }

    public static OriginalDeferredEarlyShadowViewCounterAllocationLayout PrepareNumericAllocationLayout(
        IReadOnlyList<uint> actualCompleteCapacities)
    {
        ArgumentNullException.ThrowIfNull(actualCompleteCapacities);
        if (actualCompleteCapacities.Count != 11 || actualCompleteCapacities.Any(value => value > 0xFFFFF))
            throw new ArgumentException("View19A0 requires all eleven source capacities in the tag's finite20-bit range.", nameof(actualCompleteCapacities));
        uint cursor = 0xB0;
        var offsets = new uint[11];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = cursor;
            cursor = checked(cursor + ((actualCompleteCapacities[i] * 8 + 15) & ~15u));
        }
        // BF's rounding in D78D12..D78D1D is the B0 header plus
        // align-up bias, not an additional16-byte tail.
        return new(cursor, Array.AsReadOnly(offsets), Array.AsReadOnly(actualCompleteCapacities.ToArray()));
    }
}
