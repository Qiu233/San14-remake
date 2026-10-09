namespace San14.Data;

/// <summary>Actual Scenery business instances behind the model-resource
/// interface. Other model types require their own implementations. Geometry
/// and descriptor resource operations are supplied explicitly, never as an
/// implicit successful empty renderer.</summary>
public sealed class SceneryModelHost : IUnitModelRenderResources
{
    private readonly Dictionary<int, SceneryUnitModel> _models = [];
    private readonly ISceneryModelQueries _queries;
    private readonly ISceneryModelGraphics _graphics;

    public SceneryModelHost(ISceneryModelQueries queries, ISceneryModelGraphics graphics)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(graphics);
        _queries = queries;
        _graphics = graphics;
    }
    public SceneryUnitModel Construct(UnitModelReference reference, UnitModelStorage storage)
    {
        var model = new SceneryUnitModel(reference, _queries, _graphics, storage);
        _models.Add(reference.Identity!.Value, model);
        model.ApplyConstructor();
        return model;
    }
    public SceneryUnitModel Get(UnitModelReference reference)
        => reference.Identity.HasValue && _models.TryGetValue(reference.Identity.Value, out var model) ? model
            : throw new InvalidDataException("Current Scenery model business instance is not constructed.");
    public void RetireModel(UnitModelReference model) => Get(model).Retire();
    public void ReleaseModelResources(UnitModelReference model) => Get(model).ReleaseResources();
    public void InitializeModel(UnitModelReference model) => _ = Get(model).Initialize();
    public void ConfigureModel(UnitModelReference model, RegistryObjectReference data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Kind != RegistryKind.Scenery) throw new InvalidDataException("Scenery host cannot bind another model data type.");
        Get(model).Bind(new(data.PhysicalSlot));
    }
    public void ConfigureObjectPairModel(UnitModelReference model, ObjectReference first, ObjectReference second)
        => throw new InvalidDataException("Scenery host does not implement ObjectPair model resources.");
    public bool? QueryArmyModelVisibility(UnitModelReference model)
        => throw new InvalidDataException("Scenery host does not implement Army model visibility.");
}
