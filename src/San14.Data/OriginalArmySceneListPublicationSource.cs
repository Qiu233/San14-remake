namespace San14.Data;

/// <summary>The actual source group fields consumed by1C6EF0. Data18 is the
/// created InstanceData for Kind10=1, or the actual ModelData for Kind10=2.
/// InstanceChildData30 is the InstanceCPU child returned byBCE3A0. Group route
/// fields are not catalog/display categories or ordinary-camera visibility.</summary>
public sealed record OriginalArmySceneListGroup<TData>(uint Category8, uint Kind10,
    uint Enabled14, TData? Data18, bool InstanceCpu20Present, TData? InstanceChildData30,
    uint BatchRoute20, int AnimationRoute24) where TData : class;

/// <summary>One current retained manager batch ModelData and its separate
/// activation DWORD. Active3CF0 is not inferred from the number of actors.</summary>
public sealed record OriginalArmySceneBatchModel<TData>(TData? Data3CB8,
    bool Cpu20Present, uint Active3CF0) where TData : class;

public enum OriginalArmySceneListGroupCallKind { DirectAppend, GroupedModelRequest }

public sealed record OriginalArmySceneListGroupCall<TData>(
    OriginalArmySceneListGroupCallKind Kind, TData ActualGroupModelData,
    int? BatchModelIndex, TData? ActualBatchModelData) where TData : class;

public sealed record OriginalArmySceneDescriptorPublicationOrder(
    IReadOnlyList<int> DescriptorOffsets, bool RequiresCurrentAuxiliary580);

public sealed record OriginalArmySceneBatchAppend<TData>(uint DestinationListObjectId,
    int BatchModelIndex, TData Data) where TData : class;

/// <summary>Finite original Unit/Army scene-list consumers.44C7B0 passes the
/// actual Game98 Data20 CPU to1C7270, then1C6E80. This source does not create
/// descriptor groups, infer ready flags, runD9F640 or build a shadow queue.</summary>
public sealed class LoadedOriginalArmySceneListPublicationSource
{
    internal LoadedOriginalArmySceneListPublicationSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);

    public const uint DestinationListObjectId = 0xE65D70AE;
    public const int BatchModelCount = 7;
    public string Locale { get; }
    public string SourceEvidence { get; }

    /// <summary>Actual1C6FD0 order, before the mode0 auxiliary580 consumer.
    /// The latter has independent handle170/198 and per-node selection words;
    /// an absent visible overlay does not establish that its source is empty.</summary>
    public OriginalArmySceneDescriptorPublicationOrder ReadDescriptorPublicationOrder(
        int actualMode740, int actualFlag748)
    {
        var offsets = actualMode740 switch
        {
            0 => new List<int> { 0x750, 0x778, 0x7C8 },
            1 => new List<int> { 0x818 },
            2 => new List<int> { 0x7A0 },
            3 => new List<int> { 0x840, 0x750, 0x778, 0x7C8, 0x7A0 },
            _ => []
        };
        if (actualFlag748 != 0) offsets.Add(0x7F0);
        return new(offsets.AsReadOnly(), actualMode740 == 0);
    }

    /// <summary>Actual1C5040 selector. Category8 is the group source category;
    /// AnimationRoute24 is the current1CEB90 animation selection, not a force
    /// color. A negative/outside selection returns the original failure -1.</summary>
    public int ReadBatchModelIndex(uint actualGroupCategory8, int actualAnimationRoute24)
    {
        if (actualAnimationRoute24 < 0) return -1;
        return actualGroupCategory8 switch
        {
            0 when actualAnimationRoute24 <= 2 => 4 + actualAnimationRoute24,
            1 when actualAnimationRoute24 <= 3 => actualAnimationRoute24,
            _ => -1
        };
    }

    /// <summary>1C6EF0 direct-v8 or1C2010 grouped-model call selection. The
    /// descriptor-level1C71F0 target-CPU null gate precedes this method; the
    /// group function itself tests destination only on the direct branch.</summary>
    public OriginalArmySceneListGroupCall<TData>? PrepareCurrentGroupCall<TData>(
        OriginalArmySceneListGroup<TData> actualGroup,
        IReadOnlyList<OriginalArmySceneBatchModel<TData>> actualBatchModels,
        bool actualDestinationCpuPresent) where TData : class
    {
        ArgumentNullException.ThrowIfNull(actualGroup);
        if (actualGroup.Enabled14 == 0) return null;
        TData? data = actualGroup.Kind10 switch
        {
            1 when actualGroup.InstanceCpu20Present => actualGroup.InstanceChildData30,
            2 => actualGroup.Data18,
            _ => null
        };
        if (data is null) return null;
        if (actualGroup.BatchRoute20 == 0)
            return actualDestinationCpuPresent
                ? new(OriginalArmySceneListGroupCallKind.DirectAppend, data, null, null) : null;
        if (actualGroup.Category8 > 0x60) return null;
        int index = ReadBatchModelIndex(actualGroup.Category8, actualGroup.AnimationRoute24);
        if (index < 0) return null;
        RequireBatchModels(actualBatchModels);
        var batch = actualBatchModels[index];
        return batch.Data3CB8 is not null && batch.Cpu20Present
            ? new(OriginalArmySceneListGroupCallKind.GroupedModelRequest, data, index, batch.Data3CB8) : null;
    }

    /// <summary>1C6E80 appends active manager3CB8 models in index0..6 order.
    /// It checks Data and3CF0, but does not check that Data's CPU or deduplicate.
    /// This executes after current unit.v28 collection, including other kinds.</summary>
    public IReadOnlyList<OriginalArmySceneBatchAppend<TData>> PrepareCurrentBatchAppends<TData>(
        IReadOnlyList<OriginalArmySceneBatchModel<TData>> actualBatchModels,
        bool actualDestinationCpuPresent) where TData : class
    {
        RequireBatchModels(actualBatchModels);
        var appends = new List<OriginalArmySceneBatchAppend<TData>>();
        if (!actualDestinationCpuPresent) return appends.AsReadOnly();
        for (int index = 0; index < BatchModelCount; index++)
        {
            var entry = actualBatchModels[index];
            if (entry.Data3CB8 is not null && entry.Active3CF0 != 0)
                appends.Add(new(DestinationListObjectId, index, entry.Data3CB8));
        }
        return appends.AsReadOnly();
    }

    private static void RequireBatchModels<TData>(IReadOnlyList<OriginalArmySceneBatchModel<TData>> actual)
        where TData : class
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (actual.Count != BatchModelCount)
            throw new InvalidDataException("Army list publication requires all seven current manager batch ModelData entries.");
    }
}

public static class OriginalArmySceneListPublicationSource
{
    public static LoadedOriginalArmySceneListPublicationSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadArmySceneListPublicationSource(repository.SourceDirectory, locale);
    }
}
