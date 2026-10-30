using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

public sealed record ReflectionCaptureAmbientCube(ModelDeviceBinding Texture, ModelDeviceBinding Sampler);
public sealed record ReflectionCaptureModelPreparedDraw(ModelGbufferInstancePacket Packet,
    uint NextCachedMaterialFlags, ModelSpecularPreparation Specular);

/// <summary>Original D362B0 state12 material/geometry consumer. Loads the real
/// selected dependency, selected original combination and material image slots;
/// current shader dimensions, CView constants and ambient Cube stay explicit.</summary>
public sealed class ReflectionCaptureModelSourceDraw
{
    public LoadedModelGeometry Model { get; }
    public LoadedShaderContainer Shader { get; }
    public ModelShaderInterfaceSource Selection { get; }
    public int CombinationIndex { get; }
    public int DrawIndex { get; }
    public DxbcResourceDefinitions VertexDefinitions { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    public ModelGbufferMaterialSource Material { get; }
    private readonly ModelGbufferTextureUse[] textures;
    private ReflectionCaptureModelSourceDraw(LoadedModelGeometry model,LoadedShaderContainer shader,
        ModelShaderInterfaceSource selection,int combination,int drawIndex,DxbcResourceDefinitions vertex,
        DxbcResourceDefinitions pixel,ModelGbufferMaterialSource material,ModelGbufferTextureUse[] textures)
        => (Model,Shader,Selection,CombinationIndex,DrawIndex,VertexDefinitions,PixelDefinitions,Material,this.textures)=
            (model,shader,selection,combination,drawIndex,vertex,pixel,material,textures);

    public static ReflectionCaptureModelSourceDraw Load(GameResourceRepository repository,
        LoadedModelTextureResources model,int drawIndex,ModelShaderInterfaceSource actualSelectedState12,
        ReadOnlySpan<uint> actualSevenIndices,uint actualPass,ModelStageTextureRuntime actualTextureRuntime)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(actualSelectedState12);
        if (actualSelectedState12.StateIndex != 12 || actualSelectedState12.Provider is not {} provider || actualSevenIndices.Length != 7)
            throw new InvalidDataException("reflection.capture_model.actual_state12");
        var shader=ModelShaderResources.LoadDeclaredG1S(repository,provider);
        var combination=shader.ReadG1Bindings().ResolveCombination(actualSevenIndices,actualPass);
        var vs=combination.Stages.Single(value=>value.StageCode==1&&value.Entry is not null).Entry!;
        var ps=combination.Stages.Single(value=>value.StageCode==0&&value.Entry is not null).Entry!;
        if (combination.Stages.Any(value=>value.Entry is not null&&value.StageCode is not(0 or 1)))
            throw new InvalidDataException("reflection.capture_model.selected_stages");
        var vertex=shader.ReadResourceDefinitions(vs.ProgramIndex);var pixel=shader.ReadResourceDefinitions(ps.ProgramIndex);
        var outputs=shader.ReadSignatures(ps.ProgramIndex).Parts.Where(value=>value.Kind==DxbcSignatureKind.Output)
            .SelectMany(value=>value.Parameters).ToArray();
        if (!outputs.Select(value=>value.Register).Order().SequenceEqual(new uint[]{0,1,2,3}) ||
            outputs.Any(value=>value.SemanticName.Text!="SV_Target"||value.ComponentType!=3||value.Mask!=15))
            throw new InvalidDataException("reflection.capture_model.four_outputs");
        var texturePlan=ModelStageTextures.ResolveReflectionCapture(model,drawIndex,provider,shader,actualSevenIndices,actualPass,actualTextureRuntime);
        var uses=new List<ModelGbufferTextureUse>();
        foreach(var binding in texturePlan.Bindings)
        {
            var image=ModelStageTextures.LoadImage(repository,texturePlan,binding.TextureRegister);
            uint view=ModelStageTextures.ReadNativeViewFormat(image.Image);
            var sampler=image.Sampler.EncodeD3D11Descriptor();
            uses.Add(new("Texture",binding.TextureRegister,image.Container,binding.ImageIndex,view,sampler));
            uses.Add(new("Sampler",binding.SamplerRegister,image.Container,binding.ImageIndex,view,sampler));
        }
        var bindings=ModelShaderResources.LoadBindings(repository,model);
        var material=ModelGbufferMaterialSourceReader.Read(shader,ModelShaderResources.ResolveDrawMaterials(bindings,drawIndex));
        return new(new(model.Model.Geometry,model.Model.Source),shader,actualSelectedState12,combination.Index,drawIndex,vertex,pixel,material,uses.ToArray());
    }

