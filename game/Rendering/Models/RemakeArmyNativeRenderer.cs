using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Actual source skinned draw consumer for remake-created ordinary
/// actors. Uses the original G-buffer shader, source textures/samplers, hierarchy
/// and native palette. Fresh CB storage, source action0 repetition, skin gates
/// and opaque raster are explicit remake backend choices.</summary>
public sealed partial class RemakeArmyNativeRenderer : IAsyncDisposable
{
    private readonly ModelGbufferLayer _layer;
    private readonly ArmyAnimatedSourceCache _sources;
    private readonly ArmyDrawMaterialHistory _materials = new();
    private readonly Dictionary<int, (RemakeArmyInitialization Initialization, float Start)> _epochs = [];
    public long AcceptedFrameCount => _layer.AcceptedFrameCount;
    public int DrawCount => _layer.DrawCount;
    public int ProgramCount => _layer.ProgramCount;
    public int GeometryCount => _layer.GeometryCount;
    public long DrawCreationCount => _layer.DrawCreationCount;
    public IReadOnlyList<DeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; private set; }
    public ModelGeometrySubmittedFrame? LastGeometrySubmission { get; private set; }

    public RemakeArmyNativeRenderer(GameResourceRepository repository)
    { _sources = new(repository); _layer = ModelGbufferLayer.CreateWithOwnedCompiler(); }

    public RemakeArmyNativeRenderer(GameResourceRepository repository,Vkd3dShaderCompiler borrowedCompiler)
    {ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(borrowedCompiler);_sources=new(repository);_layer=ModelGbufferLayer.Create(borrowedCompiler);}

    public Task RenderAsync(NativeMapFrameContext context,
        RemakeArmySceneVisuals visuals, float explicitSeconds)
    {
        ArgumentNullException.ThrowIfNull(visuals);
        return RenderAsync(context,visuals.Frames,visuals.Models,explicitSeconds);
    }

    public async Task RenderAsync(NativeMapFrameContext context,
        IReadOnlyDictionary<int,RemakeArmyVisualFrame> frames,
        IReadOnlyDictionary<int,LoadedArmyModel> models,float explicitSeconds,
        NativeArmyCurrentAdmission? currentNativeAdmission=null)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(frames);ArgumentNullException.ThrowIfNull(models);
        _worldPackets = null;
        DeferredCameraInputs camera = context.PreparedCamera ??
            throw new InvalidDataException("Remake Army native rendering requires the accepted source-prepared frame camera.");
        if (!float.IsFinite(explicitSeconds) || explicitSeconds < 0)
            throw new InvalidDataException("Remake Army native animation clock must be finite and nonnegative.");
        var packets = new List<ModelGbufferInstancePacket>();
        var selectedInterfaces = new Dictionary<string,ModelShaderInterfaceSource>(StringComparer.Ordinal);
        var candidates = new List<DeferredSourceMode16Candidate>();
        CurrentViewFamilyCandidates=null;LastGeometrySubmission=null;
        var pendingMaterials = new Dictionary<string, ArmyDrawMaterialState>(StringComparer.Ordinal);
        foreach ((int unitId, RemakeArmyVisualFrame frame) in frames)
        {
            if (!_epochs.TryGetValue(unitId, out var epoch) || !ReferenceEquals(epoch.Initialization, frame.Initialization))
                _epochs[unitId] = epoch = (frame.Initialization, explicitSeconds);
            foreach (RemakeArmyPosePublication actor in frame.Publications.Values)
            {
                LoadedArmyModel model = models[actor.Category];
                var sources = _sources.Get(model);
                foreach (G1DrawRecord draw in sources.Draws)
                {
                    var source = sources.GetDraw(draw.Index);
                    string instance = $"remake-army/{unitId}/{actor.ComponentId}/draw{draw.Index}";
                    candidates.Add(new("ArmyOrdinary",$"remake-army/{unitId}/{actor.ComponentId}",draw.Index,
                        source.SourceDraw.Model.Source.ResourceUri,source.SourceDraw.Model.Source.DecodedSha256,
                        source.SourceDraw.GraphSource.Draw.Binding.Shader,"actual-Army-preparation/frame="+context.FrameNumber));
                    var material = _materials.Read(instance, source, frame.Initialization);
                    var animation = actor.AnimationInput with { CurrentTimeSeconds = explicitSeconds - epoch.Start };
                    var prepared = source.Prepare(instance, new(animation, actor.WorldTransform, camera,
                        new(true, new[] { true, false, false }, ArmyAnimationClockPolicy.RepeatDeclaredDuration),
                        new byte[6256], material?.Pixel ?? new byte[80], material?.Flags ?? 0,
                        false, null, OpaqueRemakeRaster()));
                    packets.Add(prepared.Packet);
                    selectedInterfaces.Add(instance,source.SourceDraw.GraphSource.Interface);
                    pendingMaterials[instance] = new(source, frame.Initialization, prepared.Draw.NextCachedMaterialFlags,
                        prepared.Packet.Pixel.Constants[0].ToArray());
                }
            }
        }
        var submitted=currentNativeAdmission?.PrepareFamily(context,"ArmyOrdinary",packets,selectedInterfaces)??packets;
        var geometrySubmission=await _layer.RenderAsync(context.TerrainFrame, submitted);
        _materials.Commit(pendingMaterials);
        _sources.Retain(models.Keys.ToHashSet());
        foreach (int id in _epochs.Keys.Where(id => !frames.ContainsKey(id)).ToArray()) _epochs.Remove(id);
        CurrentViewFamilyCandidates=candidates.AsReadOnly();LastGeometrySubmission=geometrySubmission;
    }

    public Task RetireTargetAsync()
    {_worldPackets=null;CurrentViewFamilyCandidates=null;LastGeometrySubmission=null;return _layer.RetireTargetAsync();}

    private static ModelRasterInputs OpaqueRemakeRaster()
    {
        var attachment = new ModelBlendAttachment(false,
            RenderingDevice.BlendFactor.One, RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add,
            RenderingDevice.BlendFactor.One, RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add,
            true, true, true, true);
        return new(RenderingDevice.PolygonCullMode.Disabled, RenderingDevice.PolygonFrontFace.CounterClockwise,
            true, true, RenderingDevice.CompareOperator.LessOrEqual, Enumerable.Repeat(attachment, 5).ToArray());
    }

    public async ValueTask DisposeAsync()
    { _worldPackets = null; _worldRetired = true; await _layer.DisposeAsync(); _sources.Clear(); _materials.Clear(); _epochs.Clear(); }
}
