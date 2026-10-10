using San14.Data.Formats;

namespace San14.Data;

/// <summary>Source stencil scope for the actual newly imported procedural
/// normal18 Tree2 and PB2 AlphaTest/Instancing2 providers. Keeps the original
/// generic Tree2 renderer distinct from the class18 PB2 renderer.</summary>
public sealed class MapProceduralNativeStencilSource
{
    private readonly ModelQueueStateSource queue;
    private MapProceduralNativeStencilSource(ModelQueueStateSource queue,
        uint providerClass, uint shaderFile, string sourceEvidence)
        => (this.queue, ProviderClass, ShaderFile, SourceEvidence) = (queue, providerClass, shaderFile, sourceEvidence);
    public uint ProviderClass { get; }
    public uint ShaderFile { get; }
    public string SourceEvidence { get; }
    public bool UsesGenericTree2Renderer => ProviderClass == 0xAB9C4087;

    /// <summary>Import boundary. These are actual G1S modules and actual
    /// creator branches. AlphaTest/Instancing2 optional records introduce no
    /// separate stencil writer in the original startup registry.</summary>
    public static MapProceduralNativeStencilSource ReadFreshSelected(
        ModelQueueStateSource actualQueue, ModelShaderInterfaceSource actualInterface,
        LoadedShaderContainer actualShader)
    {
        var provider = actualInterface.Provider ?? throw new InvalidDataException("Procedural normal provider is absent.");
        if (actualQueue.RenderType != 0 || (actualQueue.Flag & 2) == 0 ||
            actualQueue.ShaderStateOffset != 18 || actualInterface.StateIndex != 18 ||
            provider.ShaderFileHash != actualShader.Source.FileId)
            throw new InvalidDataException("Procedural stencil requires its actual normal18 late ordinary provider.");
        uint file = actualShader.Source.FileId;
        bool tree = provider.Object.ClassId == 0xAB9C4087;
        string name = file switch {
            0x20A8720A when tree => "PB2TREE2-AtGbiI2m",
            0x7FA757FD when tree => "PB2TREE2-GbiI2m",
            0xA87AFFA6 when provider.Object.ClassId == 0x18EB1E56 => "PB2-AtGbiI2m",
            _ => throw new NotSupportedException("Procedural stencil source does not match these actual closed providers.")
        };
        if (tree && (actualInterface.Owner.Object.ClassId != 0xAB9C4087 ||
            actualInterface.DependencyIndex != 17 || actualInterface.Owner.DependencyObjectHashes[17] != provider.Object.ObjectId))
            throw new InvalidDataException("Tree2 normal18 must retain the actual factory dependency17.");
        var container = G2ShaderContainerReader.Read(actualShader.Source.Bytes);
        var modules = G1ShaderConfigurationReader.Read(actualShader.Source.Bytes, container).Records;
        string[] names = file == 0x7FA757FD ? ["GBuffer", "Instancing2"] : ["GBuffer", "AlphaTest", "Instancing2"];
        if (container.NamePrefix != name || !modules.Select(m => m.Name).SequenceEqual(names) ||
            modules[0].RawSecondWord != 2 || modules.Skip(1).Any(m => m.RawSecondWord != 0))
            throw new InvalidDataException("Procedural stencil shader module construction differs from its source.");
        string creator = tree
            ? "F97A60/FB4C60/F8E360/11AF2C0;VT1755240-v60=97EAA0;" +
              "pluginVT170B310-v60=E399F0/head14A0;FA7C60-fresh1408qword0"
            : "FA9810/FA9940/F8DA10/F8DC82-VT170ABB0;v60=B9E3B0/head1460;FA7490-fresh1408qword0";
        return new(actualQueue, provider.Object.ClassId!.Value, file,
            actualShader.Source.ResourceUri + "@" + actualShader.Source.DecodedSha256 + ";" + creator +
            ";D15260-PB2-prefix;modifier4&6EA=0 skipsD157DA;SSSTransmit-absent skipsD172BA/D17390;" +
            "D4D880-PB2-cleanup;D48D05-dispatcher-disable");
    }

    /// <summary>Consume current owned renderer fields. Later modifier or
    /// SSSTransmit writes require their real writer before this finite scope.</summary>
    public ModelOrdinaryDispatcherStencilPacket PrepareCurrent(
        ModelOrdinaryDispatcherRecord actualRecord, MapCommonRenderState currentCommon,
        uint actualContextFlags3E4, uint actualModifierFlags4, bool actualSsstransmitPresent)
    {
        if ((actualModifierFlags4 & 0x6EA) != 0 || actualSsstransmitPresent)
            throw new NotSupportedException("Current procedural renderer has an active separate stencil-reference writer.");
        var state = ModelOrdinaryDispatcherStencilSource.Prepare(queue, actualRecord, currentCommon, actualContextFlags3E4);
        return state with { SourceProvenance = state.SourceProvenance + ";" + SourceEvidence +
            ";current-fresh-modifier/GBuffer-plugin-preserves-draw-stencil" };
    }
}
