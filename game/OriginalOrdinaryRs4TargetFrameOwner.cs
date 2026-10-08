using San14.Data;

/// <summary>The real node43 seven-MRT attachment package over existing
/// shared AB/NS/RS/RR/D1 storage. Only these views/framebuffer are owned here.
/// Target binding/pop and queue side effects belong to the ordered caller.</summary>
public sealed class OriginalOrdinaryRs4TargetFrameOwner : IAsyncDisposable
{
    private bool retired;
    private OriginalOrdinaryRs4TargetFrameOwner(LoadedOriginalOrdinaryRs4Source source,
        OriginalRenderTargetFrame frame, OriginalRenderTargetFrame storageOwner)
        => (Source, Frame, SharedStorageOwner) = (source, frame, storageOwner);
    public LoadedOriginalOrdinaryRs4Source Source { get; }
    public OriginalRenderTargetFrame Frame { get; }
    public OriginalRenderTargetFrame SharedStorageOwner { get; }

    /// <summary>One physical target generation. Call after source/storage
    /// creation, retain for ordinary requests, rebuild when storage resizes.
    /// Borrowed RS4 draw owners must retire before these attachment views.</summary>
    public static async Task<OriginalOrdinaryRs4TargetFrameOwner> BuildAsync(
        LoadedOriginalOrdinaryRs4Source source, OriginalRenderTargetFrame actualSharedStorage)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualSharedStorage);
        var frame = await OriginalRenderTargetFrame.BuildAttachmentFrameAsync(source.Targets43, actualSharedStorage);
        return new(source, frame, actualSharedStorage);
    }
    public void RequireCurrent(OriginalRenderTargetFrame actualCurrentSharedStorage)
    {
        if (retired || !ReferenceEquals(actualCurrentSharedStorage, SharedStorageOwner))
            throw new InvalidDataException("RS4 attachment frame belongs to a retired target storage generation.");
    }
    public ValueTask DisposeAsync()
    {
        retired = true;
        // Frame retains its actual disposal task. Repeated callers must
        // observe that task's failure instead of treating retirement as
        // successful release and allowing the shared storage to disappear.
        return Frame.DisposeAsync();
    }
}
