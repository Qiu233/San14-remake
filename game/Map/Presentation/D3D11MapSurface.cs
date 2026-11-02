using Godot;
using San14.Data;
using System.Runtime.CompilerServices;
using Environment=System.Environment;

/// <summary>Godot owns Canvas/RD operations on its synchronous renderer thread.
/// One asynchronous scene task prepares, awaits Main's real Canvas release,
/// and copies to the other retained output before delivering one result.</summary>
public partial class D3D11MapSurface : Control
{
    public const int OutputWidth = 1920, OutputHeight = 1080;
    public event Action<D3D11MapInput, D3D11MapReceipt>? Accepted;
    public event Action<D3D11MapInput, D3D11MapReceipt>? FramePrepared;
    public event Action<Exception>? Failed;
    public D3D11MapInput? LastAcceptedInput { get; private set; }
    public D3D11MapReceipt? LastReceipt { get; private set; }
    public Exception? LastError { get; private set; }
    public bool IsRendering => _rendering;
    public bool IsClosed => _closed;
    internal D3D11RenderSession.Native11DeviceOwner? NativeDeviceOwner { get; init; }
    internal D3D11TerrainPreparedPrograms? TerrainPrograms { get; init; }

    // This is the slot actually accepted by Canvas, not a merely prepared back.
    // Existing borrowers update synchronously in Accepted before this pump can
    // reuse the previous slot. The surface retains all physical RID lifetimes.
    internal (Texture2D Texture,D3D11MapInput Input,D3D11MapReceipt Receipt)? AcceptedPresentation =>
        !_closed && LastError is null && _acceptedOutput is { } output &&
        LastAcceptedInput is { } input && LastReceipt is { } receipt
            ? (output.Presentation,input,receipt) : null;

    /// <summary>The SceneView first stops submissions. The single pump includes
    /// every admitted request, actual Canvas submission and an explicit GPU
    /// drain for adoption. Close waits this same pump before destroying Native.</summary>
    internal Task DrainAsync()
    {
        ObjectDisposedException.ThrowIf(_closed,this);
        _drainSubmittedWork=true;
        if(!_rendering)_pump=PumpAsync();
        return _pump!;
    }

    private sealed record OutputSlot(Rid Texture,Rid Framebuffer,Texture2Drd Presentation,ulong NativeOutput)
    {
        internal bool HasCanvasSampling;
    }
    private sealed class CanvasStage(OutputSlot output,D3D11MapInput input,D3D11MapReceipt receipt,int owner)
    {
        internal readonly OutputSlot Output=output;
        internal readonly D3D11MapInput Input=input;
        internal readonly D3D11MapReceipt Receipt=receipt;
        // Only Main's actual PostDraw/marker or Close completes this gate.
        // Unlike Task continuations, the one pump waiter resumes on this owner
        // call stack, including acceptance and the next native-job admission.
        internal readonly MainOwnerCompletion<bool> Completion=new(owner);
        internal bool DrawReady,Submitted,PostDrawObserved,MarkerCompleted;
    }

    // Canvas and delivered native results each have one Main owner and one
    // pump waiter. Completion resumes that waiter on the same Main call stack.
    // The native worker alone queues its result delivery; this gate queues none.
    private sealed class MainOwnerCompletion<T>(int owner)
    {
        private bool _completed;
        private T _result=default!;
        private Exception? _error;
        private Action? _continuation;
        private void RequireOwner()
        {
            if(Environment.CurrentManagedThreadId!=owner)
                throw new InvalidOperationException("The presentation completion belongs to its Main owner.");
        }
        internal Awaiter GetAwaiter()=>new(this);
        internal bool TrySetResult(T result)=>Complete(result,null);
        internal bool TrySetException(Exception error)=>Complete(default!,error);
        private bool Complete(T result,Exception? error)
        {
            RequireOwner();
            if(_completed)return false;
            _result=result;_error=error;_completed=true;
            var continuation=_continuation;_continuation=null;
            continuation?.Invoke();
            return true;
        }
        internal readonly struct Awaiter(MainOwnerCompletion<T> gate):INotifyCompletion
        {
            public bool IsCompleted { get { gate.RequireOwner();return gate._completed; } }
            public T GetResult()
            {
                gate.RequireOwner();
                if(!gate._completed)throw new InvalidOperationException("Presentation completion was not observed.");
                if(gate._error is { } error)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                return gate._result;
            }
            public void OnCompleted(Action continuation)
            {
                gate.RequireOwner();
                if(gate._completed){continuation();return;}
                if(gate._continuation is not null)throw new InvalidOperationException("The presentation completion already has its pump waiter.");
                gate._continuation=continuation;
            }
        }
    }

