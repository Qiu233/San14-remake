using Godot;
using San14.Data;

/// <summary>Original indirect PS on the borrowed RS3 attachment. Owns only its
/// translated program, CBs, descriptor set and masked pipeline. Every input,
/// complete constant buffer and raster recipe is explicit; creation never clears.</summary>
public sealed class DeferredIndirectOwner : IAsyncDisposable
{
    public DeferredIndirectProgramSource Source { get; }
    public DeferredPixelProgram Program { get; }
    public DeferredExtraDeviceLayer Target { get; }
    public IReadOnlyList<DeferredDeviceBinding> Inputs { get; }
    private readonly IReadOnlyList<Rid> _owned;
    private readonly Rid _pipeline,_set;
    private readonly Dictionary<uint,Rid> _cbs;
    private Task? _disposal;
    private DeferredIndirectOwner(DeferredIndirectProgramSource source,DeferredPixelProgram program,
        DeferredExtraDeviceLayer target,DeferredDeviceBinding[] inputs,List<Rid> owned,Rid pipeline,Rid set,Dictionary<uint,Rid> cbs)
        =>(Source,Program,Target,Inputs,_owned,_pipeline,_set,_cbs)=(source,program,target,Array.AsReadOnly(inputs),owned.AsReadOnly(),pipeline,set,cbs);

