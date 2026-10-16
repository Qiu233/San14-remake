using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The finite normal Post3 media bank built from the same actual
/// allocation and Before preparations as its initial numeric buffers.
/// Physical GPU histories and their current selectors belong to Native.</summary>
public sealed partial class D3D11NormalPostResources
{
    private readonly MapPostEffect3AllocationPlan allocation;
    private readonly MapPostEffect3DepthOfField2ShapeRequirement shape;
    private readonly MapPostEffect3StaticHdrNativeRecipe hdr;
    private readonly MapPostEffect3TextureShape exposure,bloom,tone;
    private readonly RenderTextureFormats outputFormats;
    public SceneColorTargetResources SceneColor { get; }
    private readonly G1TextureSamplerRecipe linear,point,bloomSampler;
    private readonly MapPostEffect3FreshStockDrawSource fresh;
    private readonly LoadedModelFrontendColorSource colors;
    private readonly MapPostEffect3NativeFullscreenFrontendSource frontend;
    private readonly MapPostEffect3FreshOwnedAllocationSides sides;
    private readonly IReadOnlyList<MapPostEffect3PassState> passStates;
    private readonly Dictionary<int,int> textureSides;
    private readonly List<PassMedia> passes=[];
    private readonly HashSet<int> textures=[],buffers=[];
    private readonly Dictionary<(uint Stage,int Index),ReadOnlyMemory<byte>> programs=[];
    private readonly Dictionary<(int Pass,string Name),PassMedia> imported=[];

    internal D3D11NormalPostResources(GameResourceRepository repository,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3DepthOfField2ShapeRequirement shape,MapPostEffect3EyeBloomViewPlan bloomView,
        LoadedMapFinalOutputResources final,MapPostEffect3StaticHdrNativeRecipe hdr,
        LoadedDeferredSamplerNativeSource native,MapPostEffect3FreshStockDrawSource fresh,
        MapPostEffect3FreshOwnedAllocationSides sides,SceneColorTargetResources sceneColor)
    {
        this.allocation=allocation;this.shape=shape;this.hdr=hdr;this.fresh=fresh;
        this.sides=sides;textureSides=sides.TextureInstances.ToDictionary();
        passStates=MapPostEffect3PassStateReader.Read(allocation.Source,native.Locale);
        colors=ModelFrontendColorSource.Load(repository,native.Locale);
        frontend=RenderSamplerSourceAdapters.ReadNativeFullscreenFrontendSource(native);
        exposure=TextureShape(61);bloom=TextureShape(bloomView.PhysicalTextureParameterIndex);tone=TextureShape(84);
        SceneColor=sceneColor;
        outputFormats=RenderTextureFormatMath.Resolve(final.OutputTexture);
        MapPostEffect3EyeExposureInitializationSource.Load(repository,native.Locale).ValidateFor(allocation);
        var manager=RenderSamplerSourceAdapters.ReadPostEffect3ManagerSamplers(native);
        linear=RenderSamplerSourceAdapters.DecodeKey(native,manager.LinearKey);
        point=RenderSamplerSourceAdapters.DecodeKey(native,manager.PointKey);
        byte[] mipKey=RenderSamplerSourceAdapters.RenderTextureKey(native,bloomView.NativeMipCount);
        ushort word=BinaryPrimitives.ReadUInt16LittleEndian(mipKey);
        BinaryPrimitives.WriteUInt16LittleEndian(mipKey,(ushort)((word&0x03FF)|bloomView.NativeSamplerFilterMask));
        bloomSampler=RenderSamplerSourceAdapters.DecodeKey(native,mipKey);
    }

    private sealed record View(string Stage,int Slot,string Resource,int Mip);
    private sealed record Target(int Slot,string Resource,int Mip,uint Width,uint Height,RenderTextureFormats Formats);
    private sealed class PassMedia(D3D11NormalPostInitialPass numeric,
        IReadOnlyList<(string Stage,ReadOnlyMemory<byte> Bytes)> programs)
    {
        internal D3D11NormalPostInitialPass Numeric{get;}=numeric;
        internal IReadOnlyList<(string Stage,ReadOnlyMemory<byte> Bytes)> Programs{get;}=programs;
        internal List<View> Views{get;}=[];
        internal List<(int Slot,string Resource)> Unordered{get;}=[];
        internal List<Target> Targets{get;}=[];
        internal IReadOnlyList<ModelNativeColorAttachment> Blend{get;set;}=[];
        internal MapPostEffect3DispatchExtent? Dispatch{get;set;}
        internal bool Indirect{get;set;}
        internal int IndirectBuffer{get;set;}
        internal int IndirectOffset{get;set;}
        internal uint StagingSide{get;set;}
        internal bool Clear{get;set;}
        internal bool Scissor{get;set;}
        internal bool MipSampler{get;set;}
    }

    private MapPostEffect3TextureShape TextureShape(int index)=>MapPostEffect3AllocationShapeReader.Read(
        allocation.TextureAllocations.Single(value=>value.Parameter.Index==index));

