namespace San14.Data;

/// <summary>Actual renderer operations, separate from the owned container.
/// A host must provide real models before registering their free-pool order.</summary>
public interface IUnitModelRenderResources
{
    void RetireModel(UnitModelReference model);
    void ReleaseModelResources(UnitModelReference model);
    void InitializeModel(UnitModelReference model);
    void ConfigureModel(UnitModelReference model, RegistryObjectReference data);
    void ConfigureObjectPairModel(UnitModelReference model, ObjectReference first, ObjectReference second);
    bool? QueryArmyModelVisibility(UnitModelReference model);
}

public sealed record UnitModelPoolSnapshot(UnitModelReference ActiveHead,
    UnitModelReference ActiveTail, UnitModelReference FreeHead, int ActiveCount, int NodeCount);

/// <summary>Per-manager, per-kind ordered active/free model storage. It does
/// not allocate a renderer model, infer pool capacity or initialize association
/// and graphics fields. Explicit resource callbacks can change current fields
/// before the manager's later release or traversal reads them.</summary>
public sealed class UnitModelStorage : IUnitModelResources
{
    private sealed class Pool
    {
        public readonly object Mutex = new();
        public UnitModelReference Head = new(null), Tail = new(null), Free = new(null);
        public int Count, NodeCount;
    }
    private sealed class Node((int Manager, UnitModelKind Kind) pool)
    {
        public readonly (int Manager, UnitModelKind Kind) Pool = pool;
        public UnitModelReference? Next, Previous;
        public RegistryObjectReference? Data;
        public int? ArmyRaw748, ArmyRaw594;
    }
    private readonly Dictionary<(int Manager, UnitModelKind Kind), Pool> _pools = [];
    private readonly Dictionary<int, Node> _nodes = [];
    private readonly IUnitModelRenderResources _renderer;

    public UnitModelStorage(IUnitModelRenderResources renderer)
    { ArgumentNullException.ThrowIfNull(renderer); _renderer = renderer; }

    /// <summary>Registers already created model identities in their actual
    /// free-chain order. An explicitly empty pool has a known-null allocator
    /// result. Association and model748/594 remain unknown until written.</summary>
    public void RegisterPool(UnitManagerReference manager, UnitModelKind kind,
        IEnumerable<UnitModelReference> freeModels)
    {
        ArgumentNullException.ThrowIfNull(freeModels);
        var key = PoolKey(manager, kind);
        if (_pools.ContainsKey(key)) throw new InvalidDataException("Unit model pool is already registered.");
        var models = freeModels.ToArray();
        var identities = new HashSet<int>();
        foreach (var model in models)
        {
            int id = ModelId(model);
            if (!identities.Add(id) || _nodes.ContainsKey(id)) throw new InvalidDataException("Model identity occurs in more than one pool node.");
        }
        var pool = new Pool { NodeCount = models.Length, Free = models.FirstOrDefault() ?? new(null) };
        for (int index = 0; index < models.Length; index++)
            _nodes.Add(ModelId(models[index]), new(key) { Next = index + 1 < models.Length ? models[index + 1] : new(null), Previous = new(null) });
        _pools.Add(key, pool);
    }

    public UnitModelPoolSnapshot ReadPool(UnitManagerReference manager, UnitModelKind kind)
    {
        var pool = GetPool(manager, kind);
        lock (pool.Mutex) return new(pool.Head, pool.Tail, pool.Free, pool.Count, pool.NodeCount);
    }

