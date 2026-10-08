namespace San14.Data;

/// <summary>Actual Scenery business instances behind the model-resource
/// interface. Other model types require their own implementations. Geometry
/// and descriptor resource operations are supplied explicitly, never as an
/// implicit successful empty renderer.</summary>
public sealed class OriginalSceneryModelHost : IOriginalUnitModelRenderResources
{
    private readonly Dictionary<int, OriginalSceneryUnitModel> _models = [];
    private readonly IOriginalSceneryModelQueries _queries;
    private readonly IOriginalSceneryModelGraphics _graphics;

    public OriginalSceneryModelHost(IOriginalSceneryModelQueries queries, IOriginalSceneryModelGraphics graphics)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(graphics);
        _queries = queries;
        _graphics = graphics;
    }
    public OriginalSceneryUnitModel Construct(OriginalUnitModelReference reference, OriginalUnitModelStorage storage)
    {
        var model = new OriginalSceneryUnitModel(reference, _queries, _graphics, storage);
        _models.Add(reference.Identity!.Value, model);
        model.ApplyConstructor();
        return model;
    }
    public OriginalSceneryUnitModel Get(OriginalUnitModelReference reference)
        => reference.Identity.HasValue && _models.TryGetValue(reference.Identity.Value, out var model) ? model
            : throw new InvalidDataException("Current Scenery model business instance is not constructed.");
    public void RetireModel(OriginalUnitModelReference model) => Get(model).Retire();
    public void ReleaseModelResources(OriginalUnitModelReference model) => Get(model).ReleaseResources();
    public void InitializeModel(OriginalUnitModelReference model) => _ = Get(model).Initialize();
    public void ConfigureModel(OriginalUnitModelReference model, OriginalRegistryObjectReference data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Kind != OriginalRegistryKind.Scenery) throw new InvalidDataException("Scenery host cannot bind another model data type.");
        Get(model).Bind(new(data.PhysicalSlot));
    }
    public void ConfigureObjectPairModel(OriginalUnitModelReference model, OriginalObjectReference first, OriginalObjectReference second)
        => throw new InvalidDataException("Scenery host does not implement ObjectPair model resources.");
    public bool? QueryArmyModelVisibility(OriginalUnitModelReference model)
        => throw new InvalidDataException("Scenery host does not implement Army model visibility.");
}
