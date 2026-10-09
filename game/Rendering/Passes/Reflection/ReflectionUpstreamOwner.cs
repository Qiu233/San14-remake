using Godot;
using San14.Data;

public sealed record ReflectionUpstreamDispatchPacket(ReadOnlyMemory<byte> ConstantBytes,
    IReadOnlyList<DeferredDeviceBinding> Inputs, uint GroupsX, uint GroupsY, uint GroupsZ,
    string PreparedSourceIdentity);
public sealed record ReflectionUpstreamProbe(ReflectionUpstreamProbeGate Gate,
    ReflectionUpstreamDispatchPacket? Indirect, ReflectionUpstreamDispatchPacket? Reflection);
public sealed record ReflectionUpstreamDeviceImage(Rid Storage, Rid Sample2D, Rid Framebuffer);

/// <summary>Original screen Reflection upstream pass4/CS10 and pass5/CS11,
/// followed by existing pass12/VS1/PS6. Allocates only the three native-format
/// screen intermediates. Provider assets, prepared CB packets, samplers,
/// current depth/camera and source gates are supplied explicitly. Borrowed
/// final attachments must identify the actual selected raw/current targets.</summary>
public sealed class ReflectionUpstreamOwner : IAsyncDisposable
{
    public LoadedReflectionProducerPlan Source { get; }
    public ReflectionUpstreamState State { get; }
    public ReflectionUpstreamSchedule Schedule { get; }
    public IReadOnlyDictionary<ReflectionUpstreamImage,ReflectionUpstreamDeviceImage> Images { get; }
    public IReadOnlyDictionary<uint,ShaderSpirv> Translations { get; }
    public IReadOnlyDictionary<uint,ReflectionComputeBindings> ComputeBindings { get; }
    public IReadOnlyDictionary<uint,ReflectionUpstreamAdaptedCompute> ComputeAdapters { get; }
    private sealed record Dispatch(ReflectionUpstreamStep Step,Rid Pipeline,Rid Set,
        uint X,uint Y,uint Z,DeferredDeviceBinding[] BorrowedInputs);
    private readonly IReadOnlyList<Rid> _owned;
    private readonly IReadOnlyList<Dispatch> _dispatches;
    private ReflectionFinalDraw? _final;
    private Task? _disposal;
    private ReflectionUpstreamOwner(LoadedReflectionProducerPlan source,ReflectionUpstreamState state,
        ReflectionUpstreamSchedule schedule,Dictionary<ReflectionUpstreamImage,ReflectionUpstreamDeviceImage> images,
        Dictionary<uint,ShaderSpirv> translations,Dictionary<uint,ReflectionComputeBindings> bindings,
        Dictionary<uint,ReflectionUpstreamAdaptedCompute> adapters,List<Rid> owned,List<Dispatch> dispatches)
        =>(Source,State,Schedule,Images,Translations,ComputeBindings,ComputeAdapters,_owned,_dispatches)=
            (source,state,schedule,images,translations,bindings,adapters,owned.AsReadOnly(),dispatches.AsReadOnly());