    private bool _configured,_rendering,_closed,_signalsAttached,_allocationSubmitted,_drainSubmittedWork;
    private int _ownerMainThreadId=Environment.CurrentManagedThreadId;
    private string _sourceDirectory="",_nativeDLLPath="";
    private uint _populationMask;
    private ulong _device,_queue;
    private OutputSlot[] _outputs=[];
    private OutputSlot? _displayed,_acceptedOutput;
    private D3D11RenderSession? _session;
    private D3D11RenderSession.OutputQueueBridge? _queueBridge;
    private SceneTree? _lifetimeTree;
    private Window? _lifetimeWindow;
    internal IG1EffectDirectComputeDevice? EffectComputeDevice => _session;
    private TextureRect? _image;
    private D3D11MapInput? _pending,_openingInput;
    private readonly List<(D3D11MapInput Input,uint FpsMode)> _animationPending=[];
    private Func<D3D11MapInput,D3D11MapInput>? _prepareCurrent;
    private double _pendingElapsed;
    private Task? _pump,_close,_marker;
    private TaskCompletionSource<bool>? _ready,_allocationWait;
    private CanvasStage? _stage;
    private readonly record struct CopyGrant(ulong Output,bool HasCanvasSampling);
    private sealed class SceneSubmission(D3D11MapInput input,int owner)
    {
        internal readonly D3D11MapInput Input=input;
        // Asynchronous continuation keeps copy off Main even if authorization
        // arrives after the prepare call has already finished.
        internal readonly TaskCompletionSource<CopyGrant?> Authorization=
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly MainOwnerCompletion<D3D11MapReceipt?> Completion=new(owner);
        internal Task Worker=Task.CompletedTask;
        internal volatile bool CopyCancelled;
        internal void CancelCopy()
        {CopyCancelled=true;Authorization.TrySetResult(null);}
    }
    private sealed record SceneResult(SceneSubmission Work,D3D11MapReceipt? Receipt,
        Exception? Error,CanvasStage? Predecessor);
    private SceneResult? _completedScene;
    private bool _sceneCanAdvance,_preparingScene;
    private SceneSubmission? _sceneAhead;

