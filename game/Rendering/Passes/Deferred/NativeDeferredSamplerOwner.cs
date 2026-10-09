using Godot;
using San14.Data;

/// <summary>Owns the active remaining PS1 sampler bindings resolved from actual
/// source keys. Proven source-null declarations are removed by specialization.
/// GGX s5 stays with its native LUT owner. Retire any cached
/// deferred draw using these bindings before disposing this owner.</summary>
public sealed class NativeDeferredSamplerOwner : IAsyncDisposable
{
    public LoadedDeferredSamplerSources Source { get; }
    public IReadOnlyList<DeferredDeviceBinding> Bindings { get; }
    public IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> SourceDescriptors { get; }
    private readonly IReadOnlyList<Rid> _owned;
    private readonly object _gate=new();
    private bool _retiring;
    private Task? _disposal;

    private NativeDeferredSamplerOwner(LoadedDeferredSamplerSources source,
        IReadOnlyList<DeferredDeviceBinding> bindings,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> descriptors,List<Rid> owned)
        =>(Source,Bindings,SourceDescriptors,_owned)=(source,bindings,descriptors,owned.AsReadOnly());

    public static async Task<NativeDeferredSamplerOwner> BuildAsync(LoadedDeferredSamplerSources source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.SourceNulls.Any(n=>n is null)||source.SourceNulls.Select(n=>n.PixelRegister).Distinct().Count()!=source.SourceNulls.Count)
            throw new InvalidDataException("Native deferred sampler source null proofs are duplicated.");
        foreach(var proof in source.SourceNulls)proof.Validate();
        var removed=source.SourceNulls.Select(n=>n.PixelRegister).ToHashSet();
        if(source.FreshSourceIesNull is not null){source.FreshSourceIesNull.Validate();removed.Add(0);}
        uint[] registers=new uint[]{0,1,2,3,4,5,6,7,8,9,10,11,15}.Where(r=>!removed.Contains(r)).ToArray();
        if(source.Shader.Source.FileId!=0x9D8E811F||
            source.Shader.Source.ResourceUri!="san14://rdb/system/9D8E811F"||
            source.TexturePlan.ResourceBindings.Combination.Index!=0||source.Bindings.Count!=registers.Length||
            !source.Bindings.Select(b=>b.Register).Order().SequenceEqual(registers))
            throw new InvalidDataException("Native deferred sampler owner requires the complete original PS1 source plan.");
        var snapshot=source with{Bindings=Array.AsReadOnly(source.Bindings.Select(b=>b with{
            Recipe=b.Recipe with{RawKey=b.Recipe.RawKey.ToArray()}}).ToArray()),SourceNulls=Array.AsReadOnly(source.SourceNulls.ToArray())};
        var descriptors=new Dictionary<uint,ReadOnlyMemory<byte>>();
        foreach(var proof in snapshot.SourceNulls)
            descriptors.Add(proof.PixelRegister,RenderSamplerSourceAdapters.BackendDefaultDescriptor(snapshot.Native));
        if(snapshot.FreshSourceIesNull is not null)
        {
            snapshot.FreshSourceIesNull.Validate();
            byte[] nativeDefault=RenderSamplerSourceAdapters.BackendDefaultDescriptor(snapshot.Native);
            if(nativeDefault.Length!=52)
                throw new InvalidDataException("Removed IES sampler requires its complete native backend-default descriptor metadata.");
            descriptors.Add(0,nativeDefault);
        }
        foreach(var binding in snapshot.Bindings)
        {
            var decoded=RenderSamplerSourceAdapters.DecodeKey(snapshot.Native,binding.Recipe.RawKey);
            byte[] descriptor=decoded.EncodeD3D11Descriptor();
            if(!descriptor.AsSpan().SequenceEqual(binding.Recipe.EncodeD3D11Descriptor())||
                (((decoded.Filter&0x80)!=0)!=(binding.Register is 3 or 4))||
                binding.SamplerName!=(binding.Register==15?"__smpsShadowPointSampler":"__smp"+binding.TextureName)||
                string.IsNullOrWhiteSpace(binding.SourceResourceUri))
                throw new InvalidDataException("Native deferred sampler recipe differs from its final source key/consumer.");
            descriptors.Add(binding.Register,descriptor);
        }
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();var bindings=new List<DeferredDeviceBinding>(12);
            var cache=new Dictionary<string,Rid>(StringComparer.Ordinal);
            try
            {
                foreach(var sourceBinding in snapshot.Bindings.Where(b=>b.Register!=5))
                {
                    string key=Convert.ToHexString(sourceBinding.Recipe.RawKey);
                    if(!cache.TryGetValue(key,out Rid sampler))
                    {
                        sampler=DeviceSampler.Create(rd,descriptors[sourceBinding.Register].Span);
                        cache.Add(key,sampler);owned.Add(sampler);
                    }
                    bindings.Add(new("Sampler",sourceBinding.Register,sampler,null,
                        sourceBinding.SourceResourceUri+"/sampler-key/"+key));
                }
                return new NativeDeferredSamplerOwner(snapshot,bindings.AsReadOnly(),
                    new System.Collections.ObjectModel.ReadOnlyDictionary<uint,ReadOnlyMemory<byte>>(descriptors),owned);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    public IReadOnlyList<DeferredDeviceBinding> GetBindings()
    {
        lock(_gate){ObjectDisposedException.ThrowIf(_retiring,this);return Bindings;}
    }
    public ValueTask DisposeAsync()
    {
        lock(_gate){_retiring=true;return new(_disposal??=DisposeCoreAsync());}
    }
    private Task DisposeCoreAsync()=>TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {
        for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;
    });
}
