using Godot;
using San14.Data;

/// <summary>Controller-owned448B storage and the two native temporary
/// registry loans. This is separate from Bank's external B4 declaration.
/// No selected shader-read capability is granted by this metadata owner.</summary>
internal sealed class MapPostEffect3Functor1GhostRegistryOwner:IAsyncDisposable
{
    private MapPostEffect3OwnedControlledCameraFrame? assigned;
    private object? borrowed8,retained16,borrowed24,retained32;
    private int references=1; //the controller828 reference
    private bool dirty,disposed,retiring;
    private readonly SemaphoreSlim retirement=new(1,1);
    internal object ControllerIdentity{get;}=new();
    internal Rid StorageRid{get;}
    private MapPostEffect3Functor1GhostRegistryOwner(Rid rid)=>StorageRid=rid;
    internal static async Task<MapPostEffect3Functor1GhostRegistryOwner> CreateAsync(
        MapPostEffect3OwnedControlledCameraFrame camera,LoadedMapPostEffect3Functor1FrontendLifetimeSource source)
    {
        source.ValidateFor(camera.Allocation);camera.Optics.ValidateCurrent(camera.State,camera.Allocation);
        byte[] initial=camera.Optics.InitialGhostRecords640.ToArray();
        if(initial.Length!=448||source.ControllerGhostBufferByteCapacity!=448||source.ControllerGhostBufferStructureStride!=28)
            throw new InvalidDataException("Actual controller828 creation lacks its complete source448B initialization.");
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            Rid storage=rd.StorageBufferCreate(448);
            if(!storage.IsValid)throw new InvalidDataException("Controller828 device allocation failed.");
            try
            {
                rd.BufferUpdate(storage,0,448,initial);
                return new MapPostEffect3Functor1GhostRegistryOwner(storage);
            }
            catch{rd.FreeRid(storage);throw;}
        });
    }
    internal async Task AssignCurrentAsync(MapPostEffect3OwnedControlledCameraFrame camera)
    {
        ObjectDisposedException.ThrowIf(disposed||retiring,this);camera.Optics.ValidateCurrent(camera.State,camera.Allocation);
        if(assigned is not null||borrowed8 is not null||retained16 is not null||borrowed24 is not null||retained32 is not null||references!=1)
            throw new InvalidDataException("Controller ghost registry requires its preceding actual cleanup.");
        byte[] current=camera.Optics.SelectedGhostRecords640.ToArray();
        if(current.Length!=camera.Optics.GhostSpriteCount*28||current.Length>448)
            throw new InvalidDataException("Controller ghost producer exceeds its real native allocation.");
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {if(current.Length!=0)rd.BufferUpdate(StorageRid,0,(uint)current.Length,current);return true;});
        // Replacement increments the incoming object before releasing former refs.
        references++;borrowed8=ControllerIdentity;retained16=ControllerIdentity;
        assigned=camera;dirty=true;
    }
    internal void RequireAssigned(MapPostEffect3OwnedControlledCameraFrame camera)
    {
        ObjectDisposedException.ThrowIf(disposed||retiring,this);
        if(!ReferenceEquals(assigned,camera)||!ReferenceEquals(borrowed8,ControllerIdentity)||
            !ReferenceEquals(retained16,ControllerIdentity)||references!=2||!dirty||!StorageRid.IsValid)
            throw new InvalidDataException("Actual controller828 registry loan is absent, cleaned or foreign.");
        camera.Optics.ValidateCurrent(camera.State,camera.Allocation);
    }
    internal void CleanupReturned(MapPostEffect3OwnedControlledCameraFrame camera)
    {
        RequireAssigned(camera);
        object? first=retained16;borrowed8=null;
        if(first is not null)Release(first);
        retained16=null;borrowed24=null;
        //Read32 after the first Release returns; do not snapshot/deduplicate.
        object? second=retained32;
        if(second is not null)Release(second);
        retained32=null;dirty=false;assigned=null;
        if(references!=1)throw new InvalidDataException("Ghost cleanup lost the controller's independent resource reference.");
    }
    private void Release(object actual)
    {if(!ReferenceEquals(actual,ControllerIdentity)||references<=1)throw new InvalidDataException("Foreign/unowned ghost registry reference.");references--;}
    public async ValueTask DisposeAsync()
    {
        await retirement.WaitAsync();
        try
        {
            if(disposed)return;retiring=true;
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(StorageRid);return true;});
            // Exceptional retirement is separate from source Return/A1clear.
            // Keep every unretired loan through a failed device release.
            assigned=null;borrowed8=retained16=borrowed24=retained32=null;references=0;dirty=false;disposed=true;
        }
        finally{retirement.Release();}
    }
}