    public static async Task<DeferredIndirectOwner> BuildAsync(DeferredIndirectProgramSource source,
        DeferredExtraDeviceLayer borrowedRs3,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitConstants,
        IReadOnlyList<DeferredDeviceBinding> explicitInputs,DeferredRasterState explicitRaster,
        Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(borrowedRs3);
        ArgumentNullException.ThrowIfNull(explicitConstants);ArgumentNullException.ThrowIfNull(explicitInputs);ArgumentNullException.ThrowIfNull(explicitRaster);
        if(borrowedRs3.TextureObjectId!=DeferredExtraLayerPlan.RsObjectId||borrowedRs3.Slice!=3||explicitRaster.EnableBlend)
            throw new InvalidDataException("Indirect owner requires original RS3 and explicit blend disable; other recipes need a complete source contract.");
        var program=await DeferredPixelProgram.BuildAsync(source.Shader,source.CombinationIndex,compiler);
        try
        {
            var declarations=program.DescriptorBindings.Bindings;
            var outputs=program.DescriptorBindings.Interface.StageVariables.Where(v=>v.StorageClass==3&&v.Location is not null).ToArray();
            if(outputs.Length!=1||outputs[0].Location!=0||outputs[0].Type is not {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}}||
                declarations.Any(d=>d.Device.DescriptorSet!=0))throw new InvalidDataException("Indirect PS output/set differs from the source fullscreen contract.");
            var constants=Snapshot(declarations,explicitConstants);
            var inputs=explicitInputs.Select(i=>i??throw new InvalidDataException("Null indirect input.")).ToArray();
            var external=declarations.Where(d=>d.DescriptorKind!="UniformBuffer").ToArray();
            if(inputs.Length!=external.Length||inputs.Select(i=>(i.DescriptorKind,i.SourceBindPoint)).Distinct().Count()!=inputs.Length)
                throw new InvalidDataException("Indirect resources must exactly match every selected PS declaration.");
            foreach(var d in external)
            {
                var input=inputs.SingleOrDefault(i=>i.DescriptorKind==d.DescriptorKind&&i.SourceBindPoint==d.Source.BindPoint);
                if(input is null||!input.Resource.IsValid||string.IsNullOrWhiteSpace(input.SourceResourceUri)||
                    (input.DescriptorKind=="Texture")!=input.OwnerTexture.HasValue||input.OwnerTexture==borrowedRs3.BorrowedStorage||
                    input.Resource==borrowedRs3.TargetView||input.Resource==borrowedRs3.BorrowedStorage)
                    throw new InvalidDataException("Missing or target-aliasing indirect resource.");
            }
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                var owned=new List<Rid>();var cbs=new Dictionary<uint,Rid>();
                Rid Own(Rid r){if(!r.IsValid)throw new InvalidDataException("Indirect device allocation failed.");owned.Add(r);return r;}
                try
                {
                    ValidateTarget(rd,borrowedRs3);
                    foreach(var d in external.Where(d=>d.DescriptorKind=="Texture"))
                    {
                        var i=inputs.Single(i=>i.DescriptorKind==d.DescriptorKind&&i.SourceBindPoint==d.Source.BindPoint);
                        if(!rd.TextureIsValid(i.Resource)||!rd.TextureIsValid(i.OwnerTexture!.Value))throw new InvalidDataException("Indirect borrowed input was released.");
                        using var f=rd.TextureGetFormat(i.Resource);
                        var expected=d.Source.Dimension switch{4=>RenderingDevice.TextureType.Type2D,5=>RenderingDevice.TextureType.Type2DArray,
                            9=>RenderingDevice.TextureType.Cube,_=>throw new InvalidDataException("Unsupported indirect image dimension.")};
                        if(f.TextureType!=expected||!f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit)||
                            f.Format.ToString().Contains("Sint",StringComparison.Ordinal)||f.Format.ToString().Contains("Uint",StringComparison.Ordinal)!=(d.Source.ReturnType==4))
                            throw new InvalidDataException("Indirect image dimension/return type differs from original declaration.");
                    }
                    foreach(var c in constants)cbs.Add(c.Key,Own(rd.UniformBufferCreate((uint)c.Value.Length,c.Value)));
                    var uniforms=new Godot.Collections.Array<RDUniform>();Rid set;
                    try
                    {
                        foreach(var d in declarations)
                        {
                            var type=d.DescriptorKind switch{"UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Texture"=>RenderingDevice.UniformType.Texture,
                                "Sampler"=>RenderingDevice.UniformType.Sampler,_=>throw new InvalidDataException("Unsupported indirect descriptor kind.")};
                            var u=new RDUniform{Binding=checked((int)d.Device.Binding!.Value),UniformType=type};
                            u.AddId(d.DescriptorKind=="UniformBuffer"?cbs[d.Source.BindPoint]:inputs.Single(i=>i.DescriptorKind==d.DescriptorKind&&i.SourceBindPoint==d.Source.BindPoint).Resource);
                            uniforms.Add(u);
                        }
                        set=Own(rd.UniformSetCreate(uniforms,program.ShaderRid,0));
                        if(!rd.UniformSetIsValid(set))throw new InvalidDataException("Indirect descriptor set rejected.");
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                    using var raster=new RDPipelineRasterizationState{CullMode=explicitRaster.Cull};
                    using var multi=new RDPipelineMultisampleState();
                    using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                    using var color=new RDPipelineColorBlendStateAttachment{EnableBlend=false,WriteR=false,WriteG=false,WriteB=false,WriteA=true};
                    using var blend=new RDPipelineColorBlendState{Attachments=[color]};
                    var pipeline=Own(rd.RenderPipelineCreate(program.ShaderRid,rd.FramebufferGetFormat(borrowedRs3.Framebuffer),-1,
                        RenderingDevice.RenderPrimitive.Triangles,raster,multi,depth,blend));
                    if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Indirect masked pipeline rejected.");
                    return new DeferredIndirectOwner(source,program,borrowedRs3,inputs,owned,pipeline,set,cbs);
                }
                catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
            });
        }
        catch{await program.DisposeAsync();throw;}
    }
    private static Dictionary<uint,byte[]> Snapshot(IReadOnlyList<TranslatedShaderBinding> declarations,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> supplied)
    {
        var cbs=declarations.Where(d=>d.DescriptorKind=="UniformBuffer").ToArray();
        if(supplied.Count!=cbs.Length)throw new InvalidDataException("Indirect draw requires every complete original CB.");
        var result=new Dictionary<uint,byte[]>();
        foreach(var cb in cbs){if(!supplied.TryGetValue(cb.Source.BindPoint,out var bytes)||bytes.Length!=cb.ConstantBuffer!.ByteSize)
            throw new InvalidDataException("Indirect CB source length differs.");result.Add(cb.Source.BindPoint,bytes.ToArray());}
        return result;
    }
    public Task UpdateConstantsAsync(IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitConstants)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);var snapshot=Snapshot(Program.DescriptorBindings.Bindings,explicitConstants);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>{ObjectDisposedException.ThrowIf(_disposal is not null,this);
            foreach(var c in snapshot)if(rd.BufferUpdate(_cbs[c.Key],0,(uint)c.Value.Length,c.Value)!=Error.Ok)throw new InvalidDataException("Indirect CB update rejected.");return true;});
    }
    public Task<DeferredIndirectDecision> DrawAsync(DeferredIndirectState explicitState)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);var decision=DeferredIndirectPlan.Select(Source.Pass,explicitState);
        if(decision.Action==DeferredIndirectAction.RequiresOtherRenderer)throw new NotSupportedException(decision.Reason);
        if(decision.Action==DeferredIndirectAction.Preserve)return Task.FromResult(decision);
        if(decision.Shader!=Source.Provider)throw new InvalidDataException("Selected source branch differs from this indirect program.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>{ObjectDisposedException.ThrowIf(_disposal is not null,this);ValidateTarget(rd,Target);
            if(!rd.RenderPipelineIsValid(_pipeline)||!rd.UniformSetIsValid(_set))throw new InvalidDataException("Indirect pipeline or input owner was released.");
            long list=rd.DrawListBegin(Target.Framebuffer,RenderingDevice.DrawFlags.DefaultAll);
            try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_set,0);rd.DrawListDraw(list,false,1,3);}
            finally{rd.DrawListEnd();}return decision;});
    }
    private static void ValidateTarget(RenderingDevice rd,DeferredExtraDeviceLayer t)
    {if(!rd.TextureIsValid(t.BorrowedStorage)||!rd.TextureIsValid(t.TargetView)||!rd.FramebufferIsValid(t.Framebuffer))throw new InvalidDataException("Indirect borrowed RS3 was released.");}
    public ValueTask DisposeAsync()=>new(_disposal??=DisposeCoreAsync());
    private async Task DisposeCoreAsync(){await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;});await Program.DisposeAsync();}
}
