using Godot;
using San14.Data;

/// <summary>GPU samplers for one selected original program's actual key
/// recipes. GGX stays with its LUT owner. A new accepted frame supplies a new
/// validated plan; descriptor reuse never imports that frame's current state.</summary>
public sealed class MapSelectedSourceSamplerOwner : IAsyncDisposable
{
    public MapSelectedSourceSamplerPlan Source { get; }
    public IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> SourceDescriptors => Source.SourceDescriptors;
    private readonly IReadOnlyDictionary<uint, Rid> samplers;
    private readonly RenderDeviceResources resources;
    private readonly object gate = new();
    private bool retiring;

    private MapSelectedSourceSamplerOwner(MapSelectedSourceSamplerPlan source,
        Dictionary<uint, Rid> handles, RenderDeviceResources resources)
        => (Source, samplers, this.resources) = (source,
            new System.Collections.ObjectModel.ReadOnlyDictionary<uint, Rid>(handles), resources);

    public static async Task<MapSelectedSourceSamplerOwner> BuildAsync(MapSelectedSourceSamplerPlan source)
    {
        ArgumentNullException.ThrowIfNull(source); source.Validate();
        var descriptors = source.SourceDescriptors.ToDictionary(p => p.Key,p => p.Value.ToArray());
        var recipes = source.Bindings.Select(b => b with {Recipe=b.Recipe with {RawKey=b.Recipe.RawKey.ToArray()}}).ToArray();
        uint? ggx = source.ImagePlan.Samplers.SingleOrDefault(s => s.KeySource.Role == "GGX")?.Declaration.BindPoint;
        var resources=new RenderDeviceResources("RenderingDevice rejected a selected source sampler.");
        try
        {
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                var handles = new Dictionary<uint,Rid>();
                var cache = new Dictionary<string,Rid>(StringComparer.Ordinal);
                foreach (var binding in recipes.Where(b => b.Register != ggx))
                {
                    string descriptorHash = Convert.ToHexString(descriptors[binding.Register]);
                    if (!cache.TryGetValue(descriptorHash,out Rid sampler))
                    {
                        sampler = resources.Own(DeviceSampler.Create(rd,descriptors[binding.Register]));
                        cache.Add(descriptorHash,sampler);
                    }
                    handles.Add(binding.Register,sampler);
                }
                return new MapSelectedSourceSamplerOwner(source,handles,resources);
            });
        }
        catch(Exception failure)
        {
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("Selected-samplers-partial",
                resources.Count>0?[resources]:[],(failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }

    public IReadOnlyList<DeferredDeviceBinding> GetBindings(MapSelectedSourceSamplerPlan currentValidatedPlan)
    {
        ArgumentNullException.ThrowIfNull(currentValidatedPlan); currentValidatedPlan.Validate();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            MapSelectedSourceImageOwner.ValidateCompatible(Source.ImagePlan,currentValidatedPlan.ImagePlan);
            if (Source.Native.Locale != currentValidatedPlan.Native.Locale ||
                Source.Bindings.Count != currentValidatedPlan.Bindings.Count ||
                !Source.Bindings.Zip(currentValidatedPlan.Bindings).All(p => p.First.Register == p.Second.Register &&
                    p.First.SamplerName == p.Second.SamplerName && p.First.TextureRegister == p.Second.TextureRegister &&
                    p.First.TextureName == p.Second.TextureName && p.First.Recipe.RawKey.AsSpan().SequenceEqual(p.Second.Recipe.RawKey)) ||
                Source.SourceDescriptors.Count != currentValidatedPlan.SourceDescriptors.Count ||
                Source.SourceDescriptors.Any(p => !currentValidatedPlan.SourceDescriptors.TryGetValue(p.Key,out var value) || !p.Value.Span.SequenceEqual(value.Span)))
                throw new InvalidDataException("Current selected sampler key, descriptor or null scope changed; drain dependent draws and rebuild its owner.");
            return Array.AsReadOnly(currentValidatedPlan.Bindings.Where(b => samplers.ContainsKey(b.Register)).Select(b =>
                new DeferredDeviceBinding("Sampler",b.Register,samplers[b.Register],null,
                    b.SourceResourceUri + "/sampler-key/" + Convert.ToHexString(b.Recipe.RawKey) +
                    ";source-program=" + currentValidatedPlan.Selection.PixelProgramSha256)).ToArray());
        }
    }

    public IReadOnlyList<DeferredDeviceBinding> GetBindings() => GetBindings(Source);
    public ValueTask DisposeAsync()
    {lock(gate){retiring=true;return resources.DisposeAsync();}}
}
