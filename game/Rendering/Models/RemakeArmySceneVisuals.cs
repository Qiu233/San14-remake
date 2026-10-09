using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Godot;
using San14.Core;
using San14.Data;
using NVector3 = System.Numerics.Vector3;

/// <summary>Owned visual state for remake-created ordinary armies. Source
/// constructor scale, nineteen source formation records and native setter
/// arguments are executed with explicitly chosen remake clock/RNG/ground routes.
/// This owner never claims to recover an original process's current army state.</summary>
public sealed partial class RemakeArmySceneVisuals : IDisposable
{
    private readonly GameResourceRepository _repository;
    private readonly string _language;
    private readonly MapWorldProjection _projection;
    private readonly LoadedHeightField _height;
    private readonly Dictionary<int, LoadedArmyModel> _models = [];
    private readonly Dictionary<int, UnitWorldTransformState> _constructors = [];
    private readonly Dictionary<int, ArmyAnimationSourceInput> _routes = [];
    private readonly Dictionary<int, RemakeArmyVisualState> _states = [];
    private readonly Dictionary<int, (NVector3 Center, float Heading)> _anchors = [];
    private readonly Dictionary<int, long> _randomCounters = [];
    private LoadedArmyCatalog? _catalog;
    private UnitWorldGroundHeightGrid? _ground;
    private bool _disposed;

    public IReadOnlyDictionary<int, RemakeArmyVisualFrame> Frames { get; private set; } =
        new Dictionary<int, RemakeArmyVisualFrame>();
    public IReadOnlyDictionary<int, LoadedArmyModel> Models => _models;

