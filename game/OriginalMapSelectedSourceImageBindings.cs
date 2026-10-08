using San14.Data;

/// <summary>Selected current image bindings and their completed source-view
/// leases. Keep this batch until every cached draw that uses its bindings retires.</summary>
public sealed class OriginalMapSelectedSourceImageBindings : IAsyncDisposable
{
    public OriginalMapSelectedSourceImagePlan Source { get; }
    private readonly IReadOnlyList<OriginalDeferredDeviceBinding> bindings;
    public IReadOnlyList<OriginalDeferredDeviceBinding> Bindings
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref leases) is null,this);
            return bindings;
        }
    }
    private IReadOnlyList<OriginalDeferredAuxiliaryTargetBorrow>? leases;

    internal OriginalMapSelectedSourceImageBindings(OriginalMapSelectedSourceImagePlan source,
        IReadOnlyList<OriginalDeferredDeviceBinding> bindings,
        IReadOnlyList<OriginalDeferredAuxiliaryTargetBorrow> borrowed)
        => (Source, this.bindings, leases) = (source, bindings, borrowed);

    /// <summary>Call after the ordered consumer read has submitted or canceled.
    /// Lifetime leases still retain the views until cached draw retirement.</summary>
    public void CompleteRead()
    {
        var reading=Volatile.Read(ref leases);
        if(reading is not null)foreach(var lease in reading)lease.CompleteRead();
    }

    public ValueTask DisposeAsync()
    {
        var retiring = Interlocked.Exchange(ref leases, null);
        if (retiring is not null)
            for (int index = retiring.Count - 1; index >= 0; index--) retiring[index].Dispose();
        return ValueTask.CompletedTask;
    }
}
