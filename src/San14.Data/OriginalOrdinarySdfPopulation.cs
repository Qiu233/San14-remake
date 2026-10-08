namespace San14.Data;

/// <summary>The five actual Frame3D8 pool words. These belong to the renderer
/// settings/provider pool, not to RawCamera fields at coincident offsets.</summary>
public readonly record struct OriginalOrdinarySdfOperation1PoolFields(
    uint SlotCapacity4, uint RecordCapacity8, uint VolumeWidthC,
    uint VolumeHeight10, uint VolumeDepth14)
{
    public ulong Records48AllocationBytes => (ulong)RecordCapacity8 * 48;
    public ulong Metadata80AllocationBytes => (ulong)RecordCapacity8 * 80;
    public uint Records48ByteBudget => unchecked(RecordCapacity8 * 48);
    public uint Metadata80ByteBudget => unchecked(RecordCapacity8 * 80);
    public uint VolumeByteBudget => unchecked(VolumeWidthC * VolumeHeight10 * VolumeDepth14 * 16);
}

public readonly record struct OriginalOrdinarySdfOperation1Reservation(
    int? Record48Offset, int? Metadata80Offset)
{
    public bool BothPresent => Record48Offset.HasValue && Metadata80Offset.HasValue;
}

/// <summary>The retained CPU arrays of one source View1C58 provider. D6D410
/// resets the two scratch arenas, while D1DED0 alone publishes Count9C.
/// GPU buffer48/50/58/60/68/70 identities remain with their actual device owner.</summary>
public sealed class OriginalOrdinarySdfOperation1Bank
{
    private readonly byte[] records48, metadata80;
    private byte[] publishedMetadata80;
    private readonly OriginalOrdinarySdfVolumeDescriptor?[] collectedVolumes, publishedVolumes;
    private ulong used18, used30;
    private bool scratchAvailable;

    internal OriginalOrdinarySdfOperation1Bank(OriginalOrdinarySdfOperation1PoolFields fields)
    {
        Fields = fields;
        records48 = new byte[checked((int)fields.Records48AllocationBytes)];
        metadata80 = new byte[checked((int)fields.Metadata80AllocationBytes)];
        publishedMetadata80 = new byte[metadata80.Length];
        collectedVolumes = new OriginalOrdinarySdfVolumeDescriptor?[metadata80.Length / 80];
        publishedVolumes = new OriginalOrdinarySdfVolumeDescriptor?[collectedVolumes.Length];
        // D02BD3=pool4; D02BD9 explicitly writes the fresh 9C DWORD0.
        Count98 = fields.SlotCapacity4;
        Count9C = 0;
        ResetAcquiredScratch();
    }

    public OriginalOrdinarySdfOperation1PoolFields Fields { get; }
    public uint Count98 { get; }
    public uint Count9C { get; private set; }
    public ulong UsedRecords18 => used18;
    public ulong UsedMetadata30 => used30;
    public ReadOnlyMemory<byte> PublishedMetadata80 => publishedMetadata80;

    /// <summary>Normal typed metadata uses this semantic getter. It keeps the
    /// held source resource identity instead of encoding an RDB key asQWORD40.
    /// Raw metadata bytes are not a pointer/unknown-tail proof for this path.</summary>
    public OriginalOrdinarySdfVolumeDescriptor ReadCurrentPublishedVolume(uint index)
    {
        if (index >= Count9C) throw new ArgumentOutOfRangeException(nameof(index));
        return publishedVolumes[index] ?? throw new InvalidOperationException("The current SDF row has no typed volume metadata publication.");
    }

    /// <summary>D6D410 restores backing arrays80/88 and their budgets90/94;
    /// it does not reset the last finalized 9C or the published metadata.</summary>
    public void ResetAcquiredScratch()
    { used18 = 0; used30 = 0; scratchAvailable = true; }

    /// <summary>D69FE0 performs both independent reservations. A successful
    /// first reservation is retained when the second one fails; no rollback.</summary>
    public OriginalOrdinarySdfOperation1Reservation ReserveCurrentVolume()
    {
        if (!scratchAvailable)
            throw new InvalidOperationException("Current SDF provider scratch needs its actual acquire/reset before collection.");
        int? Reserve(ref ulong used, ulong size, ulong capacity)
        {
            ulong end = unchecked(used + size);
            if (end > capacity) return null;
            int offset = checked((int)used); used = end; return offset;
        }
        int? a = Reserve(ref used18, 48, Fields.Records48ByteBudget);
        int? b = Reserve(ref used30, 80, Fields.Metadata80ByteBudget);
        return new(a, b);
    }

    /// <summary>Register the two actual outputs of D69D70 after its current
    /// ModelSdfD0/current-model/View cull gates. This is not a draw count or
    /// a G1MG bounding-box approximation; caller supplies both source records.</summary>
    public bool AppendCurrentVolume(ReadOnlySpan<byte> actualRecord48,
        ReadOnlySpan<byte> actualMetadata80)
    {
        if (actualRecord48.Length != 48 || actualMetadata80.Length != 80)
            throw new ArgumentException("SDF volume registration requires its source 48B and 80B records.");
        var reservation = ReserveCurrentVolume();
        if (!reservation.BothPresent) return false;
        actualRecord48.CopyTo(records48.AsSpan(reservation.Record48Offset!.Value, 48));
        actualMetadata80.CopyTo(metadata80.AsSpan(reservation.Metadata80Offset!.Value, 80));
        collectedVolumes[reservation.Metadata80Offset.Value / 80] = null;
        return true;
    }

