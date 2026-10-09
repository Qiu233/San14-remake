using San14.Core;
using San14.Data;
using System.Numerics;
using System.Security.Cryptography;

/// <summary>Current Core ownership and source-native display controls for one accepted frame.
/// Source color indices must cover current forces; source missing state is an error.</summary>
public sealed record TerritorySceneInputs(GameState World,
    IReadOnlyDictionary<int,byte> SourceForceColorIndices,TerritoryStyleSelection Style,
    long StyleInvalidationGeneration,ModelRasterInputs Raster);

/// <summary>Scene owner for the original ScreenMap pass. Source-created palettes,
/// source startup seven-layer allocation and active native style writer feed PB2SM.
/// Exposure is borrowed from its live producer for the same accepted scene frame.
/// Stages run after prior terrain/models; target resources retire before their parent frames.</summary>
public sealed class TerritorySceneProvider:IAsyncDisposable
{
    public TerritoryRenderer Renderer { get; }
    public TerritoryStyleSources Styles { get; }
    public LoadedDeferredSamplerNativeSource SamplerSource { get; }
    public long OwnershipBuildCount { get; private set; }
    public long StyleWriteCount { get; private set; }
    private readonly CeForceMapPalette _palette;
    private readonly string _mapSha;
    private readonly bool[] _eligible;
    private readonly Func<NativeMapFrameContext,TerritorySceneInputs> _current;
    private readonly Func<NativeMapFrameContext,Task<ModelDeviceBinding>> _exposure;
    private readonly IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> _samplers;
    private readonly byte[] _sourceLayers=new byte[7*48400];
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private TerritoryStylePacket _style=TerritoryStyleSources.InitializedScreenMap();
    private TerritoryOwnershipPacket? _ownership;
    private string? _ownershipKey;
    private int? _zoomCache;
    private long _styleGeneration=long.MinValue;
    private bool _retiring;
    private int _accepted;
    private Task? _disposal;
    private TaskCompletionSource? _drained;
    private TerritorySceneProvider(TerritoryRenderer renderer,TerritoryStyleSources styles,
        CeForceMapPalette palette,string mapSha,bool[] eligible,
        Func<NativeMapFrameContext,TerritorySceneInputs> current,
        Func<NativeMapFrameContext,Task<ModelDeviceBinding>> exposure,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> samplers,LoadedDeferredSamplerNativeSource samplerSource)
        =>(Renderer,Styles,_palette,_mapSha,_eligible,_current,_exposure,_samplers,SamplerSource)=
            (renderer,styles,palette,mapSha,eligible,current,exposure,samplers,samplerSource);

