using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;

/// <summary>Assembles the complete original PS1 resource contract. Owns the
/// uploaded explicit material table; borrows program, native GGX and frame.
/// The remaining images, typed buffers and samplers must be supplied by their
/// source producers. Retire the scene's cached deferred draw before this owner.
/// No active material cache, lighting input or missing resource is inferred.</summary>
public sealed partial class NativeDeferredResources : IAsyncDisposable
{
    public const string PixelSourceSha256 = "B01248B54B5D09A98D850D4627520F78808EA14C1CF090F3C804753E78F1884E";
    public DeferredPixelProgram Program { get; }
    public LoadedDeferredTexturePlan SourcePlan { get; }
    public LoadedDeferredMaterialTable MaterialTableSource => _table.Source;
    public CompiledGgxShader GgxSource => _ggx.NativeSource!;
    public IReadOnlyList<TranslatedShaderBinding> RequiredExplicitInputs { get; }
    private readonly DeferredMaterialTableDevice _table;
    private readonly GgxAmbientLut _ggx;
    private readonly TranslatedShaderBinding[] _declarations;
    private readonly DeferredDeviceBinding[] _staticInputs;
    private readonly SemaphoreSlim _gate = new(1,1);
    private readonly object _lifetimeGate = new();
    private bool _retiring;
    private Task? _disposal;

    private NativeDeferredResources(DeferredPixelProgram program,
        LoadedDeferredTexturePlan plan,DeferredMaterialTableDevice table,
        GgxAmbientLut ggx,TranslatedShaderBinding[] declarations,
        DeferredDeviceBinding[]? selectedStaticInputs = null)
    {
        (Program,SourcePlan,_table,_ggx,_declarations)=(program,plan,table,ggx,declarations);
        _staticInputs=selectedStaticInputs ?? [
            new("Texture",5,ggx.TextureRid,ggx.TextureRid,ggx.NativeSource!.ResourceUri+"#generated-ambient-lut"),
            new("Sampler",5,ggx.SamplerRid,null,ggx.NativeSource.ResourceUri+"#sampler/1514300000000000"),
            new("TextureBuffer",25,table.BufferRid,null,table.Source.Shader.Source.ResourceUri+"#explicit-source-material-table")
        ];
        var known=plan.Inputs.Select(i=>("Texture",i.PixelBinding.BindPoint))
            .Concat(_staticInputs.Select(i=>(i.DescriptorKind,i.SourceBindPoint))).ToHashSet();
        RequiredExplicitInputs=Array.AsReadOnly(declarations
            .Where(d=>!known.Contains((d.DescriptorKind,d.Source.BindPoint))).ToArray());
    }

