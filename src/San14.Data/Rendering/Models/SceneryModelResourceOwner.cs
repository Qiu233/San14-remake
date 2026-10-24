using System.Numerics;

namespace San14.Data;

public sealed record SceneryModelResourceState(int Category,int PhysicalIndex,int? UnitIdentity,
    uint RequestFlags,MapStaticModelAsset Asset,UnitWorldTransformState CurrentWorld);

/// <summary>Real CPU resource banks and descriptor members for the Scenery
/// sub-owner. Metadata/geometry preparation is not a successful device load.
/// Physical model state is retained when its record returns to the free bank.</summary>
internal sealed class SceneryModelResourceOwner : ISceneryModelGraphics,IDisposable
{
    private sealed class Record(int category,int index,MapStaticModelAsset asset)
    {
        public readonly int Category=category,Index=index;
        public readonly MapStaticModelAsset Asset=asset;
        public int? Unit;
        public uint Flags;
        public UnitWorldTransformState World=UnitWorldTransform.ConstructDefinition(asset.TextureResources);
    }
    private readonly UnitGeometry geometry;
    private readonly MapStaticModelAssetCatalog assets;
    private readonly Dictionary<int,List<Record>> descriptors=[];
    private readonly Dictionary<int,Stack<Record>> free=[];
    private readonly List<Record> records=[];
    private readonly Queue<Record> release=[];
    private readonly HashSet<Record> queued=[];
    private bool disposed;
    public bool DescriptorMembershipKnown{get;private set;}=true;
    public void InvalidateDescriptorMembership()=>DescriptorMembershipKnown=false;

    public SceneryModelResourceOwner(GameResourceRepository repository,LoadedSceneResources scene,UnitGeometry geometry)
    {
        this.geometry=geometry;assets=new(repository,scene);
        foreach(var rule in SceneryModelResourceRules.All)
        {
            var asset=assets.LoadSceneryCategory(rule.Category);
            var bank=new Stack<Record>();free.Add(rule.Category,bank);
            // Native cold loader appends increasing physical indices, then
            // allocation takes the tail. Capacity is the extracted20, not101.
            for(int i=0;i<rule.Capacity;i++){var record=new Record(rule.Category,i,asset);records.Add(record);bank.Push(record);}
        }
    }
    public void ConstructDescriptorList(SceneryUnitModel model)
    {ThrowIfDisposed();descriptors.Add(Id(model),[]);}
    public void RequestDescriptorResourceReleases(SceneryUnitModel model)
    {
        ThrowIfDisposed();
        foreach(var record in Members(model))if(queued.Add(record))release.Enqueue(record);
        ConsumeReleases();
    }
    public void ClearDescriptorList(SceneryUnitModel model)
    {ThrowIfDisposed();Members(model).Clear();}
    public ModelHexPosition? NativeHexPosition187A40(ushort hex)=>geometry.NativeHexPosition187A40(hex);
    public Vector4? NativeQuaternion1C58E0(Vector3 euler)=>geometry.NativeQuaternion1C58E0(euler);

    public bool UpdateDescriptor(SceneryUnitModel model,bool visible)
    {
        ThrowIfDisposed();
        if(!DescriptorMembershipKnown)throw new InvalidDataException("Current Scenery descriptor bank membership is unknown.");
        try{return UpdateKnownDescriptor(model,visible);}
        catch(InvalidDataException){DescriptorMembershipKnown=false;throw;}
    }
    private bool UpdateKnownDescriptor(SceneryUnitModel model,bool visible)
    {
        var members=Members(model);
        if(!visible){RequestDescriptorResourceReleases(model);members.Clear();return false;}
        int desired=model.DescriptorCount68??throw new InvalidDataException("Current Scenery descriptor count is unknown.");
        int key=model.DescriptorKey6C??throw new InvalidDataException("Current Scenery descriptor category is unknown.");
        uint flags=model.DescriptorFlags70??throw new InvalidDataException("Current Scenery descriptor flags are unknown.");
        if(desired<0)throw new InvalidDataException("Negative Scenery descriptor count.");
        bool different=members.Count!=desired;
        while(members.Count>desired){var last=members[^1];members.RemoveAt(members.Count-1);if(queued.Add(last))release.Enqueue(last);ConsumeReleases();}
        while(members.Count<desired)
        {
            if(!free.TryGetValue(key,out var bank))throw new InvalidDataException("Scenery descriptor uses an unprepared category.");
            if(bank.Count==0)break; // Known exhausted bank is not expansion or a fake ready handle.
            var record=bank.Pop();record.Unit=Id(model);record.Flags=flags;members.Add(record);
        }
        return different || members.Count!=0; // Actual device-load consumer remains pending.
    }
    public bool HasPendingDescriptors(SceneryUnitModel model)=>Members(model).Count!=0;
    public IReadOnlyList<SceneryModelResourceState>? CaptureResources(IEnumerable<SceneryUnitModel> activeModels)=>!DescriptorMembershipKnown?null:Array.AsReadOnly(activeModels
        .SelectMany(Members).Select(r=>new SceneryModelResourceState(r.Category,r.Index,r.Unit,r.Flags,r.Asset,r.World)).ToArray());
    private void ConsumeReleases()
    {
        while(release.TryDequeue(out var record))
        {
            queued.Remove(record);
            if(!record.Unit.HasValue)continue;
            record.Unit=null;free[record.Category].Push(record);
        }
    }
    private List<Record> Members(SceneryUnitModel model)=>descriptors.TryGetValue(Id(model),out var members)
        ?members:throw new InvalidDataException("Actual Scenery descriptor list was not constructed.");
    private static int Id(SceneryUnitModel model)=>model.Reference.Identity??throw new InvalidDataException("Null Scenery model identity.");
    private void ThrowIfDisposed(){ObjectDisposedException.ThrowIf(disposed,this);}
    public void Dispose()
    {
        if(disposed)return;
        foreach(var members in descriptors.Values)
            foreach(var record in members)if(queued.Add(record))release.Enqueue(record);
        ConsumeReleases();foreach(var members in descriptors.Values)members.Clear();
        descriptors.Clear();records.Clear();free.Clear();assets.Invalidate();disposed=true;
    }
}
