using Godot;
using San14.Data;
using System.Diagnostics;
using System.Security.Cryptography;

public sealed record OriginalNativeTerrainBatch(bool Coarse,IReadOnlyList<TerrainPatchInput> Patches);
public sealed record OriginalNativeDeferredPacket(IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> Constants,
    IReadOnlyList<OriginalDeferredDeviceBinding> Inputs,OriginalDeferredRasterState Raster,
    OriginalDeferredTranslucencyViewportInputs? TemporaryTranslucencyViewport=null)
{
    /// <summary>Apply restored named scene writers to an explicit complete
    /// provider packet. Unknown fields and the two shadow CBs remain retained.
    /// Callers still supply coherent source inputs and all resource producers.</summary>
    public OriginalNativeDeferredPacket ApplySceneFields(OriginalDeferredPixelProgram program,
        OriginalDeferredSceneInputs sceneInputs,out OriginalDeferredSceneConstantPacket writes)
    {
        ArgumentNullException.ThrowIfNull(program);
        if(!Constants.TryGetValue(0,out var template))throw new InvalidDataException("Deferred packet has no complete cb0 template.");
        writes=program.SourceSelection is { } selected
            ? OriginalDeferredSelectedSceneConstants.Apply(selected,template.Span,sceneInputs)
            : OriginalDeferredSceneConstants.Apply(program.DescriptorBindings.OriginalDefinitions,template.Span,sceneInputs);
        var next=Constants.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        next[0]=writes.Bytes.ToArray();
        return this with{Constants=next,TemporaryTranslucencyViewport=sceneInputs.TranslucencyViewport??TemporaryTranslucencyViewport};
    }
}
/// <summary>FrameNumber identifies an accepted render attempt. It increases
/// even when a later stage fails, so source owners can retire partial work.
/// OriginalNativeMapScene.FrameNumber separately counts completed frames.</summary>
public sealed record OriginalNativeMapFrameContext(OriginalRenderTargetFrame TerrainFrame,
    OriginalRenderTargetFrame OutputFrame,long FrameNumber,OriginalDeferredCameraInputs? PreparedCamera=null)
{
    /// <summary>The detached terrain batches accepted for this attempt. Source
    /// consumers use the same patch membership and coarse/detail selection as
    /// the geometry producer, including when a later stage rejects the frame.</summary>
    public IReadOnlyList<OriginalNativeTerrainBatch>? AcceptedTerrainBatches { get; init; }
    public LoadedTerrainGbiMapResources? AcceptedTerrainMaterial { get; init; }
}
public sealed record OriginalNativeMapTargetContext(OriginalRenderTargetFrame TerrainFrame,
    OriginalRenderTargetFrame OutputFrame);
/// <summary>Wall time of the actual serial scene stages, including awaited
/// render-thread scheduling. This does not insert GPU fences or readbacks.</summary>
public sealed record OriginalNativeMapStageTiming(string Stage,double Milliseconds,double ElapsedMilliseconds);

/// <summary>Borrowed scene stages execute under the native scene's serial gate.
/// Models and extra producers consume the same accepted frame and camera.
/// Retire framebuffer-dependent draws/views before their parent targets are
/// freed. Callbacks must not re-enter this scene's render/resize/dispose API.
/// Source programs and assets remain owned by the stage owner.</summary>
public sealed record OriginalNativeMapSceneStages(
    Func<OriginalNativeMapFrameContext,Task>? AfterTerrain=null,
    Func<OriginalNativeMapTargetContext,Task>? BeforeTargetRetirement=null,
    Func<OriginalNativeMapFrameContext,Task<OriginalRenderOutputPresentation?>>? AfterDeferred=null,
    Func<OriginalNativeMapFrameContext,Task>? BeforeFrame=null)
{
    /// <summary>Resolve current raster after CPU preparation and before GPU
    /// work. A source recipe still requires its independent execution owner.</summary>
    public Func<OriginalNativeMapFrameContext,TerrainGbufferRasterState,Task<TerrainGbufferRasterState>>? PrepareTerrainRaster { get; init; }
    /// <summary>Ordinary model producers run after the accepted geometry
    /// attachments are cleared, before terrain. Terrain then loads them.</summary>
    public Func<OriginalNativeMapFrameContext,Task>? BeforeTerrain { get; init; }
}

