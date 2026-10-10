using System.Buffers.Binary;
using System.Collections.ObjectModel;
using Godot;
using San14.Data;

/// <summary>Complete current CB owners and the remaining image/buffer/sampler
/// owners for one early shader. G-buffers, native GGX and constructed ambient
/// are supplied by the factory from the same accepted source frame.</summary>
public sealed record DeferredEarlySourcePassInputs(
    IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> CompleteCurrentConstants,
    IReadOnlyList<DeferredDeviceBinding> RemainingCurrentInputs,
    string CurrentParametersProducerProvenance)
{
    /// <summary>The current source owner must renew this frame-bound NULL
    /// authority even when the compiled specialization is reused.</summary>
    public DeferredEarlyCausticsSourceNull? EarlyCausticsSourceNull { get; init; }
}

public sealed record DeferredEarlySourceCompletedFrame(
    long FrameNumber, string CurrentProducerProvenance,
    DeferredAuxiliaryCurrentColorAttachment CurrentColorAttachment,
    IReadOnlyList<DeferredEarlySourceBin> CompletedBins,
    string Scope)
{
    public DeferredEarlySourceSubmittedFrame? Submission { get; internal init; }
}

public sealed record DeferredEarlySourceExecutedDraw(DeferredEarlySourceBin Bin,
    DeferredEarlyF103SelectedSource Source,DeferredEarlyCausticsSourceNull? Caustics);

public sealed record DeferredEarlyWorldPreparedPass(MapSourceCreatedNativeSessionPreparedFrame Accepted,
    MapSourceCreatedWorldF103Pass WorldPass,IReadOnlyList<DeferredEarlyF103SelectedSource> Selections);

/// <summary>Exact source selections and typed Caustics inputs retained only
/// after the owning early factory completes its actual ordered GPU draws.</summary>
public sealed class DeferredEarlySourceSubmittedFrame
{
    private readonly DeferredEarlySourcePixelFactory owner;
    private readonly NativeMapFrameContext context;
    internal DeferredEarlySourceSubmittedFrame(DeferredEarlySourcePixelFactory owner,
        NativeMapFrameContext context, IEnumerable<DeferredEarlyF103SelectedSource> sources,
        IDictionary<int, DeferredEarlyCausticsSourceNull> caustics,
        IEnumerable<DeferredEarlySourceExecutedDraw> draws)
    {
        this.owner = owner; this.context = context;
        ExecutedSources = Array.AsReadOnly(sources.ToArray());
        ExecutedDraws = Array.AsReadOnly(draws.ToArray());
        EarlyCausticsByNode = new ReadOnlyDictionary<int, DeferredEarlyCausticsSourceNull>(
            new Dictionary<int, DeferredEarlyCausticsSourceNull>(caustics));
    }
    public IReadOnlyList<DeferredEarlyF103SelectedSource> ExecutedSources { get; }
    public IReadOnlyList<DeferredEarlySourceExecutedDraw> ExecutedDraws { get; }
    public IReadOnlyDictionary<int, DeferredEarlyCausticsSourceNull> EarlyCausticsByNode { get; }
    public void RequireCurrent(NativeMapFrameContext actualContext,
        DeferredEarlySourceCompletedFrame actualCompletedFrame)
    {
        if (!ReferenceEquals(actualContext, context) || !ReferenceEquals(owner.LastCompletedFrame, actualCompletedFrame) ||
            !ReferenceEquals(actualCompletedFrame.Submission, this) || actualCompletedFrame.FrameNumber != context.FrameNumber ||
            ExecutedSources.Count == 0 || !actualCompletedFrame.CompletedBins.SequenceEqual(ExecutedDraws.Select(d=>d.Bin)) ||
            ExecutedDraws.Any(d=>d.Source.SourcePlan.Node.Index!=d.Bin.GraphNodeIndex||
                d.Source.Current.FrameNumber!=context.FrameNumber||!ExecutedSources.Contains(d.Source)))
            throw new InvalidDataException("Early source snapshot is not this exact owning factory's current actual completed draw roster.");
    }
}

/// <summary>Original nodes77–80, in graph order, drawing their exact selected
/// original pixel programs into source Begin73 color064B and existing E13
/// depth/stencil. Owns programs, dependent draws and a borrowed-parent FB.
/// Caustics and other current plugin owners remain explicit inputs. Retirement
/// must run before either borrowed target or source resource owner retires.</summary>
public sealed class DeferredEarlySourcePixelFactory : IAsyncDisposable
{
    private readonly IReadOnlyDictionary<int, DeferredPixelProgram> programs;
    private readonly IReadOnlyDictionary<int, DeferredEarlyF103SelectedSource> initial;
    private readonly SourceResourceRegistrationDevice? registration;
    private readonly Func<MapSourceCreatedNativeSessionPreparedFrame, SkyDirectionalAmbientDeviceBinding>? directionalAmbient;
    private readonly GgxAmbientLut ggx;
    private readonly MapSelectedSourceSamplerOwner samplers;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetime = new();
    private readonly Dictionary<(int Node, uint Reference,string Owner), DeferredDraw> draws = [];
    private readonly DeferredEarlyShaderDepthMirror shaderDepth = new();
    private RenderTargetDeviceTexture? color;
    private Rid depthParent, framebuffer;
    private bool retiring;
    private Task? disposal;
    public DeferredEarlySourceCompletedFrame? LastCompletedFrame { get; private set; }
    /// <summary>Optional numeric diagnostic observer of each actual World
    /// draw, before a later draw overwrites its output. Production leaves it
    /// unset; it supplies neither constants nor a completion receipt.</summary>
    public Func<DeferredEarlySourceExecutedDraw, DeferredDraw, Task>? AfterWorldDrawDiagnostic { get; set; }
    public IReadOnlyDictionary<int, DeferredPixelProgram> Programs => programs;

