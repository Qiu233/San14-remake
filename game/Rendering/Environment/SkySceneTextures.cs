using San14.Data;

public sealed class SkySceneTextureSelection
{
    public SkySceneTextureSelection(string key, SkyTextureSource texture)
    {Key=key;Texture=texture??throw new ArgumentNullException(nameof(texture));}
    public string Key { get; }
    public SkyTextureSource Texture { get; }

    public static SkySceneTextureSelection FromSampling(LoadedSkySamplingResource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.SourceName,source.Texture);
    }

    public static SkySceneTextureSelection FromStatic(string explicitKey,
        StaticTextureBinding binding,uint explicitNativeViewFormat,
        ReadOnlyMemory<byte>? explicitRetainedSampler=null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new(explicitKey,SkyTextureSource.Import(binding.Resource,binding.ImageIndex,
            explicitNativeViewFormat,explicitRetainedSampler));
    }
}

public sealed record SkySceneTextureMetadata(string SourceResourceUri,
    string SourceSha256,int ImageIndex,uint NativeViewFormat,uint SourceDimension,
    int Width,int Height,int Depth,int MipCount,string SamplerDescriptorSha256);

/// <summary>Lazy device lifetime for already imported texture owners. Image/view
/// aliases share one allocation; samplers remain explicit at the binding site.
/// Retire borrowing draws before disposing this scene owner.</summary>
public sealed class SkySceneTextures : IAsyncDisposable
{
    public IReadOnlyDictionary<string,SkySceneTextureSelection> Selections { get; }
    private readonly Dictionary<SkyTextureIdentity,SkyTextureDevice> _loaded=new();
    private readonly List<NativeMapBorrowerRetirementPackage> pendingTextureRetirements=new();
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetimeGate=new();
    private volatile bool _retiring;
    private Task? _disposal;

    public SkySceneTextures(IReadOnlyList<SkySceneTextureSelection> explicitSelections)
    {
        ArgumentNullException.ThrowIfNull(explicitSelections);
        if(explicitSelections.Any(s=>s is null||string.IsNullOrWhiteSpace(s.Key))||
            explicitSelections.Select(s=>s.Key).Distinct(StringComparer.Ordinal).Count()!=explicitSelections.Count)
            throw new InvalidDataException("Sky texture selections require unique explicit keys.");
        Selections=new System.Collections.ObjectModel.ReadOnlyDictionary<string,SkySceneTextureSelection>(
            explicitSelections.ToDictionary(s=>s.Key,StringComparer.Ordinal));
    }

    public SkySceneTextureMetadata GetMetadata(string explicitKey)
    {
        var source=GetSelection(explicitKey).Texture;
        return new(source.ResourceUri,source.SourceSha256,source.ImageIndex,source.NativeViewFormat,
            source.SourceDimension,source.Width,source.Height,source.Depth,source.MipCount,
            source.SamplerDescriptorSha256);
    }
    public byte[] GetSamplerDescriptor(string explicitKey) => GetSelection(explicitKey).Texture.GetSamplerDescriptor();

    private SkySceneTextureSelection GetSelection(string explicitKey)
    {
        ObjectDisposedException.ThrowIf(_retiring,this);
        if(!Selections.TryGetValue(explicitKey,out var selection))
            throw new InvalidDataException($"No explicit sky texture selection for '{explicitKey}'.");
        return selection;
    }

    public async Task<SkyTextureDevice> GetTextureAsync(string explicitKey)
    {
        var source=GetSelection(explicitKey).Texture;
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            await RetirePendingTexturesAsync();
            if(!_loaded.TryGetValue(source.Identity,out var texture))
            {
                try{texture=await SkyTextureDevice.BuildAsync(source);}
                catch(NativeMapBorrowerConstructionException failure)
                {
                    // Keep the failed allocation in this owner's retirement
                    // chain, even if an outer caller handles the exception.
                    pendingTextureRetirements.Add(failure.RetainedPackage);
                    throw;
                }
                _loaded.Add(source.Identity,texture);
            }
            return texture;
        }
        finally{_gate.Release();}
    }

    public Task<Sky2DeviceInput> GetRetainedInputAsync(uint explicitBindPoint,string explicitKey)
        => GetInputAsync(explicitBindPoint,explicitKey,GetSelection(explicitKey).Texture.GetSourceSamplerDescriptor());
    public Task<Sky2DeviceInput> GetInputAsync(uint explicitBindPoint,string explicitKey)
        => GetInputAsync(explicitBindPoint,explicitKey,GetSamplerDescriptor(explicitKey));

    public async Task<Sky2DeviceInput> GetInputAsync(uint explicitBindPoint,string explicitKey,
        ReadOnlyMemory<byte> explicitSamplerDescriptor)
    {
        if(explicitSamplerDescriptor.Length!=52)
            throw new InvalidDataException("Sky input needs a complete retained source sampler.");
        byte[] sampler=explicitSamplerDescriptor.ToArray();
        var texture=await GetTextureAsync(explicitKey);var source=texture.Source;
        return new(explicitBindPoint,source.SourceDimension,texture.TextureRid,texture.TextureRid,
            sampler,source.ResourceUri+"#image="+source.ImageIndex);
    }

    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)
        {
            _retiring=true;
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)
                _disposal=DisposeCoreAsync();
            return new(_disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await RetirePendingTexturesAsync();
            foreach(var texture in _loaded.Values.Reverse())await texture.DisposeAsync();
            _loaded.Clear();
        }
        finally{_gate.Release();}
    }

    private async Task RetirePendingTexturesAsync()
    {
        while(pendingTextureRetirements.Count>0)
        {
            await pendingTextureRetirements[0].DisposeAsync();
            pendingTextureRetirements.RemoveAt(0);
        }
    }
}
