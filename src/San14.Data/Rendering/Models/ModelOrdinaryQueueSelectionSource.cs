using San14.Data.Formats;

namespace San14.Data;

/// <summary>The original selected class18 renderer's fresh alpha queue
/// classification. It does not count World primitives or certify GPU work.</summary>
public sealed class ModelOrdinaryQueueSelectionSource
{
    private ModelOrdinaryQueueSelectionSource(ModelQueueStateSource queue,
        ModelOrdinaryNativeStencilSource selected, bool alpha)
        => (Queue, SelectedRenderer, NativeFreshAux4Bit8) = (queue, selected, alpha);

    public ModelQueueStateSource Queue { get; }
    public ModelOrdinaryNativeStencilSource SelectedRenderer { get; }
    public bool NativeFreshAux4Bit8 { get; }
    public bool SourceQueueSelectsShader => Queue.BackendAlphaEnabled == NativeFreshAux4Bit8;
    public string SourceEvidence => SelectedRenderer.SourceProvenance +
        ";C14C83/C12600-raw-configuration-AlphaTest-or-Dissolve-to-J1408-word4-bit8;" +
        "D367EF/D36822/D36828/D36831-alpha-queue-skip;no-GPU-or-World-count";

    /// <summary>Load/reload boundary. Even optional configurations with second
    /// word zero participate in C12600's name scan. Plugin creation and raw
    /// configuration presence are different source operations.</summary>
    public static ModelOrdinaryQueueSelectionSource ReadFreshSelected(
        ModelQueueStateSource actualQueue,
        ModelShaderInterfaceSource actualSelectedInterface, LoadedShaderContainer actualShader)
    {
        ArgumentNullException.ThrowIfNull(actualQueue);
        if (actualQueue.Node.Index is not (49 or 50 or 54 or 55) ||
            actualQueue.Flag != (actualQueue.Node.Index is 49 or 54 ? 0x0E : 0x06) ||
            actualQueue.Flag2 != (actualQueue.Node.Index is 49 or 50 ? 4u : 0u) ||
            actualQueue.RenderType != 0 || actualQueue.ViewId != 0 ||
            actualQueue.ShaderStateOffset != 18 || actualQueue.ClockwiseRendering)
            throw new NotSupportedException("Alpha queue selection needs the exact original ordinary View0 node49/50/54/55 profile.");
        // This existing loader authenticates graph, selected typed interface,
        // exact class18 creator and one of the four original G1S profiles.
        var selected = ModelOrdinaryNativeStencilSource.ReadFreshSelected(
            actualQueue, actualSelectedInterface, actualShader);
        var configurations = G1ShaderConfigurationReader.Read(actualShader.Source.Bytes,
            G2ShaderContainerReader.Read(actualShader.Source.Bytes)).Records;
        bool alpha = configurations.Any(c => c.Name is "AlphaTest" or "Dissolve");
        return new(actualQueue, selected, alpha);
    }

    /// <summary>Normal current-frame validation uses the actual retained
    /// renderer/cache lifecycle. It does not rehash loaded source bytes.</summary>
    public bool SelectsCurrentPrimitive(ModelFreshOwnedStencilPublication current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.FrameNumber <= 0 || string.IsNullOrWhiteSpace(current.ViewIdentity) ||
            string.IsNullOrWhiteSpace(current.PrimitiveIdentity) ||
            string.IsNullOrWhiteSpace(current.SelectedRendererOwnerIdentity) ||
            string.IsNullOrWhiteSpace(current.CurrentProducerProvenance) ||
            current.SelectedProviderObjectId != SelectedRenderer.ProviderObjectId ||
            current.SelectedShaderSha256 != SelectedRenderer.ShaderSha256 ||
            current.SourceConstructorAndStartupRegistryRetained != true ||
            current.ModifierCacheUnchanged != true ||
            current.CompletePluginChainAndDefaultCallbacksUnchanged != true ||
            current.CurrentSelectedProviderAndBindingsUnchanged != true ||
            current.NoPrimitiveOrContextStencilOverrides != true)
            throw new InvalidDataException("Alpha queue selection needs this current selected fresh renderer and unchanged modifier/plugin/provider lifecycle.");
        return SourceQueueSelectsShader;
    }

    public ModelQueueDepthState PrepareSourceQueueDepth(uint actualContextFlags3E4)
        => ModelRendererStateSourceReader.PrepareQueueDepth(Queue,
            (actualContextFlags3E4 & (1u << 29)) != 0);
}
