using System.Numerics;

namespace San14.Data;

/// <summary>One normal ScreenMap media generation. It borrows resource
/// declarations and the accepted initial camera; current Zone storage,
/// ownership, display selection and exposure remain with their live owners.</summary>
public sealed partial class ScreenMapResources
{
    private readonly TerritoryStyleResourceSettings settings;
    private readonly byte[] constants,vertex,pixel;
    private readonly Vector4[] baseCells,baseLines;
    private readonly IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> samplers;
    private readonly byte[][] stockSamplers;

    private ScreenMapResources(TerritoryStyleResourceSettings settings,byte[] constants,
        byte[] vertex,byte[] pixel,Vector4[] baseCells,Vector4[] baseLines,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> samplers,byte[][] stockSamplers)
    {
        this.settings=settings;this.constants=constants;this.vertex=vertex;this.pixel=pixel;
        this.baseCells=baseCells;this.baseLines=baseLines;this.samplers=samplers;this.stockSamplers=stockSamplers;
    }

    /// <summary>The ordinary and water ScreenMap stages borrow one media
    /// generation only when their actual declarations share its owners.</summary>
    public static ScreenMapResources PrepareNormal(GameResourceRepository repository,
        MapCameraConstructionPacket actualCamera,string locale)
    {
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        var shaders=repository.LoadObjects("system",0x718CCB8B);
        var screenMaps=repository.LoadObjects("system",0xDAB638B2);
        var textures=repository.LoadObjects("system",0x93FB3280);
        var ordinary=TerritoryResources.Read(repository,graph,60,shaders,screenMaps,textures);
        var water=TerritoryResources.Read(repository,graph,122,shaders,screenMaps,textures);
        if(ordinary.ScreenMapObject.ObjectId!=water.ScreenMapObject.ObjectId||
            ordinary.ShaderObject.ObjectId!=water.ShaderObject.ObjectId||ordinary.ViewId!=water.ViewId||
            !ordinary.Gbuffers.Select(value=>(value.Role,value.Register,value.Texture.Object.ObjectId))
                .SequenceEqual(water.Gbuffers.Select(value=>(value.Role,value.Register,value.Texture.Object.ObjectId))))
            throw new NotSupportedException("Different ScreenMap stage declarations require independent media owners.");
        // Their provider draw flags differ; those select stencil references
        // at each stage and do not change these shared media declarations.
        return Prepare(repository,ordinary,actualCamera,locale);
    }

    public static ScreenMapResources Prepare(GameResourceRepository repository,
        LoadedTerritoryResources source,MapCameraConstructionPacket actualCamera,string locale)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);
        if(actualCamera.ExecutableLocale!=locale)
            throw new InvalidDataException("ScreenMap resources require the accepted camera's locale.");
        var styles=TerritoryStyleSources.LoadCurrent(repository,source,locale);
        var initialized=TerritoryStyleSources.InitializedScreenMap();
        var stages=source.Shader.ReadG1ResourceBindings(0).Combination.Stages;
        int Program(uint stage)=>stages.Single(value=>value.StageCode==stage&&value.Entry is not null).Entry!.ProgramIndex;
        int vertexIndex=Program(1),pixelIndex=Program(0);
        byte[] constants=TerritoryConstants.Pack(source.Shader.ReadResourceDefinitions(pixelIndex),
            new byte[TerritoryConstants.ByteCount],TerritoryStyleSources.Inputs(
                PreparedCameraSceneSource.ReadCompletePrefix(actualCamera.Prefix.Span),initialized));
        var native=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        var samplers=TerritoryStyleSources.ReadSamplers(source,native).ToDictionary();
        // The supported GBufferLayout-only preparation publishes null t5.
        // Its binder uses the backend default, independently of the copied RR.
        samplers[5]=RenderSamplerSourceAdapters.BackendDefaultDescriptor(native);
        byte[][] stockSamplers=new[]{"AB","NS","RS"}.Select(role=>
            RenderSamplerSourceAdapters.DecodeKey(native,RenderSamplerSourceAdapters.FinalRenderTextureKey(
                native,source.Gbuffers.Single(value=>value.Role==role).Texture)).EncodeD3D11Descriptor()).ToArray();
        return new(styles.CaptureResourceSettings(),constants,
            source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,vertexIndex).ToArray(),
            source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,pixelIndex).ToArray(),
            initialized.CellColors,initialized.LineColors,samplers,stockSamplers);
    }
}