    internal RemakeArmySceneVisuals ForkLogicalState()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var fork=new RemakeArmySceneVisuals(_repository,_language,_projection,_height){_catalog=_catalog,_ground=_ground,Frames=Frames};
        foreach(var pair in _models)fork._models.Add(pair.Key,pair.Value);
        foreach(var pair in _constructors)fork._constructors.Add(pair.Key,pair.Value);
        foreach(var pair in _routes)fork._routes.Add(pair.Key,pair.Value);
        foreach(var pair in _states)fork._states.Add(pair.Key,pair.Value.ForkLogicalState());
        foreach(var pair in _anchors)fork._anchors.Add(pair.Key,pair.Value);
        foreach(var pair in _randomCounters)fork._randomCounters.Add(pair.Key,pair.Value);
        return fork;
    }

    public RemakeArmySceneVisuals(GameResourceRepository repository, string language,
        MapWorldProjection projection, LoadedHeightField height)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(height);
        _ = ArmyInstancePoseCodeSources.ForLanguage(language);
        (_repository, _language, _projection, _height) = (repository, language, projection, height);
    }

    public PlayableArmyPoseProvider Prepare(GameState world, PlayableArmyVisualPlan sourcePlan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var frames = new Dictionary<int, RemakeArmyVisualFrame>();
        var bindings = new Dictionary<int, IReadOnlyList<ArmyComponentVisualBinding>>();
        foreach ((int id, PlayableArmySelection selection) in sourcePlan.Selections)
        {
            UnitState unit = world.Units[id];
            // This is the explicitly selected remake ordinary-context prefix.
            // Siege/naval/transport constructors have separate native writers.
            if (ArmyNativeFormationQueries.ModelMode(selection.Terrain, false, selection.Creation.Bytes.Byte1B) != 0)
                continue;
            EnsureSourceModels();
            ProjectedMapPoint point = _projection.Project(unit.HexId);
            float x = (float)point.X, z = (float)point.Z;
            var center = new NVector3(x, Ground(x, z), z);
            float heading = 0; // Explicit standing remake route; native current heading is not substituted.
            if (_anchors.TryGetValue(id, out var prior))
                heading = center.X == prior.Center.X && center.Z == prior.Center.Z ? prior.Heading :
                    MathF.Atan2(center.X - prior.Center.X, center.Z - prior.Center.Z);
            if (!_states.TryGetValue(id, out var state))
            {
                var policy = new RemakeArmyCreationPolicy(
                    new byte[ArmyInstancePose.RecordCount * ArmyInstancePose.RecordBytes],
                    "remake-created ordinary visual; owned cleared record template")
                {
                    OrdinaryModelsResolved = Enumerable.Range(0, ArmyInstancePose.RecordCount)
                        .Select(index => _models.ContainsKey(index < 15 ? 0 : 1)).ToArray(),
                    BannerModelsResolved = [], FlagModelsResolved = [],
                    ActionRoutesByCategory = _routes,
                    RandomSource = "explicit remake SHA256-counter results at native random query boundary",
                    GroundSource = "owned fresh retained Field wrapper; PrepareFreshFieldBc49C0/QueryA0B830",
                    AnimationRouteSource = "explicit remake source action0 unique declared clip; owned presentation clock",
                    ExplicitContextPredicate16EEC0 = false
                };
                _states.Add(id, state = new(_language, policy));
            }
            RemakeArmyVisualFrame frame = state.Update(unit, selection, center, heading,
                index => RandomDraw(world, id, index), Ground, _constructors);
            _anchors[id] = (center, heading);
            frames.Add(id, frame);
            // Existing RGB preview anchors through its own source terrain sampler;
            // subtract that exact anchor so native published positions are retained.
            float previewY = (float)_height.HeightField.InterpolateSurfaceHeight(x, z);
            var anchor = new Vector3(x, previewY, z);
            bindings.Add(id, frame.Publications.Values.Select(actor =>
            {
                var transform = actor.WorldTransform;
                return new ArmyComponentVisualBinding(actor.ComponentId,
                    new(actor.Category,
                        new Quaternion(transform.Quaternion24.X, transform.Quaternion24.Y, transform.Quaternion24.Z, transform.Quaternion24.W),
                        new Vector3(transform.Scale34.X, transform.Scale34.Y, transform.Scale34.Z),
                        "source fresh ordinary records and WorldPQ; explicit remake visual prefix"),
                    new Vector3(transform.Position18.X, transform.Position18.Y, transform.Position18.Z) - anchor)
                { SourceDescriptorOffset = actor.SourceWrite.DescriptorOffset,
                    SourceInstanceIndex = actor.SourceWrite.DescriptorInstanceIndex };
            }).ToArray());
        }
        foreach (int id in _states.Keys.Where(id => !frames.ContainsKey(id)).ToArray())
        { _states.Remove(id); _anchors.Remove(id); _randomCounters.Remove(id); }
        Frames = frames;
        return (unit, _) => bindings.GetValueOrDefault(unit.Id);
    }

    private void EnsureSourceModels()
    {
        if (_models.Count != 0) return;
        _catalog ??= CeArmyModels.LoadCatalog(_repository);
        var actions = new ArmyGraphicsActions(_repository);
        foreach (int category in new[] { 0, 1 })
        {
            LoadedArmyModel model = CeArmyModels.LoadKnownCategory(_repository, _catalog, category);
            UnitWorldTransformState constructor = UnitWorldTransform.ConstructDefinition(model.Resources);
            var profile = actions.Actions[(category, 0)];
            LoadedArmyAnimationAction action = profile.Animation;
            if (action.AnimationData.Count != 1)
                throw new InvalidDataException("Remake fresh ordinary action0 requires its one source-declared animation clip.");
            uint fileId = action.AnimationData[0].Source.FileId;
            _models.Add(category, model);
            _constructors.Add(category, constructor);
            _routes.Add(category, new(category, action.ActionIndex, fileId, 0));
        }
    }

    private float Ground(float x, float z)
    {
        _ground ??= UnitWorldGroundHeightWriters.PrepareFreshFieldBc49C0(_height,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated);
        return UnitWorldGroundHeight.QueryA0B830(_ground, x, z);
    }

    private int RandomDraw(GameState world, int unitId, int sourceRecordIndex)
    {
        long counter = _randomCounters.GetValueOrDefault(unitId);
        _randomCounters[unitId] = checked(counter + 1);
        // This is an owned remake generator, never a purported native CRT seed.
        byte[] seed = Encoding.UTF8.GetBytes($"{world.ScenarioKey}|{world.MapSha256}|{unitId}|{counter}|{sourceRecordIndex}");
        return (int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(seed)) & 0x7FFFFFFF);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _states.Clear(); _anchors.Clear(); _randomCounters.Clear(); _motions.Clear(); _hexPositions.Clear();
        _models.Clear(); _constructors.Clear(); _routes.Clear();
        Frames = new Dictionary<int, RemakeArmyVisualFrame>();
        _catalog = null; _ground = null;
    }
}