/// <summary>Scene-scoped original terrain/deferred device resources. Serializes
/// camera/LOD rendering, resize and retirement; no native packet is guessed.
/// A caller supplies all extra producers and a complete deferred packet for
/// each frame before the output can be submitted. Main-map enablement and
/// model/territory composition remain with the scene integration.</summary>
public sealed class OriginalNativeMapScene : IAsyncDisposable
{
    public OriginalTerrainGbufferTextures TerrainTextures { get; }
    public OriginalTerrainGbufferProgram DetailProgram { get; }
    public OriginalTerrainGbufferProgram CoarseProgram { get; }
    public OriginalDeferredPixelProgram DeferredProgram { get; }
    public OriginalRenderTargetFrame TerrainFrame { get; private set; }
    public OriginalRenderTargetFrame OutputFrame { get; private set; }
    public OriginalRenderOutputPresentation? Presentation { get; private set; }
    public bool RequiresExternalPresentation { get; }
    public bool HasDisplayPresentation => _externalPresentation is not null || Presentation is not null;
    /// <summary>Completed postprocess output when supplied by the borrowed
    /// source stage; otherwise the original deferred output. The stage owns
    /// its presentation and retires it before parent targets are freed.</summary>
    public OriginalRenderOutputPresentation DisplayPresentation => _externalPresentation ?? Presentation ??
        throw new InvalidOperationException("The required final presentation has not completed.");
    private OriginalRenderOutputPresentation? _externalPresentation;
    private OriginalRenderColorPass _outputPass;
    public long FrameNumber { get; private set; }
    public OriginalNativeMapFrameContext? LastCompletedContext { get; private set; }
    public IReadOnlyList<OriginalNativeMapStageTiming> LastCompletedTimings { get; private set; } = [];
    public long RenderAttemptNumber { get; private set; }
    private OriginalTerrainGeometrySubmittedFrame? _lastTerrainSubmittedFrame;
    public OriginalTerrainGeometrySubmittedFrame? LastTerrainSubmittedFrame => Volatile.Read(ref _lastTerrainSubmittedFrame);
    private OriginalNativeMapWorldDeferredSubmittedFrame? _lastWorldDeferredSubmittedFrame;
    public OriginalNativeMapWorldDeferredSubmittedFrame? LastWorldDeferredSubmittedFrame=>Volatile.Read(ref _lastWorldDeferredSubmittedFrame);
    internal void RequireCurrentWorldDeferredSubmission(OriginalNativeMapWorldDeferredSubmittedFrame receipt,
        OriginalNativeMapFrameContext context)
    {
        lock(_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            if(!ReferenceEquals(LastWorldDeferredSubmittedFrame,receipt)||!ReferenceEquals(receipt.Context,context)||
                !ReferenceEquals(context.TerrainFrame,TerrainFrame)||!ReferenceEquals(context.OutputFrame,OutputFrame)||
                context.FrameNumber!=RenderAttemptNumber)
                throw new InvalidDataException("Late World publication was failed, replaced, retired or belongs to another attempt/target.");
        }
    }
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly List<(OriginalNativeTerrainBatch Batch,OriginalTerrainGbufferDraw Draw)> _batches=[];
    private OriginalDeferredDraw? _deferred;
    private OriginalDeferredRasterState? _deferredRaster;
    private TerrainGbufferRasterState? _terrainRaster;
    private readonly object _lifetimeGate=new();
    private Task? _disposal;
    private bool _retiring;
    private readonly OriginalNativeMapSceneStages? _stages;
    private readonly bool _ownsDeferredProgram;