    private DeferredEarlySourcePixelFactory(Dictionary<int, DeferredPixelProgram> programs,
        Dictionary<int, DeferredEarlyF103SelectedSource> initial,
        SourceResourceRegistrationDevice? registration, GgxAmbientLut ggx,
        MapSelectedSourceSamplerOwner samplers,
        Func<MapSourceCreatedNativeSessionPreparedFrame, SkyDirectionalAmbientDeviceBinding>? directionalAmbient = null)
    {
        this.programs = new ReadOnlyDictionary<int, DeferredPixelProgram>(programs);
        this.initial = new ReadOnlyDictionary<int, DeferredEarlyF103SelectedSource>(initial);
        if ((registration is null) == (directionalAmbient is null))
            throw new ArgumentException("Early shaders require exactly one ambient resource owner.");
        this.registration = registration; this.ggx = ggx; this.samplers = samplers;
        this.directionalAmbient = directionalAmbient;
    }

    /// <summary>Build each distinct early program once. The late166 program is
    /// never relabeled as an early producer. The pinned Main node78 NULL
    /// Caustics read uses its typed source specialization.</summary>
    public static async Task<DeferredEarlySourcePixelFactory> BuildAsync(
        IReadOnlyList<DeferredEarlyF103SelectedSource> bootstrapSelections,
        Vkd3dShaderCompiler compiler, SourceResourceRegistrationDevice borrowedRegistration,
        GgxAmbientLut borrowedGgx, MapSelectedSourceSamplerOwner borrowedSamplers,
        DeferredEarlyCausticsSourceNull? bootstrapCausticsSourceNull = null)
    {
        ArgumentNullException.ThrowIfNull(borrowedRegistration);
        return await BuildCoreAsync(bootstrapSelections, compiler, borrowedRegistration,
            borrowedGgx, borrowedSamplers, null, bootstrapCausticsSourceNull);
    }

    /// <summary>Actual World Directionals use the current Q80 Sky wrapper.
    /// Its callback is read only when the selected early program consumes it.</summary>
    public static Task<DeferredEarlySourcePixelFactory> BuildDirectionalAsync(
        IReadOnlyList<DeferredEarlyF103SelectedSource> bootstrapSelections,
        Vkd3dShaderCompiler compiler, GgxAmbientLut borrowedGgx,
        MapSelectedSourceSamplerOwner borrowedSamplers,
        Func<MapSourceCreatedNativeSessionPreparedFrame, SkyDirectionalAmbientDeviceBinding> actualAmbient,
        DeferredEarlyCausticsSourceNull? bootstrapCausticsSourceNull = null)
    {
        ArgumentNullException.ThrowIfNull(actualAmbient);
        return BuildCoreAsync(bootstrapSelections, compiler, null, borrowedGgx,
            borrowedSamplers, actualAmbient, bootstrapCausticsSourceNull);
    }

    private static async Task<DeferredEarlySourcePixelFactory> BuildCoreAsync(
        IReadOnlyList<DeferredEarlyF103SelectedSource> bootstrapSelections,
        Vkd3dShaderCompiler compiler, SourceResourceRegistrationDevice? borrowedRegistration,
        GgxAmbientLut borrowedGgx, MapSelectedSourceSamplerOwner borrowedSamplers,
        Func<MapSourceCreatedNativeSessionPreparedFrame, SkyDirectionalAmbientDeviceBinding>? actualAmbient,
        DeferredEarlyCausticsSourceNull? bootstrapCausticsSourceNull)
    {
        ArgumentNullException.ThrowIfNull(bootstrapSelections); ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(borrowedGgx);
        ArgumentNullException.ThrowIfNull(borrowedSamplers);
        if (!bootstrapSelections.Select(s => s?.SourcePlan.Node.Index ?? -1).Order().SequenceEqual(new[] { 77, 78, 79, 80 }))
            throw new InvalidDataException("Early source factory requires the four exact ordered graph shader identities.");
        var originals = bootstrapSelections.ToDictionary(s => s.SourcePlan.Node.Index);
        var programs = new Dictionary<int, DeferredPixelProgram>();
        try
        {
            foreach (var selected in originals.OrderBy(p => p.Key).Select(p => p.Value))
            {

                if (selected.SourcePlan.Node.Index == 78)
                {
                    if (bootstrapCausticsSourceNull is null)
                        throw new NotSupportedException("The fresh Main early Caustics program requires its actual source NULL owner.");
                    programs.Add(78, await DeferredPixelProgram.BuildAsync(selected, compiler, bootstrapCausticsSourceNull));
                }
                else programs.Add(selected.SourcePlan.Node.Index, await DeferredPixelProgram.BuildAsync(
                    selected.SourcePlan.Shader, selected.Combination.Index, compiler));
            }
            return new(programs, originals, borrowedRegistration, borrowedGgx, borrowedSamplers, actualAmbient);
        }
        catch { foreach (var program in programs.Values.Reverse()) await program.DisposeAsync(); throw; }
    }