    /// <summary>The table contains caller-provided source rows, including the
    /// original reserved zero row. GGX must have native executable provenance;
    /// its byte-only construction overload cannot satisfy this source contract.</summary>
    public static async Task<NativeDeferredResources> BuildAsync(DeferredPixelProgram program,
        LoadedDeferredTexturePlan plan,LoadedDeferredMaterialTable explicitTable,
        GgxAmbientLut borrowedNativeGgx)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(explicitTable);ArgumentNullException.ThrowIfNull(borrowedNativeGgx);
        if(program.Translation.SourceProgramSha256!=PixelSourceSha256||
            !SameShader(program.Source,plan.Shader)||!SameShader(program.Source,explicitTable.Shader)||
            plan.ResourceBindings.Combination.Index!=program.Combination.Index||
            plan.Inputs.Count!=5||!plan.Inputs.Select(i=>i.Role).Order().SequenceEqual(new[]{"AB","D1","NS","RR","RS"}))
            throw new InvalidDataException("Native deferred resource owners differ from the selected original PS1 identity.");
        foreach(var source in new[]{program.Source,plan.Shader,explicitTable.Shader})
            if(!Convert.ToHexString(SHA256.HashData(source.Source.Bytes)).Equals(source.Source.DecodedSha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Native deferred resource source bytes differ from their declared hash.");
        var all=program.SourceDescriptorBindings.Bindings;
        var sourceDeclarations=all.Where(d=>d.DescriptorKind!="UniformBuffer").ToArray();
        if(all.Count!=42||all.Count(d=>d.DescriptorKind=="UniformBuffer")!=3||
            sourceDeclarations.Length!=39||sourceDeclarations.Select(d=>(d.DescriptorKind,d.Source.BindPoint)).Distinct().Count()!=39||
            sourceDeclarations.Count(d=>d.DescriptorKind=="Texture")!=17||
            sourceDeclarations.Count(d=>d.DescriptorKind=="TextureBuffer")!=9||
            sourceDeclarations.Count(d=>d.DescriptorKind=="Sampler")!=13)
            throw new InvalidDataException("Native deferred declarations differ from the complete original PS1 inventory.");
        TranslatedShaderBinding Declaration(string kind,uint register,string name,uint dimension,uint? returnType=null)
        {
            var rows=sourceDeclarations.Where(d=>d.DescriptorKind==kind&&d.Source.BindPoint==register).ToArray();
            if(rows.Length!=1||rows[0].Source.Name.Text!=name||rows[0].Source.Dimension!=dimension||
                rows[0].Source.BindCount!=1||returnType.HasValue&&rows[0].Source.ReturnType!=returnType.Value)
                throw new InvalidDataException("Native deferred known resource differs from its source consumer declaration.");
            return rows[0];
        }
        _=Declaration("Texture",5,"sGxAmbLut",4,5);
        _=Declaration("Sampler",5,"__smpsGxAmbLut",0);
        var material=Declaration("TextureBuffer",25,"tMaterialTable",1,4);
        if(explicitTable.PixelBinding!=material.Source||explicitTable.SlotParameter.RawValue!=25||
            explicitTable.Layout.Name!="GBufferLayout"||explicitTable.EntryCount<1||
            explicitTable.Bytes.Length!=checked(explicitTable.EntryCount*4)||
            BinaryPrimitives.ReadUInt32LittleEndian(explicitTable.Bytes.AsSpan(0,4))!=0)
            throw new InvalidDataException("Native deferred explicit material table differs from its original slot/uint-row contract.");
        (string Role,short Encoded,uint Register,string Name,uint Dimension,uint ArgumentHash)[] roles=[
            ("D1",8,11,"sDepth",4,0xB7CCDD1F),("AB",128,12,"sGBuf0",4,0x33E0132B),
            ("RS",129,13,"sGBuf1",5,0x454BF54B),("NS",130,14,"sGBuf2",4,0xF43A3947),
            ("RR",131,15,"sGBuf3",5,0x5102842C)
        ];
        foreach(var role in roles)
        {
            var input=plan.Inputs.Single(i=>i.Role==role.Role);
            var match=Declaration("Texture",role.Register,role.Name,role.Dimension,5);
            if(match.Source!=input.PixelBinding||input.EncodedParameter!=role.Encoded||
                input.ResourceMapping.PrimaryEncoded!=role.Encoded||input.ResourceMapping.SlotIndex!=(int)role.Register||
                input.ResourceMapping.Name!=role.Name||input.SourceArgument.NameHash!=role.ArgumentHash||
                input.SourceArgument.DescriptorWord!=0x05000001||input.SourceArgument.RawValue.Length!=4||
                BinaryPrimitives.ReadUInt32LittleEndian(input.SourceArgument.RawValue)!=input.Texture.Object.ObjectId)
                throw new InvalidDataException("Native deferred G-buffer plan differs from its pixel declarations.");
        }
        var native=borrowedNativeGgx.NativeSource;
        if(native is null||
            !borrowedNativeGgx.SourceSamplerKey.AsSpan().SequenceEqual(Convert.FromHexString("1514300000000000")))
            throw new InvalidDataException("Native deferred GGX source lacks the recovered generator and sampler provenance.");
        uint[] expectedSampler=[0x14,3,3,3,0,1,4,0,0,0,0,0,0];
        byte[] sampler=borrowedNativeGgx.SourceSamplerDescriptor;
        if(sampler.Length!=52||Enumerable.Range(0,13).Any(i=>
            BinaryPrimitives.ReadUInt32LittleEndian(sampler.AsSpan(i*4,4))!=expectedSampler[i]))
            throw new InvalidDataException("Native deferred GGX consumer sampler differs from the source recipe.");
        await ValidateGgxAsync(borrowedNativeGgx);
        var declarations=program.DescriptorBindings.Bindings.Where(d=>d.DescriptorKind!="UniformBuffer").ToArray();
        var removed=program.SourceNullTextureSamplerRegisters.ToHashSet();
        if(program.NullImageSpecialization is { } specialization && program.LocalAmbientNullSpecialization is null && program.IesSourceNull is null)
        {
            var verified=DeferredNullImageAdapter.ReadDeviceBindings(program.SourceDescriptorBindings,specialization);
            if(verified.Interface.Sha256!=program.Translation.SpirvSha256||
                !verified.Bindings.Select(DeclarationIdentity).SequenceEqual(program.DescriptorBindings.Bindings.Select(DeclarationIdentity)))
                throw new InvalidDataException("Deferred compiled descriptors differ from their proven source-null specialization.");
        }
        if(program.LocalAmbientNullSpecialization is { } las && program.IesSourceNull is null)
        {
            if(las.Preceding2D!=program.NullImageSpecialization ||
                las.Source.Shader.Source.ResourceUri!=program.Source.Source.ResourceUri ||
                las.Source.Shader.Source.DecodedSha256!=program.Source.Source.DecodedSha256 ||
                las.Source.CombinationIndex!=program.Combination.Index)
                throw new InvalidDataException("Deferred LAS null proof differs from its source shader or preceding2D scope.");
            var verified=DeferredSourceLocalAmbientNullAdapter.ReadDeviceBindings(program.SourceDescriptorBindings,las);
            if(verified.Interface.Sha256!=program.Translation.SpirvSha256 ||
                !verified.Bindings.Select(DeclarationIdentity).SequenceEqual(program.DescriptorBindings.Bindings.Select(DeclarationIdentity)))
                throw new InvalidDataException("Deferred LAS compiled descriptors differ from the proven original null-read specialization.");
        }
        _=program.ReadValidatedSourceDeviceBindings();
        if(!declarations.Select(DeclarationIdentity).SequenceEqual(sourceDeclarations.Where(d=>
                !(d.DescriptorKind is "Texture" or "Sampler" && removed.Contains(d.Source.BindPoint)) &&
                !(program.LocalAmbientNullSpecialization is not null && d.Source.InputType==2 && d.Source.BindPoint is 22 or 23 or 24)).Select(DeclarationIdentity))||
            !Convert.ToHexString(SHA256.HashData(program.Translation.Bytes)).Equals(program.Translation.SpirvSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Deferred device declarations must retain every original input except proven source-null image/sampler pairs.");
        var snapshot=explicitTable with{Bytes=explicitTable.Bytes.ToArray()};
        var table=await DeferredMaterialTableDevice.BuildAsync(snapshot);
        try{return new(program,plan,table,borrowedNativeGgx,declarations);}
        catch{await table.DisposeAsync();throw;}
    }

    /// <summary>Re-read full source views from the current frame after resize.
    /// Caller bindings cover exactly the 31 unresolved declarations; supplying
    /// an owned known binding is rejected. Returned order is stable for cache reuse.</summary>
    public async Task<IReadOnlyList<DeferredDeviceBinding>> AssembleAsync(RenderTargetFrame frame,
        IReadOnlyList<DeferredDeviceBinding> explicitRemainingInputs)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(explicitRemainingInputs);
        var inputs=explicitRemainingInputs.Select(i=>i??throw new InvalidDataException("Null explicit deferred input.")).ToArray();
        var required=RequiredExplicitInputs.Select(d=>(d.DescriptorKind,d.Source.BindPoint)).ToHashSet();
        if(inputs.Length!=required.Count||inputs.Select(i=>(i.DescriptorKind,i.SourceBindPoint)).Distinct().Count()!=inputs.Length||
            inputs.Any(i=>!required.Contains((i.DescriptorKind,i.SourceBindPoint))||!i.Resource.IsValid||
                string.IsNullOrWhiteSpace(i.SourceResourceUri)||(i.DescriptorKind=="Texture")!=i.OwnerTexture.HasValue||
                i.OwnerTexture is Rid parent&&!parent.IsValid))
            throw new InvalidDataException("Native deferred requires every remaining source resource exactly once, with no known-resource override.");
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            var gbuffer=await DeferredGbufferInputs.ReadAsync(SourcePlan,frame);
            await ValidateGgxAsync(_ggx);
            var rows=gbuffer.Select(i=>new DeferredDeviceBinding("Texture",i.Source.PixelBinding.BindPoint,i.ShaderView,
                frame.Textures[i.Source.Texture.Object.ObjectId].StorageRid,
                i.Source.Texture.Source.Source.ResourceUri+"#"+i.Source.Texture.Object.ObjectId.ToString("X8")))
                .Concat(_staticInputs).Concat(inputs).ToDictionary(i=>(i.DescriptorKind,i.SourceBindPoint));
            return Array.AsReadOnly(_declarations.Select(d=>rows[(d.DescriptorKind,d.Source.BindPoint)]).ToArray());
        }
        finally{_gate.Release();}
    }

    /// <summary>Combine all 39 resources with the three complete caller CBs.
    /// Scene fields and temporary viewport policy remain explicit source inputs.</summary>
    public async Task<NativeDeferredPacket> AssemblePacketAsync(RenderTargetFrame frame,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitConstants,
        IReadOnlyList<DeferredDeviceBinding> explicitRemainingInputs,DeferredRasterState explicitRaster,
        DeferredTranslucencyViewportInputs? temporaryViewport=null)
    {
        ArgumentNullException.ThrowIfNull(explicitConstants);ArgumentNullException.ThrowIfNull(explicitRaster);
        var cbs=Program.DescriptorBindings.Bindings.Where(d=>d.DescriptorKind=="UniformBuffer").ToArray();
        if(explicitConstants.Count!=cbs.Length||cbs.Any(d=>
            !explicitConstants.TryGetValue(d.Source.BindPoint,out var bytes)||bytes.Length!=d.ConstantBuffer!.ByteSize))
            throw new InvalidDataException("Native deferred resource packet requires all three complete source constant buffers.");
        var constants=explicitConstants.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        var resources=await AssembleAsync(frame,explicitRemainingInputs);
        return new(constants,resources,explicitRaster,temporaryViewport);
    }

    private static bool SameShader(LoadedShaderContainer left,LoadedShaderContainer right)
        =>left.Source.ResourceUri==right.Source.ResourceUri&&left.Source.DecodedSha256==right.Source.DecodedSha256;
    private static (string Kind,uint Register,uint Variable,uint? Set,uint? Binding) DeclarationIdentity(TranslatedShaderBinding value)
        =>(value.DescriptorKind,value.Source.BindPoint,value.Device.VariableId,value.Device.DescriptorSet,value.Device.Binding);
    private static Task<bool> ValidateGgxAsync(GgxAmbientLut ggx)
        =>TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(!ggx.TextureRid.IsValid||!ggx.SamplerRid.IsValid||!rd.TextureIsValid(ggx.TextureRid))
                throw new InvalidDataException("Borrowed native GGX resource has retired.");
            using var format=rd.TextureGetFormat(ggx.TextureRid);
            if(format.TextureType!=RenderingDevice.TextureType.Type2D||format.Format!=RenderingDevice.DataFormat.R16G16Sfloat||
                format.Width!=256||format.Height!=256||format.ArrayLayers!=1||format.Mipmaps!=1||
                !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                throw new InvalidDataException("Borrowed native GGX lookup shape/format differs from its source contract.");
            return true;
        });
    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)
        {
            _retiring=true;
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)_disposal=DisposeCoreAsync();
            return new(_disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try{await _table.DisposeAsync();}
        finally{_gate.Release();}
    }
}