    private OriginalNativeMapScene(OriginalTerrainGbufferTextures textures,OriginalTerrainGbufferProgram detail,
        OriginalTerrainGbufferProgram coarse,OriginalDeferredPixelProgram deferred,OriginalRenderTargetFrame terrain,
        OriginalRenderTargetFrame output,OriginalRenderOutputPresentation? presentation,OriginalRenderColorPass outputPass,
        OriginalNativeMapSceneStages? stages,bool ownsDeferredProgram,bool requiresExternalPresentation)
        =>(TerrainTextures,DetailProgram,CoarseProgram,DeferredProgram,TerrainFrame,OutputFrame,Presentation,_outputPass,_stages,_ownsDeferredProgram,RequiresExternalPresentation)=
            (textures,detail,coarse,deferred,terrain,output,presentation,outputPass,stages,ownsDeferredProgram,requiresExternalPresentation);

    /// <summary>Compile/upload once for this source identity. The compiler is
    /// borrowed only during construction; no original assets are packaged.</summary>
    public static async Task<OriginalNativeMapScene> BuildAsync(LoadedTerrainGbiMapResources material,
        LoadedTerrainInitialHeightResources height,LoadedRenderGraphTargets terrainTargets,
        LoadedRenderGraphTargets outputTargets,LoadedShaderContainer deferredSource,int explicitCombination,
        IReadOnlyDictionary<uint,Vector2I> terrainExtents,IReadOnlyDictionary<uint,Vector2I> outputExtents,
        Vkd3dShaderCompiler compiler,bool allowReadback=false,OriginalNativeMapSceneStages? stages=null,
        OriginalDeferredPixelProgram? borrowedDeferredProgram=null,
        OriginalRenderTextureResolutionFrame? resolvedTargets=null,bool requireExternalPresentation=false)
    {
        if(outputTargets.ColorTargets.Count!=1 ||
            outputTargets.ColorTargets[0].Texture.Kind!=OriginalRenderTextureKind.Texture2D)
            throw new InvalidDataException("Native map currently presents one explicit 2D color output.");
        if(requireExternalPresentation && stages?.AfterDeferred is null)
            throw new InvalidDataException("A scene without raw presentation requires an actual final output stage.");
        if(borrowedDeferredProgram is not null)
        {
            var source=deferredSource.Source;
            var borrowed=borrowedDeferredProgram.Source.Source;
            var selected=deferredSource.ReadG1ResourceBindings(explicitCombination).Combination;
            var pixel=selected.Stages.Single(s=>s.StageCode==0&&s.Entry is not null).Entry!;
            string sourceHash=Convert.ToHexString(SHA256.HashData(source.Bytes));
            if(!borrowedDeferredProgram.ShaderRid.IsValid || source.ResourceUri!=borrowed.ResourceUri ||
                sourceHash!=source.DecodedSha256 || sourceHash!=borrowed.DecodedSha256 ||
                !source.Bytes.AsSpan().SequenceEqual(borrowed.Bytes) ||
                selected.Index!=borrowedDeferredProgram.Combination.Index ||
                !selected.RawRow.AsSpan().SequenceEqual(borrowedDeferredProgram.Combination.RawRow) ||
                Convert.ToHexString(SHA256.HashData(deferredSource.Container.GetProgramBytes(source.Bytes,pixel.ProgramIndex)))!=
                    borrowedDeferredProgram.OriginalTranslation.SourceProgramSha256)
                throw new InvalidDataException("Borrowed deferred program must retain the same source bytes and selected original combination.");
        }
        var owned=new List<IAsyncDisposable>();
        try
        {
            var textures=await OriginalTerrainGbufferTextures.BuildAsync(material,height);owned.Add(textures);
            var detail=await OriginalTerrainGbufferProgram.BuildAsync(material,false,compiler);owned.Add(detail);
            var coarse=await OriginalTerrainGbufferProgram.BuildAsync(material,true,compiler);owned.Add(coarse);
            var deferred=borrowedDeferredProgram;
            if(deferred is null)
            {
                deferred=await OriginalDeferredPixelProgram.BuildAsync(deferredSource,explicitCombination,compiler);
                owned.Add(deferred);
            }
            // Source ScreenMap reads the preceding G-buffer while writing
            // selected attachments. Its stage copies sampled inputs first.
            RequireSeparateTargetOwners(terrainTargets,outputTargets,resolvedTargets,resolvedTargets);
            var terrain=await OriginalRenderTargetFrame.BuildAsync(terrainTargets,terrainExtents,allowReadback,allowGpuCopy:true,resolutions:resolvedTargets);owned.Add(terrain);
            var output=await OriginalRenderTargetFrame.BuildAsync(outputTargets,outputExtents,allowReadback,allowGpuCopy:true,resolutions:resolvedTargets);owned.Add(output);
            var outputPass=await OriginalRenderColorPass.BuildAsync(output);owned.Add(outputPass);
            OriginalRenderOutputPresentation? present=null;
            if(!requireExternalPresentation)
            {
                present=await OriginalRenderOutputPresentation.BuildAsync(output.Textures[outputTargets.ColorTargets[0].Texture.Object.ObjectId]);owned.Add(present);
            }
            return new(textures,detail,coarse,deferred,terrain,output,present,outputPass,stages,borrowedDeferredProgram is null,requireExternalPresentation);
        }
        catch{for(int i=owned.Count-1;i>=0;i--)await owned[i].DisposeAsync();throw;}
    }