    /// <summary>Normal D69D70 semantic publication after its real selected View
    /// cull. ActualVolumeResource20 is retained with the row; no native pointer
    /// or eight unwritten metadata tail bytes are invented by the port.</summary>
    public bool AppendCurrentVolume(OriginalOrdinarySdfVolumeDescriptor actualVolume)
    {
        ArgumentNullException.ThrowIfNull(actualVolume);
        var reservation = ReserveCurrentVolume();
        if (!reservation.BothPresent) return false;
        actualVolume.WriteSourceRecord48().CopyTo(records48, reservation.Record48Offset!.Value);
        actualVolume.WriteSourceMetadataMatrix64().CopyTo(metadata80, reservation.Metadata80Offset!.Value);
        collectedVolumes[reservation.Metadata80Offset.Value / 80] = actualVolume; return true;
    }

    /// <summary>The actual source View culler must run before reserve/append.
    /// Returningfalse here preserves both scratch used counters.</summary>
    public bool RegisterCurrentViewVolume(OriginalOrdinarySdfVolumeDescriptor actualVolume,
        Func<OriginalOrdinarySdfVolumeViewBox,bool> testActualCurrentView)
    {
        ArgumentNullException.ThrowIfNull(actualVolume); ArgumentNullException.ThrowIfNull(testActualCurrentView);
        return testActualCurrentView(actualVolume.ViewBox) && AppendCurrentVolume(actualVolume);
    }

    /// <summary>D1DED0 reads the actual collected scratch size, uploads each
    /// 48B record through buffer48 and copies actual used metadata into array78.
    /// The callback must execute its real transport; a recipe is not completion.
    /// On count0 the source neither uploads nor overwrites retained metadata.</summary>
    public void FinalizeCurrentCollection(Action<ReadOnlyMemory<byte>> uploadActualRecords48)
    {
        ArgumentNullException.ThrowIfNull(uploadActualRecords48);
        if (!scratchAvailable)
            throw new InvalidOperationException("SDF provider collection was already finalized.");
        uint count = unchecked((uint)(used18 / 48));
        if (count != 0)
        {
            uploadActualRecords48(records48.AsMemory(0, checked((int)used18)));
            metadata80.AsSpan(0, checked((int)used30)).CopyTo(publishedMetadata80);
            collectedVolumes.AsSpan(0, checked((int)(used30 / 80))).CopyTo(publishedVolumes);
        }
        // D1E005..021 clears only the two scratch arenas; D1E026 writes9C.
        used18 = 0; used30 = 0; scratchAvailable = false; Count9C = count;
    }
}

/// <summary>One import of the complete paired source bodies. Current renderer
/// capacities, records, resource identities and collection stay dynamic.</summary>
public sealed class LoadedOriginalOrdinarySdfPopulationSource
{
    internal LoadedOriginalOrdinarySdfPopulationSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public OriginalOrdinarySdfModelShapeCatalog ModelShapes { get; } = OriginalOrdinarySdfModelShapeCatalog.ConstructImported();
    public OriginalOrdinarySdfOperation0Bank? CreateOperation0ViewBank(
        OriginalOrdinarySdfOperation0PoolFields actualCurrentPoolFields)
        => actualCurrentPoolFields.SphereCapacity4 == 0 || actualCurrentPoolFields.DualCapacity8 == 0
            ? null : new(actualCurrentPoolFields);
    public OriginalOrdinarySdfOperation0Ring CreateOperation0Ring(uint sphereCapacity, uint dualCapacity)
        => new(sphereCapacity, dualCapacity);
    public OriginalOrdinarySdfVolumeDescriptor DescribeCurrentVolume(
        OriginalOrdinarySdfVolumeHeader actualHeader28, OriginalOrdinarySdfVolumeTransform actualWorld,
        object? actualVolumeResource20)
        => OriginalOrdinarySdfVolumeMath.DescribeCurrent(actualHeader28, actualWorld, actualVolumeResource20);
    public OriginalOrdinarySdfOperation1Bank? CreateOperation1Provider(
        OriginalOrdinarySdfOperation1PoolFields actualCurrentPoolFields)
        // D0B9B7 actual gate is pool4 alone. Retained pool reuse is separate.
        => actualCurrentPoolFields.SlotCapacity4 == 0 ? null : new(actualCurrentPoolFields);
}

public static class OriginalOrdinarySdfPopulationSource
{
    public static LoadedOriginalOrdinarySdfPopulationSource Load(
        OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(locale, OriginalDeferredSamplerSources.LoadOrdinarySdfPopulationNativeSource(
            repository.SourceDirectory, locale));
    }
}
