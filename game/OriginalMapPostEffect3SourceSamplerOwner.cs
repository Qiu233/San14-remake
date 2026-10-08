using Godot;
using San14.Data;
using San14.Data.Formats;

public enum OriginalMapPostEffect3ManagerSamplerKind{Linear,Point}

/// <summary>A same-frame native stage/slot selection capability. The producer
/// must validate the actual ordered writer and owner lifetime. Creating the
/// manager pair alone does not supply this capability (notably for Shaft7/8).</summary>
public interface IOriginalMapPostEffect3ManagerSamplerSelection
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    int NativeStageOrdinal{get;}
    uint SourceRegister{get;}
    OriginalMapPostEffect3ManagerSamplerKind Kind{get;}
    string SourceEvidence{get;}
    void RequireCurrent();
}

/// <summary>Owns the two samplers from the original manager wrapper recipes.
/// Leases borrow this owner and an actual current selection producer. It does
/// not allocate substitute textures or assert that Shaft has installed7/8.</summary>
public sealed class OriginalMapPostEffect3SourceSamplerOwner:IAsyncDisposable
{
    public OriginalMapPostEffect3ManagerSamplerSources Source{get;}
    private readonly string context;
    private readonly Rid linear,point;
    private readonly G1TextureSamplerRecipe linearRecipe,pointRecipe;
    private readonly object lifetime=new();private bool retiring,disposed,pointRetired,linearRetired;
    private readonly SemaphoreSlim retirement=new(1,1);
    private OriginalMapPostEffect3SourceSamplerOwner(OriginalMapPostEffect3ManagerSamplerSources source,
        string contextOwner,Rid linearRid,Rid pointRid,G1TextureSamplerRecipe actualLinear,G1TextureSamplerRecipe actualPoint)
        =>(Source,context,linear,point,linearRecipe,pointRecipe)=(source,contextOwner,linearRid,pointRid,actualLinear,actualPoint);

    public static async Task<OriginalMapPostEffect3SourceSamplerOwner> BuildAsync(
        LoadedOriginalDeferredSamplerNativeSource native,string contextOwnerIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextOwnerIdentity);
        // Borrow the recipe imported by this loaded native generation.
        // These device allocations and their current leases stay owner-local.
        var source=OriginalDeferredSamplerSources.ReadPostEffect3ManagerSamplers(native);
        return await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            Rid a=default,b=default;
            try
            {
                var actualLinear=OriginalDeferredSamplerSources.DecodeKey(native,source.LinearKey);
                var actualPoint=OriginalDeferredSamplerSources.DecodeKey(native,source.PointKey);
                a=OriginalDeviceSampler.Create(rd,actualLinear);
                b=OriginalDeviceSampler.Create(rd,actualPoint);
                return new OriginalMapPostEffect3SourceSamplerOwner(source,contextOwnerIdentity,a,b,actualLinear,actualPoint);
            }
            catch{if(b.IsValid)rd.FreeRid(b);if(a.IsValid)rd.FreeRid(a);throw;}
        });
    }

    public IOriginalMapPostEffect3ComputeSamplerLease LeaseSelected(IOriginalMapPostEffect3ManagerSamplerSelection selection,
        int expectedNativeStageOrdinal)
    {
        ArgumentNullException.ThrowIfNull(selection);selection.RequireCurrent();
        lock(lifetime)
        {
            if(retiring)throw new ObjectDisposedException(nameof(OriginalMapPostEffect3SourceSamplerOwner));
            if((uint)expectedNativeStageOrdinal>=6||selection.NativeStageOrdinal!=expectedNativeStageOrdinal||
                selection.FrameNumber<=0||selection.ContextOwnerIdentity!=context||selection.SourceRegister>15||
                !Enum.IsDefined(selection.Kind)||string.IsNullOrWhiteSpace(selection.SourceEvidence))
                throw new InvalidDataException("Manager sampler selection is not this current source owner/stage slot.");
            return new Selected(this,selection,selection.FrameNumber,selection.ContextOwnerIdentity,
                selection.NativeStageOrdinal,selection.SourceRegister,selection.Kind,
                selection.Kind==OriginalMapPostEffect3ManagerSamplerKind.Linear?linear:point,
                selection.SourceEvidence);
        }
    }

    private sealed record Selected(OriginalMapPostEffect3SourceSamplerOwner Owner,
        IOriginalMapPostEffect3ManagerSamplerSelection Producer,long FrameNumber,string ContextOwnerIdentity,
        int NativeStageOrdinal,uint SourceRegister,OriginalMapPostEffect3ManagerSamplerKind Kind,Rid SamplerRid,string SelectionEvidence)
        :IOriginalMapPostEffect3SourceMipSamplerLease
    {
        public string SourceEvidence=>Owner.Source.SourceEvidence+";actual-selection:"+SelectionEvidence;
        public void RequireSourceMipSampling(uint filter,uint bias,uint minimum,uint maximum)
        {
            RequireCurrent();var recipe=Kind==OriginalMapPostEffect3ManagerSamplerKind.Linear?Owner.linearRecipe:Owner.pointRecipe;
            if(recipe.Filter!=filter||recipe.MipLodBiasBits!=bias||recipe.MinimumLodBits!=minimum||recipe.MaximumLodBits!=maximum)
                throw new InvalidDataException("Actual source manager sampler cannot authorize this shader's explicit mip footprint.");
        }
        public void RequireCurrent()
        {
            Producer.RequireCurrent();lock(Owner.lifetime)
            {
                if(Owner.retiring)throw new ObjectDisposedException(nameof(OriginalMapPostEffect3SourceSamplerOwner));
                if(Producer.FrameNumber!=FrameNumber||Producer.ContextOwnerIdentity!=ContextOwnerIdentity||
                    Producer.NativeStageOrdinal!=NativeStageOrdinal||Producer.SourceRegister!=SourceRegister||
                    Producer.Kind!=Kind||Producer.SourceEvidence!=SelectionEvidence)
                    throw new InvalidDataException("Accepted manager sampler selection changed or belongs to another frame.");
            }
        }
    }

    /// <summary>Borrowers must finish first. Retiring rejects every lease
    /// immediately; no resource is silently retained through a stale frame.</summary>
    public async ValueTask DisposeAsync()
    {
        lock(lifetime)retiring=true;
        await retirement.WaitAsync();
        try
        {
            if(disposed)return;
            if(point.IsValid&&!pointRetired||linear.IsValid&&!linearRetired)
                await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(!pointRetired){if(point.IsValid)rd.FreeRid(point);pointRetired=true;}
                if(!linearRetired){if(linear.IsValid)rd.FreeRid(linear);linearRetired=true;}
                return true;
            });
            pointRetired=true;linearRetired=true;
            disposed=true;
        }
        finally{retirement.Release();}
    }
}
