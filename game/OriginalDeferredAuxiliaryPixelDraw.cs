using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>One actual original auxiliary fullscreen draw. Owns only its
/// complete CBs, uniform sets and pipeline; source images/program/target stay
/// borrowed. The producer calls Submit on the target's ordered render thread.</summary>
public sealed class OriginalDeferredAuxiliaryPixelDraw : IAsyncDisposable
{
    public OriginalDeferredAuxiliaryPixelProgram Program { get; }
    public OriginalDeferredAuxiliaryTargetDevice Target { get; }
    public OriginalDeferredAuxiliaryDeviceSurface Surface { get; }
    public IReadOnlyList<OriginalDeferredDeviceBinding> Inputs { get; }
    private readonly IReadOnlyList<Rid> owned;
    private readonly IReadOnlyDictionary<uint,Rid> sets;
    private readonly Rid pipeline;
    private readonly int vertexCount;
    private readonly RenderingDevice.RenderPrimitive primitive;
    private readonly Rid vertices,indices;
    private readonly OriginalDeferredAuxiliaryExtent viewport;
    private Task? disposal;
    private readonly object retirementGate=new();

    private OriginalDeferredAuxiliaryPixelDraw(OriginalDeferredAuxiliaryPixelProgram program,
        OriginalDeferredAuxiliaryTargetDevice target,OriginalDeferredAuxiliaryDeviceSurface surface,
        IReadOnlyList<OriginalDeferredDeviceBinding> inputs,List<Rid> allocated,Dictionary<uint,Rid> uniformSets,
        Rid renderPipeline,int count,RenderingDevice.RenderPrimitive sourcePrimitive,Rid vertexArray=default,Rid indexArray=default,
        OriginalDeferredAuxiliaryExtent currentViewport=default)
        =>(Program,Target,Surface,Inputs,owned,sets,pipeline,vertexCount,primitive,vertices,indices,viewport)=
            (program,target,surface,inputs,allocated.AsReadOnly(),uniformSets,renderPipeline,count,sourcePrimitive,vertexArray,indexArray,currentViewport);

