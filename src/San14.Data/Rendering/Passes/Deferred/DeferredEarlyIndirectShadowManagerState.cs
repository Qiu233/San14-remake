namespace San14.Data;

/// <summary>D77940's retained CPU manager loans. T is the caller's actual
/// resource/CPU owner type. Keeping a loan does not grant texture pixels.</summary>
public sealed class DeferredEarlyIndirectShadowManagerState<T> where T : class
{
    private IReadOnlyList<T?> currentRows = Array.Empty<T?>();
    private bool acquired;
    public T? SelectedShadowCpu { get; private set; }
    public T? LitBuffer { get; private set; }
    public IReadOnlyList<T?> CurrentRows => currentRows;
    public int CurrentCount => currentRows.Count;
    public bool SourceCleanupRequired => acquired;
    public string SourceEvidence => "D77940/View1BFC-low4/View1BD0-slots30..48-CPU-dereference;" +
        "9B4540(rows,currentCPU,false)/9B4530(count)/9B4550(Lit,false);source-loans-only/no-pixel-publication";

    /// <summary>Slots are the four actual wrapper→CPU getter results, including
    /// resolved null slots. The source appends active slots in index order.</summary>
    public void PrepareCurrent(MapSourceCreatedF103CurrentFrame actualGeometry,
        T? actualLitBuffer, T? actualShadowCpu, IReadOnlyList<T?> actualViewShadowCpuSlots)
    {
        ArgumentNullException.ThrowIfNull(actualGeometry); ArgumentNullException.ThrowIfNull(actualViewShadowCpuSlots);
        if (acquired) throw new InvalidOperationException("Early68's previous source shadow-manager loan has not returned.");
        if (actualLitBuffer is null) return; //D77940 entry leaves the output/manager unchanged.
        if (actualViewShadowCpuSlots.Count != 4)
            throw new InvalidDataException("Early68 D77940 requires four actual nullable wrapper CPU results.");
        uint bits = unchecked((uint)actualGeometry.ViewFields.CurrentPublication.ShadowPrimitiveCount1BFC);
        var rows = new List<T?>();
        for (int i = 0; i < 4; i++) if ((bits & (1u << i)) != 0) rows.Add(actualViewShadowCpuSlots[i]);
        SelectedShadowCpu = actualShadowCpu; currentRows = rows.AsReadOnly(); LitBuffer = actualLitBuffer; acquired = true;
    }
    public void ClearAfterSourcePass()
    {
        if (!acquired) return;
        //EBD847→EBD85C→EBD866: null Lit, clear rows/currentCPU, then count0.
        LitBuffer = null; currentRows = Array.Empty<T?>(); SelectedShadowCpu = null; acquired = false;
    }
}