    public static async Task<TerritorySceneProvider> BuildAsync(GameResourceRepository repository,
        LoadedTerritoryResources source,LoadedMap map,LoadedTerrainTypes terrain,string locale,
        Vkd3dShaderCompiler compiler,Func<NativeMapFrameContext,TerritorySceneInputs> current,
        Func<NativeMapFrameContext,Task<ModelDeviceBinding>> sourceExposure)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrain);ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(sourceExposure);
        var samplerSource=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        var samplers=TerritoryStyleSources.ReadSamplers(source,samplerSource);
        var styles=TerritoryStyleSources.LoadCurrent(repository,source,locale);
        var palette=CeForceMapPalette.LoadCurrent(locale);
        var eligible=TerritoryOwnership.ReadSourceTintEligibility(map,terrain);
        var renderer=await TerritoryRenderer.BuildAsync(source,compiler);
        return new(renderer,styles,palette,map.Source.Sha256,eligible,current,sourceExposure,samplers,samplerSource);
    }

    /// <summary>Compose this pass after an existing terrain/model stage. Retirement runs
    /// in reverse order. Keep this provider alive until the scene finishes retirement.</summary>
    public NativeMapSceneStages Stages(NativeMapSceneStages? previous=null)
    {
        lock(_lifetime)if(_retiring)throw new ObjectDisposedException(nameof(TerritorySceneProvider));
        return (previous ?? new NativeMapSceneStages()) with
        {
            AfterTerrain = async frame=>
            {
                if(previous?.AfterTerrain is not null)await previous.AfterTerrain(frame);
                await RenderAsync(frame);
            },
            BeforeTargetRetirement = async targets=>
            {
                await RetireTargetAsync();
                if(previous?.BeforeTargetRetirement is not null)await previous.BeforeTargetRetirement(targets);
            }
        };
    }

    public Task RenderAsync(NativeMapFrameContext frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock(_lifetime){if(_retiring)throw new ObjectDisposedException(nameof(TerritorySceneProvider));_accepted++;}
        return RenderAcceptedAsync(frame);
    }
    private async Task RenderAcceptedAsync(NativeMapFrameContext frame)
    {
        await _gate.WaitAsync();
        try
        {
            var current=_current(frame)??throw new InvalidDataException("Current territory source state is missing.");
            if(frame.PreparedCamera is null)throw new InvalidDataException("Territory needs the accepted terrain/model source Camera.");
            var forceColors=current.SourceForceColorIndices.ToDictionary(p=>p.Key,p=>p.Value);
            string ownershipKey=OwnershipKey(current.World,forceColors);
            bool changed=_ownership is null||ownershipKey!=_ownershipKey;
            if(changed)
            {
                // Native reset/allocator builds base colors from source force indices.
                var initial=TerritoryStyleSources.InitializedScreenMap();
                _ownership=TerritoryOwnership.Apply(current.World,_mapSha,_sourceLayers,_eligible,
                    forceColors,_palette,initial.CellColors,initial.LineColors);
                _style=_style with{CellColors=_ownership.CellColors.ToArray(),LineColors=_ownership.LineColors.ToArray()};
                _ownershipKey=ownershipKey;OwnershipBuildCount++;
            }
            int zoom=TerritoryStyleSources.CacheZoomValue(current.Style);
            if(changed||_zoomCache!=zoom||_styleGeneration!=current.StyleInvalidationGeneration)
            {
                _style=Styles.Write(current.Style,_ownership!,_style);
                _zoomCache=zoom;_styleGeneration=current.StyleInvalidationGeneration;StyleWriteCount++;
            }
            byte[] constants=TerritoryConstants.Pack(Renderer.Source.Shader.ReadResourceDefinitions(1),
                new byte[TerritoryConstants.ByteCount],TerritoryStyleSources.Inputs(frame.PreparedCamera,
                    _style with{LayerViewMask=TerritoryStyleSources.LayerMask(current.Style)}));
            var exposure=await _exposure(frame)??throw new InvalidDataException("Accepted-frame source Exposure producer is missing.");
            if(exposure.DescriptorKind!="Texture"||exposure.SourceBindPoint!=0||exposure.OwnerTexture is null||
                !exposure.Resource.IsValid||!exposure.OwnerTexture.Value.IsValid||string.IsNullOrWhiteSpace(exposure.SourceResourceUri))
                throw new InvalidDataException("Territory t0 requires the source Exposure texture and its live producer identity.");
            await Renderer.RenderAsync(frame.TerrainFrame,new(constants,_ownership!.ZoneLayers,exposure,_samplers,current.Raster));
        }
        finally
        {
            _gate.Release();lock(_lifetime){_accepted--;if(_retiring&&_accepted==0)_drained?.TrySetResult();}
        }
    }

    private string OwnershipKey(GameState world,IReadOnlyDictionary<int,byte> colors)
    {
        if(world.MapWidth!=220||world.MapHeight!=220||world.MapSha256!=_mapSha||world.Territory.Count!=48400)
            throw new InvalidDataException("Current territory does not match this provider's source map.");
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        for(int id=0;id<48400;id++)
        {
            if(!world.Territory.TryGetValue(id,out var cell)||cell.Id!=id)throw new InvalidDataException("Current territory has a missing source slot.");
            writer.Write(cell.ForceId??int.MinValue);
            if(cell.ForceId is int force&&_eligible[id]&&(!world.Forces.ContainsKey(force)||!colors.ContainsKey(force)))
                throw new InvalidDataException($"Current force {force} has no source territory color; no fallback is available.");
        }
        foreach(var color in colors.OrderBy(p=>p.Key)){writer.Write(color.Key);writer.Write(color.Value);}
        writer.Flush();return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0,(int)stream.Length)));
    }
    public Task RetireTargetAsync()
    {
        lock(_lifetime){if(_retiring)throw new ObjectDisposedException(nameof(TerritorySceneProvider));_accepted++;}
        return RetireAcceptedAsync();
    }
    private async Task RetireAcceptedAsync()
    {
        await _gate.WaitAsync();try{await Renderer.RetireTargetAsync();}
        finally{_gate.Release();lock(_lifetime){_accepted--;if(_retiring&&_accepted==0)_drained?.TrySetResult();}}
    }
    public ValueTask DisposeAsync()
    {
        lock(_lifetime)
        {
            if(_disposal is not null)return new(_disposal);
            _retiring=true;Task wait=Task.CompletedTask;
            if(_accepted>0){_drained=new(TaskCreationOptions.RunContinuationsAsynchronously);wait=_drained.Task;}
            return new(_disposal=DisposeCoreAsync(wait));
        }
    }
    private async Task DisposeCoreAsync(Task drained)
    {
        await drained;await _gate.WaitAsync();try{await Renderer.DisposeAsync();}finally{_gate.Release();}
    }
}