    /// <summary>One camera packet is shared by all terrain groups. The callback
    /// runs after terrain and must prepare every extra source layer/provider
    /// used by the chosen original deferred branch. It owns its input resources
    /// until the cached draw is retired or replaced. Clear only the first group.</summary>
    public Task RenderAsync(IReadOnlyList<OriginalNativeTerrainBatch> batches,OriginalDeferredCameraInputs preparedCamera,
        TerrainGbufferDrawInputs terrainParameters,TerrainGbufferRasterState terrainRaster,Color[] clearColors,
        float clearDepth,uint clearStencil,Func<OriginalNativeMapFrameContext,Task<OriginalNativeDeferredPacket>> prepareDeferred,
        RenderingDevice.DrawFlags geometryClearActions=RenderingDevice.DrawFlags.ClearAll)
    {
        ArgumentNullException.ThrowIfNull(preparedCamera);ArgumentNullException.ThrowIfNull(prepareDeferred);
        // Validate the shared full matrices before any terrain submission.
        _=OriginalNativeMatrixMath.Invert(preparedCamera.SourceWorldToView,out _);
        _=OriginalNativeMatrixMath.Invert(preparedCamera.SourceWorldToProjection,out _);
        var terrainCamera=OriginalDeferredSceneConstants.PrepareTerrainCamera(preparedCamera,terrainParameters);
        return RenderCoreAsync(batches,terrainCamera,terrainRaster,clearColors,clearDepth,clearStencil,async context=>
        {
            var packet=await prepareDeferred(context)??
                throw new InvalidDataException("Native map requires a complete explicit deferred packet.");
            // Terrain and final shading consume the SAME immutable camera
            // fields even when the provider's retained template is older.
            return (IReadOnlyList<OriginalNativeDeferredPacket>)new[] { packet.ApplySceneFields(DeferredProgram,new(Camera:preparedCamera),out _) };
        },preparedCamera,geometryClearActions);
    }

    public Task RenderAsync(IReadOnlyList<OriginalNativeTerrainBatch> batches,TerrainGbufferDrawInputs camera,
        TerrainGbufferRasterState terrainRaster,Color[] clearColors,float clearDepth,uint clearStencil,
        Func<OriginalNativeMapFrameContext,Task<OriginalNativeDeferredPacket>> prepareDeferred,
        RenderingDevice.DrawFlags geometryClearActions=RenderingDevice.DrawFlags.ClearAll)
    {
        ArgumentNullException.ThrowIfNull(prepareDeferred);
        return RenderCoreAsync(batches,camera,terrainRaster,clearColors,clearDepth,clearStencil,
            async context => new[] { await prepareDeferred(context) },null,geometryClearActions);
    }

