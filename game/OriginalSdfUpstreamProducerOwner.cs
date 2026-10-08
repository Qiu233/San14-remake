using Godot;
using San14.Data;

/// <summary>Complete supported source graphics SDF stage: native pass1 half
/// buffers, explicitly empty operation0, native pass0 SDF composition, pass4
/// RS3 draw/publication. Prepared source volumes, indices and previous target
/// remain explicit borrowed current inputs. Other operation0 branches refuse.</summary>
public sealed class OriginalSdfUpstreamProducerOwner : IAsyncDisposable
{
    public OriginalSdfUpstreamProducerSource Source {get;}
    public Rid HalfNormal {get;}
    public Rid HalfDepth {get;}
    public Rid HalfOcclusion {get;}
    private readonly OriginalSdfUpstreamComputeOwner _downsample,_occlusion;
    private readonly OriginalSdfFinalProducerOwner _final;
    private readonly OriginalSdfFinalSelection _selection;
    private Task? _disposal;
    private OriginalSdfUpstreamProducerOwner(OriginalSdfUpstreamProducerSource source,Rid normal,Rid depth,Rid occlusion,
        OriginalSdfUpstreamComputeOwner downsample,OriginalSdfUpstreamComputeOwner trace,OriginalSdfFinalProducerOwner final,
        OriginalSdfFinalSelection selection)
        =>(Source,HalfNormal,HalfDepth,HalfOcclusion,_downsample,_occlusion,_final,_selection)=
            (source,normal,depth,occlusion,downsample,trace,final,selection);

