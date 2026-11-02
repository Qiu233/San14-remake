using System.Runtime.InteropServices;

public sealed partial class D3D11RenderSession
{
    // These calls preserve one session-owned prepared scene. Its texture is
    // copied only after Main releases the old Godot output through real Canvas.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PrepareSceneCall(IntPtr owner,in NativeInput input,
        IntPtr command,IntPtr routes,IntPtr effects,out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CopySceneCall(IntPtr owner,ulong output,ulong request,uint outputUse);
    private PrepareSceneCall? _prepareScene;
    private CopySceneCall? _copyScene;

    internal void CopyScene(ulong output,ulong request,bool outputHasCanvasSampling)
    {
        RequireSubmissionThread();ObjectDisposedException.ThrowIf(_module==IntPtr.Zero,this);
        _copyScene??=Export<CopySceneCall>("san14_render_copy_scene_v1");
        if(_copyScene(_owner,output,request,outputHasCanvasSampling?1u:0u)!=0)throw NativeFailure();
    }

    private int PrepareSceneNative(D3D11MapInput input,in NativeInput native,
        ref WorldPins pins,out NativeReceipt receipt)
    {
        _prepareScene??=Export<PrepareSceneCall>("san14_render_prepare_scene_v1");
        GCHandle commandPin=default,resourcesPin=default,framePin=default,effectsPin=default;
        IntPtr routePointer=IntPtr.Zero;
        try
        {
            IntPtr commandPointer=IntPtr.Zero,effectsPointer=IntPtr.Zero;
            if(input.CommandMap is { } command&&
                (!ReferenceEquals(command,_sentCommandMap)||input.CommandMapFpsMode!=_sentCommandMapFpsMode))
            {
                var value=PinCommandMap(command,input.CommandMapFpsMode,ref pins);
                commandPin=GCHandle.Alloc(new[]{value},GCHandleType.Pinned);
                commandPointer=commandPin.AddrOfPinnedObject();
            }
            if(input.Routes is { } routes&&!ReferenceEquals(routes,_sentRoutes))
            {
                var value=PinRoutes(routes,ref pins);
                routePointer=Marshal.AllocHGlobal(Marshal.SizeOf<NativeRouteInputV1>());
                Marshal.StructureToPtr(value,routePointer,false);
            }
            if(input.Effects is { } effects)
            {
                static IntPtr Pin(ReadOnlyMemory<byte> bytes,ref GCHandle handle)
                {
                    if(bytes.IsEmpty)return IntPtr.Zero;
                    if(!MemoryMarshal.TryGetArray(bytes,out ArraySegment<byte> storage)||storage.Array is null)
                        throw new InvalidOperationException("Effect publication has no owned contiguous array storage.");
                    handle=GCHandle.Alloc(storage.Array,GCHandleType.Pinned);
                    return IntPtr.Add(handle.AddrOfPinnedObject(),storage.Offset);
                }
                bool replaceResources=!ReferenceEquals(effects.Resources,_sentEffectResources);
                var value=new NativeEffectInputV1
                {
                    Size=EffectInputSize,Version=1,ResourceGeneration=effects.Resources.Generation,
                    Resources=replaceResources?Pin(effects.Resources.Bytes,ref resourcesPin):IntPtr.Zero,
                    ResourceBytes=replaceResources?checked((uint)effects.Resources.Bytes.Length):0,
                    Frame=Pin(effects.Frame,ref framePin),FrameBytes=checked((uint)effects.Frame.Length),
                    UpdateKind=(uint)effects.UpdateKind,Sequence=effects.Sequence
                };
                effectsPin=GCHandle.Alloc(new[]{value},GCHandleType.Pinned);
                effectsPointer=effectsPin.AddrOfPinnedObject();
            }
            return _prepareScene(_owner,in native,commandPointer,routePointer,effectsPointer,out receipt);
        }
        finally
        {
            if(routePointer!=IntPtr.Zero)
            {Marshal.DestroyStructure<NativeRouteInputV1>(routePointer);Marshal.FreeHGlobal(routePointer);}
            if(effectsPin.IsAllocated)effectsPin.Free();
            if(framePin.IsAllocated)framePin.Free();
            if(resourcesPin.IsAllocated)resourcesPin.Free();
            if(commandPin.IsAllocated)commandPin.Free();
        }
    }
}