    public async Task ConfigureAsync(string sourceDirectory,string nativeDLLPath,
        D3D11MapInput initialInput,uint populationMask,
        Func<D3D11MapInput,D3D11MapInput>? prepareCurrent=null)
    {
        ObjectDisposedException.ThrowIf(_closed,this);
        if(_configured)throw new InvalidOperationException("Native map surface is already configured.");
        if(NativeDeviceOwner is not null&&TerrainPrograms is null)
            throw new InvalidDataException("The application map must borrow its startup Landscape program generation.");
        if(!IsInsideTree())throw new InvalidOperationException("Attach the native surface before opening it.");
        if(RenderingServer.GetCurrentRenderingDriverName()!="d3d12"||GetViewport().UseHdr2D)
            throw new NotSupportedException("Native Main presentation requires D3D12 and a non-HDR Canvas.");
        // This surface requires Safe. Do not apply this inline Main Canvas
        // protocol to the separate-render-thread experiment's asynchronous Post.
        if(!RenderingServer.IsOnRenderThread())
            throw new NotSupportedException("The native-worker surface requires Godot's synchronous Safe renderer.");
        _ownerMainThreadId=Environment.CurrentManagedThreadId;
        _lifetimeTree=GetTree();_lifetimeWindow=GetWindow();
        ArgumentNullException.ThrowIfNull(initialInput);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeDLLPath);
        _sourceDirectory=sourceDirectory;_nativeDLLPath=nativeDLLPath;_populationMask=populationMask;
        _prepareCurrent=prepareCurrent;_configured=true;
        MouseFilter=MouseFilterEnum.Ignore;
        _image=new TextureRect { MouseFilter=MouseFilterEnum.Ignore,
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,
            TextureFilter=TextureFilterEnum.Nearest };
        AddChild(_image);_image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _image.Draw+=ImageDraw;
        RenderingServer.FramePreDraw+=BeforeCanvasDraw;
        RenderingServer.FramePostDraw+=AfterCanvasDraw;
        _signalsAttached=true;
        _openingInput=initialInput;_pending=null;_pendingElapsed=0;
        _ready=new();_pump=PumpAsync();
        if(!await OnMain(_ready.Task))throw new OperationCanceledException("Native map surface closed while opening.");
    }

    public void Record(MainMapCameraFrame frame,double elapsedSeconds,
        bool paused=false,bool eligibleAnimationUpdate=false) =>
        Record(D3D11MapInput.FromCamera(frame,elapsedSeconds,paused,eligibleAnimationUpdate));

    public void Record(D3D11MapInput input)
    {
        ObjectDisposedException.ThrowIf(_closed,this);
        if(!_configured||LastError is not null)throw new InvalidOperationException("Native surface is not available.");
        ArgumentNullException.ThrowIfNull(input);
        _pending=input.WithElapsed(_pendingElapsed+input.ElapsedSeconds);_pendingElapsed=_pending.ElapsedSeconds;
        if(!_rendering)_pump=PumpAsync();
        else TryStartSceneAhead();
    }
    public void RecordAnimationUpdate(D3D11MapInput input,uint fpsMode)
    {
        ObjectDisposedException.ThrowIf(_closed,this);
        _animationPending.Add((input,fpsMode));
    }
    private D3D11MapInput TakePending()
    {
        var input=_pending!.WithElapsed(_pendingElapsed);_pending=null;_pendingElapsed=0;
        return _prepareCurrent?.Invoke(input)??input;
    }

    private async Task PumpAsync()
    {
        _rendering=true;
        try
        {
            if(_session is null)
            {
                var allocated=await OnMain(OnRenderThread(rd=>
                {
                    var outputs=new List<OutputSlot>(2);
                    D3D11RenderSession.OutputQueueBridge? bridge=null;
                    ulong device=rd.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice,default,0);
                    ulong queue=rd.GetDriverResource(RenderingDevice.DriverResource.CommandQueue,default,0);
                    try
                    {
                        bridge=NativeDeviceOwner is { } deviceOwner
                            ? D3D11RenderSession.OutputQueueBridge.CreateNative11(
                                _nativeDLLPath,deviceOwner,queue,(uint)OutputWidth,(uint)OutputHeight)
                            : D3D11RenderSession.OutputQueueBridge.CreateNative11(
                                _nativeDLLPath,device,queue,(uint)OutputWidth,(uint)OutputHeight);
                        var shared=bridge.Native11Outputs??throw new InvalidOperationException("Native11 outputs are missing.");
                        outputs.Add(AllocateImportedOutput(rd,shared.Output0Resource12));
                        outputs.Add(AllocateImportedOutput(rd,shared.Output1Resource12));
                        return (Outputs:outputs.ToArray(),Device:device,Queue:queue,Bridge:bridge);
                    }
                    catch
                    {
                        // Patched RD retains each imported resource through its
                        // own deferred disposal, including a recorded clear.
                        // No map owner or GPU admission exists in this branch.
                        RetireOutputs(rd,outputs);bridge?.Dispose();throw;
                    }
                }));
                _outputs=allocated.Outputs;_device=allocated.Device;_queue=allocated.Queue;
                _queueBridge=allocated.Bridge;
                if(_closed)return;
                // Real Godot submission of these initial clears must Signal
                // Consumed before the worker is allowed to write either slot.
                _allocationWait=new();
                if(!await OnMain(_allocationWait.Task)||_closed)return;
                var initial=_prepareCurrent?.Invoke(_openingInput!)??_openingInput!;_openingInput=null;
                var output=_outputs[0];
                string source=_sourceDirectory,library=_nativeDLLPath;uint mask=_populationMask;
                ulong device=_device,queue=_queue;
                var created=await OnNativeWorker(()=>
                {
                    var session=new D3D11RenderSession(source,library,mask,nativeWorker:true,
                        outputQueueBridge:_queueBridge,terrainPrograms:TerrainPrograms);
                    return session.RunNativeJob(()=>
                    {
                        try { return (Session:session,Receipt:session.Create(device,queue,output.NativeOutput,initial)); }
                        catch { session.Dispose();throw; }
                    });
                });
                _session=created.Session;
                if(_closed)return;
                var opening=Present(output,initial,created.Receipt);
                if(!await opening.Completion||!Accept(opening))return;
                _ready!.TrySetResult(true);
            }
            while(!_closed&&(_sceneAhead is not null||_pending is not null||_drainSubmittedWork))
            {
                if(_sceneAhead is null&&_pending is null)
                {
                    _drainSubmittedWork=false;
                    var drainingSession=_session!;
                    await OnNativeWorker(()=>drainingSession.RunNativeJob(()=>
                    {drainingSession.DrainSubmittedOutputWork();return true;}));
                    continue;
                }
                // The previous Canvas has actually submitted and its Accepted
                // borrowers have switched to the displayed slot before here.
                if(_sceneAhead is null)
                {
                    var input=TakePending();
                    var updates=_animationPending.ToArray();_animationPending.Clear();
                    _sceneAhead=StartSceneSubmission(input,updates,null);
                }
                var work=_sceneAhead;
                var output=_outputs.First(slot=>!ReferenceEquals(slot,_displayed));
                // This Main decision follows the old real Accepted callbacks.
                // The worker may still be preparing or already awaiting it.
                work.Authorization.TrySetResult(new CopyGrant(output.NativeOutput,output.HasCanvasSampling));
                var receipt=await work.Completion;
                _sceneAhead=null;
                if(_closed||receipt is not { } delivered)return;
                var stage=Present(output,work.Input,delivered);
                // FramePrepared is finished. Capture one future scene, either
                // now or when the next source Record arrives during Canvas.
                _sceneCanAdvance=true;TryStartSceneAhead();
                bool submitted=await stage.Completion;
                _sceneCanAdvance=false;
                if(!submitted||!Accept(stage))return;
            }
        }
        catch(Exception error)
        {
            LastError=error;_pending=null;_openingInput=null;_pendingElapsed=0;_animationPending.Clear();
            _ready?.TrySetException(error);Failed?.Invoke(error);
            GD.PushError($"Native D3D11 map surface failed: {error}");
        }
        finally
        {
            _sceneCanAdvance=false;
            // Close may cancel Canvas while its future scene is still on the
            // one worker. Join it before this pump can retire the session.
            if(_sceneAhead is { } retiring)
            {
                retiring.CancelCopy();
                // Never hold the native job token or block Main while waiting
                // for authorization. Close joins the actual task before Native.
                try
                {
                    await OnMain(retiring.Worker);
                    DeliverSceneResult();
                    await retiring.Completion;
                }
                catch { /* A failed future scene already faults its Canvas gate. */ }
                _sceneAhead=null;
            }
            _rendering=false;
            if(LastError is not null&&!_closed)_=ObserveCloseAsync();
        }
    }

    private void TryStartSceneAhead()
    {
        if(_closed||!_sceneCanAdvance||_preparingScene||_pending is null||_sceneAhead is not null||_stage is not { } stage)return;
        _preparingScene=true;
        try
        {
            var input=TakePending();
            var updates=_animationPending.ToArray();_animationPending.Clear();
            _sceneAhead=StartSceneSubmission(input,updates,stage);
        }
        catch(Exception error) { stage.Completion.TrySetException(error); }
        finally { _preparingScene=false; }
    }

    private SceneSubmission StartSceneSubmission(D3D11MapInput input,
        (D3D11MapInput Input,uint FpsMode)[] updates,CanvasStage? predecessor)
    {
        var work=new SceneSubmission(input,_ownerMainThreadId);
        var session=_session!;
        work.Worker=Task.Run(async()=>
        {
            try
            {
                var prepared=session.RunNativeJob(()=>
                {
                    foreach(var update in updates)session.UpdateAnimations(update.Input,update.FpsMode);
                    return session.PrepareScene(input);
                });
                // The prepare job released both its CAS token and Native mutex.
                // Awaiting this real Main authorization consumes no pool thread.
                var grant=await work.Authorization.Task.ConfigureAwait(false);
                D3D11MapReceipt? receipt=null;
                if(grant is { } permission)
                {
                    bool copied=session.RunNativeJob(()=>
                    {
                        if(work.CopyCancelled)return false;
                        session.CopyScene(permission.Output,prepared.Request,permission.HasCanvasSampling);
                        return true;
                    });
                    if(copied)receipt=prepared;
                }
                else receipt=null;
                // One bounded owner mailbox. The pump cannot admit a new
                // submission until Main has consumed this result.
                Volatile.Write(ref _completedScene,new SceneResult(work,receipt,null,predecessor));
            }
            catch(Exception error)
            {
                Volatile.Write(ref _completedScene,new SceneResult(work,null,error,predecessor));
            }
        });
        return work;
    }

    // These entry points both belong to Safe Main. Exchange consumes at most
    // one actual result; neither path synthesizes a Canvas completion.
    public override void _Process(double delta)
    {if(!_closed)DeliverSceneResult();}

    private void DeliverSceneResult()
    {
        var result=Interlocked.Exchange(ref _completedScene,null);
        if(result is null)return;
        if(result.Error is { } error)
        {
            result.Work.Completion.TrySetException(error);
            result.Predecessor?.Completion.TrySetException(error);
        }
        else result.Work.Completion.TrySetResult(result.Receipt);
    }

    private CanvasStage Present(OutputSlot output,D3D11MapInput input,D3D11MapReceipt receipt)
    {
        // Queue a GPU-side dependency before Godot can record sampling this
        // image. This short bridge call never enters the native context gate.
        _queueBridge!.WaitReady(output.NativeOutput,receipt.Request);
        var stage=new CanvasStage(output,input,receipt,_ownerMainThreadId);_stage=stage;_displayed=output;
        _image!.Texture=output.Presentation;
        FramePrepared?.Invoke(input,receipt);
        return stage;
    }
    private bool Accept(CanvasStage stage)
    {
        if(_closed)return false;
        stage.Output.HasCanvasSampling=true;_acceptedOutput=stage.Output;
        LastAcceptedInput=stage.Input;LastReceipt=stage.Receipt;
        Accepted?.Invoke(stage.Input,stage.Receipt);
        return !_closed;
    }
    private void ImageDraw()
    {
        if(_stage is { } stage)stage.DrawReady=true;
    }
    private void BeforeCanvasDraw()
    {
        if(_closed)return;
        DeliverSceneResult();
        if(_closed)return;
        if(_allocationWait is not null)_allocationSubmitted=true;
        if(_stage is { DrawReady:true,Submitted:false } stage&&_image is not null&&
            _image.IsVisibleInTree()&&_image.Size.X>0&&_image.Size.Y>0)stage.Submitted=true;
    }
    private void AfterCanvasDraw()
    {
        if(_closed)return;
        try
        {
            // A displayed slot may be sampled again on every Engine frame.
            // This actual post-submission signal covers both registered slots,
            // including their last repeat draw before the next image switch.
            _queueBridge?.SignalConsumed();
        }
        catch(Exception error)
        {
            if(_allocationWait is { } allocationFailure)allocationFailure.TrySetException(error);
            else if(_stage is { } stageFailure)stageFailure.Completion.TrySetException(error);
            else
            {
                LastError=error;_ready?.TrySetException(error);Failed?.Invoke(error);
                _=ObserveCloseAsync();
            }
            return;
        }
        if(_allocationWait is { } allocation&&_allocationSubmitted)
        {
            _allocationWait=null;_allocationSubmitted=false;
            var marker=OnRenderThread(_=>true);_marker=marker;
            _=FinishAllocationMarkerAsync(allocation,marker);
        }
        if(_stage is { Submitted:true,PostDrawObserved:false } stage)
        {
            stage.PostDrawObserved=true;
            var marker=OnRenderThread(_=>true);_marker=marker;
            _=FinishCanvasMarkerAsync(stage,marker);
        }
    }
    private async Task FinishAllocationMarkerAsync(TaskCompletionSource<bool> allocation,Task<bool> marker)
    {
        try { await OnMain(marker);allocation.TrySetResult(!_closed); }
        catch(Exception error) { allocation.TrySetException(error); }
    }
    private async Task FinishCanvasMarkerAsync(CanvasStage stage,Task<bool> marker)
    {
        try
        {
            // In Safe, PostDraw belongs to this synchronous actual draw. The
            // Godot queue has waited for NativeReady before sampling, and its
            // actual submitted draw is followed by GodotConsumed. This remains
            // Canvas submission, not hardware Present or GPU completion.
            await OnMain(marker);
            if(_closed||!ReferenceEquals(_stage,stage))return;
            stage.MarkerCompleted=true;_stage=null;stage.Completion.TrySetResult(true);
        }
        catch(Exception error) { stage.Completion.TrySetException(error); }
    }

    private static OutputSlot AllocateImportedOutput(RenderingDevice rd,ulong resource12)
    {
        const RenderingDevice.TextureUsageBits usage=RenderingDevice.TextureUsageBits.ColorAttachmentBit|
            RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanCopyFromBit|
            RenderingDevice.TextureUsageBits.CanCopyToBit;
        // The shared resource is BGRA8, RT|SA, initially COMMON. The candidate
        // engine rejects an SA import unless its actual enhanced-barrier path
        // supports it, and holds a COM reference through RD physical retirement.
        Rid texture=rd.TextureCreateFromExtension(RenderingDevice.TextureType.Type2D,
            RenderingDevice.DataFormat.B8G8R8A8Unorm,RenderingDevice.TextureSamples.Samples1,
            usage,resource12,OutputWidth,OutputHeight,1,1,1),framebuffer=default;
        Texture2Drd? presentation=null;
        if(!texture.IsValid)throw new InvalidOperationException("Native11 shared output import requires the candidate D3D12 engine.");
        try
        {
            framebuffer=rd.FramebufferCreate(new Godot.Collections.Array<Rid> { texture });
            if(!framebuffer.IsValid)throw new InvalidOperationException("Native11 output framebuffer creation failed.");
            rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.ClearColor0,[Colors.Black]);rd.DrawListEnd();
            presentation=new Texture2Drd { TextureRdRid=texture };
            ulong actual=rd.GetDriverResource(RenderingDevice.DriverResource.Texture,texture,0);
            if(actual!=resource12)throw new InvalidOperationException("Native11 imported output resource differs.");
            return new(texture,framebuffer,presentation,actual);
        }
        catch
        {
            if(presentation is not null){presentation.TextureRdRid=default;presentation.Dispose();}
            if(framebuffer.IsValid)rd.FreeRid(framebuffer);rd.FreeRid(texture);throw;
        }
    }
    private static void RetireOutputs(RenderingDevice rd,IEnumerable<OutputSlot> outputs)
    {
        foreach(var slot in outputs)
        {
            slot.Presentation.TextureRdRid=default;slot.Presentation.Dispose();
            rd.FreeRid(slot.Framebuffer);rd.FreeRid(slot.Texture);
        }
    }
    public Task CloseAsync()
    {
        if(_close is not null)return _close;
        // Cancelling the Canvas gate resumes the pump and its Drain borrowers
        // synchronously. Publish the real retirement task before that can
        // re-enter CloseAsync; every caller must await this same cleanup.
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _close=completion.Task;
        _=CloseAndCompleteAsync(completion);
        return _close;
    }
    private async Task CloseAndCompleteAsync(TaskCompletionSource completion)
    {
        try
        {
            _closed=true;_pending=null;_openingInput=null;_pendingElapsed=0;_animationPending.Clear();
            var pendingScene=_sceneAhead;pendingScene?.CancelCopy();
            if(_signalsAttached)
            {
                RenderingServer.FramePreDraw-=BeforeCanvasDraw;RenderingServer.FramePostDraw-=AfterCanvasDraw;
                if(GodotObject.IsInstanceValid(_image))_image!.Draw-=ImageDraw;
                _signalsAttached=false;
            }
            if(GodotObject.IsInstanceValid(_image))_image!.Texture=null;
            _ready?.TrySetResult(false);_allocationWait?.TrySetResult(false);_allocationWait=null;
            var stage=_stage;_stage=null;stage?.Completion.TrySetResult(false);
            // ExitTree may stop all future Process/PreDraw callbacks while the
            // pump is awaiting its result. Join, then deliver explicitly before
            // awaiting that pump; no close-time mailbox dependency cycle.
            if(pendingScene is not null)
            {
                await OnMain(pendingScene.Worker);
                DeliverSceneResult();
            }
            await OnMain(CloseCoreAsync());
            completion.TrySetResult();
        }
        catch(Exception error) { completion.TrySetException(error); }
    }
    private async Task CloseCoreAsync()
    {
        if(_pump is not null)
        {
            try { await OnMain(_pump); }
            catch(Exception error) { GD.PushError($"Native map pump failed while retiring: {error}"); }
        }
        if(_marker is not null)
        {
            try { await OnMain(_marker); }
            catch(Exception error) { GD.PushError($"Native map Canvas marker failed while retiring: {error}"); }
        }
        var session=_session;var bridge=_queueBridge;
        if(bridge is not null)
        {
            // Normal close follows a real global drawn-frame callback. When
            // minimized, the next global process boundary instead seals only
            // already-enqueued queue work. Neither path invents a Canvas ACK.
            await OnMain(SealFinalGodotQueueAsync(bridge,_lifetimeTree!,_lifetimeWindow!));
            await OnNativeWorker(()=>
            {
                if(session is not null)session.RunNativeJob(()=>
                {
                    session.DrainSubmittedOutputWork();
                    bridge.Retire();
                    session.Dispose();return true;
                });
                else bridge.Retire();
                bridge.Dispose();return true;
            });
        }
        else if(session is not null)
            await OnNativeWorker(()=>session.RunNativeJob(()=>{session.Dispose();return true;}));
        // An incomplete retirement retains the owners and physical outputs.
        // Only a completed GPU retirement permits the RID owners to be freed.
        _session=null;_queueBridge=null;
        {
            var outputs=_outputs;
            if(outputs.Length>0)await OnMain(OnRenderThread(rd=>{RetireOutputs(rd,outputs);return true;}));
            _outputs=[];_displayed=null;_acceptedOutput=null;
        }
    }
    private static Task SealFinalGodotQueueAsync(D3D11RenderSession.OutputQueueBridge bridge,
        SceneTree tree,Window window)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Seal()
        {
            // A no-draw close can complete first. The uncancellable late
            // FrameDrawn callback must not access an already retired bridge.
            if(done.Task.IsCompleted)return;
            tree.ProcessFrame-=CheckNoDraw;
            try {bridge.SealGodotTail();done.TrySetResult();}
            catch(Exception error) {done.TrySetException(error);}
        }
        void CheckNoDraw()
        {
            if(done.Task.IsCompleted)return;
            // This global Main process boundary is outside Canvas recording;
            // the surface and its consumers have detached and all jobs joined.
            if(!DisplayServer.WindowCanDraw(window.GetWindowId()))Seal();
        }
        tree.ProcessFrame+=CheckNoDraw;
        RenderingServer.RequestFrameDrawnCallback(Callable.From(Seal));
        return done.Task;
    }
    public override void _ExitTree()=>_=ObserveCloseAsync();
    private async Task ObserveCloseAsync()
    {
        try { await OnMain(CloseAsync()); }
        catch(Exception error) { GD.PushError($"Native D3D11 map retirement failed: {error}"); }
    }
    // Task inlining is an optimization, not a thread-ownership guarantee. These
    // awaiters resume immediately when already on this Main owner; a .NET
    // fallback continuation on a pool thread is explicitly handed back first.
    private MainAwaitable OnMain(Task task)=>new(task,_ownerMainThreadId);
    private MainAwaitable<T> OnMain<T>(Task<T> task)=>new(task,_ownerMainThreadId);
    private static void ResumeOnMain(int owner,Action continuation)
    {
        if(Environment.CurrentManagedThreadId==owner)continuation();
        else Callable.From(continuation).CallDeferred();
    }
    private readonly struct MainAwaitable(Task task,int owner)
    {
        public Awaiter GetAwaiter()=>new(task,owner);
        public readonly struct Awaiter(Task task,int owner):INotifyCompletion
        {
            public bool IsCompleted=>task.IsCompleted&&Environment.CurrentManagedThreadId==owner;
            public void GetResult()=>task.GetAwaiter().GetResult();
            public void OnCompleted(Action continuation)
            {
                int ownerThread=owner;
                task.ConfigureAwait(false).GetAwaiter().OnCompleted(()=>ResumeOnMain(ownerThread,continuation));
            }
        }
    }
    private readonly struct MainAwaitable<T>(Task<T> task,int owner)
    {
        public Awaiter GetAwaiter()=>new(task,owner);
        public readonly struct Awaiter(Task<T> task,int owner):INotifyCompletion
        {
            public bool IsCompleted=>task.IsCompleted&&Environment.CurrentManagedThreadId==owner;
            public T GetResult()=>task.GetAwaiter().GetResult();
            public void OnCompleted(Action continuation)
            {
                int ownerThread=owner;
                task.ConfigureAwait(false).GetAwaiter().OnCompleted(()=>ResumeOnMain(ownerThread,continuation));
            }
        }
    }
    private MainOwnerCompletion<T> OnNativeWorker<T>(Func<T> action)
    {
        var done=new MainOwnerCompletion<T>(_ownerMainThreadId);
        _=Task.Run(()=>
        {
            try
            {
                T result=action();
                Callable.From(()=>done.TrySetResult(result)).CallDeferred();
            }
            catch(Exception error) { Callable.From(()=>done.TrySetException(error)).CallDeferred(); }
        });
        return done;
    }
    private static Task<T> OnRenderThread<T>(Func<RenderingDevice,T> action)
    {
        var done=new TaskCompletionSource<T>();
        RenderingServer.CallOnRenderThread(Callable.From(()=>
        {
            try { done.TrySetResult(action(RenderingServer.GetRenderingDevice()??throw new NotSupportedException("Godot has no global RenderingDevice."))); }
            catch(Exception error) { done.TrySetException(error); }
        }));
        return done.Task;
    }
}