    private PassMedia Import(D3D11NormalPostInitialPass numeric)
    {
        var state=passStates[numeric.SourcePassIndex];
        var stages=new List<(string,ReadOnlyMemory<byte>)>();
        foreach(var reference in allocation.Passes[numeric.SourcePassIndex].Pass.ProgramReferences.Where(value=>value.StageIndex is not null))
        {
            var key=(reference.StageCode,reference.StageIndex!.Value);
            if(!programs.TryGetValue(key,out var bytes))
                programs.Add(key,bytes=allocation.Source.GetProgram(key.Item1,key.Item2).Bytes);
            stages.Add((reference.StageCode switch{1=>"vertex",0=>"pixel",5=>"compute",
                _=>throw new NotSupportedException("Normal Post3 media requires another shader-stage producer.")},bytes));
        }
        bool compute=stages.Any(value=>value.Item1=="compute");
        if(!compute&&(state.AutomaticExecution!=MapPostEffect3AutomaticExecution.SpecificFactoryGraphics||state.UsesStockScreenShaderBinding))
            throw new NotSupportedException("Normal Post3 stock-screen media needs its stock vertex layout and screen constant writer.");
        if(compute?stages.Count!=1:stages.Count!=2||!stages.Any(value=>value.Item1=="vertex")||!stages.Any(value=>value.Item1=="pixel"))
            throw new NotSupportedException("Normal Post3 requires the declared compute stage or vertex/pixel pair.");
        var result=new PassMedia(numeric,stages.AsReadOnly());passes.Add(result);
        imported.Add((numeric.SourcePassIndex,numeric.Name),result);return result;
    }

    private (string Name,int Mip) Texture(MapPostEffect3TextureParameterState table,int index,int mip=0)
    {
        if(index==1)return("MainDepth",mip);
        if(index==shape.ActualShapeTextureParameterIndex)return("DOFShape58",mip);
        if(index==0)return("SceneColor",mip);
        var reference=table.ReadKnownNullablePointer(index);
        if(reference is null)return("Null",mip);
        if(ReferenceEquals(reference,table.ReadKnownNullablePointer(0)))return("SceneColor",mip);
        if(reference.PhysicalAllocationParameterIndex is not int physical)
            throw new NotSupportedException("Normal Post3 needs the actual external texture media producer for parameter "+index);
        mip=checked(mip+(int)(reference.NativeFirstMipLevel??0));
        textureSides[physical]=reference.InstanceIndex;
        if(physical==61)return("Exposure61",mip);
        if(physical==bloom.ParameterIndex)return("Bloom85",mip);
        if(physical==tone.ParameterIndex)return("Tone108",mip);
        textures.Add(physical);return("Texture"+physical,mip);
    }

    private Target DescribeTarget(int slot,string resource,int mip)
    {
        if(resource=="SceneColor")return new(slot,resource,mip,checked((uint)SceneColor.Width),checked((uint)SceneColor.Height),
            SceneColor.Formats);
        if(resource=="Final109")return new(slot,resource,mip,allocation.ContextWidth,allocation.ContextHeight,outputFormats);
        if(resource=="DOFShape58")
        {
            var f=MapPostEffect3AllocationShapeReader.ReadFormats(shape.RequiredNativeFormat);
            return new(slot,resource,mip,shape.RequiredWidth,shape.RequiredHeight,new(f.ResourceFormat,f.ShaderResourceFormat0,f.RenderTargetFormat0));
        }
        if(!resource.StartsWith("Texture",StringComparison.Ordinal)&&resource is not("Bloom85" or "Tone108"))
            throw new NotSupportedException("Normal Post3 has no render-target producer for "+resource);
        var texture=resource=="Bloom85"?bloom:resource=="Tone108"?tone:TextureShape(int.Parse(resource[7..],System.Globalization.CultureInfo.InvariantCulture));
        return new(slot,resource,mip,Math.Max(1u,texture.Width>>mip),Math.Max(1u,texture.Height>>mip),
            new(texture.Formats.ResourceFormat,texture.Formats.ShaderResourceFormat0,texture.Formats.RenderTargetFormat0));
    }

    private void SetGraphics(PassMedia pass,bool blend,uint? factor,bool scissor)
    {
        if(pass.Targets.Count==0)throw new NotSupportedException("Normal Post3 graphics media has no actual target.");
        pass.Scissor=scissor;
        pass.Blend=colors.ReadDrawAttachments(colors.ReadCurrentFrontend(Enumerable.Range(0,8).Select(slot=>
            new ModelFrontendColorSlot(slot==0?blend:fresh.ModuleBlendEnabled,
                (byte)(fresh.ModuleColorWriteMask&7),(byte)((fresh.ModuleColorWriteMask>>3)&1),
                factor??frontend.AdmittedFreshWorldNativeColorStateF4)).ToArray()),pass.Targets.Max(target=>target.Slot)+1);
    }
}
