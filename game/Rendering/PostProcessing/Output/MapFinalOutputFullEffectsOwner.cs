using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Actual source DOF, Hex, Bloom, Star, Flare, LightScene, Shaft and
/// Radial producers. One ordered batch uses original shaders and every native
/// custom draw. The initialized saved-template noise is an owned producer.</summary>
public sealed class MapFinalOutputFullEffectsOwner:IAsyncDisposable
{
    public MapFinalOutputPassGraph Graph { get; }
    public MapFinalOutputPassOwner.NativeRecipeOwner Native { get; }
    public MapFinalOutputNoiseOwner Noise { get; }
    public long SubmittedDrawCount=>Native.SubmittedDrawCount;
    public IReadOnlyDictionary<int,ModelDeviceBinding>? LatestTextures=>Native.LatestTextures;
    private readonly HashSet<int> _clearedOnce=new();private Vector2I _extent;
    private readonly NativeMapBorrowerRetirementPackage retirement;
    private static readonly int[] SpkPasses=[0,1,2,3,10,11,12,13,14,15,16,17,19,20,21,22,23,24,27];
    private MapFinalOutputFullEffectsOwner(MapFinalOutputPassGraph graph,
        MapFinalOutputPassOwner.NativeRecipeOwner native,MapFinalOutputNoiseOwner noise)
    {
        (Graph,Native,Noise)=(graph,native,noise);
        retirement=new("Final-full-effects:"+Guid.NewGuid().ToString("N"),native,noise);
    }
    public static async Task<MapFinalOutputFullEffectsOwner> BuildAsync(
        MapFinalOutputPassGraph graph,Vkd3dShaderCompiler compiler,bool allowReadback=false)
    {
        var programs=SpkPasses.Select(i=>new MapFinalOutputNativeProgram("spk"+i,MapFinalOutputNativeProgramKind.Spk,i))
            .Concat(new[]{new MapFinalOutputNativeProgram("flare",MapFinalOutputNativeProgramKind.StockScreenDefault3,18),
                new MapFinalOutputNativeProgram("light",MapFinalOutputNativeProgramKind.StockScreenDefault1,-1)}).ToArray();
        MapFinalOutputPassOwner.NativeRecipeOwner? native=null;MapFinalOutputNoiseOwner? noise=null;
        try{native=await MapFinalOutputPassOwner.NativeRecipeOwner.BuildAsync(graph,programs,compiler,allowReadback);
            noise=await MapFinalOutputNoiseOwner.BuildSuccessfulFirstInitializationAsync(graph.Source);return new(graph,native,noise);}
        catch(Exception failure)
        {
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("Final-full-effects-partial",
                new IAsyncDisposable?[]{native,noise}.OfType<IAsyncDisposable>(),
                (failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }
    internal Task<IReadOnlyDictionary<int,ModelDeviceBinding>> RenderAsync(
        MapCameraConstructionPacket camera,MapFinalOutputDofWeightPacket weight,int width,int height,
        MapFinalOutputEffectsContext context,ReadOnlyMemory<byte> finalPixelConstants,
        IReadOnlyDictionary<int,ModelDeviceBinding> actualExternal,Action commitWeight)
    {
        if(camera.FrameNumber!=weight.FrameNumber||width<=0||height<=0||finalPixelConstants.Length!=240||
            string.IsNullOrWhiteSpace(context.NativeContextSource)||context.RetainedStockPixelConstants.Length!=16||
            context.Wrapper.RetainedUvVectors.Length!=0||context.RadialWrapper.RetainedUvVectors.Length!=48||
            context.OrdinaryRaster.DepthTest||context.OrdinaryRaster.DepthWrite||context.OrdinaryRaster.Attachments.Count!=1)
            throw new InvalidDataException("Complete effects require same source Camera/weight and complete explicit native retained context.");
        if(new Vector2I(width,height)!=_extent){_clearedOnce.Clear();_extent=new(width,height);}
        var parameters=context.CurrentParameters.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        if(parameters.Count!=Graph.Parameters.Count||Graph.Parameters.Any(p=>!parameters.TryGetValue(p.Index,out var b)||b.Length!=p.ByteSize))
            throw new InvalidDataException("Complete effects require all80 complete current native parameters.");
        foreach(var field in Graph.Passes[28].ConstantBuffers.Single(b=>b.StageCode==0&&b.Register==0).SourceParameterSlices)
            parameters[field.ParameterIndex]=finalPixelConstants.Slice(field.Offset,field.ByteSize).ToArray();
        parameters[14]=Floats(0,0,1,1);parameters[15]=Floats(0,0,1,1);
        parameters[16]=Floats(weight.EyeToAtLength);
        foreach(var (index,at) in new[]{(17,0x15C),(18,0x148),(19,0x140),(20,0x144)})parameters[index]=camera.Prefix.Slice(at,4).ToArray();
        uint dof=camera.Source.Declaration.DepthOfFieldMode;parameters[25]=Floats(dof<=2?dof:0);
        if(Graph.ShouldRunDistantDofPasses(parameters)||dof!=0)throw new NotSupportedException("Complete source effects currently instantiate the proved source DOF0 activation route.");
        // State already resolved the actual Apply working-target and whole
        // Camera Bloom caller. Preserve those source writers when repeating the
        // Camera projection; the archive Host-default overload is not current.
        if(!context.CurrentParameters.TryGetValue(2,out var hdrBytes)||hdrBytes.Length!=4||
            !context.CurrentParameters.TryGetValue(43,out var maxLumBytes)||maxLumBytes.Length!=4)
            throw new InvalidDataException("Complete effects require current source TrueHDR and resolved Bloom MaxLum parameters.");
        float trueHdrValue=BinaryPrimitives.ReadSingleLittleEndian(hdrBytes.Span);
        float resolvedMaxLum=BinaryPrimitives.ReadSingleLittleEndian(maxLumBytes.Span);
        if(trueHdrValue is not(0f or 1f)||!float.IsFinite(resolvedMaxLum))
            throw new InvalidDataException("Current TrueHDR must be exactly0/1 and resolved Bloom MaxLum must be finite.");
        parameters=MapFinalOutputBloomStarFlare.ApplySourceCreatedCameraParameters(Graph,camera,parameters,
            context.NativeContextSource,trueHdrValue==1f,resolvedMaxLum).ToDictionary(p=>p.Key,p=>p.Value);
        var shaft=MapFinalOutputShaft.PrepareSourceGroupZero(Graph,camera,(uint)width,(uint)height,parameters,context.NativeContextSource);
        if(!shaft.DedicatedLightScene)throw new NotSupportedException("Complete source effects require the source dedicated LightScene group0 route.");
        parameters=shaft.CurrentParameters.ToDictionary(p=>p.Key,p=>p.Value);
        var quad=MapFinalOutputShaft.CopyNativeQuad96();var targets=new Dictionary<int,MapFinalOutputNativeTarget>();
        var draws=new List<MapFinalOutputNativeDraw>();string evidence=Graph.Source.PostEffect.Source.ResourceUri+"#actual-complete-source-effects";
        var external=actualExternal.ToDictionary(p=>p.Key,p=>p.Value);if(!external.TryAdd(24,Noise.Binding))throw new InvalidDataException("Source Noise24 is owned by the successful native initialization.");
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> Templates(int index)
            =>context.RetainedTemplatesByPass.TryGetValue(index,out var value)?value:throw new InvalidDataException($"Complete effects miss retained native CBs for pass{index}.");
        void Target(int id,int sourceIndex)
        {
            if(targets.ContainsKey(id))return;var a=Graph.ResolveAllocation(sourceIndex,width,height);
            targets.Add(id,new(id,sourceIndex,a.Width,a.Height,evidence+$"/TEMP{sourceIndex}/owned{id}"));
        }
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> Samplers(int pass)=>Graph.PrepareSamplers(pass,Graph.Passes[pass].Inputs.ToDictionary(i=>i.SamplerRegister,i=>ProducerKey(i.TextureIndex)))
            .ToDictionary(s=>s.Register,s=>(ReadOnlyMemory<byte>)s.Recipe.EncodeD3D11Descriptor());
        MapFinalOutputPassDrawPacket Packet(int pass,IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> cbs,
            ReadOnlyMemory<byte> vertices,int outputCount,bool additive=false,bool alphaBlend=false)
            =>new(pass,cbs.Where(p=>p.Key.Stage==1).ToDictionary(p=>p.Key.Register,p=>p.Value),
                cbs.Where(p=>p.Key.Stage==0).ToDictionary(p=>p.Key.Register,p=>p.Value),vertices,Samplers(pass),
                Raster(context.OrdinaryRaster,outputCount,additive,alphaBlend));
        void Ordinary(int index,IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> cbs,Color? clear=null,Rect2I? scissor=null,ReadOnlyMemory<byte>? vertices=null)
        {
            var pass=Graph.Passes[index];foreach(var output in pass.Outputs)Target(output.TextureIndex,output.TextureIndex);
            var packet=Packet(index,cbs,vertices??quad,pass.Outputs.Count);
            if(index==0)packet=packet with{DrawSucceeded=commitWeight};
            if(pass.RawFunctor.Span[0x1D]!=0){var previous=packet.DrawSucceeded;packet=packet with{DrawSucceeded=()=>{previous?.Invoke();_clearedOnce.Add(index);}};}
            draws.Add(new("spk"+index,pass.Outputs.Select(o=>o.TextureIndex).ToArray(),pass.Inputs.ToDictionary(i=>i.TextureRegister,i=>i.TextureIndex),packet,clear,evidence+$"/pass{index}"){Scissor=scissor});
        }
        foreach(int index in new[]{0,1,2})
        {
            var pass=Graph.Passes[index];var a=Graph.ResolveAllocation(pass.Outputs[0].TextureIndex,width,height);
            var cbs=Graph.BuildConstants(index,Templates(index),parameters).ToDictionary(p=>p.Key,p=>p.Value);
            cbs[(1,0)]=Graph.BuildRequiredPassVertexConstants(index,a.Width,a.Height,cbs[(1,0)],context.Wrapper,parameters);Ordinary(index,cbs);
        }
        var hex=MapFinalOutputHexDof.Create(Graph,width,height,Templates(3),parameters,context.Wrapper);
        Target(-301,2);Target(-302,3);Target(6,6);Target(7,7);
        int Hex(HexDofTextureRole role)=>role switch{HexDofTextureRole.SourceFront=>2,HexDofTextureRole.SourceBack=>3,
            HexDofTextureRole.ScratchFront=>-301,HexDofTextureRole.ScratchBack=>-302,HexDofTextureRole.FinalFront=>6,_=>7};
        foreach(var d in hex.Draws)draws.Add(new("spk3",d.Outputs.Select(o=>Hex(o.Role)).ToArray(),d.Inputs.ToDictionary(i=>i.Register,i=>Hex(i.Role)),
            Packet(3,d.Constants,quad,2,true),d.Clear?Packed(d.ClearPackedColor):null,evidence+"/native-Hex-six-draws"));
        foreach(int index in Enumerable.Range(10,6))
        {
            var d=MapFinalOutputBloomStarFlare.CreateOrdinary(Graph,index,width,height,Templates(index),parameters,context.Wrapper,quad,!_clearedOnce.Contains(index));
            Ordinary(index,d.Constants,d.Clear?Packed(d.ClearPackedColor):null,new(d.Scissor.Left,d.Scissor.Top,d.Scissor.Width,d.Scissor.Height),d.Quad);
        }
        var star=MapFinalOutputBloomStarFlare.CreateStar(Graph,width,height,Templates(16),parameters,context.Wrapper,camera.Prefix,quad);
        Target(-1601,16);Target(-1602,16);Target(20,20);
        int Star(StarTextureRole role)=>role switch{StarTextureRole.Source=>16,StarTextureRole.Scratch0=>-1601,StarTextureRole.Scratch1=>-1602,_=>20};
        foreach(var d in star.Draws)draws.Add(new("spk16",d.Outputs.Select(o=>Star(o.Role)).ToArray(),d.Inputs.ToDictionary(i=>i.Register,i=>Star(i.Role)),
            Packet(16,d.Constants,d.Quad,1,true),d.Clear?Packed(d.ClearPackedColor):null,evidence+"/native-Star-twelve-draws"));
        var makeFlare=MapFinalOutputBloomStarFlare.CreateOrdinary(Graph,17,width,height,Templates(17),parameters,context.Wrapper,quad,!_clearedOnce.Contains(17));
        Ordinary(17,makeFlare.Constants,makeFlare.Clear?Packed(makeFlare.ClearPackedColor):null,new(makeFlare.Scissor.Left,makeFlare.Scissor.Top,makeFlare.Scissor.Width,makeFlare.Scissor.Height),makeFlare.Quad);
        var flare=MapFinalOutputBloomStarFlare.CreateFlare(Graph,width,height,context.RetainedStockPixelConstants,
            new(context.StockScreenScale.X,context.StockScreenScale.Y),new(context.StockColorScale.R,context.StockColorScale.G,context.StockColorScale.B,context.StockColorScale.A));Target(22,22);
        foreach(var d in flare.Draws)
        {
            var p=Packet(18,d.Constants,d.Quad,1,true,true) with{SamplerDescriptors=new Dictionary<uint,ReadOnlyMemory<byte>>{{0,d.Sampler.EncodeD3D11Descriptor()}}};
            draws.Add(new("flare",new[]{22},new Dictionary<uint,int>{{0,21}},p,d.Clear?Packed(d.ClearPackedColor):null,evidence+"/native-Flare-thirteen-indexed-draws"));
        }
        var light=RenderTextureResources.Read(Graph.Source.TextureObjects,MapFinalOutputShaft.LightSceneObjectId);
        targets.Add(23,new(23,23,width,height,evidence+"/source-LightScene-2A691F4A"){SourceDeclaration=light});
        var stock=LightScenePacket(width,height,context,ProducerKey(0));
        draws.Add(new("light",new[]{23},new Dictionary<uint,int>{{0,0}},stock,new Color(0,0,0,1),evidence+"/EB66EE-stock-default1-Scene-copy"));
        foreach(int index in Enumerable.Range(19,6))Ordinary(index,MapFinalOutputShaft.BuildDrawConstants(Graph,index,width,height,Templates(index),shaft,context.Wrapper));
        var radial=Graph.ResolveAllocation(Graph.Passes[27].Outputs[0].TextureIndex,width,height);
        var radialCbs=Graph.BuildConstants(27,Templates(27),parameters).ToDictionary(p=>p.Key,p=>p.Value);
        radialCbs[(1,0)]=Graph.BuildRequiredPassVertexConstants(27,radial.Width,radial.Height,radialCbs[(1,0)],context.RadialWrapper,parameters);Ordinary(27,radialCbs);
        return Native.RenderAsync(new(camera.FrameNumber,targets.Values.ToArray(),external,draws,evidence+";"+context.NativeContextSource));
    }
    public ReadOnlyMemory<byte> ProducerKey(int textureIndex)
    {
        if(textureIndex==24)return Noise.NativeSamplerKey;if(textureIndex==30)return Convert.FromHexString(MapFinalOutputDofWeight.ProducerSamplerKeyHex);
        var definition=textureIndex switch{0=>Graph.Source.SceneTexture,14=>Graph.Source.ExposureTexture,
            29=>RenderTextureResources.Read(Graph.Source.TextureObjects,0xE13DDB4F),23=>RenderTextureResources.Read(Graph.Source.TextureObjects,MapFinalOutputShaft.LightSceneObjectId),_=>null};
        return definition is not null?RenderSamplerSourceAdapters.FinalRenderTextureKey(Graph.Source.StockScreen.Native,definition):
            MapFinalOutputBloomStarFlare.TempProducerKey(Graph,textureIndex);
    }
    private MapFinalOutputPassDrawPacket LightScenePacket(int width,int height,MapFinalOutputEffectsContext context,ReadOnlyMemory<byte> key)
    {
        byte[] vs=Floats((2f/width)*context.StockScreenScale.X,(-2f/height)*context.StockScreenScale.Y,-1,1,
            context.StockColorScale.R,context.StockColorScale.G,context.StockColorScale.B,context.StockColorScale.A);
        byte[] ps=context.RetainedStockPixelConstants.ToArray();BinaryPrimitives.WriteSingleLittleEndian(ps,-float.MaxValue);
        byte[] vertices=new byte[96];float[][] corners=[[0,0,0,0],[0,height,0,1],[width,height,1,1],[width,0,1,0]];
        for(int i=0;i<4;i++){Write(vertices,i*24,corners[i][0]);Write(vertices,i*24+4,corners[i][1]);Write(vertices,i*24+8,0);
            BinaryPrimitives.WriteUInt32LittleEndian(vertices.AsSpan(i*24+12),uint.MaxValue);Write(vertices,i*24+16,corners[i][2]);Write(vertices,i*24+20,corners[i][3]);}
        return new(-1,new Dictionary<uint,ReadOnlyMemory<byte>>{{0,vs}},new Dictionary<uint,ReadOnlyMemory<byte>>{{0,ps}},vertices,
            new Dictionary<uint,ReadOnlyMemory<byte>>{{0,RenderSamplerSourceAdapters.DecodeKey(Graph.Source.StockScreen.Native,key.Span).EncodeD3D11Descriptor()}},Raster(context.OrdinaryRaster,1));
    }
    private static ModelRasterInputs Raster(ModelRasterInputs source,int count,bool additive=false,bool alphaBlend=false)
    {
        var a=source.Attachments[0] with{EnableBlend=false};if(additive)a=a with{EnableBlend=true,SourceColor=alphaBlend?RenderingDevice.BlendFactor.SrcAlpha:RenderingDevice.BlendFactor.One,
            DestinationColor=RenderingDevice.BlendFactor.One,ColorOperation=RenderingDevice.BlendOperation.Add,
            SourceAlpha=alphaBlend?RenderingDevice.BlendFactor.SrcAlpha:RenderingDevice.BlendFactor.One,DestinationAlpha=RenderingDevice.BlendFactor.One,AlphaOperation=RenderingDevice.BlendOperation.Add};
        return source with{Attachments=Enumerable.Repeat(a,count).ToArray()};
    }
    private static Color Packed(uint value)=>new((value&255)/255f,((value>>8)&255)/255f,((value>>16)&255)/255f,(value>>24)/255f);
    private static byte[] Floats(params float[] values){byte[] b=new byte[values.Length*4];for(int i=0;i<values.Length;i++)Write(b,i*4,values[i]);return b;}
    private static void Write(byte[] b,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at,4),value);
    public async Task RetireTargetAsync(){await Native.RetireTargetAsync();_clearedOnce.Clear();_extent=default;}
    public ValueTask DisposeAsync()=>retirement.DisposeAsync();
}
