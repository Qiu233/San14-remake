using Godot;
using San14.Data;

/// <summary>Owns populated original clustered/local ambient typed buffers.
/// The complete prepared source packets retain their selected view formats and
/// allocation bytes. Retire cached Deferred draws before disposing this owner.
/// Original source-null branches have no device upload and remain external.</summary>
public sealed partial class NativeDeferredBufferOwner : IAsyncDisposable
{
    public DeferredPixelProgram Program { get; }
    public IReadOnlyList<LoadedDeferredBufferSources> Sources { get; }
    public IReadOnlyList<DeferredDeviceBinding> Bindings { get; }
    private readonly IReadOnlyList<Rid> _owned;
    private readonly object _lifetimeGate=new();
    private Task? _disposal;

    private NativeDeferredBufferOwner(DeferredPixelProgram program,
        LoadedDeferredBufferSources[] sources,DeferredDeviceBinding[] bindings,List<Rid> owned)
        =>(Program,Sources,Bindings,_owned)=(program,Array.AsReadOnly(sources),Array.AsReadOnly(bindings),owned.AsReadOnly());

    public static async Task<NativeDeferredBufferOwner> BuildAsync(DeferredPixelProgram program,
        IReadOnlyList<LoadedDeferredBufferSources> explicitPopulatedSources)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(explicitPopulatedSources);
        if(program.Translation.SourceProgramSha256!=DeferredBufferSources.PixelSourceSha256 ||
            explicitPopulatedSources.Count==0 || explicitPopulatedSources.Any(s=>s is null))
            throw new InvalidDataException("Native Deferred buffer owner requires exact PS1 and populated source groups.");
        var layout=DeferredBufferSources.ReadLayout(program.Source,program.Combination.Index);
        return await BuildCoreAsync(program,explicitPopulatedSources,layout);
    }

    private static async Task<NativeDeferredBufferOwner> BuildCoreAsync(DeferredPixelProgram program,
        IReadOnlyList<LoadedDeferredBufferSources> explicitPopulatedSources,LoadedDeferredBufferLayout layout)
    {
        var sources=new List<LoadedDeferredBufferSources>();
        foreach(var input in explicitPopulatedSources)
        {
            if(input.Layout.Shader.Source.ResourceUri!=program.Source.Source.ResourceUri ||
                input.Layout.Shader.Source.DecodedSha256!=program.Source.Source.DecodedSha256 ||
                input.Layout.CombinationIndex!=program.Combination.Index)
                throw new InvalidDataException("Native Deferred buffer packet differs from the selected source shader.");
            var prepared=input.Buffers.Select(b=>new DeferredPreparedBuffer(b.Declaration.SourceBindPoint,
                b.NativeViewFormat,b.Bytes.ToArray(),b.SourceResourceUri)).ToArray();
            var registers=prepared.Select(b=>b.SourceBindPoint).Order().ToArray();
            if(registers.SequenceEqual(new uint[]{16,17,18,19,20,21}))
                sources.Add(DeferredBufferSources.ReadPreparedCluster(layout,prepared,
                    input.CellCount,input.PointCount,input.SpotCount));
            else if(registers.SequenceEqual(new uint[]{23,24}))
                sources.Add(DeferredBufferSources.ReadPreparedLocalAmbient(layout,
                    prepared.Single(b=>b.SourceBindPoint==23),prepared.Single(b=>b.SourceBindPoint==24),
                    input.CellCount,input.LocalAmbientCount));
            else throw new InvalidDataException("Native Deferred buffer owner requires a complete original source group.");
        }
        var buffers=sources.SelectMany(s=>s.Buffers).OrderBy(b=>b.Declaration.SourceBindPoint).ToArray();
        if(buffers.Select(b=>b.Declaration.SourceBindPoint).Distinct().Count()!=buffers.Length)
            throw new InvalidDataException("Native Deferred source groups duplicate a typed-buffer register.");
        foreach(var buffer in buffers)
        {
            var declaration=program.DescriptorBindings.Bindings.SingleOrDefault(d=>
                d.DescriptorKind=="TextureBuffer" && d.Source.BindPoint==buffer.Declaration.SourceBindPoint);
            if(declaration is null || declaration.Source.Name.Text!=buffer.Declaration.Name ||
                declaration.Source.Dimension!=1 || declaration.Source.BindCount!=1 ||
                declaration.Source.ReturnType!=buffer.Declaration.PixelBinding.ReturnType)
                throw new InvalidDataException("Native Deferred source buffer differs from the translated consumer.");
        }
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();var bindings=new List<DeferredDeviceBinding>();
            try
            {
                foreach(var source in buffers)
                {
                    Rid buffer=rd.TextureBufferCreate(checked((uint)source.TexelCount),DeviceFormat(source.NativeViewFormat),
                        source.Bytes.ToArray());
                    if(!buffer.IsValid)throw new InvalidDataException("Device rejected the exact original typed-buffer upload.");
                    owned.Add(buffer);
                    bindings.Add(new("TextureBuffer",source.Declaration.SourceBindPoint,buffer,null,source.SourceResourceUri));
                }
                return new NativeDeferredBufferOwner(program,sources.ToArray(),bindings.ToArray(),owned);
            }
            catch {for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    /// <summary>Combine source buffers with other explicit provider resources.
    /// An override of an owned source register is rejected.</summary>
    public IReadOnlyList<DeferredDeviceBinding> Combine(
        IReadOnlyList<DeferredDeviceBinding> otherExplicitInputs)
    {
        ArgumentNullException.ThrowIfNull(otherExplicitInputs);
        lock(_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            var rows=Bindings.Concat(otherExplicitInputs.Select(i=>i??throw new InvalidDataException("Null Deferred resource input."))).ToArray();
            if(rows.Select(i=>(i.DescriptorKind,i.SourceBindPoint)).Distinct().Count()!=rows.Length)
                throw new InvalidDataException("Explicit Deferred resources override a source-owned buffer or repeat a register.");
            return Array.AsReadOnly(rows);
        }
    }

    private static RenderingDevice.DataFormat DeviceFormat(uint native) => native switch
    {
        2=>RenderingDevice.DataFormat.R32G32B32A32Sfloat,6=>RenderingDevice.DataFormat.R32G32B32Sfloat,
        10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,16=>RenderingDevice.DataFormat.R32G32Sfloat,
        34=>RenderingDevice.DataFormat.R16G16Sfloat,41=>RenderingDevice.DataFormat.R32Sfloat,
        42=>RenderingDevice.DataFormat.R32Uint,54=>RenderingDevice.DataFormat.R16Sfloat,
        57=>RenderingDevice.DataFormat.R16Uint,
        _=>throw new InvalidDataException("Unsupported explicit native Deferred buffer view format.")
    };

    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)return new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;}));
    }
}