    /// <summary>Submit the actual ordered World F103 passes against the same
    /// geometry attachments. Presentation and final stages commit only after
    /// every pass completes. Raster and blending remain source-owned inputs.</summary>
    public Task RenderOrderedAsync(IReadOnlyList<OriginalNativeTerrainBatch> batches,
        OriginalDeferredCameraInputs preparedCamera, TerrainGbufferDrawInputs terrainParameters,
        TerrainGbufferRasterState terrainRaster, Color[] clearColors, float clearDepth, uint clearStencil,
        Func<OriginalNativeMapFrameContext,Task<IReadOnlyList<OriginalNativeDeferredPacket>>> prepareDeferred,
        RenderingDevice.DrawFlags geometryClearActions=RenderingDevice.DrawFlags.ClearAll)
    {
        ArgumentNullException.ThrowIfNull(preparedCamera); ArgumentNullException.ThrowIfNull(prepareDeferred);
        _=OriginalNativeMatrixMath.Invert(preparedCamera.SourceWorldToView,out _);
        _=OriginalNativeMatrixMath.Invert(preparedCamera.SourceWorldToProjection,out _);
        var terrainCamera=OriginalDeferredSceneConstants.PrepareTerrainCamera(preparedCamera,terrainParameters);
        return RenderCoreAsync(batches,terrainCamera,terrainRaster,clearColors,clearDepth,clearStencil,async context=>
        {
            var packets=await prepareDeferred(context)??throw new InvalidDataException("Native map requires explicit ordered deferred packets.");
            if(packets.Count==0||packets.Any(p=>p is null))
                throw new InvalidDataException("Ordered deferred submission requires a nonempty complete source pass list.");
            return packets.Select(p=>p.ApplySceneFields(DeferredProgram,new(Camera:preparedCamera),out _)).ToArray();
        },preparedCamera,geometryClearActions);
    }

