using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record RemakeArmyFlagRenderStatus(int UnitId, int DescriptorOffset,
    ulong NodeIdentity, int DrawIndex, bool Submitted, string SourceBoundary);

public sealed record RemakeArmyFlagMaterialInputs(int ExplicitOwnedForceVariant,
    RemakeArmyFlagSourcePaneInputs SourcePane,float ExplicitSpecularW,
    string ExplicitSpecularSource,bool ExplicitBackendAlphaEnabled,byte? ExplicitForcedAlphaReference=null);
public delegate RemakeArmyFlagMaterialInputs? RemakeArmyFlagMaterialProvider(RemakeArmyFlagActor actor);

/// <summary>Source flag native draw owner. The unchanged pole components use
/// complete static source materials. Cloth requires the source ColorVariation
/// material and UI render-image producer; its missing primary texture never
/// becomes a neighboring or white image. Every existing unit marker is external
/// to this renderer and is retained by its caller.</summary>
public sealed partial class RemakeArmyFlagNativeRenderer : IAsyncDisposable
{
    private readonly ModelGbufferLayer _layer;
    private readonly ArmyFlagSourceCache _sources;
    private readonly ArmyDrawMaterialHistory _materials = new();
    private readonly Dictionary<int, (RemakeArmyInitialization Initialization, float Start)> _epochs = [];
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private bool _retiring;
    private int _accepted;
    private TaskCompletionSource? _drained;
    private Task? _disposal;
    public RemakeArmyFlagMaterialProvider? SourceMaterialProvider { get; set; }
    public IReadOnlyList<RemakeArmyFlagRenderStatus> Statuses { get; private set; } = [];
    public int DrawCount => _layer.DrawCount;
    public int ProgramCount => _layer.ProgramCount;
    public int GeometryCount => _layer.GeometryCount;
    public long DrawCreationCount => _layer.DrawCreationCount;
    public long AcceptedFrameCount => _layer.AcceptedFrameCount;
    public IReadOnlyList<DeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; private set; }
    public ModelGeometrySubmittedFrame? LastGeometrySubmission { get; private set; }

    public RemakeArmyFlagNativeRenderer(GameResourceRepository repository)
    { ArgumentNullException.ThrowIfNull(repository); _sources = new(repository); _layer = ModelGbufferLayer.CreateWithOwnedCompiler(); }

    public RemakeArmyFlagNativeRenderer(GameResourceRepository repository,Vkd3dShaderCompiler borrowedCompiler)
    {ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(borrowedCompiler);_sources=new(repository,borrowedCompiler);_layer=ModelGbufferLayer.Create(borrowedCompiler);}

    public Task RenderAsync(NativeMapFrameContext context,
        RemakeArmyFlagVisuals visuals, float callerSeconds)
    {
        ArgumentNullException.ThrowIfNull(visuals);
        return RenderAsync(context,visuals.Frames,SourceMaterialProvider,callerSeconds);
    }

    public Task RenderAsync(NativeMapFrameContext context,
        IReadOnlyDictionary<int,RemakeArmyFlagFrame> frames,
        RemakeArmyFlagMaterialProvider? materialProvider,float callerSeconds,
        NativeArmyCurrentAdmission? currentNativeAdmission=null)
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

    private async Task RenderAcceptedAsync(NativeMapFrameContext context,DeferredCameraInputs camera,
        IReadOnlyList<(int Unit,RemakeArmyFlagActor Actor)> actors,IReadOnlySet<int> keepUnits,
        RemakeArmyFlagMaterialProvider? materialProvider,float callerSeconds,
        NativeArmyCurrentAdmission? currentNativeAdmission)
    {
        await _gate.WaitAsync();
        try
        {
        var packets = new List<ModelGbufferInstancePacket>();
        var selectedInterfaces = new Dictionary<string,ModelShaderInterfaceSource>(StringComparer.Ordinal);
        var pending = new Dictionary<string, ArmyDrawMaterialState>(StringComparer.Ordinal);
        var statuses = new List<RemakeArmyFlagRenderStatus>();
        var candidates = new List<DeferredSourceMode16Candidate>();
        CurrentViewFamilyCandidates=null;LastGeometrySubmission=null;
        foreach (var (unit,actor) in actors)
        {
            if (!_epochs.TryGetValue(unit, out var epoch) || !ReferenceEquals(epoch.Initialization, actor.Initialization))
                _epochs[unit] = epoch = (actor.Initialization, callerSeconds);
            var sources=_sources.Get(actor.Source);
            foreach (var draw in actor.Source.Draws)
            {
                var source=sources.GetDraw(draw.Index);
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
                        var variant=sources.GetVariant(draw.Index,materialInput.ExplicitOwnedForceVariant);
                        var producer=sources.GetProducer(actor.Initialization.CodeSource.Language);
                        var primary=await producer.PrepareAsync(variant.Variation,variant.TextureUses.Single(t=>t.TextureRegister==0),materialInput.SourcePane);
                        candidates.Add(new("ArmyFlagCloth",actor.InstanceKey,draw.Index,
                            actor.Source.Model.Source.ResourceUri,actor.Source.Model.Source.DecodedSha256,
                            variant.Variation.ShaderOwner,"actual-Army-cloth-preparation/frame="+context.FrameNumber));
                        string clothInstance=actor.InstanceKey+$"/draw{draw.Index}";
                        var previousCloth=_materials.Read(clothInstance,variant,actor.Initialization);
                        var cloth=variant.Prepare(clothInstance,new(callerSeconds-epoch.Start,actor.World,camera,new byte[6256],
                            previousCloth?.Pixel??new byte[80],previousCloth?.Flags??0,OpaqueRemakeRaster(),
                            new Dictionary<uint,RemakeArmyFlagRuntimeTexture>{[primary.ProxyObjectId]=primary},
                            materialInput.ExplicitSpecularW,materialInput.ExplicitSpecularSource,materialInput.ExplicitBackendAlphaEnabled,
                            materialInput.ExplicitForcedAlphaReference));
                        packets.Add(cloth.Packet);pending[clothInstance]=new(variant,actor.Initialization,cloth.NextCachedMaterialFlags,cloth.Packet.Pixel.Constants[0].ToArray());
                        selectedInterfaces.Add(clothInstance,ModelShaderResources.ResolveType16Interface(variant.Variation.ShaderOwner,
                            ModelRendererStateSourceReader.ReadQueue(context.TerrainFrame.Source.Graph,49).ShaderStateOffset));
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
                var previous=_materials.Read(instance,source,actor.Initialization);
                var prepared = source.Prepare(instance, new(callerSeconds - epoch.Start, actor.World, camera,
                    new byte[6256], previous?.Pixel ?? new byte[80], previous?.Flags ?? 0, OpaqueRemakeRaster(),
                    new Dictionary<uint, RemakeArmyFlagRuntimeTexture>()));
                packets.Add(prepared.Packet);
                selectedInterfaces.Add(instance,source.GraphSource.Interface);
                pending[instance] = new(source,actor.Initialization,prepared.NextCachedMaterialFlags, prepared.Packet.Pixel.Constants[0].ToArray());
                statuses.Add(new(unit, actor.DescriptorOffset, actor.OwnedNodeIdentity, draw.Index, true,
                    "complete original pole geometry/material/action0; four-influence source palette"));
            }
        }
        var submitted=currentNativeAdmission?.PrepareFamily(context,"ArmyFlag",packets,selectedInterfaces)??packets;
        var geometrySubmission=await _layer.RenderAsync(context.TerrainFrame, submitted);
        _materials.Commit(pending);
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
    private static ModelRasterInputs OpaqueRemakeRaster()
    {
        var blend = new ModelBlendAttachment(false, RenderingDevice.BlendFactor.One,
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
        await _layer.DisposeAsync(); _materials.Clear(); _epochs.Clear();
        await _sources.DisposeAsync(); Statuses = [];
    }
}
