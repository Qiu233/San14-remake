using System.Runtime.InteropServices;
using Godot;

public sealed partial class D3D11RenderSession
{
    private readonly OutputQueueBridge? _outputQueueBridge;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)]
    private delegate IntPtr CreateMapWorkerCall(IntPtr bridge,ulong output,
        [MarshalAs(UnmanagedType.LPWStr)] string source,int width,int height,uint populationMask,
        in NativeInput input,out NativeReceipt receipt);
    private CreateMapWorkerCall? _createMapWorker;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)]
    private delegate IntPtr CreateResidentMapWorkerCall(IntPtr bridge,ulong output,
        [MarshalAs(UnmanagedType.LPWStr)] string source,int width,int height,uint populationMask,
        [MarshalAs(UnmanagedType.LPWStr)] string terrainProgramKey,
        [MarshalAs(UnmanagedType.LPWStr)] string terrainProgramIdentity,
        in NativeInput input,out NativeReceipt receipt);
    private CreateResidentMapWorkerCall? _createResidentMapWorker;


    /// <summary>Owns output resources and the two real GPU dependencies.
    /// Native11 and Godot12 use the same adapter, with distinct devices.
    /// Main only enqueues known fence values; GPU retirement belongs to the
    /// joined worker. The legacy independent On12 bridge remains available.</summary>
    internal sealed class OutputQueueBridge : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Native11OutputInfo
        {
            internal uint StructBytes,Version,Width,Height,DxgiFormat,ResourceFlags,BackendKind,Reserved;
            internal ulong Output0Resource12,Output1Resource12,NativeDevice11,AdapterLuid;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CreateBridgeCall(ulong device,ulong godotQueue,ulong output0,ulong output1);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CreateNative11BridgeCall(ulong device,ulong godotQueue,uint width,uint height,
            out Native11OutputInfo outputs,uint bytes);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SignalConsumedCall(IntPtr bridge,out ulong enqueuedValue);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int WaitReadyCall(IntPtr bridge,ulong output,ulong request,out ulong readyValue);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BridgeCall(IntPtr bridge);
        private IntPtr module,handle;
        private Native11DeviceOwner.BridgeLease? deviceLease;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CreateNative11BridgeV2Call(IntPtr deviceOwner,ulong godotQueue,
            uint width,uint height,out Native11OutputInfo outputs,uint bytes);
        private readonly SignalConsumedCall signalConsumed,sealGodotTail;
        private readonly WaitReadyCall waitReady;
        private readonly BridgeCall retire,destroy;
        private readonly ErrorCall error;
        internal Native11OutputInfo? Native11Outputs { get; }

        internal IntPtr Handle
        {
            get {ObjectDisposedException.ThrowIf(handle==IntPtr.Zero,this);return handle;}
        }
        private OutputQueueBridge(string nativeDLLPath)
        {
            RequireRenderThread();
            module=NativeLibrary.Load(NativeFileSystemPath(nativeDLLPath));
            try
            {
                signalConsumed=Export<SignalConsumedCall>("san14_render_queue_bridge_signal_consumed_v1");
                sealGodotTail=Export<SignalConsumedCall>("san14_render_queue_bridge_seal_godot_tail_v1");
                waitReady=Export<WaitReadyCall>("san14_render_queue_bridge_wait_ready_v1");
                retire=Export<BridgeCall>("san14_render_queue_bridge_retire_v1");
                destroy=Export<BridgeCall>("san14_render_queue_bridge_destroy_v1");
                error=Export<ErrorCall>("san14_render_error");
            }
            catch {NativeLibrary.Free(module);module=IntPtr.Zero;throw;}
        }
        internal OutputQueueBridge(string nativeDLLPath,ulong device,ulong godotQueue,ulong output0,ulong output1)
            :this(nativeDLLPath)
        {
            try
            {
                handle=Export<CreateBridgeCall>("san14_render_queue_bridge_create_v1")(device,godotQueue,output0,output1);
                if(handle==IntPtr.Zero)throw Failure();
            }
            catch {ReleaseUnadmittedConstruction();throw;}
        }
        private OutputQueueBridge(string nativeDLLPath,ulong device,ulong godotQueue,uint width,uint height)
            :this(nativeDLLPath)
        {
            try
            {
                const uint bytes=64;
                if(Marshal.SizeOf<Native11OutputInfo>()!=bytes)
                    throw new InvalidOperationException("Native11 output description layout differs.");
                handle=Export<CreateNative11BridgeCall>("san14_render_native11_bridge_create_v1")(
                    device,godotQueue,width,height,out var outputs,bytes);
                if(handle==IntPtr.Zero)throw Failure();
                if(outputs.StructBytes!=bytes||outputs.Version!=1||outputs.BackendKind!=2||
                    outputs.Width!=width||outputs.Height!=height||outputs.DxgiFormat!=87||
                    outputs.ResourceFlags!=33||outputs.Output0Resource12==0||outputs.Output1Resource12==0||
                    outputs.Output0Resource12==outputs.Output1Resource12||outputs.NativeDevice11==0)
                    throw new InvalidOperationException("Native11 shared output description is incompatible.");
                Native11Outputs=outputs;
            }
            catch {ReleaseUnadmittedConstruction();throw;}
        }
        internal static OutputQueueBridge CreateNative11(string nativeDLLPath,ulong device,ulong godotQueue,
            uint width,uint height)=>new(nativeDLLPath,device,godotQueue,width,height);

        private OutputQueueBridge(string nativeDLLPath,Native11DeviceOwner owner,
            ulong godotQueue,uint width,uint height):this(nativeDLLPath)
        {
            try
            {
                const uint bytes=64;
                if(Marshal.SizeOf<Native11OutputInfo>()!=bytes)
                    throw new InvalidOperationException("Native11 output description layout differs.");
                deviceLease=owner.BorrowForBridge(nativeDLLPath);
                handle=Export<CreateNative11BridgeV2Call>("san14_render_native11_bridge_create_v2")(
                    deviceLease.Handle,godotQueue,width,height,out var outputs,bytes);
                if(handle==IntPtr.Zero)throw Failure();
                if(outputs.StructBytes!=bytes||outputs.Version!=1||outputs.BackendKind!=2||
                    outputs.Width!=width||outputs.Height!=height||outputs.DxgiFormat!=87||
                    outputs.ResourceFlags!=33||outputs.Output0Resource12==0||outputs.Output1Resource12==0||
                    outputs.Output0Resource12==outputs.Output1Resource12||
                    outputs.NativeDevice11!=deviceLease.Info.Device11||outputs.AdapterLuid!=deviceLease.Info.AdapterLuid)
                    throw new InvalidOperationException("The shared outputs do not belong to the application's Native11 device.");
                Native11Outputs=outputs;
            }
            catch {ReleaseUnadmittedConstruction();throw;}
        }
        internal static OutputQueueBridge CreateNative11(string nativeDLLPath,Native11DeviceOwner owner,
            ulong godotQueue,uint width,uint height)=>new(nativeDLLPath,owner,godotQueue,width,height);

        // Only failed construction reaches this branch: no owner claim, GPU
        // submission or initial Consumed admission has occurred. Native checks
        // that boundary. Imported RIDs are freed first by the allocation owner.
        private void ReleaseUnadmittedConstruction()
        {
            if(handle!=IntPtr.Zero)
            {
                if(destroy(handle)!=0)throw Failure();
                handle=IntPtr.Zero;
            }
            if(module!=IntPtr.Zero){NativeLibrary.Free(module);module=IntPtr.Zero;}
            deviceLease?.Dispose();deviceLease=null;
        }
        internal ulong SignalConsumed()
        {
            RequireRenderThread();
            if(signalConsumed(Handle,out var value)!=0)throw Failure();
            return value;
        }
        internal ulong WaitReady(ulong output,ulong request)
        {
            RequireRenderThread();
            if(waitReady(Handle,output,request,out var value)!=0)throw Failure();
            return value;
        }
        // Close-only seal covers actual queued work after detach/join. It
        // neither submits a pending RD list nor pretends a Canvas was drawn.
        internal ulong SealGodotTail()
        {
            RequireRenderThread();
            if(sealGodotTail(Handle,out var value)!=0)throw Failure();
            return value;
        }
        internal void Retire()
        {
            if(retire(Handle)!=0)throw Failure();
        }
        public void Dispose()
        {
            if(handle==IntPtr.Zero)return;
            if(destroy(handle)!=0)throw Failure();
            handle=IntPtr.Zero;
            NativeLibrary.Free(module);module=IntPtr.Zero;
            deviceLease?.Dispose();deviceLease=null;
        }
        private T Export<T>(string name) where T:Delegate =>
            Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name));
        private InvalidOperationException Failure()=>new(
            Marshal.PtrToStringUTF8(error())??"Native output bridge failed.");
    }
}