    /// <summary>Read each actual early source and select with this frame's owned
    /// feature plus actual raw View bank results. This method publishes no
    /// current query defaults, GPU descriptors or produced target contents.</summary>
    public static IReadOnlyList<DeferredEarlyF103SelectedSource> PrepareSelections(
        GameResourceRepository repository, MapSourceCreatedNativeSessionPreparedFrame frame,
        DeferredEarlyF103CurrentVariantInputs current)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(current);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var textures = repository.LoadObjects("system", 0x93FB3280);
        var references = RenderGraphShaderReferences.Read(graph, shaderObjects);
        return Array.AsReadOnly(new[] { 77, 78, 79, 80 }.Select(node =>
        {
            var reference = references.References.Single(r => r.Node.Index == node);
            var shader = repository.LoadShaderContainer("system", reference.ShaderFileHash);
            var source = DeferredTexturePlan.Read(graph, node, shaderObjects, textures, shader, 0);
            return DeferredEarlyF103SourceSelection.PrepareFreshShader(source, frame.Feature, frame.Lighting, current);
        }).ToArray());
    }

    /// <summary>The factory already owns the four parsed source plans. Only
    /// feature, lighting and View queries are selected again for this request.</summary>
    public IReadOnlyList<DeferredEarlyF103SelectedSource> PrepareCurrentSelections(
        MapSourceCreatedNativeSessionPreparedFrame frame,
        DeferredEarlyF103CurrentVariantInputs current)
        =>Array.AsReadOnly(new[]{77,78,79,80}.Select(node=>
            DeferredEarlyF103SourceSelection.PrepareFreshShader(
                initial[node].SourcePlan,frame.Feature,frame.Lighting,current)).ToArray());

    /// <summary>Preflight every admitted program, complete parameter packet and
    /// resource owner before original node75's color-only clear. A failed draw
    /// publishes no completion receipt. The scene's serial order protects the
    /// borrowed current color and existing geometry depth/stencil generation.</summary>
    public async Task<DeferredEarlySourceCompletedFrame> DrawAsync(
        MapSourceCreatedNativeSessionPreparedFrame accepted,
        MainMapContextCurrentPacket currentQ, MapSourceCreatedViewFieldsPacket currentView,
        IReadOnlyList<DeferredEarlyF103SelectedSource> currentSelections,
        byte? currentCommonViewAux3F2,
        Func<DeferredEarlyF103SelectedSource, Task<DeferredEarlySourcePassInputs>> prepareCurrentInputs)
    {
        ArgumentNullException.ThrowIfNull(accepted); ArgumentNullException.ThrowIfNull(currentQ);
        ArgumentNullException.ThrowIfNull(currentView); ArgumentNullException.ThrowIfNull(currentSelections);
        ArgumentNullException.ThrowIfNull(prepareCurrentInputs);
        await gate.WaitAsync();
        try
        {
            lock (lifetime) ObjectDisposedException.ThrowIf(retiring, this);
            LastCompletedFrame = null;
            string provenance = accepted.Selection.Current.CurrentProducerProvenance;
            if (accepted.Context.FrameNumber <= 0 || currentQ.FrameNumber != accepted.Context.FrameNumber ||
                currentView.Camera.FrameNumber != accepted.Context.FrameNumber ||
                currentView.Camera.CameraObjectId != accepted.MainCamera.Source.Camera.ObjectId ||
                currentView.ViewFlags19AC != accepted.Feature.CurrentView.ViewFlags19AC ||
                currentView.Binding.ViewIdentity != accepted.Feature.Binding.ViewIdentity ||
                currentView.Binding.CameraOwnerIdentity != accepted.Feature.Binding.CameraOwnerIdentity ||
                !currentSelections.Select(s => s.SourcePlan.Node.Index).Order().SequenceEqual(new[] { 77, 78, 79, 80 }))
                throw new InvalidDataException("Early draw requires one complete accepted Camera/View/Q source frame.");
            if ((accepted.Feature.CurrentView.AuxiliaryPresent == true) != currentCommonViewAux3F2.HasValue)
                throw new InvalidDataException("Early outer cache requires the current CommonView+240 presence and its actual byte3F2.");
            if (registration is not null) await registration.ValidateAsync();
            await PrepareTargetAsync(accepted.Context, currentSelections[0].SourcePlan.Graph);
            await shaderDepth.CopyAsync(accepted.Context.TerrainFrame.Textures[0xE13DDB4F], accepted.Context.FrameNumber,
                provenance, "same accepted Scene after successful ordered terrain/static/Army geometry; " +
                "logical source E13DDB4F; frame=" + accepted.Context.FrameNumber + ";" + provenance);
            var ordered = new List<(DeferredEarlySourceBin Bin, DeferredDraw Draw)>();
            var executedSources = new List<DeferredEarlyF103SelectedSource>();
            var executedCaustics = new Dictionary<int, DeferredEarlyCausticsSourceNull>();
            var executedDraws = new List<DeferredEarlySourceExecutedDraw>();
            foreach (var selected in currentSelections.OrderBy(s => s.SourcePlan.Node.Index))
            {
                selected.ValidateParameterFrame(accepted.MainCamera, accepted.Lighting);
                if (selected.Current.CurrentProducerProvenance != provenance ||
                    (selected.Current.ContextFlags3F0 ^ currentQ.ContextFlags3F0) != 0)
                    throw new InvalidDataException("Early shader selector differs from its exact current Q/producer boundary.");
                var first = initial[selected.SourcePlan.Node.Index];
                if (selected.Combination.Index != first.Combination.Index || selected.PixelProgramSha256 != first.PixelProgramSha256 ||
                    !selected.RequestedVariant96.Span.SequenceEqual(first.RequestedVariant96.Span))
                    throw new InvalidDataException("Early original program key changed; retire dependent draws and rebuild its program generation.");
                var bins = DeferredEarlySourceExecution.ReadBins(selected.SourcePlan, currentQ, currentView, currentCommonViewAux3F2);
                if (bins.Count == 0) continue;
                var parameters = await prepareCurrentInputs(selected) ?? throw new InvalidDataException("Early current parameter producer returned no packet.");
                ArgumentException.ThrowIfNullOrWhiteSpace(parameters.CurrentParametersProducerProvenance);
                var program = programs[selected.SourcePlan.Node.Index];
                ValidateCurrentCausticsAuthority(program, selected, parameters.EarlyCausticsSourceNull);
                executedSources.Add(selected);
                if (parameters.EarlyCausticsSourceNull is { } caustics)
                    executedCaustics.Add(selected.SourcePlan.Node.Index, caustics);
                var snapshot = parameters.CompleteCurrentConstants.ToDictionary(p => p.Key, p => (ReadOnlyMemory<byte>)p.Value.ToArray());
                var remaining = parameters.RemainingCurrentInputs.ToArray();
                var inputs = await ReadOwnedInputsAsync(accepted, selected, remaining);
                var target = new DeferredDrawTarget(framebuffer, [color!.StorageRid, depthParent],
                    selected.SourcePlan.Graph.Source.ResourceUri + "#Begin73/064B746B+E13DDB4F") { DepthStencilTexture = depthParent };
                foreach (var bin in bins)
                {
                    if (bin.CurrentLightOwnerIdentity != accepted.Lighting.Binding.LightOwnerIdentity)
                        throw new NotSupportedException("Another early light record requires its own current parameter frame.");
                    var key = (bin.GraphNodeIndex, bin.StencilReference,bin.CurrentLightOwnerIdentity);
                    if (draws.TryGetValue(key, out var old) && !SamePhysicalInputs(old.Inputs, inputs))
                    { draws.Remove(key); await old.DisposeAsync(); old = null; }
                    if (old is null)
                    {
                        old = await DeferredDraw.BuildAsync(program, target, snapshot, inputs,
                            new(RenderingDevice.PolygonCullMode.Disabled, false), new(bin.StencilReference));
                        draws.Add(key, old);
                    }
                    else await old.UpdateConstantsAsync(snapshot);
                    ordered.Add((bin, old));
                    executedDraws.Add(new(bin,selected,parameters.EarlyCausticsSourceNull));
                }
            }
            if (ordered.Count == 0)
                throw new NotSupportedException("No admitted early source F103 bin can certify a current color writer.");
            // Actual graph node75 Target0F, Color0, Flag2: clear color only.
            // Existing geometry depth and stencil are preserved for Equal tests.
            await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                long list = rd.DrawListBegin(framebuffer, RenderingDevice.DrawFlags.ClearColor0, [new Color(0, 0, 0, 0)]);
                rd.DrawListEnd(); return true;
            });
            foreach (var draw in ordered) await draw.Draw.DrawAsync();
            int lastNode = ordered[^1].Bin.GraphNodeIndex;
            var graph = currentSelections[0].SourcePlan.Graph;
            var receipt = DeferredAuxiliaryProductionReceipt.FromSourceWriter(graph, lastNode,
                accepted.Context.FrameNumber, provenance, "actual selected original early F103 VS/PS; source ordered bins; " +
                "RGBA/blend-off/depth-off/stencil-Equal-Keep; node75 color clear; explicit current plugin owners; " +
                "exact same-frame E13 depth transport for Godot sampling, actual E13 stencil attachment retained; " +
                "scope: nodes77–80 only, subsequent graph stages are separate producers", color!.Source.Object.ObjectId, [0]);
            var attachment = new DeferredAuxiliaryCurrentColorAttachment(color!, accepted.Context.FrameNumber,
                provenance, 0, "source Begin73 inherited color064B surface0; same-frame owned framebuffer; graph prefix/restored temporary targets", receipt);
            return LastCompletedFrame = new(accepted.Context.FrameNumber, provenance, attachment,
                Array.AsReadOnly(ordered.Select(item => item.Bin).ToArray()), receipt.SourceScope)
                { Submission = new(this, accepted.Context, executedSources, executedCaustics,executedDraws) };
        }
        finally { gate.Release(); }
    }

    /// <summary>Execute actual World early bins with each independent inner
    /// modifier's feature and parameter frame. Node75 clears once; completion
    /// records every physical draw rather than collapsing equal node IDs.
    /// An actual final59 sample view borrows the completed source depth copy;
    /// its target scope must remain current until these consumers finish.</summary>
    public async Task<DeferredEarlySourceCompletedFrame> DrawWorldAsync(
        MapSourceCreatedWorldF103Frames world,
        IReadOnlyList<DeferredEarlyWorldPreparedPass> currentPasses,
        NativeSceneGeometryStencilCompletedFrame actualSharedGeometry,
        Func<DeferredEarlyWorldPreparedPass,DeferredEarlyF103SelectedSource,
            Task<DeferredEarlySourcePassInputs>> prepareCurrentInputs,
        WorldGeometryDepthSamplingView? actual59Sampling = null)
    {
        ArgumentNullException.ThrowIfNull(world);ArgumentNullException.ThrowIfNull(currentPasses);
        ArgumentNullException.ThrowIfNull(actualSharedGeometry);
        ArgumentNullException.ThrowIfNull(prepareCurrentInputs);
        if(currentPasses.Count!=world.Passes.Count||currentPasses.Count==0)
            throw new InvalidDataException("World early execution needs every actual ordered pass.");
        var first=currentPasses[0].Accepted;
        actualSharedGeometry.ValidateCurrent(first,actualSharedGeometry.Population,actualSharedGeometry.Army,
            actualSharedGeometry.TerrainSubmission);
        if(actual59Sampling is not null)RequireActual59DepthSampling(actual59Sampling,first.Context);
        foreach(var pass in currentPasses)
        {
            if(!world.Passes.Any(p=>ReferenceEquals(p,pass.WorldPass))||
                !ReferenceEquals(pass.Accepted.PreparedSourceFrame,pass.WorldPass.Frame)||
                !ReferenceEquals(pass.Accepted.Context,first.Context)||
                !pass.Selections.Select(s=>s.SourcePlan.Node.Index).Order().SequenceEqual(new[]{77,78,79,80}))
                throw new InvalidDataException("World early execution has a foreign pass or incomplete node selections.");
        }
        await gate.WaitAsync();
        try
        {
            lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
            LastCompletedFrame=null;
            string provenance=first.Selection.Current.CurrentProducerProvenance;
            var common=world.Passes[0].Frame.Common.ViewGroupSelector;
            var q=world.Passes[0].Frame.Context;
            await PrepareTargetAsync(first.Context,currentPasses[0].Selections[0].SourcePlan.Graph);
            DeferredEarlyShaderDepthPublication? sharedTransport=null;
            if(actual59Sampling is null)
            {
                string transportProducer="actual-owned-World-geometry-E13/"+world.View.Binding.ViewIdentity+
                    "/attempt="+first.Context.FrameNumber+"/parent="+first.Context.TerrainFrame.Textures[0xE13DDB4F].StorageRid.Id;
                sharedTransport=await shaderDepth.CopyAsync(first.Context.TerrainFrame.Textures[0xE13DDB4F],first.Context.FrameNumber,
                    transportProducer,"actual private terrain/static/Army completed geometry receipt; same Scene context and logical E13");
            }
            else RequireActual59DepthSampling(actual59Sampling,first.Context);
            var ordered=new List<(DeferredEarlySourceBin Bin,DeferredDraw Draw)>();
            var sources=new List<DeferredEarlyF103SelectedSource>();
            var caustics=new Dictionary<int,DeferredEarlyCausticsSourceNull>();
            var receipts=new List<DeferredEarlySourceExecutedDraw>();
            foreach(int node in new[]{77,78,79,80})
            {
                var nodeSource=currentPasses[0].Selections.Single(s=>s.SourcePlan.Node.Index==node).SourcePlan;
                foreach(var actual in DeferredEarlySourceExecution.ReadWorldBins(nodeSource,q,world.View,common,world))
                {
                    var pass=currentPasses.Single(p=>ReferenceEquals(p.WorldPass,actual.Pass));
                    var accepted=pass.Accepted;
                    var selected=pass.Selections.Single(s=>s.SourcePlan.Node.Index==node);
                    selected.ValidateParameterFrame(accepted.MainCamera,accepted.Lighting);
                    if(!ReferenceEquals(selected.OwnedFeatureSource,accepted.Feature)||
                        selected.Current.CurrentProducerProvenance!=accepted.Selection.Current.CurrentProducerProvenance||
                        selected.Current.ContextFlags3F0!=q.ContextFlags3F0)
                        throw new InvalidDataException("World early selection is not its actual pass feature/Q frame.");
                    var bootstrap=initial[node];
                    if(selected.Combination.Index!=bootstrap.Combination.Index||selected.PixelProgramSha256!=bootstrap.PixelProgramSha256||
                        !selected.RequestedVariant96.Span.SequenceEqual(bootstrap.RequestedVariant96.Span))
                        throw new InvalidDataException("World early program changed; rebuild the source program generation.");
                    var parameters=await prepareCurrentInputs(pass,selected)??throw new InvalidDataException("World early parameters are unavailable.");
                    var program=programs[node];ValidateCurrentCausticsAuthority(program,selected,parameters.EarlyCausticsSourceNull);
                    var constants=parameters.CompleteCurrentConstants.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
                    actualSharedGeometry.ValidateCurrent(first,actualSharedGeometry.Population,actualSharedGeometry.Army,
                        actualSharedGeometry.TerrainSubmission);
                    var inputs=await ReadOwnedInputsAsync(accepted,selected,parameters.RemainingCurrentInputs.ToArray(),sharedTransport,actual59Sampling);
                    var target=new DeferredDrawTarget(framebuffer,[color!.StorageRid,depthParent],
                        selected.SourcePlan.Graph.Source.ResourceUri+"#Begin73/actual-World-early"){DepthStencilTexture=depthParent};
                    var bin=actual.Bin;var key=(node,bin.StencilReference,bin.CurrentLightOwnerIdentity);
                    if(draws.TryGetValue(key,out var draw)&&!SamePhysicalInputs(draw.Inputs,inputs))
                    {draws.Remove(key);await draw.DisposeAsync();draw=null;}
                    if(draw is null)
                    {draw=await DeferredDraw.BuildAsync(program,target,constants,inputs,
                        new(RenderingDevice.PolygonCullMode.Disabled,false),new(bin.StencilReference));draws.Add(key,draw);}
                    else await draw.UpdateConstantsAsync(constants);
                    ordered.Add((bin,draw));sources.Add(selected);receipts.Add(new(bin,selected,parameters.EarlyCausticsSourceNull));
                    if(parameters.EarlyCausticsSourceNull is { } authority)caustics.TryAdd(node,authority);
                }
            }
            if(ordered.Count==0)throw new NotSupportedException("World early execution has no admitted actual color writer.");
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(actual59Sampling is not null)RequireActual59DepthSampling(actual59Sampling,first.Context);
                rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.ClearColor0,[new Color(0,0,0,0)]);rd.DrawListEnd();return true;
            });
            for (int index = 0; index < ordered.Count; index++)
            {
                if(actual59Sampling is not null)RequireActual59DepthSampling(actual59Sampling,first.Context);
                await ordered[index].Draw.DrawAsync();
                if (AfterWorldDrawDiagnostic is { } observe)
                    await observe(receipts[index], ordered[index].Draw);
            }
            if(actual59Sampling is not null)RequireActual59DepthSampling(actual59Sampling,first.Context);
            var receipt=DeferredAuxiliaryProductionReceipt.FromSourceWriter(
                currentPasses[0].Selections[0].SourcePlan.Graph,ordered[^1].Bin.GraphNodeIndex,first.Context.FrameNumber,
                provenance,"actual World early node-order/independent-inner-order; one node75 clear; every physical draw retained",
                color!.Source.Object.ObjectId,[0]);
            var attachment=new DeferredAuxiliaryCurrentColorAttachment(color!,first.Context.FrameNumber,provenance,0,
                "actual World source Begin73 current color",receipt);
            return LastCompletedFrame=new(first.Context.FrameNumber,provenance,attachment,
                Array.AsReadOnly(ordered.Select(d=>d.Bin).ToArray()),receipt.SourceScope)
                {Submission=new(this,first.Context,sources,caustics,receipts)};
        }
        finally{gate.Release();}
    }

    private static void ValidateCurrentCausticsAuthority(DeferredPixelProgram program,
        DeferredEarlyF103SelectedSource selected, DeferredEarlyCausticsSourceNull? current)
    {
        if (program.EarlyCausticsNullSpecialization is { } compiled)
        {
            if (current is null)
                throw new NotSupportedException("The current early Caustics pass has no frame-bound source NULL authority.");
            current.Validate(selected);
            if (current.SourceOwnerIdentity != compiled.SourceNull.SourceOwnerIdentity ||
                current.ShaderSourceSha256 != compiled.SourceNull.ShaderSourceSha256 ||
                current.PixelProgramSha256 != compiled.SourceNull.PixelProgramSha256)
                throw new InvalidDataException("Current Caustics NULL authority differs from the compiled source owner generation.");
        }
        else if (current is not null || selected.SourcePlan.Node.Index == 78)
            throw new NotSupportedException("The admitted early Caustics pass has no matching source-specialized program.");
    }

    private async Task<IReadOnlyList<DeferredDeviceBinding>> ReadOwnedInputsAsync(
        MapSourceCreatedNativeSessionPreparedFrame frame, DeferredEarlyF103SelectedSource selected,
        IReadOnlyList<DeferredDeviceBinding> remaining,
        DeferredEarlyShaderDepthPublication? sharedGeometryTransport=null,
        WorldGeometryDepthSamplingView? actual59Sampling=null)
    {
        if(actual59Sampling is not null)RequireActual59DepthSampling(actual59Sampling,frame.Context);
        if(sharedGeometryTransport is not null&&(!ReferenceEquals(shaderDepth.Current,sharedGeometryTransport)||
            sharedGeometryTransport.FrameNumber!=frame.Context.FrameNumber||
            !ReferenceEquals(sharedGeometryTransport.LogicalSource,frame.Context.TerrainFrame.Textures[0xE13DDB4F])))
            throw new InvalidDataException("World early shared depth does not own this exact logical E13/Scene attempt.");
        var source = selected.ReadSelectedTexturePlan();
        var inputs = (await DeferredGbufferInputs.ReadAsync(source, frame.Context.TerrainFrame))
            .Select(row =>
            {
                var logical = frame.Context.TerrainFrame.Textures[row.Source.Texture.Object.ObjectId];
                if (row.Source.Texture.Object.ObjectId == 0xE13DDB4F)
                {
                    if(actual59Sampling is not null)
                        return new DeferredDeviceBinding("Texture",row.Source.PixelBinding.BindPoint,
                            actual59Sampling.ShaderView,actual59Sampling.ShaderView,
                            row.Source.Texture.Source.Source.ResourceUri+"#E13DDB4F;"+actual59Sampling.SourceEvidence);
                    var copy = shaderDepth.RequireCurrent(logical, frame.Context.FrameNumber,
                        sharedGeometryTransport?.FrameProducerProvenance??frame.Selection.Current.CurrentProducerProvenance);
                    return new DeferredDeviceBinding("Texture", row.Source.PixelBinding.BindPoint,
                        copy.ShaderViewRid, copy.TransportParent,
                        row.Source.Texture.Source.Source.ResourceUri + "#E13DDB4F;" + copy.BackendTransportProvenance);
                }
                return new DeferredDeviceBinding("Texture", row.Source.PixelBinding.BindPoint, row.ShaderView,
                    logical.StorageRid, row.Source.Texture.Source.Source.ResourceUri + "#" + row.Source.Texture.Object.ObjectId.ToString("X8"));
            }).ToList();
        var definitions = selected.PixelDefinitions;
        void Image(string name, Rid view, Rid parent, string identity)
        {
            var declaration = definitions.Bindings.SingleOrDefault(b => b.InputType == 2 && b.Name.Text == name);
            if (declaration is not null) inputs.Add(new("Texture", declaration.BindPoint, view, parent, identity));
        }
        void Sampler(string name, Rid rid, string identity)
        {
            var declaration = definitions.Bindings.SingleOrDefault(b => b.InputType == 3 && b.Name.Text == name);
            if (declaration is not null) inputs.Add(new("Sampler", declaration.BindPoint, rid, null, identity));
        }
        Image("sGxAmbLut", ggx.TextureRid, ggx.TextureRid, "source native GGX original pixel producer:" + ggx.Translation.SourceProgramSha256);
        Sampler("__smpsGxAmbLut", ggx.SamplerRid, "source native GGX sampler generation");
        if (registration is not null)
        {
            var cube = registration.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
            if (BinaryPrimitives.ReadUInt64LittleEndian(frame.Feature.EffectiveLocalLightData.Span.Slice(0xFE0, 8)) != cube.TextureRid.Id)
                throw new InvalidDataException("Early ambient resource differs from its current constructed kind1 generation.");
            Image("sAmbSpc", cube.TextureRid, cube.TextureRid, cube.OwnerProvenance + ";current-LightDataFE0");
            var ambient = frame.Samplers.ImagePlan.Samplers.Single(s => s.KeySource.Role == "AmbientSpecular");
            var ambientBinding = samplers.GetBindings(frame.Samplers).Single(b => b.SourceBindPoint == ambient.Declaration.BindPoint);
            Sampler("__smpsAmbSpc", ambientBinding.Resource, ambientBinding.SourceResourceUri);
        }
        else if (definitions.Bindings.Any(b => b.Name.Text is "sAmbSpc" or "__smpsAmbSpc"))
        {
            var ambient = directionalAmbient!(frame)
                ?? throw new InvalidDataException("Current Directional Sky returned no owned ambient wrapper.");
            ambient.State.ValidateCurrent(ambient.State.OwnedSkyIdentity, frame.Context.FrameNumber);
            if (!ambient.HasCompleteGeneratedContents || ambient.Texture.DescriptorKind != "Texture" ||
                ambient.Sampler.DescriptorKind != "Sampler" || !ambient.Texture.Resource.IsValid ||
                !ambient.Sampler.Resource.IsValid || ambient.Texture.Resource != ambient.Cube.TextureRid ||
                ambient.Texture.OwnerTexture != ambient.Cube.TextureRid)
                throw new InvalidOperationException("Early Directional ambient read requires the actual selected generated Sky wrapper.");
            Image("sAmbSpc", ambient.Texture.Resource, ambient.Cube.TextureRid, ambient.Texture.SourceResourceUri);
            Sampler("__smpsAmbSpc", ambient.Sampler.Resource, ambient.Sampler.SourceResourceUri);
        }
        inputs.AddRange(remaining);
        if (inputs.Select(i => (i.DescriptorKind, i.SourceBindPoint)).Distinct().Count() != inputs.Count)
            throw new InvalidDataException("Early current resource owners duplicate an owned source role.");
        return inputs.AsReadOnly();
    }

    private static void RequireActual59DepthSampling(WorldGeometryDepthSamplingView actual,
        NativeMapFrameContext context)
    {
        actual.RequireCurrent();
        var depth=context.TerrainFrame.Textures[0xE13DDB4F];
        if(!ReferenceEquals(actual.Owner.RequireFinal59SamplingView(),actual)||
            !ReferenceEquals(actual.Accepted.Context,context)||actual.PrimaryResource!=depth.StorageRid||
            actual.Extent!=depth.ExplicitExtent)
            throw new InvalidDataException("Early borrowed depth requires this current scope's actual final59 copy of the same E13 parent.");
    }

    private async Task PrepareTargetAsync(NativeMapFrameContext context, LoadedRenderGraph graph)
    {
        if (!context.OutputFrame.Textures.TryGetValue(0x064B746B, out var nextColor) ||
            !context.TerrainFrame.Textures.TryGetValue(0xE13DDB4F, out var nextDepth) ||
            nextColor.ExplicitExtent != nextDepth.ExplicitExtent || nextColor.EffectiveSource.LevelNum != 1 || nextDepth.EffectiveSource.LevelNum != 1 ||
            nextColor.TargetFormat != RenderTargetFrame.ViewFormat(RenderTextureFormatMath.Resolve(
                nextColor.EffectiveSource.PixelFormat,nextColor.EffectiveSource.ColorSpace,nextColor.EffectiveSource.OutputColorSpace).TargetViewFormat) ||
            nextColor.TargetFormat is RenderingDevice.DataFormat.D32Sfloat or RenderingDevice.DataFormat.D32SfloatS8Uint ||
            nextDepth.TargetFormat != RenderingDevice.DataFormat.D32SfloatS8Uint)
            throw new InvalidDataException("Early Begin73 requires the actual compatible color064B and geometry E13 depth/stencil owners.");
        var source = RenderGraphTargetResources.Read(graph, 73, context.OutputFrame.Source.TextureObjects);
        if (source.ColorTargets is not [{ ObjectNameHash: 0x064B746B, SurfaceId: 0 }] ||
            source.DepthObjectNameHash != 0xE13DDB4F || source.DepthSurfaceId != 0)
            throw new InvalidDataException("Early framebuffer declaration changed from original Begin73.");
        if (color?.StorageRid == nextColor.StorageRid && depthParent == nextDepth.StorageRid && framebuffer.IsValid) return;
        await RetireTargetsCoreAsync();
        color = nextColor; depthParent = nextDepth.StorageRid;
        framebuffer = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            Rid fb = rd.FramebufferCreate([nextColor.StorageRid, nextDepth.StorageRid]);
            if (!rd.FramebufferIsValid(fb)) throw new InvalidDataException("Device rejected original early color/depth-stencil attachment pairing.");
            return fb;
        });
    }

    private static bool SamePhysicalInputs(IReadOnlyList<DeferredDeviceBinding> a, IReadOnlyList<DeferredDeviceBinding> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First.DescriptorKind == pair.Second.DescriptorKind &&
            pair.First.SourceBindPoint == pair.Second.SourceBindPoint && pair.First.Resource == pair.Second.Resource && pair.First.OwnerTexture == pair.Second.OwnerTexture);

    public async Task RetireTargetsAsync()
    { await gate.WaitAsync(); try { await RetireTargetsCoreAsync(); } finally { gate.Release(); } }
    private async Task RetireTargetsCoreAsync()
    {
        LastCompletedFrame = null;
        foreach (var draw in draws.Values.Reverse()) await draw.DisposeAsync(); draws.Clear();
        await shaderDepth.RetireAsync();
        Rid old = framebuffer; framebuffer = default; color = null; depthParent = default;
        if (old.IsValid) await TerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(old); return true; });
    }
    public ValueTask DisposeAsync()
    { lock (lifetime) { retiring = true; return new(disposal ??= RetireAsync()); } }
    private async Task RetireAsync()
    { await RetireTargetsAsync(); await shaderDepth.DisposeAsync(); foreach (var program in programs.Values.Reverse()) await program.DisposeAsync(); }
}
