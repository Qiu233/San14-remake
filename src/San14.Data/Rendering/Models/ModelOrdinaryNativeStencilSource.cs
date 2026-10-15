namespace San14.Data;

/// <summary>Current owned renderer state that a source declaration cannot
/// certify after edits. Every gate refers to this selected renderer and this
/// primitive's current publication; unknown values are rejected.</summary>
public sealed record ModelFreshOwnedStencilPublication(long FrameNumber, string ViewIdentity,
    string PrimitiveIdentity, string SelectedRendererOwnerIdentity, uint SelectedProviderObjectId,
    string SelectedShaderSha256, bool? SourceConstructorAndStartupRegistryRetained,
    bool? ModifierCacheUnchanged, bool? CompletePluginChainAndDefaultCallbacksUnchanged,
    bool? CurrentSelectedProviderAndBindingsUnchanged, bool? NoPrimitiveOrContextStencilOverrides,
    string CurrentProducerProvenance);

/// <summary>Final stencil state at the real primitive submission. This is
/// separate from postdraw cleanup and the later dispatcher disable. It does
/// not certify depth/color/blend state, resource readiness or a GPU draw.</summary>
public sealed class ModelOrdinaryNativeStencilReceipt
{
    internal ModelOrdinaryNativeStencilReceipt(ModelOrdinaryNativeStencilSource source,
        ModelOrdinaryDispatcherStencilPacket state, ModelFreshOwnedStencilPublication current)
    {
        Source = source; DrawTimeState = state; CurrentPublication = current;
        // Actual PB2 cleanup preserves80 when40 is set. B7 is clear in this
        // admitted fresh modifier scope. Other current cleanup gates remain
        // explicit rather than becoming a fabricated saved-state restoration.
        ReferenceAfterPrimitiveCleanup = (state.Reference & 0x80) == 0 || (state.Reference & 0x40) != 0
            ? state.Reference : null;
    }
    public ModelOrdinaryNativeStencilSource Source { get; }
    public ModelOrdinaryDispatcherStencilPacket DrawTimeState { get; }
    public ModelFreshOwnedStencilPublication CurrentPublication { get; }
    public bool ShouldSubmitPrimitive => DrawTimeState.HasPrimitiveSubmission;
    public byte? ReferenceAfterPrimitiveCleanup { get; }
    public bool StencilEnabledAfterDispatcher => false;
    public string SourceProvenance => Source.SourceProvenance + ";" + CurrentPublication.CurrentProducerProvenance +
        ";actual-class18-hooks-preserve-draw-time-stencil;postdraw-reference=" +
        (ReferenceAfterPrimitiveCleanup?.ToString("X2") ?? "requires-current-cleanup-gates") +
        ";D48D05-dispatcher-disable;no-GPU-or-whole-raster-receipt";
}

/// <summary>The actual class18 creator installs its renderer with plugin
/// head1460. This finite source recipe excludes all three
/// subsequent stencil-reference writers using the real initialized modifier
/// header and created GBuffer plugin.</summary>
public sealed class ModelOrdinaryNativeStencilSource
{
    private readonly ModelQueueStateSource queue;
    private ModelOrdinaryNativeStencilSource(ModelQueueStateSource sourceQueue,
        uint owner, uint provider, string shaderUri, string shaderSha256, string name,
        string providerDictionaryUri, string providerDictionarySha256)
    {
        queue = sourceQueue; OwnerObjectId = owner; ProviderObjectId = provider;
        ShaderUri = shaderUri; ShaderSha256 = shaderSha256; ShaderName = name;
        ProviderDictionaryUri = providerDictionaryUri; ProviderDictionarySha256 = providerDictionarySha256;
    }
    public uint OwnerObjectId { get; }
    public uint ProviderObjectId { get; }
    public string ShaderUri { get; }
    public string ShaderSha256 { get; }
    public string ShaderName { get; }
    public string GraphUri => queue.Graph.Source.ResourceUri;
    public string GraphSha256 => queue.Graph.Source.DecodedSha256;
    public string ProviderDictionaryUri { get; }
    public string ProviderDictionarySha256 { get; }
    public string SourceKey => $"{GraphUri}@{GraphSha256}/node{QueueNodeIndex}/state{StateIndex}|" +
        $"{ProviderDictionaryUri}@{ProviderDictionarySha256}/{OwnerObjectId:X8}/{ProviderObjectId:X8}|{ShaderUri}@{ShaderSha256}";
    public int QueueNodeIndex => queue.Node.Index;
    public int StateIndex => queue.ShaderStateOffset;
    public string SourceProvenance => $"actual-class18-stencil/{ShaderUri}@{ShaderSha256};" +
        $"owner={OwnerObjectId:X8}/provider={ProviderObjectId:X8}/queue={QueueNodeIndex}/state={StateIndex};" +
        "class18-renderer;aux6EA=0/GBuffer-only/SSSTransmit-absent;plugin-head1460;" +
        "stencil-reference-writers-skipped;PB2-cleanup";