    public static async Task<OriginalSdfUpstreamProducerOwner> BuildAsync(OriginalSdfUpstreamProducerSource source,
        OriginalSdfUpstreamPreparedState explicitState,OriginalSdfFinalSelection explicitSelection,
        OriginalDeferredExtraDeviceLayer borrowedRs3,Vector2I explicitFullExtent,Rid borrowedPreviousHalfOcclusion,
        ReadOnlyMemory<byte> explicitHalfConstants,ReadOnlyMemory<byte> explicitSdfConstants,ReadOnlyMemory<byte> explicitFinalConstants,
        IReadOnlyList<OriginalSdfUpstreamDeviceBinding> explicitFullGbufferInputs,
        IReadOnlyList<OriginalSdfUpstreamDeviceBinding> explicitPreparedSdfInputs,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);OriginalSdfUpstreamProducerPlan.RequireSupported(explicitState);
        ArgumentNullException.ThrowIfNull(explicitSelection);ArgumentNullException.ThrowIfNull(borrowedRs3);
        ArgumentNullException.ThrowIfNull(explicitFullGbufferInputs);ArgumentNullException.ThrowIfNull(explicitPreparedSdfInputs);
        ArgumentNullException.ThrowIfNull(compiler);
        var canonical=OriginalSdfUpstreamProducerPlan.Read(source.Final,explicitState);
        if(source.HalfGbuffer.ProgramIndex!=canonical.HalfGbuffer.ProgramIndex ||
            source.Occlusion.ProgramIndex!=canonical.Occlusion.ProgramIndex ||
            !source.HalfGbuffer.Dimensions.SequenceEqual(canonical.HalfGbuffer.Dimensions) ||
            !source.Occlusion.Dimensions.SequenceEqual(canonical.Occlusion.Dimensions) ||
            source.HalfGbuffer.Pass!=1 || source.Occlusion.Pass!=0)
            throw new InvalidDataException("SDF whole stage differs from its current original selected variant.");
        source=canonical;
        if(!explicitSelection.Operation1Present || !explicitSelection.SelectedShaderReady || explicitSelection.Interface79==0)
            throw new NotSupportedException("SDF stage needs the current original selected graphics interface.");
        if(explicitFullExtent.X<2 || explicitFullExtent.Y<2 || explicitFullExtent.X%2!=0 || explicitFullExtent.Y%2!=0)
            throw new NotSupportedException("Supported original half resolution SDF stage requires an even current full extent.");
        if(source.HalfGbuffer.ProgramIndex!=3 || source.Occlusion.ProgramIndex!=7 || source.Final.ExplicitPass!=4 ||
            explicitHalfConstants.Length!=80 || explicitSdfConstants.Length!=352 || explicitFinalConstants.Length!=64)
            throw new InvalidDataException("Complete source SDF stage programs/full constant packets differ.");
        var full=explicitFullGbufferInputs.ToArray();var prepared=explicitPreparedSdfInputs.ToArray();
        (string,uint)[] fullExpected=[("Sampler",8),("Sampler",9),("Sampler",10),("Texture",8),("Texture",9),("Texture",10)];
        (string,uint)[] preparedExpected=[("Sampler",0),("Sampler",1),("Sampler",2),("Texture",0),("Texture",1),("Texture",2),
            ("TextureBuffer",16),("TextureBuffer",17),("TextureBuffer",18)];
        bool Packet(OriginalSdfUpstreamDeviceBinding[] p,(string Kind,uint Register)[] e)=>p.Length==e.Length &&
            e.All(k=>p.Count(i=>i.Kind==k.Kind && i.Register==k.Register)==1);
        if(!Packet(full,fullExpected) || !Packet(prepared,preparedExpected))throw new InvalidDataException("Complete original current SDF stage inputs missing.");
        Vector2I half=explicitFullExtent/2;
        var images=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(!rd.TextureIsValid(borrowedPreviousHalfOcclusion))throw new InvalidDataException("Original previous half occlusion missing.");
            using(var f=rd.TextureGetFormat(borrowedPreviousHalfOcclusion))
                if(f.TextureType!=RenderingDevice.TextureType.Type2D || f.Format!=RenderingDevice.DataFormat.R16G16Sfloat ||
                    f.Width!=(uint)half.X || f.Height!=(uint)half.Y || !f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                    throw new InvalidDataException("Original previous half occlusion must use native DXGI34 RG16_FLOAT.");
            if(!rd.TextureIsValid(borrowedRs3.TargetView) || !rd.TextureIsValid(borrowedRs3.BorrowedStorage) ||
                !rd.FramebufferIsValid(borrowedRs3.Framebuffer))
                throw new InvalidDataException("Original current SDF final target was released.");
            using(var f=rd.TextureGetFormat(borrowedRs3.TargetView))
                if(f.Width!=(uint)explicitFullExtent.X || f.Height!=(uint)explicitFullExtent.Y)
                    throw new InvalidDataException("Current SDF target extent differs from full source extent.");
            Rid Make(RenderingDevice.DataFormat format)
            {
                using var f=new RDTextureFormat{Format=format,Width=(uint)half.X,Height=(uint)half.Y,Depth=1,ArrayLayers=1,Mipmaps=1,
                    TextureType=RenderingDevice.TextureType.Type2D,UsageBits=RenderingDevice.TextureUsageBits.SamplingBit|
                    RenderingDevice.TextureUsageBits.StorageBit|RenderingDevice.TextureUsageBits.CanCopyFromBit};
                using var v=new RDTextureView();var rid=rd.TextureCreate(f,v);if(!rid.IsValid)throw new InvalidDataException("Original half buffer allocation failed.");return rid;
            }
            var owned=new List<Rid>();
            try{owned.Add(Make(RenderingDevice.DataFormat.R16G16B16A16Sfloat));owned.Add(Make(RenderingDevice.DataFormat.R32Sfloat));
                owned.Add(Make(RenderingDevice.DataFormat.R16G16Sfloat));return owned.ToArray();}
            catch{for(int n=owned.Count-1;n>=0;n--)rd.FreeRid(owned[n]);throw;}
        });
        OriginalSdfUpstreamComputeOwner? downsample=null,occlusion=null;OriginalSdfFinalProducerOwner? final=null;
        try
        {
            downsample=await OriginalSdfUpstreamComputeOwner.BuildAsync(source.HalfGbuffer,explicitHalfConstants,
                full.Concat(new[]{new OriginalSdfUpstreamDeviceBinding("Image",1,images[0],"original-half-normal-native-format3"),
                    new OriginalSdfUpstreamDeviceBinding("Image",2,images[1],"original-half-depth-native-format2")}).ToArray(),half,compiler);
            occlusion=await OriginalSdfUpstreamComputeOwner.BuildAsync(source.Occlusion,explicitSdfConstants,
                prepared.Concat(new[]{new OriginalSdfUpstreamDeviceBinding("Texture",12,images[0],"original-half-normal-pass1"),
                    new OriginalSdfUpstreamDeviceBinding("Texture",13,images[1],"original-half-depth-pass1"),
                    new OriginalSdfUpstreamDeviceBinding("Texture",14,borrowedPreviousHalfOcclusion,"original-current-previous-occlusion"),
                    new OriginalSdfUpstreamDeviceBinding("Image",0,images[2],"original-half-occlusion-pass0")}).ToArray(),half,compiler);
            OriginalSdfUpstreamDeviceBinding F(string k,uint r)=>full.Single(i=>i.Kind==k && i.Register==r);
            var finalInputs=new[]{new OriginalDeferredDeviceBinding("Sampler",0,F("Sampler",8).Resource,null,F("Sampler",8).SourceResourceUri),
                new OriginalDeferredDeviceBinding("Sampler",1,F("Sampler",9).Resource,null,F("Sampler",9).SourceResourceUri),
                new OriginalDeferredDeviceBinding("Texture",0,F("Texture",8).Resource,F("Texture",8).Resource,F("Texture",8).SourceResourceUri),
                new OriginalDeferredDeviceBinding("Texture",1,F("Texture",9).Resource,F("Texture",9).Resource,F("Texture",9).SourceResourceUri),
                new OriginalDeferredDeviceBinding("Texture",4,images[0],images[0],"original-half-normal-pass1"),
                new OriginalDeferredDeviceBinding("Texture",5,images[1],images[1],"original-half-depth-pass1"),
                new OriginalDeferredDeviceBinding("Texture",6,images[2],images[2],"original-half-occlusion-pass0")};
            var completed=explicitSelection with{IntermediatesCompleted=true};
            final=await OriginalSdfFinalProducerOwner.BuildAsync(source.Final,completed,borrowedRs3,
                new Dictionary<uint,ReadOnlyMemory<byte>>{{0,explicitFinalConstants}},finalInputs,
                new OriginalDeferredRasterState(RenderingDevice.PolygonCullMode.Disabled,false),compiler);
            return new(source,images[0],images[1],images[2],downsample,occlusion,final,completed);
        }
        catch
        {
            if(final is not null)await final.DisposeAsync();if(occlusion is not null)await occlusion.DisposeAsync();if(downsample is not null)await downsample.DisposeAsync();
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int n=images.Length-1;n>=0;n--)rd.FreeRid(images[n]);return true;});throw;
        }
    }
    public async Task<OriginalSdfFinalDrawResult> ProduceAsync(OriginalSdfUpstreamPreparedState explicitCurrentState,uint explicitCpuState40)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);OriginalSdfUpstreamProducerPlan.RequireSupported(explicitCurrentState);
        await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            _downsample.ValidateCurrentResources(rd);_occlusion.ValidateCurrentResources(rd);
            var target=_final.Target;
            if(!rd.TextureIsValid(target.BorrowedStorage) || !rd.TextureIsValid(target.TargetView) ||
                !rd.FramebufferIsValid(target.Framebuffer) ||
                _final.Inputs.Where(i=>i.DescriptorKind=="Texture").Any(i=>!rd.TextureIsValid(i.Resource) ||
                    !rd.TextureIsValid(i.OwnerTexture!.Value)))
                throw new InvalidDataException("SDF stage current final attachments were released before submission.");
            return true;
        });
        await _downsample.DispatchAsync();
        // AAF7F0 explicitly skips the original operation0 dispatch when its
        // selected count sum is zero. The current previous target was supplied.
        await _occlusion.DispatchAsync();
        return await _final.DrawAsync(_selection,explicitCpuState40);
    }
    public ValueTask DisposeAsync()=>new(_disposal??=DisposeCoreAsync());
    private async Task DisposeCoreAsync()
    {
        await _final.DisposeAsync();await _occlusion.DisposeAsync();await _downsample.DisposeAsync();
        await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(HalfOcclusion);rd.FreeRid(HalfDepth);rd.FreeRid(HalfNormal);return true;});
    }
}
