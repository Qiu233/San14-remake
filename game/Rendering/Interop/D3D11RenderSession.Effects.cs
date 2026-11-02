using System.Runtime.InteropServices;
using San14.Data;

public sealed partial class D3D11RenderSession
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeEffectInputV1
    {
        public uint Size,Version;
        public ulong ResourceGeneration;
        public IntPtr Resources;
        public uint ResourceBytes;
        public IntPtr Frame;
        public uint FrameBytes,UpdateKind;
        public ulong Sequence;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EffectsNextCall(IntPtr owner,ulong output,in NativeInput input,
        IntPtr command,IntPtr routes,in NativeEffectInputV1 effects,uint outputUse,out NativeReceipt receipt);
    private EffectsNextCall? _effectsNext;
    private static readonly uint EffectInputSize=checked((uint)Marshal.SizeOf<NativeEffectInputV1>());
    private G1EffectGpuResourceUpload? _sentEffectResources;

    private int NextWithEffects(ulong output,D3D11MapInput input,in NativeInput native,
        G1EffectGpuPublication effects,ref WorldPins worldPins,uint outputUse,out NativeReceipt receipt)
    {
        _effectsNext??=Export<EffectsNextCall>("san14_render_next_map_effects_v1");
        GCHandle resourcePin=default,framePin=default,commandPin=default;
        IntPtr routePointer=IntPtr.Zero;
        try
        {
            static IntPtr Pin(ReadOnlyMemory<byte> bytes,ref GCHandle handle)
            {
                if(bytes.IsEmpty)return IntPtr.Zero;
                if(!MemoryMarshal.TryGetArray(bytes,out ArraySegment<byte> segment)||segment.Array is null)
                    throw new InvalidOperationException("Effect publication has no owned contiguous array storage.");
                handle=GCHandle.Alloc(segment.Array,GCHandleType.Pinned);
                return IntPtr.Add(handle.AddrOfPinnedObject(),segment.Offset);
            }
            bool replaceResources=!ReferenceEquals(effects.Resources,_sentEffectResources);
            var extension=new NativeEffectInputV1
            {
                Size=EffectInputSize,Version=1,ResourceGeneration=effects.Resources.Generation,
                ResourceBytes=replaceResources?checked((uint)effects.Resources.Bytes.Length):0,
                Resources=replaceResources?Pin(effects.Resources.Bytes,ref resourcePin):IntPtr.Zero,
                FrameBytes=checked((uint)effects.Frame.Length),Frame=Pin(effects.Frame,ref framePin),
                UpdateKind=(uint)effects.UpdateKind,Sequence=effects.Sequence
            };
            IntPtr commandPointer=IntPtr.Zero;
            if(input.CommandMap is {} command &&
                (!ReferenceEquals(command,_sentCommandMap)||input.CommandMapFpsMode!=_sentCommandMapFpsMode))
            {
                var value=PinCommandMap(command,input.CommandMapFpsMode,ref worldPins);
                commandPin=GCHandle.Alloc(new[]{value},GCHandleType.Pinned);
                commandPointer=commandPin.AddrOfPinnedObject();
            }
            if(input.Routes is {} routes&&!ReferenceEquals(routes,_sentRoutes))
            {
                var value=PinRoutes(routes,ref worldPins);
                routePointer=Marshal.AllocHGlobal(Marshal.SizeOf<NativeRouteInputV1>());
                Marshal.StructureToPtr(value,routePointer,false);
            }
            return _effectsNext(_owner,output,in native,commandPointer,routePointer,in extension,outputUse,out receipt);
        }
        finally
        {
            if(routePointer!=IntPtr.Zero)
            { Marshal.DestroyStructure<NativeRouteInputV1>(routePointer);Marshal.FreeHGlobal(routePointer); }
            if(commandPin.IsAllocated)commandPin.Free();
            if(framePin.IsAllocated)framePin.Free();
            if(resourcePin.IsAllocated)resourcePin.Free();
        }
    }
}