    /// <summary>Read once for the full graph/provider/shader source identity.
    /// Resolve the selected provider from its imported declaration and require
    /// the supported default plugin chain. A neighbouring type16 slot cannot substitute.</summary>
    public static ModelOrdinaryNativeStencilSource ReadFreshSelected(
        ModelQueueStateSource actualQueue, ModelShaderInterfaceSource actualSelectedInterface,
        LoadedShaderContainer actualShader)
    {
        ArgumentNullException.ThrowIfNull(actualQueue); ArgumentNullException.ThrowIfNull(actualSelectedInterface);
        ArgumentNullException.ThrowIfNull(actualShader);
        var canonicalQueue = ModelRendererStateSourceReader.ReadQueue(actualQueue.Graph, actualQueue.Node.Index);
        if (actualQueue.Flag != canonicalQueue.Flag || actualQueue.Flag2 != canonicalQueue.Flag2 ||
            actualQueue.RenderType != canonicalQueue.RenderType || actualQueue.ShaderStateOffset != canonicalQueue.ShaderStateOffset ||
            actualQueue.ViewId != canonicalQueue.ViewId || actualQueue.ViewObjectHash != canonicalQueue.ViewObjectHash ||
            actualQueue.ClockwiseRendering != canonicalQueue.ClockwiseRendering ||
            actualSelectedInterface.StateIndex != canonicalQueue.ShaderStateOffset || canonicalQueue.RenderType != 0 ||
            (canonicalQueue.Flag & 2) == 0 || canonicalQueue.ShaderStateOffset == 15)
            throw new InvalidDataException("Final native stencil needs the actual supported late ordinary queue and selected state entry.");
        var objects = actualSelectedInterface.Owner.Objects;
        var canonicalOwner = ModelShaderResources.ReadDeclaredShader(objects,
            actualSelectedInterface.Owner.Object.ObjectId);
        var selected = ModelShaderResources.ResolveType16Interface(canonicalOwner, canonicalQueue.ShaderStateOffset);
        var provider = selected.Provider ??
            throw new InvalidDataException("A source-null selected interface has no primitive stencil receipt.");
        if (provider.Object.ClassId != 0x18EB1E56 || actualSelectedInterface.Provider is not { } supplied ||
            !ReferenceEquals(supplied.Objects, objects) ||
            supplied.Object.ObjectId != provider.Object.ObjectId || supplied.Object.ClassId != provider.Object.ClassId ||
            supplied.ShaderFileHash != provider.ShaderFileHash || provider.ShaderFileHash != actualShader.Source.FileId ||
            actualShader.Source.ResourceUri != GameResourceId.Rdb("system", provider.ShaderFileHash).ToString())
            throw new InvalidDataException("Selected source provider does not identify the actual class18 shader.");
        GBufferStencilConfiguration.RequireDefaultCallbacks(actualShader, allowSkippedModelModules: true);
        return new(canonicalQueue, canonicalOwner.Object.ObjectId, provider.Object.ObjectId,
            actualShader.Source.ResourceUri, actualShader.Source.DecodedSha256, actualShader.Container.NamePrefix,
            objects.Source.ResourceUri, objects.Source.DecodedSha256);
    }

    /// <summary>Current owned lifecycle publication extends the dispatcher
    /// through the real selected renderer. A retained/imported cache, changed
    /// plugin registry, callback, binding or context override must have its
    /// separate complete current source recipe.</summary>
    public ModelOrdinaryNativeStencilReceipt PrepareCurrent(
        ModelOrdinaryDispatcherRecord actualRecord, MapCommonRenderState currentCommon,
        uint actualContextFlags3E4, ModelFreshOwnedStencilPublication current)
    {
        ArgumentNullException.ThrowIfNull(actualRecord); ArgumentNullException.ThrowIfNull(current);
        if (current.FrameNumber != actualRecord.FrameNumber || current.ViewIdentity != actualRecord.ViewIdentity ||
            current.SelectedProviderObjectId != ProviderObjectId || current.SelectedShaderSha256 != ShaderSha256 ||
            new[] { current.PrimitiveIdentity, current.SelectedRendererOwnerIdentity, current.CurrentProducerProvenance }.Any(string.IsNullOrWhiteSpace) ||
            current.SourceConstructorAndStartupRegistryRetained != true || current.ModifierCacheUnchanged != true ||
            current.CompletePluginChainAndDefaultCallbacksUnchanged != true || current.CurrentSelectedProviderAndBindingsUnchanged != true ||
            current.NoPrimitiveOrContextStencilOverrides != true)
            throw new InvalidDataException("Final stencil needs the exact same-frame fresh owned renderer/cache/plugin/callback/binding publication.");
        var state = ModelOrdinaryDispatcherStencilSource.Prepare(queue, actualRecord, currentCommon, actualContextFlags3E4);
        if (state.NativeFailOperation != 0 || state.NativeDepthFailOperation != 0 || state.NativePassOperation != 2)
            throw new InvalidDataException("Final ordinary stencil requires the proven Keep/Keep/Replace operation setter.");
        return new(this, state, current);
    }
}