    /// <summary>Fresh-manager pool initialization after the host has allocated
    /// an actual contiguous node block. Commit storage/free head before each
    /// constructor, then link only successfully constructed nodes.</summary>
    internal void PopulateFreshPool(UnitManagerReference manager, UnitModelKind kind,
        IReadOnlyList<UnitModelReference> models, Action<UnitModelReference> construct)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(construct);
        var key = PoolKey(manager, kind);
        var pool = GetPool(manager, kind);
        if (pool.NodeCount != 0 || pool.Count != 0 || pool.Head.Identity.HasValue || pool.Tail.Identity.HasValue || pool.Free.Identity.HasValue)
            throw new InvalidDataException("Fresh model pool initialization cannot replace existing storage.");
        if (models.Count == 0) throw new InvalidDataException("A positive startup pool requires an allocated model block.");
        var identities = new HashSet<int>();
        foreach (var model in models)
        {
            int id = ModelId(model);
            if (!identities.Add(id) || _nodes.ContainsKey(id)) throw new InvalidDataException("Allocated model block has duplicate node identities.");
        }
        foreach (var model in models) _nodes.Add(ModelId(model), new(key));
        pool.NodeCount = models.Count;
        pool.Free = models[0];
        UnitModelReference previous = new(null);
        foreach (var model in models)
        {
            construct(model);
            var node = GetNode(model);
            node.Next = new(null);
            node.Previous = new(null);
            if (previous.Identity.HasValue) GetNode(previous).Next = model;
            previous = model;
        }
    }
    public UnitModelReference ReadActiveModelHead(UnitManagerReference manager, UnitModelKind kind)
        => GetPool(manager, kind).Head;
    public RegistryObjectReference? ReadModelData(UnitModelReference model) => GetNode(model).Data;
    public UnitModelKind ReadModelKind(UnitModelReference model) => GetNode(model).Pool.Kind;
    public UnitModelReference? ReadActiveModelNext(UnitModelReference model) => GetNode(model).Next;
    public UnitModelReference? ReadModelPrevious(UnitModelReference model) => GetNode(model).Previous;
    public void WriteModelData(UnitModelReference model, RegistryObjectReference? data) => GetNode(model).Data = data;
    public void WriteModelNext(UnitModelReference model, UnitModelReference? next)
    { ValidateLink(next); GetNode(model).Next = next; }
    public void WriteModelPrevious(UnitModelReference model, UnitModelReference? previous)
    { ValidateLink(previous); GetNode(model).Previous = previous; }
    public int? ReadArmyModelRaw748(UnitModelReference model) => GetNode(model).ArmyRaw748;
    public void WriteArmyModelRaw748(UnitModelReference model, int value) => GetNode(model).ArmyRaw748 = value;
    public int? ReadArmyModelRaw594(UnitModelReference model) => GetNode(model).ArmyRaw594;
    public void WriteArmyModelRaw594(UnitModelReference model, int value) => GetNode(model).ArmyRaw594 = value;

    public UnitModelReference AllocateModel(UnitManagerReference manager, UnitModelKind kind, byte active, int argument)
    {
        //The implemented callers all request append1/preferredNull. Do not
        //silently invent semantics for a different native allocator branch.
        if (active != 1 || argument != 0) throw new InvalidDataException("Only append1/preferredNull model allocation is implemented.");
        var key = PoolKey(manager, kind);
        var pool = GetPool(manager, kind);
        lock (pool.Mutex)
        {
            var model = pool.Free;
            if (!model.Identity.HasValue) return model;
            var node = PoolNode(model, key);
            pool.Free = KnownLink(node.Next, "free next");
            node.Next = new(null);
            node.Previous = new(null);
            if (pool.Head.Identity.HasValue && pool.Tail.Identity.HasValue)
            {
                PoolNode(pool.Tail, key).Next = model;
                node.Previous = pool.Tail;
            }
            else pool.Head = model;
            pool.Tail = model;
            pool.Count = unchecked(pool.Count + 1);
            return model;
        }
    }

    public void ReturnModelToPool(UnitManagerReference manager, UnitModelKind kind, UnitModelReference model)
    {
        var key = PoolKey(manager, kind);
        var pool = GetPool(manager, kind);
        lock (pool.Mutex)
        {
            ArgumentNullException.ThrowIfNull(model);
            if (!model.Identity.HasValue) return;
            var node = PoolNode(model, key);
            //These fields are read after resource callbacks, including any
            //callback changes to the old node's current links.
            if (pool.Tail == model) pool.Tail = KnownLink(node.Previous, "released tail previous");
            if (pool.Head == model) pool.Head = KnownLink(node.Next, "released head next");
            var previous = KnownLink(node.Previous, "released previous");
            if (previous.Identity.HasValue) PoolNode(previous, key).Next = KnownLink(node.Next, "released next");
            var next = KnownLink(node.Next, "released next");
            if (next.Identity.HasValue) PoolNode(next, key).Previous = KnownLink(node.Previous, "released previous");
            node.Next = new(null);
            node.Previous = new(null);
            node.Next = pool.Free;
            pool.Free = model;
            pool.Count = unchecked(pool.Count - 1);
        }
    }

    public void RetireModel(UnitModelReference model) { _ = GetNode(model); _renderer.RetireModel(model); }
    public void ReleaseModelResources(UnitModelReference model) { _ = GetNode(model); _renderer.ReleaseModelResources(model); }
    public void InitializeModel(UnitModelReference model) { _ = GetNode(model); _renderer.InitializeModel(model); }
    public void ConfigureModel(UnitModelReference model, RegistryObjectReference data)
    { _ = GetNode(model); _renderer.ConfigureModel(model, data); }
    public void ConfigureObjectPairModel(UnitModelReference model, ObjectReference first, ObjectReference second)
    { _ = GetNode(model); _renderer.ConfigureObjectPairModel(model, first, second); }
    public bool? QueryArmyModelVisibility(UnitModelReference model)
    { _ = GetNode(model); return _renderer.QueryArmyModelVisibility(model); }

    private Pool GetPool(UnitManagerReference manager, UnitModelKind kind)
        => _pools.TryGetValue(PoolKey(manager, kind), out var pool) ? pool
            : throw new InvalidDataException("Current unit-model pool storage is not registered.");
    private Node GetNode(UnitModelReference model)
        => _nodes.TryGetValue(ModelId(model), out var node) ? node
            : throw new InvalidDataException("Current model node is not registered.");
    private Node PoolNode(UnitModelReference model, (int Manager, UnitModelKind Kind) key)
    {
        var node = GetNode(model);
        if (node.Pool != key) throw new InvalidDataException("Current model link points into a different manager pool.");
        return node;
    }
    private void ValidateLink(UnitModelReference? model)
    { if (model?.Identity is not null) _ = GetNode(model); }
    private static UnitModelReference KnownLink(UnitModelReference? model, string field)
        => model ?? throw new InvalidDataException($"Current model {field} is unknown.");
    private static int ModelId(UnitModelReference model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Identity is not int id || id < 0) throw new InvalidDataException("Null or negative model node identity.");
        return id;
    }
    private static (int Manager, UnitModelKind Kind) PoolKey(UnitManagerReference manager, UnitModelKind kind)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (manager.Identity is not int id || id < 0 || !Enum.IsDefined(kind)) throw new InvalidDataException("Invalid unit-model pool identity.");
        return (id, kind);
    }
}