    public ReflectionCaptureModelPreparedDraw Prepare(string actualPrimitiveKey,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> actualCompleteVertexConstants,
        ReadOnlySpan<byte> retainedPixelTemplate,uint previousMaterialFlags,
        ReflectionCaptureFieldCellGeometry actualCell,ReflectionCaptureModelLighting actualLighting,
        ReflectionCaptureAmbientCube actualAmbient,bool actualAlphaEnabled,byte? forcedAlphaReference,
        ModelRasterInputs actualRaster,ArmySkinVertexProfile? actualSkinProfile=null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualPrimitiveKey);ArgumentNullException.ThrowIfNull(actualAmbient);
        if (actualAmbient.Texture is not {DescriptorKind:"Texture",SourceBindPoint:5,OwnerTexture:not null} ||
            actualAmbient.Sampler is not {DescriptorKind:"Sampler",SourceBindPoint:5})
            throw new InvalidDataException("reflection.capture_model.actual_ambient_cube");
        var cbs=VertexDefinitions.Bindings.Where(value=>value.InputType==0).ToArray();
        if (VertexDefinitions.Bindings.Any(value=>value.InputType!=0)||cbs.Length!=actualCompleteVertexConstants.Count ||
            cbs.Any(value=>!actualCompleteVertexConstants.TryGetValue(value.BindPoint,out var data)||
                data.Length!=VertexDefinitions.Buffers.Single(buffer=>buffer.Name.Text==value.Name.Text).ByteSize))
            throw new InvalidDataException("reflection.capture_model.complete_vertex_constants");
        var globals=PixelDefinitions.Buffers.Single(value=>value.Name.Text=="$Globals");
        var spec=globals.Variables.Single(value=>value.Name.Text=="cSpec");
        if (retainedPixelTemplate.Length!=globals.ByteSize)throw new InvalidDataException("reflection.capture_model.retained_material");
        Vector4 previous=new(F32(retainedPixelTemplate,spec.StartOffset),F32(retainedPixelTemplate,spec.StartOffset+4),
            F32(retainedPixelTemplate,spec.StartOffset+8),F32(retainedPixelTemplate,spec.StartOffset+12));
        var transition=ModelGbufferMaterialSourceReader.PrepareSpecular(Material,previousMaterialFlags);
        float? alpha=globals.Variables.Any(value=>value.Name.Text=="vATest")
            ?ModelGbufferMaterialSourceReader.PrepareAlphaTest(Material.SourceAlphaReference,actualAlphaEnabled,forcedAlphaReference):null;
        var pixel=ReflectionCaptureModelParameters.Pixel(PixelDefinitions,retainedPixelTemplate,actualCell,actualLighting,transition.Value??previous,alpha);
        var vertex=actualCompleteVertexConstants.ToDictionary(value=>value.Key,value=>(ReadOnlyMemory<byte>)value.Value.ToArray());
        var packet=new ModelGbufferInstancePacket(actualPrimitiveKey,Model,Shader,CombinationIndex,DrawIndex,
            new(vertex,[],[]),new(new Dictionary<uint,ReadOnlyMemory<byte>>{[0]=pixel},textures,[actualAmbient.Texture,actualAmbient.Sampler]),actualRaster)
            {ArmySkinProfile=actualSkinProfile};
        return new(packet,transition.NextCachedFlags,transition);
    }
    private static float F32(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
}