    public static async Task<ReflectionUpstreamOwner> BuildAsync(LoadedReflectionProducerPlan source,
        Vkd3dShaderCompiler compiler,uint explicitWidth,uint explicitHeight,ReflectionUpstreamState explicitState,
        IReadOnlyList<ReflectionUpstreamProbe> explicitProviderList,
        IReadOnlyList<ReflectionFinalAttachment> explicitFinalAttachments,DeferredRasterState explicitRaster)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(explicitState);ArgumentNullException.ThrowIfNull(explicitProviderList);
        ArgumentNullException.ThrowIfNull(explicitFinalAttachments);ArgumentNullException.ThrowIfNull(explicitRaster);
        if(explicitWidth==0 || explicitHeight==0 || explicitWidth>ushort.MaxValue || explicitHeight>ushort.MaxValue ||
            explicitRaster.EnableBlend || explicitRaster.Cull!=RenderingDevice.PolygonCullMode.Disabled)
            throw new InvalidDataException("Reflection upstream requires the explicit native screen extent and no-blend/no-cull backend state.");
        var probes=explicitProviderList.Select(p=>p??throw new InvalidDataException("Null explicit Reflection list record.")).ToArray();
        var schedule=ReflectionUpstreamPlan.Schedule(explicitState,probes.Select(p=>p.Gate).ToArray());
        var translations=new Dictionary<uint,ShaderSpirv>();var bindings=new Dictionary<uint,ReflectionComputeBindings>();
        var adapters=new Dictionary<uint,ReflectionUpstreamAdaptedCompute>();
        if(!schedule.Active)return new(source,explicitState,schedule,[],translations,bindings,adapters,[],[]);
        var targets=explicitFinalAttachments.ToArray();
        if(targets.Length!=3 || targets.Any(t=>t is null || !t.ColorView.IsValid || !t.OwnerTexture.IsValid || string.IsNullOrWhiteSpace(t.SourceResourceUri)) ||
            targets.Select(t=>t.ColorView).Distinct().Count()!=3)
            throw new InvalidDataException("Reflection upstream requires three explicit distinct final attachments and source identities.");
        foreach(uint pass in new uint[]{4,5})
        {
            var stage=ReflectionUpstreamPlan.RequireStage(source,pass);
            var translated=compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,stage.ProgramIndex));
            if(translated.SourceProgramSha256!=stage.ProgramSha256)throw new InvalidDataException("Reflection upstream source shader bytes changed.");
            translations.Add(pass,translated);bindings.Add(pass,ReflectionUpstreamPlan.ReadBindings(stage,translated.Bytes));
            adapters.Add(pass,ReflectionUpstreamBufferAdapter.Adapt(translated.Bytes,bindings[pass]));
        }
        var packets=schedule.Steps.Select(step=>
        {
            var p=probes[(int)step.SourceListIndex];var packet=(step.PassIndex==4?p.Indirect:p.Reflection)
                ??throw new InvalidDataException("Selected original Reflection dispatch lacks its complete prepared packet.");
            if(string.IsNullOrWhiteSpace(packet.PreparedSourceIdentity) || packet.GroupsX!=(explicitWidth+7)/8 ||
                packet.GroupsY!=(explicitHeight+7)/8 || packet.GroupsZ!=1)
                throw new InvalidDataException("Reflection dispatch must supply its source identity and explicit complete 8x8x1 group coverage.");
            var stage=ReflectionUpstreamPlan.RequireStage(source,step.PassIndex);
            var constant=ReflectionUpstreamPlan.SnapshotConstants(stage,packet.ConstantBytes,explicitWidth,explicitHeight,step.SourceListIndex);
            var inputs=packet.Inputs.Select(i=>i??throw new InvalidDataException("Null Reflection compute resource.")).ToArray();
            var external=bindings[step.PassIndex].Bindings.Where(b=>b.DescriptorKind!="UniformBuffer" &&
                !(b.DescriptorKind=="Texture" && b.Source.BindPoint==15) && !(b.DescriptorKind=="Image" && b.Source.BindPoint==1)).ToArray();
            if(inputs.Length!=external.Length || inputs.Select(i=>(i.DescriptorKind,i.SourceBindPoint)).Distinct().Count()!=inputs.Length)
                throw new InvalidDataException("Reflection prepared inputs must exactly cover all external compute descriptors.");
            foreach(var d in external)
            {
                var i=inputs.SingleOrDefault(i=>i.DescriptorKind==d.DescriptorKind && i.SourceBindPoint==d.Source.BindPoint);
                if(i is null || !i.Resource.IsValid || string.IsNullOrWhiteSpace(i.SourceResourceUri) ||
                    (i.DescriptorKind=="Texture")!=i.OwnerTexture.HasValue ||
                    i.OwnerTexture is Rid parent && (!parent.IsValid || targets.Any(t=>t.OwnerTexture==parent || t.ColorView==parent)) ||
                    targets.Any(t=>t.ColorView==i.Resource || t.OwnerTexture==i.Resource))
                    throw new InvalidDataException("Reflection prepared input is missing or aliases a final target.");
            }
            return(Step:step,Packet:packet,Constants:constant,Inputs:inputs);
        }).ToArray();
        var owner=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();var dispatches=new List<Dispatch>();var images=new Dictionary<ReflectionUpstreamImage,ReflectionUpstreamDeviceImage>();
            Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Reflection upstream device allocation failed.");owned.Add(rid);return rid;}
            try
            {
                foreach(var t in targets)
                {
                    if(!rd.TextureIsValid(t.ColorView) || !rd.TextureIsValid(t.OwnerTexture))throw new InvalidDataException("Reflection borrowed final target was released.");
                    using var f=rd.TextureGetFormat(t.ColorView);
                    if(f.Width!=explicitWidth || f.Height!=explicitHeight || f.Format!=RenderingDevice.DataFormat.B10G11R11UfloatPack32 ||
                        !f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit))
                        throw new InvalidDataException("Reflection final attachment extent/format differs from source.");
                }
                foreach(var image in Enum.GetValues<ReflectionUpstreamImage>())
                {
                    using var f=new RDTextureFormat{Format=RenderingDevice.DataFormat.R16G16B16A16Sfloat,
                        Width=explicitWidth,Height=explicitHeight,Depth=1,ArrayLayers=1,Mipmaps=1,
                        TextureType=RenderingDevice.TextureType.Type2DArray,Samples=RenderingDevice.TextureSamples.Samples1,
                        UsageBits=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.StorageBit|
                            RenderingDevice.TextureUsageBits.ColorAttachmentBit|RenderingDevice.TextureUsageBits.CanCopyFromBit};
                    using var view=new RDTextureView();var storage=Own(rd.TextureCreate(f,view));
                    var sample=Own(rd.TextureCreateSharedFromSlice(view,storage,0,0,1,RenderingDevice.TextureSliceType.Slice2D));
                    var framebuffer=Own(rd.FramebufferCreate([sample]));
                    if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("Reflection source intermediate framebuffer rejected.");
                    images.Add(image,new(storage,sample,framebuffer));
                }
                var pipelines=new Dictionary<uint,(Rid Shader,Rid Pipeline)>();
                foreach(var pass in translations)
                {
                    using var spirv=new RDShaderSpirV();spirv.SetStageBytecode(RenderingDevice.ShaderStage.Compute,adapters[pass.Key].Bytes);
                    var shader=Own(rd.ShaderCreateFromSpirV(spirv,$"OriginalReflectionPass{pass.Key}"));
                    var pipeline=Own(rd.ComputePipelineCreate(shader));
                    if(!rd.ComputePipelineIsValid(pipeline))throw new InvalidDataException("Reflection original compute pipeline rejected.");
                    pipelines.Add(pass.Key,(shader,pipeline));
                }
                foreach(var p in packets)
                {
                    ValidateInputs(rd,bindings[p.Step.PassIndex],p.Inputs);
                    var cb=Own(rd.UniformBufferCreate((uint)p.Constants.Length,p.Constants));
                    var uniforms=new Godot.Collections.Array<RDUniform>();Rid set;
                    try
                    {
                        foreach(var d in bindings[p.Step.PassIndex].Bindings)
                        {
                            Rid resource=d.DescriptorKind=="UniformBuffer"?cb:
                                d.DescriptorKind=="Texture" && d.Source.BindPoint==15?images[p.Step.PreviousResult].Sample2D:
                                d.DescriptorKind=="Image" && d.Source.BindPoint==1?images[p.Step.Output].Storage:
                                p.Inputs.Single(i=>i.DescriptorKind==d.DescriptorKind && i.SourceBindPoint==d.Source.BindPoint).Resource;
                            var u=new RDUniform{Binding=checked((int)d.Device.Binding!.Value),UniformType=d.DescriptorKind switch{
                                "UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Sampler"=>RenderingDevice.UniformType.Sampler,
                                "Texture"=>RenderingDevice.UniformType.Texture,"Image"=>RenderingDevice.UniformType.Image,
                                "ImageBuffer"=>RenderingDevice.UniformType.StorageBuffer,_=>throw new InvalidDataException("Unsupported Reflection compute descriptor.")}};
                            u.AddId(resource);uniforms.Add(u);
                        }
                        set=Own(rd.UniformSetCreate(uniforms,pipelines[p.Step.PassIndex].Shader,0));
                        if(!rd.UniformSetIsValid(set))throw new InvalidDataException("Reflection original compute descriptor set rejected.");
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                    dispatches.Add(new(p.Step,pipelines[p.Step.PassIndex].Pipeline,set,p.Packet.GroupsX,p.Packet.GroupsY,p.Packet.GroupsZ,p.Inputs));
                }
                return new ReflectionUpstreamOwner(source,explicitState,schedule,images,translations,bindings,adapters,owned,dispatches);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
        try
        {
            var first=owner.Images[ReflectionUpstreamImage.IndirectResult];var second=owner.Images[ReflectionUpstreamImage.ReflectionResult];
            owner._final=await ReflectionProducerOwner.BuildFinalDrawAsync(source,compiler,targets,
                new Dictionary<uint,ReadOnlyMemory<byte>>(),[
                    new("Texture",8,first.Sample2D,first.Storage,source.Shader.Source.ResourceUri+"#screen-indirect-result"),
                    new("Texture",9,second.Sample2D,second.Storage,source.Shader.Source.ResourceUri+"#screen-reflection-result")],explicitRaster);
            return owner;
        }
        catch{await owner.DisposeAsync();throw;}
    }

    private static void ValidateInputs(RenderingDevice rd,ReflectionComputeBindings bindings,IReadOnlyList<DeferredDeviceBinding> inputs)
    {
        foreach(var input in inputs.Where(i=>i.DescriptorKind=="Texture"))
        {
            if(!rd.TextureIsValid(input.Resource) || !rd.TextureIsValid(input.OwnerTexture!.Value))throw new InvalidDataException("Reflection borrowed compute image was released.");
            var source=bindings.Bindings.Single(b=>b.DescriptorKind==input.DescriptorKind && b.Source.BindPoint==input.SourceBindPoint).Source;
            using var f=rd.TextureGetFormat(input.Resource);
            var expected=source.Dimension==5?RenderingDevice.TextureType.Type2DArray:RenderingDevice.TextureType.Type2D;
            if(f.TextureType!=expected || !f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit) ||
                f.Samples!=RenderingDevice.TextureSamples.Samples1 || f.Format.ToString().Contains("Uint",StringComparison.Ordinal) ||
                f.Format.ToString().Contains("Sint",StringComparison.Ordinal))throw new InvalidDataException("Reflection compute image dimension/return type differs.");
        }
    }

    /// <summary>Source clear success flags, list skips, dispatch ordering and
    /// final bind success are honored. Construction does not initialize pixels.</summary>
    public async Task<ReflectionUpstreamSchedule> ExecuteAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(!Schedule.Active)return Schedule;
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            foreach(var image in Images.Values)
                if(!rd.TextureIsValid(image.Storage) || !rd.TextureIsValid(image.Sample2D) || !rd.FramebufferIsValid(image.Framebuffer))
                    throw new InvalidDataException("Reflection owned intermediate was released.");
            if(Schedule.DrawFinal && (_final is null || !rd.FramebufferIsValid(_final.Framebuffer) ||
                _final.Attachments.Any(t=>!rd.TextureIsValid(t.ColorView) || !rd.TextureIsValid(t.OwnerTexture))))
                throw new InvalidDataException("Reflection borrowed final attachments were released before upstream execution.");
            foreach(var d in _dispatches)
            {
                ValidateInputs(rd,ComputeBindings[d.Step.PassIndex],d.BorrowedInputs);
                if(!rd.ComputePipelineIsValid(d.Pipeline) || !rd.UniformSetIsValid(d.Set))
                    throw new InvalidDataException("Reflection compute descriptor or borrowed resource was released.");
            }
            void Clear(ReflectionUpstreamImage name)
            {
                long list=rd.DrawListBegin(Images[name].Framebuffer,RenderingDevice.DrawFlags.ClearColorAll,[new Color(0,0,0,0)]);
                rd.DrawListEnd();
            }
            if(Schedule.ClearIntermediateResults){Clear(ReflectionUpstreamImage.IndirectResult);Clear(ReflectionUpstreamImage.ReflectionResult);}
            foreach(uint pass in new uint[]{4,5})
            {
                if(pass==4?Schedule.ClearScratchBeforeIndirect:Schedule.ClearScratchBeforeReflection)Clear(ReflectionUpstreamImage.Scratch);
                foreach(var d in _dispatches.Where(d=>d.Step.PassIndex==pass))
                {
                    long list=rd.ComputeListBegin();
                    try{rd.ComputeListBindComputePipeline(list,d.Pipeline);rd.ComputeListBindUniformSet(list,d.Set,0);rd.ComputeListDispatch(list,d.X,d.Y,d.Z);}
                    finally{rd.ComputeListEnd();}
                }
            }
            return true;
        });
        if(Schedule.DrawFinal)await _final!.DrawAsync();
        return Schedule;
    }
    public ValueTask DisposeAsync()=>new(_disposal??=DisposeCoreAsync());
    private async Task DisposeCoreAsync()
    {
        if(_final is not null)await _final.DisposeAsync();
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;});
    }
}
