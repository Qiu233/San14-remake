using San14.Data;
using N = System.Numerics;

public sealed record RemakeArmyFlagActor(int UnitId, ulong OwnedNodeIdentity,
    int DescriptorOffset, int DescriptorInstanceIndex, int Category,
    ArmyInstancePoseWrite SourceWrite, UnitWorldTransformState World,
    LoadedArmyFlagResources Source, RemakeArmyInitialization Initialization)
{
    public string InstanceKey => $"remake-army/{UnitId}/flag/{DescriptorOffset:X}/{OwnedNodeIdentity}";
    public bool InstanceStateFidelityVerified => false;
}

public sealed record RemakeArmyFlagFrame(int UnitId,
    ArmyFlagLinkedPool BannerPool, ArmyFlagLinkedPool FlagPool,
    ArmyFlagPoolPublicationResult Publications,
    IReadOnlyList<RemakeArmyFlagActor> Actors)
{
    public bool RemakeCreatedVisualPrefix => true;
    public bool CompleteAssemblyFidelityVerified => false;
    public bool PreserveUnitMarkers => true;
}

/// <summary>Source-bound fresh ordinary flag model prefix. Executes the known
/// banner request1/category2 and ordinary flag request3/category3 reconciliation
/// against successful owned source model loads. The publication consumer follows
/// actual links. Dynamic material results belong to their separate producer.
/// This object does not inspect or replace the resident unit sign layer.</summary>
public sealed class RemakeArmyFlagVisuals : IDisposable
{
    private sealed record OwnedPools(ArmyFlagLinkedPool Banner, ArmyFlagLinkedPool Flags);
    private readonly GameResourceRepository _repository;
    private readonly Dictionary<int, LoadedArmyFlagResources> _sources = [];
    private readonly Dictionary<int, OwnedPools> _pools = [];
    private bool _disposed;
    public IReadOnlyDictionary<int, RemakeArmyFlagFrame> Frames { get; private set; } =
        new Dictionary<int, RemakeArmyFlagFrame>();
    public IReadOnlyDictionary<int, LoadedArmyFlagResources> Sources => _sources;

    internal RemakeArmyFlagVisuals ForkLogicalState()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var fork=new RemakeArmyFlagVisuals(_repository){Frames=Frames};
        foreach(var pair in _sources)fork._sources.Add(pair.Key,pair.Value);
        foreach(var pair in _pools)fork._pools.Add(pair.Key,pair.Value);
        return fork;
    }

    public RemakeArmyFlagVisuals(GameResourceRepository repository)
    { ArgumentNullException.ThrowIfNull(repository); _repository = repository; }

    public void Prepare(IReadOnlyDictionary<int, RemakeArmyVisualFrame> ordinaryFrames)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(ordinaryFrames);
        var next = new Dictionary<int, RemakeArmyFlagFrame>();
        foreach ((int id, var ordinary) in ordinaryFrames)
        {
            if (ordinary.UnitId != id || ordinary.Mode is not(0 or 1 or 2) || !ordinary.RemakeCreatedVisualState)
                throw new InvalidDataException("Source-created flags require the matching owned unit prefix.");
            if (!_pools.TryGetValue(id, out var pools))
            {
                // SC1C81F5/1C81FF: bound constructor request1/category2/flags0.
                // SC1C8FEE/1C8FF8: ordinary request3/category3/flags0.
                // 1C9130 calls the factory for missing nodes and appends only
                // successful results. These are owned fresh allocation results,
                // not a claim that requests determine an original current list.
                pools = new(CreateFreshPool(0x7F0, 2, 1), CreateFreshPool(0x7C8, 3, ordinary.Mode==0?3:0));
                _pools.Add(id, pools);
            }
            int sideFlags=ordinary.Mode==0?3:0;
            if(pools.Flags.Nodes.Count!=sideFlags)
            {
                pools=pools with{Flags=CreateFreshPool(0x7C8,3,sideFlags)};
                _pools[id]=pools;
            }
            var writes = ArmyFlagPoolPublication.Publish(ordinary.Records,
                ordinary.Mode, pools.Banner, pools.Flags);
            var actors = new List<RemakeArmyFlagActor>();
            foreach (var visit in writes.Visits.Where(row => row.Write is not null))
            {
                var write = visit.Write!;
                int category = visit.DescriptorOffset == 0x7F0 ? 2 : 3;
                var source = _sources[category];
                var scale = write.ScaleArgument ?? throw new InvalidDataException("Source flag scale publication is missing.");
                var p = write.PositionArgument; var q = write.RotationArgument;
                var world = source.Constructor.PublishScale(scale).PublishPosition(new(p.X,p.Y,p.Z))
                    .PublishQuaternion(new(q.X,q.Y,q.Z,q.W));
                actors.Add(new(id, visit.NodeIdentity, visit.DescriptorOffset, visit.DescriptorInstanceIndex,
                    category, write, world, source, ordinary.Initialization));
            }
            next.Add(id, new(id, pools.Banner, pools.Flags, writes, actors.AsReadOnly()));
        }
        foreach (int id in _pools.Keys.Where(id => !next.ContainsKey(id)).ToArray()) _pools.Remove(id);
        Frames = next;
    }

    private ArmyFlagLinkedPool CreateFreshPool(int descriptor, int category, int requested)
    {
        // Successful raw model/definition/action loading constructs an owned
        // model pointer result. Runtime cloth materials are resolved by the
        // draw producer, and are not replaced with an arbitrary primary image.
        if (!_sources.ContainsKey(category)) _sources.Add(category, LoadedArmyFlagResources.Load(_repository, category));
        var nodes = new List<ArmyFlagPoolNode>();
        for (int i = 0; i < requested; i++)
            nodes.Add(new((ulong)i + 1, i + 1 == requested ? 0 : (ulong)i + 2,
                1, 2, true, true));
        return new(descriptor, nodes.Count == 0 ? 0 : nodes[0].NodeIdentity, nodes.AsReadOnly(),
            "owned fresh source model/WorldPQ/action results; source 1C9130 factory+append reconciliation; " +
            "node identities are managed keys; dynamic cloth material results remain separate");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _sources.Clear(); _pools.Clear();
        Frames = new Dictionary<int, RemakeArmyFlagFrame>();
    }
}
