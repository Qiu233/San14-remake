using Godot;
using San14.Data;
using San14.Data.Formats;

public enum MapPostEffect3ManagerSamplerKind{Linear,Point}

/// <summary>A same-frame native stage/slot selection capability. The producer
/// must validate the actual ordered writer and owner lifetime. Creating the
/// manager pair alone does not supply this capability (notably for Shaft7/8).</summary>
public interface IMapPostEffect3ManagerSamplerSelection
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    int NativeStageOrdinal{get;}
    uint SourceRegister{get;}
    MapPostEffect3ManagerSamplerKind Kind{get;}
    string SourceEvidence{get;}
    void RequireCurrent();
}

/// <summary>Owns the two samplers from the original manager wrapper recipes.
/// Leases borrow this owner and an actual current selection producer. It does
/// not allocate substitute textures or assert that Shaft has installed7/8.</summary>
public sealed class MapPostEffect3SourceSamplerOwner:IAsyncDisposable
{
    public MapPostEffect3ManagerSamplerSources Source{get;}
    private readonly string context;
    private readonly Rid linear,point;
    private readonly G1TextureSamplerRecipe linearRecipe,pointRecipe;
    private readonly RenderDeviceResources resources;
    private readonly object lifetime=new();private bool retiring;
    private MapPostEffect3SourceSamplerOwner(MapPostEffect3ManagerSamplerSources source,
        string contextOwner,Rid linearRid,Rid pointRid,G1TextureSamplerRecipe actualLinear,G1TextureSamplerRecipe actualPoint,
        RenderDeviceResources resources)
        =>(Source,context,linear,point,linearRecipe,pointRecipe,this.resources)=(source,contextOwner,linearRid,pointRid,actualLinear,actualPoint,resources);

    public static async Task<MapPostEffect3SourceSamplerOwner> BuildAsync(
        LoadedDeferredSamplerNativeSource native,string contextOwnerIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextOwnerIdentity);
        // Borrow the recipe imported by this loaded native generation.
        // These device allocations and their current leases stay owner-local.
        var source=RenderSamplerSourceAdapters.ReadPostEffect3ManagerSamplers(native);
        var resources=new RenderDeviceResources("PostEffect3 manager sampler allocation failed.");
        try
        {
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                var actualLinear=RenderSamplerSourceAdapters.DecodeKey(native,source.LinearKey);
                var actualPoint=RenderSamplerSourceAdapters.DecodeKey(native,source.PointKey);
                Rid a=resources.Own(DeviceSampler.Create(rd,actualLinear));
                Rid b=resources.Own(DeviceSampler.Create(rd,actualPoint));
                return new MapPostEffect3SourceSamplerOwner(source,contextOwnerIdentity,a,b,actualLinear,actualPoint,resources);
            });
        }
        catch(Exception failure)
        {
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("PostEffect3-manager-samplers-partial",
                resources.Count>0?[resources]:[],(failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }

    public IMapPostEffect3ComputeSamplerLease LeaseSelected(IMapPostEffect3ManagerSamplerSelection selection,
        int expectedNativeStageOrdinal)
    {
        ArgumentNullException.ThrowIfNull(selection);selection.RequireCurrent();
        lock(lifetime)
        {
            if(retiring)throw new ObjectDisposedException(nameof(MapPostEffect3SourceSamplerOwner));
            if((uint)expectedNativeStageOrdinal>=6||selection.NativeStageOrdinal!=expectedNativeStageOrdinal||
                selection.FrameNumber<=0||selection.ContextOwnerIdentity!=context||selection.SourceRegister>15||
                !Enum.IsDefined(selection.Kind)||string.IsNullOrWhiteSpace(selection.SourceEvidence))
                throw new InvalidDataException("Manager sampler selection is not this current source owner/stage slot.");
            return new Selected(this,selection,selection.FrameNumber,selection.ContextOwnerIdentity,
                selection.NativeStageOrdinal,selection.SourceRegister,selection.Kind,
                selection.Kind==MapPostEffect3ManagerSamplerKind.Linear?linear:point,
                selection.SourceEvidence);
        }
    }

    private sealed record Selected(MapPostEffect3SourceSamplerOwner Owner,
        IMapPostEffect3ManagerSamplerSelection Producer,long FrameNumber,string ContextOwnerIdentity,
        int NativeStageOrdinal,uint SourceRegister,MapPostEffect3ManagerSamplerKind Kind,Rid SamplerRid,string SelectionEvidence)
        :IMapPostEffect3SourceMipSamplerLease
    {
        public string SourceEvidence=>Owner.Source.SourceEvidence+";actual-selection:"+SelectionEvidence;
        public void RequireSourceMipSampling(uint filter,uint bias,uint minimum,uint maximum)
        {
            RequireCurrent();var recipe=Kind==MapPostEffect3ManagerSamplerKind.Linear?Owner.linearRecipe:Owner.pointRecipe;
            if(recipe.Filter!=filter||recipe.MipLodBiasBits!=bias||recipe.MinimumLodBits!=minimum||recipe.MaximumLodBits!=maximum)
                throw new InvalidDataException("Actual source manager sampler cannot authorize this shader's explicit mip footprint.");
        }
        public void RequireCurrent()
        {
            Producer.RequireCurrent();lock(Owner.lifetime)
            {
                if(Owner.retiring)throw new ObjectDisposedException(nameof(MapPostEffect3SourceSamplerOwner));
                if(Producer.FrameNumber!=FrameNumber||Producer.ContextOwnerIdentity!=ContextOwnerIdentity||
                    Producer.NativeStageOrdinal!=NativeStageOrdinal||Producer.SourceRegister!=SourceRegister||
                    Producer.Kind!=Kind||Producer.SourceEvidence!=SelectionEvidence)
                    throw new InvalidDataException("Accepted manager sampler selection changed or belongs to another frame.");
            }
        }
    }

    /// <summary>Borrowers must finish first. Retiring rejects every lease
    /// immediately; no resource is silently retained through a stale frame.</summary>
    public ValueTask DisposeAsync()
    {lock(lifetime){retiring=true;return resources.DisposeAsync();}}
}
