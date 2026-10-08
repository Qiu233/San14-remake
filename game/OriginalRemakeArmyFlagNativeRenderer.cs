using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record OriginalRemakeArmyFlagRenderStatus(int UnitId, int DescriptorOffset,
    ulong NodeIdentity, int DrawIndex, bool Submitted, string SourceBoundary);

public sealed record OriginalRemakeArmyFlagMaterialInputs(int ExplicitOwnedForceVariant,
    OriginalRemakeArmyFlagSourcePaneInputs SourcePane,float ExplicitSpecularW,
    string ExplicitSpecularSource,bool ExplicitBackendAlphaEnabled,byte? ExplicitForcedAlphaReference=null);
public delegate OriginalRemakeArmyFlagMaterialInputs? OriginalRemakeArmyFlagMaterialProvider(OriginalRemakeArmyFlagActor actor);

/// <summary>Source flag native draw owner. The unchanged pole components use
/// complete static source materials. Cloth requires the source ColorVariation
/// material and UI render-image producer; its missing primary texture never
/// becomes a neighboring or white image. Every existing unit marker is external
/// to this renderer and is retained by its caller.</summary>
public sealed partial class OriginalRemakeArmyFlagNativeRenderer : IAsyncDisposable
{
    private readonly OriginalGameRepository _repository;
    private readonly OriginalModelGbufferLayer _layer;
    private readonly Vkd3dShaderCompiler? _borrowedPaneCompiler;
    private readonly Dictionary<(int Category, int Draw), OriginalRemakeArmyFlagGbufferSource> _draws = [];
    private readonly Dictionary<string, (uint Flags, byte[] Pixel)> _materials = [];
    private readonly Dictionary<int, (OriginalRemakeArmyInitialization Initialization, float Start)> _epochs = [];
    private readonly Dictionary<(int Category,int Draw,int ForceVariant),OriginalRemakeArmyFlagVariantGbufferSource> _variants=[];
    private readonly Dictionary<string,OriginalRemakeArmyFlagCaoPaneProducer> _paneProducers=[];
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private bool _retiring;
    private int _accepted;
    private TaskCompletionSource? _drained;
    private Task? _disposal;
    public OriginalRemakeArmyFlagMaterialProvider? SourceMaterialProvider { get; set; }
    private LoadedObjects? _objects;
    private LoadedRenderGraph? _graph;
    public IReadOnlyList<OriginalRemakeArmyFlagRenderStatus> Statuses { get; private set; } = [];
    public int DrawCount => _layer.DrawCount;
    public int ProgramCount => _layer.ProgramCount;
    public int GeometryCount => _layer.GeometryCount;
    public long DrawCreationCount => _layer.DrawCreationCount;
    public long AcceptedFrameCount => _layer.AcceptedFrameCount;
    public IReadOnlyList<OriginalDeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; private set; }
    public OriginalModelGeometrySubmittedFrame? LastGeometrySubmission { get; private set; }

    public OriginalRemakeArmyFlagNativeRenderer(OriginalGameRepository repository)
    { ArgumentNullException.ThrowIfNull(repository); _repository = repository; _layer = OriginalModelGbufferLayer.CreateWithOwnedCompiler(); }

    public OriginalRemakeArmyFlagNativeRenderer(OriginalGameRepository repository,Vkd3dShaderCompiler borrowedCompiler)
    {ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(borrowedCompiler);_repository=repository;_layer=OriginalModelGbufferLayer.Create(borrowedCompiler);_borrowedPaneCompiler=borrowedCompiler;}

    public Task RenderAsync(OriginalNativeMapFrameContext context,
        OriginalRemakeArmyFlagVisuals visuals, float callerSeconds)
    {
        ArgumentNullException.ThrowIfNull(visuals);
        return RenderAsync(context,visuals.Frames,SourceMaterialProvider,callerSeconds);
    }

    public Task RenderAsync(OriginalNativeMapFrameContext context,
        IReadOnlyDictionary<int,OriginalRemakeArmyFlagFrame> frames,
        OriginalRemakeArmyFlagMaterialProvider? materialProvider,float callerSeconds,
        OriginalNativeArmyCurrentAdmission? currentNativeAdmission=null)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(frames);
        var camera = context.PreparedCamera ?? throw new InvalidDataException("Source flags require the accepted source-prepared camera.");
        _worldPackets=null;
        if (!float.IsFinite(callerSeconds) || callerSeconds < 0) throw new InvalidDataException("Flag presentation clock must be finite and nonnegative.");
        // Logical refresh can retire the CPU owner while accepted glyph work
        // awaits the render thread. Its immutable actors remain owned here.
        var actors=frames.SelectMany(pair=>pair.Value.Actors.Select(actor=>(Unit:pair.Key,Actor:actor))).ToArray();
        var keepUnits=frames.Keys.ToHashSet();
        Accept();
        return RenderAcceptedAsync(context,camera,actors,keepUnits,materialProvider,callerSeconds,currentNativeAdmission);
    }

    private async Task RenderAcceptedAsync(OriginalNativeMapFrameContext context,OriginalDeferredCameraInputs camera,
        IReadOnlyList<(int Unit,OriginalRemakeArmyFlagActor Actor)> actors,IReadOnlySet<int> keepUnits,
        OriginalRemakeArmyFlagMaterialProvider? materialProvider,float callerSeconds,
        OriginalNativeArmyCurrentAdmission? currentNativeAdmission)
    {
        await _gate.WaitAsync();
        try
        {
        var packets = new List<OriginalModelGbufferInstancePacket>();
        var selectedInterfaces = new Dictionary<string,OriginalModelShaderInterfaceSource>(StringComparer.Ordinal);
        var pending = new Dictionary<string, (uint Flags, byte[] Pixel)>();
        var statuses = new List<OriginalRemakeArmyFlagRenderStatus>();
        var candidates = new List<OriginalDeferredSourceMode16Candidate>();
        CurrentViewFamilyCandidates=null;LastGeometrySubmission=null;
        foreach (var (unit,actor) in actors)
        {
            if (!_epochs.TryGetValue(unit, out var epoch) || !ReferenceEquals(epoch.Initialization, actor.Initialization))
                _epochs[unit] = epoch = (actor.Initialization, callerSeconds);
            foreach (var draw in G1ModelDrawReader.Read(actor.Source.Model.Geometry).Draws)
            {
                var key = (actor.Category, draw.Index);
                if (!_draws.TryGetValue(key, out var source))
                {
                    _objects ??= _repository.LoadObjects("system", 0x718CCB8B);
                    _graph ??= _repository.LoadRenderGraph("system", 0x024496EE);
                    _draws.Add(key, source = OriginalRemakeArmyFlagGbufferSource.Load(_repository, actor.Source, _objects, _graph, draw.Index));
                }
                if (source.RequiredRuntimeProxyIds.Count != 0)
                {
                    try
                    {
                        var materialInput=materialProvider?.Invoke(actor);
                        if(materialInput is null)
                        {
                            statuses.Add(new(unit,actor.DescriptorOffset,actor.OwnedNodeIdentity,draw.Index,false,
                                "source ColorVariation force fields/text/complete UI glyph producer required"));continue;
                        }
                        var variantKey=(actor.Category,draw.Index,materialInput.ExplicitOwnedForceVariant);
                        if(!_variants.TryGetValue(variantKey,out var variant))
                            _variants.Add(variantKey,variant=OriginalRemakeArmyFlagVariantGbufferSource.Load(_repository,actor.Source,
                                draw.Index,materialInput.ExplicitOwnedForceVariant));
                        string locale=actor.Initialization.CodeSource.Language;
                        if(!_paneProducers.TryGetValue(locale,out var producer))
                            _paneProducers.Add(locale,producer=new(_repository,locale,_borrowedPaneCompiler));
                        var primary=await producer.PrepareAsync(variant.Variation,materialInput.SourcePane);
                        candidates.Add(new("ArmyFlagCloth",actor.InstanceKey,draw.Index,
                            actor.Source.Model.Source.ResourceUri,actor.Source.Model.Source.DecodedSha256,
                            variant.Variation.ShaderOwner,"actual-Army-cloth-preparation/frame="+context.FrameNumber));
                        string clothInstance=actor.InstanceKey+$"/draw{draw.Index}";
                        _materials.TryGetValue(clothInstance,out var previousCloth);
                        var cloth=variant.Prepare(clothInstance,new(callerSeconds-epoch.Start,actor.World,camera,new byte[6256],
                            previousCloth.Pixel??new byte[80],previousCloth.Flags,OpaqueRemakeRaster(),
                            new Dictionary<uint,OriginalRemakeArmyFlagRuntimeTexture>{[primary.ProxyObjectId]=primary},
                            materialInput.ExplicitSpecularW,materialInput.ExplicitSpecularSource,materialInput.ExplicitBackendAlphaEnabled,
                            materialInput.ExplicitForcedAlphaReference));
                        packets.Add(cloth.Packet);pending[clothInstance]=(cloth.NextCachedMaterialFlags,cloth.Packet.Pixel.Constants[0].ToArray());
                        selectedInterfaces.Add(clothInstance,OriginalModelShaderResources.ResolveType16Interface(variant.Variation.ShaderOwner,
                            OriginalModelRendererStateSourceReader.ReadQueue(context.TerrainFrame.Source.Graph,49).ShaderStateOffset));
                        statuses.Add(new(unit,actor.DescriptorOffset,actor.OwnedNodeIdentity,draw.Index,true,
                            "actual source ColorVariation/G1N/stock-glyph primary/A72/native action0; explicit owned force inputs"));
                    }
                    catch(InvalidDataException error)
                    {
                        statuses.Add(new(unit,actor.DescriptorOffset,actor.OwnedNodeIdentity,draw.Index,false,error.Message));
                    }
                    continue;
                }
                string instance = actor.InstanceKey + $"/draw{draw.Index}";
                candidates.Add(new("ArmyFlagBase",actor.InstanceKey,draw.Index,
                    source.Source.Model.Source.ResourceUri,source.Source.Model.Source.DecodedSha256,
                    source.GraphSource.Draw.Binding.Shader,"actual-Army-flag-preparation/frame="+context.FrameNumber));
                _materials.TryGetValue(instance, out var previous);
                var prepared = source.Prepare(instance, new(callerSeconds - epoch.Start, actor.World, camera,
                    new byte[6256], previous.Pixel ?? new byte[80], previous.Flags, OpaqueRemakeRaster(),
                    new Dictionary<uint, OriginalRemakeArmyFlagRuntimeTexture>()));
                packets.Add(prepared.Packet);
                selectedInterfaces.Add(instance,source.GraphSource.Interface);
                pending[instance] = (prepared.NextCachedMaterialFlags, prepared.Packet.Pixel.Constants[0].ToArray());
                statuses.Add(new(unit, actor.DescriptorOffset, actor.OwnedNodeIdentity, draw.Index, true,
                    "complete original pole geometry/material/action0; four-influence source palette"));
            }
        }
        var submitted=currentNativeAdmission?.PrepareFamily(context,"ArmyFlag",packets,selectedInterfaces)??packets;
        var geometrySubmission=await _layer.RenderAsync(context.TerrainFrame, submitted);
        foreach (var pair in pending) _materials[pair.Key] = pair.Value;
        foreach (string key in _materials.Keys.Where(key => !pending.ContainsKey(key)).ToArray()) _materials.Remove(key);
        foreach (int key in _epochs.Keys.Where(key => !keepUnits.Contains(key)).ToArray()) _epochs.Remove(key);
        Statuses = statuses.AsReadOnly();
        CurrentViewFamilyCandidates=statuses.All(s=>s.Submitted)?candidates.AsReadOnly():null;
        LastGeometrySubmission=geometrySubmission;
        }
        finally{_gate.Release();CompleteAccepted();}
    }

    public Task RetireTargetAsync(){Accept();return RetireAcceptedAsync();}
    private async Task RetireAcceptedAsync()
    {
        await _gate.WaitAsync();
        try{_worldPackets=null;await _layer.RetireTargetAsync();CurrentViewFamilyCandidates=null;LastGeometrySubmission=null;}
        finally{_gate.Release();CompleteAccepted();}
    }
    private void Accept()
    {
        lock(_lifetime)
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            _accepted++;
        }
    }
    private void CompleteAccepted()
    {
        lock(_lifetime)
        {
            _accepted--;
            if(_retiring&&_accepted==0)_drained?.TrySetResult();
        }
    }
    private static OriginalModelRasterInputs OpaqueRemakeRaster()
    {
        var blend = new OriginalModelBlendAttachment(false, RenderingDevice.BlendFactor.One,
            RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add, RenderingDevice.BlendFactor.One,
            RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add, true,true,true,true);
        return new(RenderingDevice.PolygonCullMode.Disabled, RenderingDevice.PolygonFrontFace.CounterClockwise,
            true,true,RenderingDevice.CompareOperator.LessOrEqual,Enumerable.Repeat(blend,5).ToArray());
    }
    public ValueTask DisposeAsync()
    {
        lock(_lifetime)
        {
            _retiring=true;
            _worldPackets=null;
            return new(_disposal??=DisposeCoreAsync());
        }
    }
    private async Task DisposeCoreAsync()
    {
        Task pending;
        lock(_lifetime)pending=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        await pending;
        await _layer.DisposeAsync(); _draws.Clear(); _materials.Clear(); _epochs.Clear();
        foreach(var group in _worldTextureGroups.Values)group.Invalidate("Army flag renderer disposed");
        _worldTextureGroups.Clear();_worldFinalVariants.Clear();
        foreach(var producer in _paneProducers.Values)await producer.DisposeAsync();_paneProducers.Clear();_variants.Clear();
        _objects = null; _graph = null; Statuses = [];
    }
}