    public static Task<OriginalDeferredAuxiliaryPixelDraw> BuildAsync(OriginalDeferredAuxiliaryPixelProgram program,
        OriginalDeferredAuxiliaryTargetDevice target,uint surfaceId,
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> completeConstants,
        IReadOnlyList<OriginalDeferredDeviceBinding> explicitPixelInputs,int sourceVertexCount,
        RenderingDevice.RenderPrimitive sourcePrimitive,OriginalDeferredAuxiliaryStockQuad? stockQuad=null,
        IReadOnlyDictionary<uint,OriginalDeferredAuxiliaryTargetBorrow>? sourceMipViews=null,
        OriginalDeferredAuxiliaryExtent? sourceViewport=null)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(completeConstants);ArgumentNullException.ThrowIfNull(explicitPixelInputs);
        program.ValidateSource();OriginalDeferredAuxiliaryTargetSources.Validate(target.Construction);
        var surface=target.Surfaces.SingleOrDefault(s=>s.Source.SurfaceId==surfaceId)
            ??throw new InvalidDataException("Original auxiliary target surface is absent.");
        var viewport=sourceViewport??surface.Source.Extent;
        if(viewport.Width<=0||viewport.Height<=0||viewport.Width>surface.Source.Extent.Width||viewport.Height>surface.Source.Extent.Height)
            throw new InvalidDataException("Actual auxiliary viewport exceeds its native target surface.");
        bool vertexAttributes=program.VertexBindings.Interface.StageVariables.Any(v=>v.StorageClass==1&&v.Location is not null);
        if(vertexAttributes!=(stockQuad is not null)||
            sourceVertexCount is not(3 or 4)||sourcePrimitive is not(RenderingDevice.RenderPrimitive.Triangles or RenderingDevice.RenderPrimitive.TriangleStrips))
            throw new InvalidDataException("This auxiliary draw needs an explicit supported original VertexID fullscreen geometry contract.");
        var stages=new[]{(Stage:1u,Bindings:program.VertexBindings),(Stage:0u,Bindings:program.PixelBindings)};
        var cbs=stages.SelectMany(s=>s.Bindings.Bindings.Where(b=>b.DescriptorKind=="UniformBuffer").Select(b=>(s.Stage,Binding:b))).ToArray();
        if(completeConstants.Count!=cbs.Length)throw new InvalidDataException("Auxiliary draw requires every complete original VS/PS CB.");
        var constants=new Dictionary<(uint,uint),byte[]>();
        foreach(var cb in cbs)
        {
            if(!completeConstants.TryGetValue((cb.Stage,cb.Binding.Source.BindPoint),out var bytes)||
                bytes.Length!=cb.Binding.ConstantBuffer!.ByteSize)
                throw new InvalidDataException("Auxiliary source CB is missing or has another RDEF size.");
            constants.Add((cb.Stage,cb.Binding.Source.BindPoint),bytes.ToArray());
        }
        var inputs=explicitPixelInputs.Select(i=>i??throw new InvalidDataException("Auxiliary source input is null.")).ToArray();
        var expected=program.PixelBindings.Bindings.Where(b=>b.DescriptorKind!="UniformBuffer").ToArray();
        if(inputs.Length!=expected.Length||inputs.Select(i=>(i.DescriptorKind,i.SourceBindPoint)).Distinct().Count()!=inputs.Length)
            throw new InvalidDataException("Auxiliary source inputs differ from their exact selected original RDEF.");
        foreach(var declaration in expected)
        {
            var input=inputs.SingleOrDefault(i=>i.DescriptorKind==declaration.DescriptorKind&&i.SourceBindPoint==declaration.Source.BindPoint);
            if(input is null||!input.Resource.IsValid||string.IsNullOrWhiteSpace(input.SourceResourceUri)||
                (input.DescriptorKind=="Texture")!=input.OwnerTexture.HasValue||
                input.Resource==target.StorageRid)
                throw new InvalidDataException("Auxiliary source input is missing or aliases its output; use the original scratch chain.");
            if(input.OwnerTexture==target.StorageRid&&
                (sourceMipViews is null||!sourceMipViews.TryGetValue(declaration.Source.BindPoint,out var view)||
                    view.StorageRid!=target.StorageRid||view.ShaderViewRid!=input.Resource||view.ViewMipCount!=1||
                    view.MostDetailedMip==surface.Source.MipLevel))
                throw new InvalidDataException("Auxiliary same-parent input needs its actual distinct single-mip source lease.");
        }
        bool depthTarget=target.TargetFormat==RenderingDevice.DataFormat.D32Sfloat;
        var outputs=program.PixelBindings.Interface.StageVariables.Where(v=>v.StorageClass==3).ToArray();
        if(outputs.Length!=1||(depthTarget?outputs[0].BuiltIn!=22:outputs[0].Location!=0))
            throw new InvalidDataException("Original auxiliary pixel output differs from its real color/depth target.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var allocated=new List<Rid>();var uniformSets=new Dictionary<uint,Rid>();
            Rid Own(Rid value){if(!value.IsValid)throw new InvalidDataException("Auxiliary source draw device allocation failed.");allocated.Add(value);return value;}
            try
            {
                if(!rd.FramebufferIsValid(surface.Framebuffer)||!rd.TextureIsValid(target.StorageRid))
                    throw new InvalidDataException("Auxiliary source target has retired.");
                foreach(var declaration in expected.Where(b=>b.DescriptorKind=="Texture"))
                {
                    var input=inputs.Single(i=>i.DescriptorKind=="Texture"&&i.SourceBindPoint==declaration.Source.BindPoint);
                    if(!rd.TextureIsValid(input.Resource)||!rd.TextureIsValid(input.OwnerTexture!.Value))
                        throw new InvalidDataException("Auxiliary source input has retired.");
                    using var format=rd.TextureGetFormat(input.Resource);
                    var required=declaration.Source.Dimension switch
                    {4=>RenderingDevice.TextureType.Type2D,5=>RenderingDevice.TextureType.Type2DArray,
                     _=>throw new InvalidDataException("Auxiliary pass source texture is outside the supported original dimensions.")};
                    bool depth=format.Format is RenderingDevice.DataFormat.D32Sfloat or RenderingDevice.DataFormat.D32SfloatS8Uint or
                        RenderingDevice.DataFormat.D24UnormS8Uint or RenderingDevice.DataFormat.D16Unorm;
                    bool unsigned=!depth&&format.Format.ToString().Contains("Uint",StringComparison.Ordinal);
                    if(format.TextureType!=required||!format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit)||
                        format.Format.ToString().Contains("Sint",StringComparison.Ordinal)||unsigned!=(declaration.Source.ReturnType==4))
                        throw new InvalidDataException("Auxiliary actual input does not match its original shader shape/return type.");
                }
                var buffers=constants.ToDictionary(p=>p.Key,p=>Own(rd.UniformBufferCreate((uint)p.Value.Length,p.Value)));
                foreach(var stage in stages)
                {
                    if(stage.Bindings.Bindings.Count==0)continue;
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var declaration in stage.Bindings.Bindings)
                        {
                            var type=declaration.DescriptorKind switch
                            {"UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Texture"=>RenderingDevice.UniformType.Texture,
                             "Sampler"=>RenderingDevice.UniformType.Sampler,_=>throw new InvalidDataException("Unsupported auxiliary original descriptor.")};
                            Rid value=declaration.DescriptorKind=="UniformBuffer"?buffers[(stage.Stage,declaration.Source.BindPoint)]:
                                inputs.Single(i=>i.DescriptorKind==declaration.DescriptorKind&&i.SourceBindPoint==declaration.Source.BindPoint).Resource;
                            var uniform=new RDUniform{Binding=checked((int)declaration.Device.Binding!.Value),UniformType=type};
                            uniform.AddId(value);uniforms.Add(uniform);
                        }
                        Rid set=Own(rd.UniformSetCreate(uniforms,program.ShaderRid,stage.Stage));
                        if(!rd.UniformSetIsValid(set))throw new InvalidDataException("Device rejected auxiliary original uniform set.");
                        uniformSets.Add(stage.Stage,set);
                    }
                    finally{foreach(var uniform in uniforms)uniform.Dispose();}
                }
                long vertexFormat=-1;Rid vertexArray=default,indexArray=default;
                if(stockQuad is not null)
                {
                    var attrs=program.VertexBindings.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location).ToArray();
                    if(attrs.Length!=3||attrs.Select(v=>v.Location!.Value).SequenceEqual(new uint[]{0,1,2})==false||
                        stockQuad.Extent!=new Vector2I(surface.Source.Extent.Width,surface.Source.Extent.Height)||
                        (program.DepthCopySource is null&&program.StockCopySource is null)||sourceVertexCount!=4||sourcePrimitive!=RenderingDevice.RenderPrimitive.Triangles)
                        throw new InvalidDataException("Stock source quad differs from the recovered depth-copy vertex contract.");
                    using var position=new RDVertexAttribute{Location=0,Offset=0,Stride=24,Format=RenderingDevice.DataFormat.R32G32B32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex};
                    using var color=new RDVertexAttribute{Location=1,Offset=12,Stride=24,Format=RenderingDevice.DataFormat.R8G8B8A8Unorm,Frequency=RenderingDevice.VertexFrequency.Vertex};
                    using var uv=new RDVertexAttribute{Location=2,Offset=16,Stride=24,Format=RenderingDevice.DataFormat.R32G32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex};
                    vertexFormat=rd.VertexFormatCreate([position,color,uv]);
                    Rid vb=Own(rd.VertexBufferCreate(96,stockQuad.VertexBytes.ToArray()));
                    vertexArray=Own(rd.VertexArrayCreate(4,vertexFormat,[vb,vb,vb]));
                    Rid ib=Own(rd.IndexBufferCreate(6,RenderingDevice.IndexBufferFormat.Uint16,stockQuad.IndexBytes.ToArray()));
                    indexArray=Own(rd.IndexArrayCreate(ib,0,6));
                }
                using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multi=new RDPipelineMultisampleState();
                using var depthState=new RDPipelineDepthStencilState{EnableDepthTest=depthTarget,EnableDepthWrite=depthTarget,
                    DepthCompareOperator=RenderingDevice.CompareOperator.Always};
                using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=false};
                using var blend=new RDPipelineColorBlendState{Attachments=depthTarget?[]:[attachment]};
                Rid pipeline=Own(rd.RenderPipelineCreate(program.ShaderRid,rd.FramebufferGetFormat(surface.Framebuffer),vertexFormat,
                    sourcePrimitive,raster,multi,depthState,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Device rejected actual auxiliary source pipeline.");
                return new OriginalDeferredAuxiliaryPixelDraw(program,target,surface,Array.AsReadOnly(inputs),allocated,
                    uniformSets,pipeline,sourceVertexCount,sourcePrimitive,vertexArray,indexArray,viewport);
            }
            catch{for(int i=allocated.Count-1;i>=0;i--)rd.FreeRid(allocated[i]);throw;}
        });
    }

    public void Submit(RenderingDevice rd)
    {
        ObjectDisposedException.ThrowIf(disposal is not null,this);
        if(!rd.FramebufferIsValid(Surface.Framebuffer)||!rd.RenderPipelineIsValid(pipeline)||
            sets.Values.Any(set=>!rd.UniformSetIsValid(set)))throw new InvalidDataException("Actual auxiliary draw dependencies have retired.");
        long list=rd.DrawListBegin(Surface.Framebuffer,region:new Rect2(0,0,viewport.Width,viewport.Height));
        try
        {
            rd.DrawListBindRenderPipeline(list,pipeline);
            foreach(var set in sets)rd.DrawListBindUniformSet(list,set.Value,set.Key);
            if(vertices.IsValid)rd.DrawListBindVertexArray(list,vertices);
            if(indices.IsValid)rd.DrawListBindIndexArray(list,indices);
            rd.DrawListDraw(list,indices.IsValid,1,indices.IsValid?0u:(uint)vertexCount);
        }
        finally{rd.DrawListEnd();}
    }
    public ValueTask DisposeAsync(){lock(retirementGate)return new(disposal??=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);return true;}));}
}
