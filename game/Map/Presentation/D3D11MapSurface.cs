using Godot;
using San14.Data;
using System.Runtime.CompilerServices;
using Environment=System.Environment;

/// <summary>Godot owns Canvas/RD operations on its synchronous renderer thread.
/// One serialized native worker writes only the other of two retained outputs.</summary>
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

    // This is the slot actually accepted by Canvas, not a merely prepared back.
    // Existing borrowers update synchronously in Accepted before this pump can
    // reuse the previous slot. The surface retains all physical RID lifetimes.
    internal (Texture2D Texture,D3D11MapInput Input,D3D11MapReceipt Receipt)? AcceptedPresentation =>
        !_closed && LastError is null && _acceptedOutput is { } output &&
        LastAcceptedInput is { } input && LastReceipt is { } receipt
            ? (output.Presentation,input,receipt) : null;

    /// <summary>The SceneView first stops submissions. The single pump includes
    /// every already admitted native request and its actual Canvas acceptance.</summary>
    internal Task DrainAsync()
    {
        ObjectDisposedException.ThrowIf(_closed,this);
        return _pump ?? Task.CompletedTask;
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
        internal readonly MainCanvasGate Completion=new(owner);
        internal bool DrawReady,Submitted,PostDrawObserved,MarkerCompleted;
    }

    // Canvas has one Main owner and one waiter (the surface pump). Task's
    // ConfigureAwait(false) may still queue its continuation under a custom
    // SynchronizationContext. This gate dispatches no Task or deferred callback.
    private sealed class MainCanvasGate(int owner)
    {
        private bool _completed,_result;
        private Exception? _error;
        private Action? _continuation;
        private void RequireOwner()
        {
            if(Environment.CurrentManagedThreadId!=owner)
                throw new InvalidOperationException("The Canvas gate belongs to its Main owner.");
        }
        internal Awaiter GetAwaiter()=>new(this);
        internal bool TrySetResult(bool result)=>Complete(result,null);
        internal bool TrySetException(Exception error)=>Complete(false,error);
        private bool Complete(bool result,Exception? error)
        {
            RequireOwner();
            if(_completed)return false;
            _result=result;_error=error;_completed=true;
            var continuation=_continuation;_continuation=null;
            continuation?.Invoke();
            return true;
        }
        internal readonly struct Awaiter(MainCanvasGate gate):INotifyCompletion
        {
            public bool IsCompleted { get { gate.RequireOwner();return gate._completed; } }
            public bool GetResult()
            {
                gate.RequireOwner();
                if(!gate._completed)throw new InvalidOperationException("Canvas completion was not observed.");
                if(gate._error is { } error)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                return gate._result;
            }
            public void OnCompleted(Action continuation)
            {
                gate.RequireOwner();
                if(gate._completed){continuation();return;}
                if(gate._continuation is not null)throw new InvalidOperationException("The Canvas gate already has its pump waiter.");
                gate._continuation=continuation;
            }
        }
    }

    private bool _configured,_rendering,_closed,_signalsAttached,_allocationSubmitted;
    private int _ownerMainThreadId=Environment.CurrentManagedThreadId;
    private string _sourceDirectory="",_nativeDLLPath="";
    private uint _populationMask;
    private ulong _device,_queue;
    private OutputSlot[] _outputs=[];
    private OutputSlot? _displayed,_acceptedOutput;
    private D3D11RenderSession? _session;
    internal IG1EffectDirectComputeDevice? EffectComputeDevice => _session;
    private TextureRect? _image;
    private D3D11MapInput? _pending,_openingInput;
    private readonly List<(D3D11MapInput Input,uint FpsMode)> _animationPending=[];
    private Func<D3D11MapInput,D3D11MapInput>? _prepareCurrent;
    private double _pendingElapsed;
    private Task? _pump,_close,_marker;
    private TaskCompletionSource<bool>? _ready,_allocationWait;
    private CanvasStage? _stage;

    public async Task ConfigureAsync(string sourceDirectory,string nativeDLLPath,
        D3D11MapInput initialInput,uint populationMask,
        Func<D3D11MapInput,D3D11MapInput>? prepareCurrent=null)
    {
        ObjectDisposedException.ThrowIf(_closed,this);
        if(_configured)throw new InvalidOperationException("Native map surface is already configured.");
        if(!IsInsideTree())throw new InvalidOperationException("Attach the native surface before opening it.");
        if(RenderingServer.GetCurrentRenderingDriverName()!="d3d12"||GetViewport().UseHdr2D)
            throw new NotSupportedException("Native Main presentation requires D3D12 and a non-HDR Canvas.");
        // This surface requires Safe. Do not apply this inline Main Canvas
        // protocol to the separate-render-thread experiment's asynchronous Post.
        if(!RenderingServer.IsOnRenderThread())
            throw new NotSupportedException("The native-worker surface requires Godot's synchronous Safe renderer.");
        _ownerMainThreadId=Environment.CurrentManagedThreadId;
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
                    try
                    {
                        for(int i=0;i<2;i++)outputs.Add(AllocateOutput(rd));
                        return (Outputs:outputs.ToArray(),
                            Device:rd.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice,default,0),
                            Queue:rd.GetDriverResource(RenderingDevice.DriverResource.CommandQueue,default,0));
                    }
                    catch { RetireOutputs(rd,outputs);throw; }
                }));
                _outputs=allocated.Outputs;_device=allocated.Device;_queue=allocated.Queue;
                if(_closed)return;
                _allocationWait=new();
                if(!await OnMain(_allocationWait.Task)||_closed)return;
                var initial=_prepareCurrent?.Invoke(_openingInput!)??_openingInput!;_openingInput=null;
                var output=_outputs[0];
                string source=_sourceDirectory,library=_nativeDLLPath;uint mask=_populationMask;
                ulong device=_device,queue=_queue;
                var created=await OnMain(OnNativeWorker(()=>
                {
                    var session=new D3D11RenderSession(source,library,mask,nativeWorker:true);
                    return session.RunNativeJob(()=>
                    {
                        try { return (Session:session,Receipt:session.Create(device,queue,output.NativeOutput,initial)); }
                        catch { session.Dispose();throw; }
                    });
                }));
                _session=created.Session;
                if(_closed)return;
                var opening=Present(output,initial,created.Receipt);
                if(!await opening.Completion||!Accept(opening))return;
                _ready!.TrySetResult(true);
            }
            while(!_closed&&_pending is not null)
            {
                var input=TakePending();
                var updates=_animationPending.ToArray();_animationPending.Clear();
                var output=_outputs.First(slot=>!ReferenceEquals(slot,_displayed));
                var session=_session!;bool sampled=output.HasCanvasSampling;
                var receipt=await OnMain(OnNativeWorker(()=>session.RunNativeJob(()=>
                {
                    foreach(var update in updates)session.UpdateAnimations(update.Input,update.FpsMode);
                    return session.Next(output.NativeOutput,input,sampled);
                })));
                if(_closed)return;
                var stage=Present(output,input,receipt);
                if(!await stage.Completion||!Accept(stage))return;
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
            _rendering=false;
            if(LastError is not null&&!_closed)_=ObserveCloseAsync();
        }
    }

    private CanvasStage Present(OutputSlot output,D3D11MapInput input,D3D11MapReceipt receipt)
    {
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
        if(_allocationWait is not null)_allocationSubmitted=true;
        if(_stage is { DrawReady:true,Submitted:false } stage&&_image is not null&&
            _image.IsVisibleInTree()&&_image.Size.X>0&&_image.Size.Y>0)stage.Submitted=true;
    }
    private void AfterCanvasDraw()
    {
        if(_closed)return;
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
            // In Safe, PostDraw belongs to this synchronous actual draw. This
            // queue marker follows that draw; native copyFinal's GPU fence is
            // unchanged. Complete on this Main callback, without another frame.
            await OnMain(marker);
            if(_closed||!ReferenceEquals(_stage,stage))return;
            stage.MarkerCompleted=true;_stage=null;stage.Completion.TrySetResult(true);
        }
        catch(Exception error) { stage.Completion.TrySetException(error); }
    }

    private static OutputSlot AllocateOutput(RenderingDevice rd)
    {
        using var format=new RDTextureFormat { Format=RenderingDevice.DataFormat.B8G8R8A8Unorm,
            Width=OutputWidth,Height=OutputHeight,Depth=1,ArrayLayers=1,Mipmaps=1,
            TextureType=RenderingDevice.TextureType.Type2D,
            UsageBits=RenderingDevice.TextureUsageBits.ColorAttachmentBit|RenderingDevice.TextureUsageBits.SamplingBit|
                RenderingDevice.TextureUsageBits.CanCopyFromBit|RenderingDevice.TextureUsageBits.CanCopyToBit };
        using var view=new RDTextureView();Rid texture=rd.TextureCreate(format,view),framebuffer=default;
        Texture2Drd? presentation=null;
        if(!texture.IsValid)throw new InvalidOperationException("Native BGRA output allocation failed.");
        try
        {
            framebuffer=rd.FramebufferCreate(new Godot.Collections.Array<Rid> { texture });
            if(!framebuffer.IsValid)throw new InvalidOperationException("Native output framebuffer creation failed.");
            rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.ClearColor0,[Colors.Black]);rd.DrawListEnd();
            presentation=new Texture2Drd { TextureRdRid=texture };
            return new(texture,framebuffer,presentation,rd.GetDriverResource(RenderingDevice.DriverResource.Texture,texture,0));
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
            if(_signalsAttached)
            {
                RenderingServer.FramePreDraw-=BeforeCanvasDraw;RenderingServer.FramePostDraw-=AfterCanvasDraw;
                if(GodotObject.IsInstanceValid(_image))_image!.Draw-=ImageDraw;
                _signalsAttached=false;
            }
            if(GodotObject.IsInstanceValid(_image))_image!.Texture=null;
            _ready?.TrySetResult(false);_allocationWait?.TrySetResult(false);_allocationWait=null;
            var stage=_stage;_stage=null;stage?.Completion.TrySetResult(false);
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
        var session=_session;
        try
        {
            if(session is not null)await OnMain(OnNativeWorker(()=>session.RunNativeJob(()=>{session.Dispose();return true;})));
        }
        finally
        {
            _session=null;var outputs=_outputs;
            if(outputs.Length>0)await OnMain(OnRenderThread(rd=>{RetireOutputs(rd,outputs);return true;}));
            _outputs=[];_displayed=null;_acceptedOutput=null;
        }
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
    private static Task<T> OnNativeWorker<T>(Func<T> action)
    {
        var done=new TaskCompletionSource<T>();
        _=Task.Run(()=>
        {
            try
            {
                T result=action();
                Callable.From(()=>done.TrySetResult(result)).CallDeferred();
            }
            catch(Exception error) { Callable.From(()=>done.TrySetException(error)).CallDeferred(); }
        });
        return done.Task;
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