    private async Task RenderCoreAsync(IReadOnlyList<OriginalNativeTerrainBatch> batches,TerrainGbufferDrawInputs camera,
        TerrainGbufferRasterState terrainRaster,Color[] clearColors,float clearDepth,uint clearStencil,
        Func<OriginalNativeMapFrameContext,Task<IReadOnlyList<OriginalNativeDeferredPacket>>> prepareDeferred,
        OriginalDeferredCameraInputs? preparedCamera,RenderingDevice.DrawFlags geometryClearActions)
    {
        ArgumentNullException.ThrowIfNull(batches);ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(terrainRaster);ArgumentNullException.ThrowIfNull(clearColors);
        ArgumentNullException.ThrowIfNull(prepareDeferred);
        var colorActions=geometryClearActions&RenderingDevice.DrawFlags.ClearColorAll;
        if((geometryClearActions&~RenderingDevice.DrawFlags.ClearAll)!=0 ||
            (colorActions!=0 && colorActions!=RenderingDevice.DrawFlags.ClearColorAll) ||
            clearColors.Length!=(colorActions==0?0:5) || !float.IsFinite(clearDepth) || clearDepth<0 || clearDepth>1)
            throw new InvalidDataException("Native terrain clear actions require exactly their color inputs and finite [0,1] depth; partial-color/discard actions are unsupported.");
        var snapshot=batches.Select(b=>new OriginalNativeTerrainBatch(b.Coarse,Array.AsReadOnly(b.Patches.ToArray()))).ToArray();
        if(snapshot.Length==0 || snapshot.Any(b=>b.Patches.Count==0))throw new InvalidDataException("Native map frame requires explicit nonempty terrain batches.");
        camera=camera with{WorldToProjectionRows=camera.WorldToProjectionRows.ToArray(),WorldToViewRows=camera.WorldToViewRows.ToArray()};
        var colors=clearColors.ToArray();
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            // Only an accepted serial attempt supersedes the current receipt.
            // A queued request or rejected preflight must not invalidate the
            // frame whose AfterTerrain consumers still hold this scene gate.
            Volatile.Write(ref _lastTerrainSubmittedFrame, null);
            Volatile.Write(ref _lastWorldDeferredSubmittedFrame,null);
            long attempt=checked(++RenderAttemptNumber);
            var stageWatch=Stopwatch.StartNew();
            var stageTimings=new List<OriginalNativeMapStageTiming>(9);
            double previousStageMilliseconds=0;
            void MarkStage(string stage)
            {
                double elapsed=stageWatch.Elapsed.TotalMilliseconds;
                stageTimings.Add(new(stage,elapsed-previousStageMilliseconds,elapsed));
                previousStageMilliseconds=elapsed;
            }
            var context=new OriginalNativeMapFrameContext(TerrainFrame,OutputFrame,attempt,preparedCamera)
            {
                AcceptedTerrainBatches=Array.AsReadOnly(snapshot),
                AcceptedTerrainMaterial=TerrainTextures.Source
            };
            // A prior failed attempt may still hold an input read lease. The
            // owner closes that read before any new upstream texture writes;
            // cached descriptor lifetimes remain until draw retirement.
            if(_stages?.BeforeFrame is { } beforeFrame)await beforeFrame(context);
            MarkStage("before-frame");
            bool geometryAlreadyCleared = _stages?.BeforeTerrain is not null;
            if (_stages?.BeforeTerrain is { } beforeTerrain)
            {
                await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => {
                    rd.DrawListBegin(TerrainFrame.Framebuffer, geometryClearActions, colors, clearDepth, clearStencil);
                    rd.DrawListEnd(); return true;
                });
                await beforeTerrain(context);
            }
            MarkStage("before-terrain-models-army-sky");
            // F8 source state can inherit the raster actually left by the
            // current model queue. Resolve it after that queue submits, while
            // the accepted context still owns the same attachment set.
            if(_stages?.PrepareTerrainRaster is { } prepareRaster)
                terrainRaster=await prepareRaster(context,terrainRaster)??throw new InvalidDataException("Current terrain raster producer returned no state.");
            MarkStage("terrain-raster-preparation");
            bool same=terrainRaster==_terrainRaster && snapshot.Length==_batches.Count && snapshot.Where((b,i)=>!SameBatch(b,_batches[i].Batch)).Any()==false;
            if(!same)
            {
                var next=new List<(OriginalNativeTerrainBatch Batch,OriginalTerrainGbufferDraw Draw)>();
                try
                {
                    foreach(var batch in snapshot)next.Add((batch,await OriginalTerrainGbufferDraw.BuildAsync(
                        batch.Coarse?CoarseProgram:DetailProgram,TerrainTextures,TerrainFrame,batch.Patches,camera,terrainRaster)));
                }
                catch{for(int i=next.Count-1;i>=0;i--)await next[i].Draw.DisposeAsync();throw;}
                await RetireTerrainBatchesAsync();_batches.AddRange(next);_terrainRaster=terrainRaster;
            }
            else foreach(var entry in _batches)await entry.Draw.UpdateConstantsAsync(camera);
            MarkStage("terrain-draw-preparation");
            for(int i=0;i<_batches.Count;i++)await _batches[i].Draw.DrawAsync(i==0&&!geometryAlreadyCleared?geometryClearActions:RenderingDevice.DrawFlags.DefaultAll,
                i==0&&!geometryAlreadyCleared?colors:[],clearDepth,clearStencil);
            Volatile.Write(ref _lastTerrainSubmittedFrame, new OriginalTerrainGeometrySubmittedFrame(this, context, terrainRaster, _batches));
            MarkStage("terrain-submission");
            if(_stages?.AfterTerrain is { } afterTerrain)await afterTerrain(context);
            MarkStage("after-terrain");
            var preparedPasses=await prepareDeferred(context)??
                throw new InvalidDataException("Native map requires complete explicit deferred packets.");
            var packets=preparedPasses.ToArray();
            if(packets.Length==0||packets.Any(p=>p is null))
                throw new InvalidDataException("Native map requires a nonempty complete deferred pass list.");
            MarkStage("deferred-preparation");
            var lateSubmitted=new List<OriginalNativeMapWorldDeferredSubmittedFrame.SubmittedPass>();
            foreach(var packet in packets)
            {
                if(_deferred is null || packet.Raster!=_deferredRaster || !packet.Inputs.SequenceEqual(_deferred.Inputs))
                {
                    var next=await OriginalDeferredDraw.BuildAsync(DeferredProgram,OutputTarget(),packet.Constants,packet.Inputs,packet.Raster);
                    if(_deferred is not null)await _deferred.DisposeAsync();_deferred=next;_deferredRaster=packet.Raster;
                }
                else await _deferred.UpdateConstantsAsync(packet.Constants);
                await _deferred.DrawAsync(packet.TemporaryTranslucencyViewport);
                lateSubmitted.Add(new(_deferred,packet));
            }
            Volatile.Write(ref _lastWorldDeferredSubmittedFrame,
                new OriginalNativeMapWorldDeferredSubmittedFrame(this,context,DeferredProgram,lateSubmitted));
            MarkStage("deferred-submission");
            OriginalRenderOutputPresentation? final = _stages?.AfterDeferred is { } afterDeferred
                ? await afterDeferred(context) : null;
            MarkStage("after-deferred-postprocess");
            OriginalRenderOutputPresentation display = final ?? Presentation ??
                throw new InvalidDataException("Required final output stage returned no presentation.");
            if (!GodotObject.IsInstanceValid(display.Texture) || display.Texture.GetWidth() <= 0 || display.Texture.GetHeight() <= 0)
                throw new InvalidDataException("Native map final source stage returned a retired or empty presentation.");
            // Keep the original raw presentation usable by explicit stage
            // consumers; the displayed postprocess output is borrowed.
            if(Presentation is not null)await Presentation.RefreshAsync();
            if (!ReferenceEquals(display, Presentation)) await display.RefreshAsync();
            MarkStage("presentation-refresh");
            _externalPresentation = final;
            LastCompletedContext = context;
            LastCompletedTimings = stageTimings.AsReadOnly();
            FrameNumber++;
        }
        catch { Volatile.Write(ref _lastTerrainSubmittedFrame, null);Volatile.Write(ref _lastWorldDeferredSubmittedFrame,null); throw; }
        finally{_gate.Release();}
    }

    public async Task ResizeAsync(IReadOnlyDictionary<uint,Vector2I> terrainExtents,
        IReadOnlyDictionary<uint,Vector2I> outputExtents,bool allowReadback=false,
        OriginalRenderTextureResolutionFrame? resolvedTargets=null)
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            OriginalRenderTargetFrame? terrain=null,output=null;OriginalRenderOutputPresentation? present=null;OriginalRenderColorPass? outputPass=null;
            OriginalRenderOutputPresentation? previousExternal = _externalPresentation;
            try
            {
                var terrainResolution=resolvedTargets??TerrainFrame.Resolutions;
                var outputResolution=resolvedTargets??OutputFrame.Resolutions;
                RequireSeparateTargetOwners(TerrainFrame.Source,OutputFrame.Source,terrainResolution,outputResolution);
                terrain=await OriginalRenderTargetFrame.BuildAsync(TerrainFrame.Source,terrainExtents,allowReadback,allowGpuCopy:true,resolutions:terrainResolution);
                output=await OriginalRenderTargetFrame.BuildAsync(OutputFrame.Source,outputExtents,allowReadback,allowGpuCopy:true,resolutions:outputResolution);
                outputPass=await OriginalRenderColorPass.BuildAsync(output);
                if(!RequiresExternalPresentation)
                    present=await OriginalRenderOutputPresentation.BuildAsync(output.Textures[output.Source.ColorTargets[0].Texture.Object.ObjectId]);
                // Retire our deferred draw first: it can borrow the extra
                // owner's views. A failed stage keeps the old parent targets;
                // the next frame may recreate its retired cached draws.
                await RetireDrawsAsync();
                _externalPresentation = null;
                if(_stages?.BeforeTargetRetirement is { } retireTargets)
                    await retireTargets(new(TerrainFrame,OutputFrame));
            }
            catch
            {
                if (previousExternal is not null && GodotObject.IsInstanceValid(previousExternal.Texture) &&
                    previousExternal.Texture.TextureRdRid.IsValid)
                    _externalPresentation = previousExternal;
                if(present is not null)await present.DisposeAsync();if(outputPass is not null)await outputPass.DisposeAsync();if(output is not null)await output.DisposeAsync();
                if(terrain is not null)await terrain.DisposeAsync();throw;
            }
            var oldPresent=Presentation;var oldOutput=OutputFrame;var oldTerrain=TerrainFrame;var oldPass=_outputPass;
            (TerrainFrame,OutputFrame,Presentation,_outputPass)=(terrain,output,present,outputPass);
            LastCompletedContext = null;
            if(oldPresent is not null)await oldPresent.DisposeAsync();await oldPass.DisposeAsync();await oldOutput.DisposeAsync();await oldTerrain.DisposeAsync();
        }
        finally{_gate.Release();}
    }

    private static void RequireSeparateTargetOwners(LoadedRenderGraphTargets terrain,LoadedRenderGraphTargets output,
        OriginalRenderTextureResolutionFrame? terrainResolution,OriginalRenderTextureResolutionFrame? outputResolution)
    {
        // These frames retire independently. Explicit cross-frame proxies need
        // one shared allocation owner. Declaration fallback retains the existing
        // independent E13 geometry/depth-mirror representation.
        static IEnumerable<OriginalRenderTextureDefinition> Definitions(LoadedRenderGraphTargets targets)=>
            targets.ColorTargets.Select(t=>t.Texture).Concat(targets.DepthTarget is null?[]:new[]{targets.DepthTarget});
        if(terrainResolution is null||outputResolution is null)return;
        var terrainOwners=Definitions(terrain).Select(terrainResolution.Resolve)
            .GroupBy(r=>r.EffectiveOwnerIdentity,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.Any(r=>r.IsExplicitTerminalProxy),StringComparer.Ordinal);
        if(Definitions(output).Select(outputResolution.Resolve).Any(r=>
            terrainOwners.TryGetValue(r.EffectiveOwnerIdentity,out bool terrainHasProxy)&&
            (terrainHasProxy||r.IsExplicitTerminalProxy)))
            throw new InvalidDataException("A terminal RT shared across independently retired terrain/output frames requires a shared allocation owner.");
    }

    private OriginalDeferredDrawTarget OutputTarget()=>new(_outputPass.Framebuffer,_outputPass.OwnerTextures,
        OutputFrame.Source.TextureObjects.Source.ResourceUri+"#native-color-output");
    private static bool SameBatch(OriginalNativeTerrainBatch left,OriginalNativeTerrainBatch right)
        =>left.Coarse==right.Coarse && left.Patches.Count==right.Patches.Count &&
            left.Patches.Where((p,i)=>p.GridSize!=right.Patches[i].GridSize || !p.EncodeInstance().SequenceEqual(right.Patches[i].EncodeInstance())).Any()==false;
    private async Task RetireTerrainBatchesAsync()
    {Volatile.Write(ref _lastTerrainSubmittedFrame, null);for(int i=_batches.Count-1;i>=0;i--)await _batches[i].Draw.DisposeAsync();_batches.Clear();_terrainRaster=null;}
    private async Task RetireDrawsAsync()
    {Volatile.Write(ref _lastWorldDeferredSubmittedFrame,null);if(_deferred is not null)await _deferred.DisposeAsync();_deferred=null;_deferredRaster=null;await RetireTerrainBatchesAsync();}
    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)
        {
            _retiring=true;
            Volatile.Write(ref _lastWorldDeferredSubmittedFrame,null);
            return new(_disposal??=DisposeCoreAsync());
        }
    }
    private async Task DisposeCoreAsync()
    {
        // Publish the task in DisposeAsync before a synchronous stage failure
        // can reset it. Failed retirement retains targets and can be retried.
        await Task.Yield();
        await _gate.WaitAsync();
        try
        {
            await RetireDrawsAsync();
            _externalPresentation = null;
            if(_stages?.BeforeTargetRetirement is { } retireTargets)
                await retireTargets(new(TerrainFrame,OutputFrame));
            if(Presentation is not null)await Presentation.DisposeAsync();await _outputPass.DisposeAsync();await OutputFrame.DisposeAsync();await TerrainFrame.DisposeAsync();
            if(_ownsDeferredProgram)await DeferredProgram.DisposeAsync();
            await CoarseProgram.DisposeAsync();await DetailProgram.DisposeAsync();await TerrainTextures.DisposeAsync();
            LastCompletedContext = null;
        }
        catch
        {
            lock(_lifetimeGate)_disposal=null;
            throw;
        }
        finally{_gate.Release();}
    }
}
