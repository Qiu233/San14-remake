using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Actual source skinned draw consumer for remake-created ordinary
/// actors. Uses the original G-buffer shader, source textures/samplers, hierarchy
/// and native palette. Fresh CB storage, source action0 repetition, skin gates
/// and opaque raster are explicit remake backend choices.</summary>
public sealed partial class OriginalRemakeArmyNativeRenderer : IAsyncDisposable
{
    private readonly OriginalGameRepository _repository;
    private readonly OriginalModelGbufferLayer _layer;
    private readonly Dictionary<(int Category, int Draw), OriginalArmyAnimatedGbufferSource> _sources = [];
    private readonly Dictionary<int, IReadOnlyList<G1DrawRecord>> _sourceDraws = [];
    private readonly Dictionary<string, (uint Flags, byte[] Pixel)> _materials = [];
    private readonly Dictionary<int, (OriginalRemakeArmyInitialization Initialization, float Start)> _epochs = [];
    private LoadedObjects? _shaderObjects;
    private LoadedRenderGraph? _graph;
    public long AcceptedFrameCount => _layer.AcceptedFrameCount;
    public int DrawCount => _layer.DrawCount;
    public int ProgramCount => _layer.ProgramCount;
    public int GeometryCount => _layer.GeometryCount;
    public long DrawCreationCount => _layer.DrawCreationCount;
    public IReadOnlyList<OriginalDeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; private set; }
    public OriginalModelGeometrySubmittedFrame? LastGeometrySubmission { get; private set; }

    public OriginalRemakeArmyNativeRenderer(OriginalGameRepository repository)
    { _repository = repository; _layer = OriginalModelGbufferLayer.CreateWithOwnedCompiler(); }

    public OriginalRemakeArmyNativeRenderer(OriginalGameRepository repository,Vkd3dShaderCompiler borrowedCompiler)
    {ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(borrowedCompiler);_repository=repository;_layer=OriginalModelGbufferLayer.Create(borrowedCompiler);}

    public Task RenderAsync(OriginalNativeMapFrameContext context,
        OriginalRemakeArmySceneVisuals visuals, float explicitSeconds)
    {
        ArgumentNullException.ThrowIfNull(visuals);
        return RenderAsync(context,visuals.Frames,visuals.Models,explicitSeconds);
    }

    public async Task RenderAsync(OriginalNativeMapFrameContext context,
        IReadOnlyDictionary<int,OriginalRemakeArmyVisualFrame> frames,
        IReadOnlyDictionary<int,LoadedOriginalArmyModel> models,float explicitSeconds,
        OriginalNativeArmyCurrentAdmission? currentNativeAdmission=null)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(frames);ArgumentNullException.ThrowIfNull(models);
        _worldPackets = null;
        OriginalDeferredCameraInputs camera = context.PreparedCamera ??
            throw new InvalidDataException("Remake Army native rendering requires the accepted source-prepared frame camera.");
        if (!float.IsFinite(explicitSeconds) || explicitSeconds < 0)
            throw new InvalidDataException("Remake Army native animation clock must be finite and nonnegative.");
        var packets = new List<OriginalModelGbufferInstancePacket>();
        var selectedInterfaces = new Dictionary<string,OriginalModelShaderInterfaceSource>(StringComparer.Ordinal);
        var candidates = new List<OriginalDeferredSourceMode16Candidate>();
        CurrentViewFamilyCandidates=null;LastGeometrySubmission=null;
        var pendingMaterials = new Dictionary<string, (uint Flags, byte[] Pixel)>(StringComparer.Ordinal);
        foreach ((int unitId, OriginalRemakeArmyVisualFrame frame) in frames)
        {
            if (!_epochs.TryGetValue(unitId, out var epoch) || !ReferenceEquals(epoch.Initialization, frame.Initialization))
                _epochs[unitId] = epoch = (frame.Initialization, explicitSeconds);
            foreach (OriginalRemakeArmyPosePublication actor in frame.Publications.Values)
            {
                LoadedOriginalArmyModel model = models[actor.Category];
                if (!_sourceDraws.TryGetValue(actor.Category, out var draws))
                    _sourceDraws.Add(actor.Category, draws = G1ModelDrawReader.Read(model.Transforms.Geometry).Draws);
                foreach (G1DrawRecord draw in draws)
                {
                    var key = (actor.Category, draw.Index);
                    if (!_sources.TryGetValue(key, out var source))
                    {
                        _shaderObjects ??= _repository.LoadObjects("system", 0x718CCB8B);
                        _graph ??= _repository.LoadRenderGraph("system", 0x024496EE);
                        _sources.Add(key, source = OriginalArmyAnimatedGbufferSource.Load(_repository,
                            model, _shaderObjects, _graph, 49, draw.Index));
                    }
                    string instance = $"remake-army/{unitId}/{actor.ComponentId}/draw{draw.Index}";
                    candidates.Add(new("ArmyOrdinary",$"remake-army/{unitId}/{actor.ComponentId}",draw.Index,
                        source.SourceDraw.Model.Source.ResourceUri,source.SourceDraw.Model.Source.DecodedSha256,
                        source.SourceDraw.GraphSource.Draw.Binding.Shader,"actual-Army-preparation/frame="+context.FrameNumber));
                    _materials.TryGetValue(instance, out var material);
                    var animation = actor.AnimationInput with { CurrentTimeSeconds = explicitSeconds - epoch.Start };
                    var prepared = source.Prepare(instance, new(animation, actor.WorldTransform, camera,
                        new(true, new[] { true, false, false }, OriginalArmyAnimationClockPolicy.RepeatDeclaredDuration),
                        new byte[6256], material.Pixel ?? new byte[80], material.Flags,
                        false, null, OpaqueRemakeRaster()));
                    packets.Add(prepared.Packet);
                    selectedInterfaces.Add(instance,source.SourceDraw.GraphSource.Interface);
                    pendingMaterials[instance] = (prepared.Draw.NextCachedMaterialFlags,
                        prepared.Packet.Pixel.Constants[0].ToArray());
                }
            }
        }
        var submitted=currentNativeAdmission?.PrepareFamily(context,"ArmyOrdinary",packets,selectedInterfaces)??packets;
        var geometrySubmission=await _layer.RenderAsync(context.TerrainFrame, submitted);
        foreach (var item in pendingMaterials) _materials[item.Key] = item.Value;
        var keep = packets.Select(item => item.InstanceKey).ToHashSet(StringComparer.Ordinal);
        foreach (string key in _materials.Keys.Where(key => !keep.Contains(key)).ToArray()) _materials.Remove(key);
        foreach (int id in _epochs.Keys.Where(id => !frames.ContainsKey(id)).ToArray()) _epochs.Remove(id);
        CurrentViewFamilyCandidates=candidates.AsReadOnly();LastGeometrySubmission=geometrySubmission;
    }

    public Task RetireTargetAsync()
    {_worldPackets=null;CurrentViewFamilyCandidates=null;LastGeometrySubmission=null;return _layer.RetireTargetAsync();}

    private static OriginalModelRasterInputs OpaqueRemakeRaster()
    {
        var attachment = new OriginalModelBlendAttachment(false,
            RenderingDevice.BlendFactor.One, RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add,
            RenderingDevice.BlendFactor.One, RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add,
            true, true, true, true);
        return new(RenderingDevice.PolygonCullMode.Disabled, RenderingDevice.PolygonFrontFace.CounterClockwise,
            true, true, RenderingDevice.CompareOperator.LessOrEqual, Enumerable.Repeat(attachment, 5).ToArray());
    }

    public async ValueTask DisposeAsync()
    { _worldPackets = null; _worldRetired = true; await _layer.DisposeAsync(); _sources.Clear(); _sourceDraws.Clear(); _materials.Clear(); _epochs.Clear(); _graph = null; _shaderObjects = null; }
}
