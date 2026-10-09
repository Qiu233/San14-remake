namespace San14.Data;

public sealed record UnitModelPoolRequest(UnitModelKind Kind, int NodeCount);
/// <summary>Null Nodes is a known allocation failure. A C# null block result
/// is unknown. Nonnull Nodes lists actual block identities in memory order.</summary>
public sealed record UnitModelNodeBlock(IReadOnlyList<UnitModelReference>? Nodes);
public sealed record UnitManagerStartupResult(UnitManagerReference Manager,
    bool Success, int InitializedPools, UnitModelKind? FailedPool);

public interface IUnitModelStartupResources : IUnitModelRenderResources
{
    UnitManagerReference? AllocateUnitManager();
    void ConstructUnitManager(UnitManagerReference manager);
    UnitModelNodeBlock? AllocateModelBlock(UnitManagerReference manager, UnitModelPoolRequest request);
    void ConstructModel(UnitModelKind kind, UnitModelReference model, UnitModelStorage storage);
}

/// <summary>1C9070 publication and fresh-manager1C4A20 pool initialization.
/// Actual allocation and constructors require a resource host. This helper
/// neither selects its enclosing application phase nor makes Core ready.</summary>
public static class UnitManagerStartup
{
    //These are literal pool-init arguments, independent of source table rows.
    //Extensions can supply a different explicit configuration; the original
    //restoration profile remains immutable.
    public static IReadOnlyList<UnitModelPoolRequest> DefaultConfiguration { get; } = Array.AsReadOnly(new[]
    {
        new UnitModelPoolRequest(UnitModelKind.Army, 500),
        new UnitModelPoolRequest(UnitModelKind.Area, 501),
        new UnitModelPoolRequest(UnitModelKind.City, 51),
        new UnitModelPoolRequest(UnitModelKind.Gate, 10),
        new UnitModelPoolRequest(UnitModelKind.Object, 6000),
        new UnitModelPoolRequest(UnitModelKind.ObjectPair, 6000),
        new UnitModelPoolRequest(UnitModelKind.Auxiliary, 2),
        new UnitModelPoolRequest(UnitModelKind.Scenery, 101)
    });

    public static UnitManagerStartupResult Initialize(SourceWorldContext context,
        IUnitModelStartupResources resources, IReadOnlyList<UnitModelPoolRequest>? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resources);
        var profile = (configuration ?? DefaultConfiguration).ToArray();
        ValidateConfiguration(profile);
        var reference = resources.AllocateUnitManager()
            ?? throw new InvalidDataException("Current CUnitManager allocation result is unknown.");
        if (!reference.Identity.HasValue)
        {
            context.CurrentUnitManager = reference; //The original publishes null before its unsafe tail call.
            throw new InvalidDataException("CUnitManager allocation returned null before pool initialization.");
        }
        if (reference.Identity.Value < 0) throw new InvalidDataException("Negative allocated CUnitManager identity.");
        resources.ConstructUnitManager(reference);
        var storage = new UnitModelStorage(resources);
        //All eight constructor headers exist and are empty before publication.
        foreach (var request in DefaultConfiguration) storage.RegisterPool(reference, request.Kind, []);
        var manager = context.RegisterUnitManager(reference.Identity.Value, storage);
        manager.InitializeConstructorTailFields();
        context.RegisterUnitModelStorage(reference, storage);
        context.CurrentUnitManager = reference;

        int initialized = 0;
        foreach (var request in profile)
        {
            var block = resources.AllocateModelBlock(reference, request)
                ?? throw new InvalidDataException($"Current {request.Kind} model block allocation result is unknown.");
            if (block.Nodes is null) return new(reference, false, initialized, request.Kind);
            if (block.Nodes.Count != request.NodeCount) throw new InvalidDataException($"Allocated {request.Kind} block has an incorrect node count.");
            //Snapshot identities before callbacks. Their order is allocation
            //order, never sorted by scenario or opaque model identity.
            var nodes = block.Nodes.ToArray();
            storage.PopulateFreshPool(reference, request.Kind, nodes,
                model => resources.ConstructModel(request.Kind, model, storage));
            initialized++;
        }
        manager.CompletePoolInitialization();
        return new(reference, true, initialized, null);
    }

    private static void ValidateConfiguration(IReadOnlyList<UnitModelPoolRequest> profile)
    {
        if (profile.Count != DefaultConfiguration.Count) throw new InvalidDataException("Unit manager startup requires all eight pool definitions.");
        var kinds = new HashSet<UnitModelKind>();
        foreach (var request in profile)
            if (request is null || !Enum.IsDefined(request.Kind) || request.NodeCount <= 0 || !kinds.Add(request.Kind))
                throw new InvalidDataException("Invalid or duplicate unit-model pool definition.");
    }
}
