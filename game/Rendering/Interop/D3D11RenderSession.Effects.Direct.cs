using System.Runtime.InteropServices;
using San14.Data;

public sealed partial class D3D11RenderSession : IG1EffectDirectComputeDevice
{
    // Pump serializes preparation with the next render request. This gate also
    // makes worker-owned allocations mutually exclusive with session teardown.
    private readonly object _directGate=new();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong DirectCreateCall(IntPtr owner,[In] byte[] bytes,uint byteCount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DirectSubmitCall(IntPtr owner,ulong allocation,[In] byte[] bytes,uint byteCount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DirectLiveCall(IntPtr owner,ulong allocation,out int live);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DirectReleaseCall(IntPtr owner,ulong allocation);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DirectDrawCall(IntPtr owner,ulong allocation,in NativeEffectInputV1 material,long submissionOrdinal);
    private DirectCreateCall? _directCreate;
    private DirectSubmitCall? _directSubmit;
    private DirectLiveCall? _directLive;
    private DirectReleaseCall? _directRelease;
    private DirectDrawCall? _directDraw;

    /// <summary>Creates actual compute shaders and device buffers on this session's
    /// serialized context owner. Unsupported kernels remain unprepared rather than allocating a CPU pool.</summary>
    public IG1EffectDirectComputeAllocation? Create(G1EffectDirectComputePackage package)
    {
        lock(_directGate)
        {
            ArgumentNullException.ThrowIfNull(package);RequireDirectOwner();
            if(package.MissingKernels.Count!=0)return null;
            byte[] bytes=G1EffectDirectPublication.EncodePackage(package);
            _directCreate??=Export<DirectCreateCall>("san14_effect_direct_create_v1");
            ulong id=_directCreate(_owner,bytes,checked((uint)bytes.Length));
            if(id==0)throw NativeFailure();
            return new DirectAllocation(this,id);
        }
    }
    private void RequireDirectOwner()
    {
        ObjectDisposedException.ThrowIf(_module==IntPtr.Zero,this);
        if(_owner==IntPtr.Zero)throw new InvalidOperationException("Direct effects require an open native session.");
    }
    private sealed class DirectAllocation(D3D11RenderSession session,ulong id) : IG1EffectDirectDrawAllocation
    {
        private bool disposed;
        private G1EffectGpuResourceUpload? sentResources;
        public bool IsPrepared { get { lock(session._directGate)return !disposed&&session._module!=IntPtr.Zero&&session._owner!=IntPtr.Zero; } }
        public bool? HasLiveParticles
        {
            get
            {
                lock(session._directGate)
                {
                    if(!IsPrepared)return null;
                    session.RequireDirectOwner();session._directLive??=session.Export<DirectLiveCall>("san14_effect_direct_live_v1");
                    if(session._directLive(session._owner,id,out int live)!=0)throw session.NativeFailure();
                    return live<0?null:live!=0;
                }
            }
        }
        public bool Submit(G1EffectDirectCommands commands)
        {
            lock(session._directGate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);session.RequireDirectOwner();
                byte[] bytes=G1EffectDirectPublication.EncodeCommands(commands);
                session._directSubmit??=session.Export<DirectSubmitCall>("san14_effect_direct_submit_v1");
                if(session._directSubmit(session._owner,id,bytes,checked((uint)bytes.Length))!=0)throw session.NativeFailure();
                return true;
            }
        }
        public void SetDraw(G1EffectGpuPublication? publication,long submissionOrdinal)
        {
            lock(session._directGate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);session.RequireDirectOwner();
                session._directDraw??=session.Export<DirectDrawCall>("san14_effect_direct_draw_v1");
                GCHandle resourcePin=default,framePin=default;
                try
                {
                    static IntPtr Pin(ReadOnlyMemory<byte> bytes,ref GCHandle pin)
                    {
                        if(bytes.IsEmpty)return IntPtr.Zero;
                        if(!MemoryMarshal.TryGetArray(bytes,out ArraySegment<byte> segment)||segment.Array is null)
                            throw new InvalidOperationException("Direct material requires owned contiguous storage.");
                        pin=GCHandle.Alloc(segment.Array,GCHandleType.Pinned);
                        return IntPtr.Add(pin.AddrOfPinnedObject(),segment.Offset);
                    }
                    bool replace=publication is not null&&!ReferenceEquals(sentResources,publication.Resources);
                    var input=new NativeEffectInputV1 {Size=EffectInputSize,Version=1,UpdateKind=2};
                    if(publication is not null)
                    {
                        input.ResourceGeneration=publication.Resources.Generation;
                        input.Resources=replace?Pin(publication.Resources.Bytes,ref resourcePin):IntPtr.Zero;
                        input.ResourceBytes=replace?checked((uint)publication.Resources.Bytes.Length):0;
                        input.Frame=Pin(publication.Frame,ref framePin);input.FrameBytes=checked((uint)publication.Frame.Length);
                        input.UpdateKind=(uint)publication.UpdateKind;input.Sequence=publication.Sequence;
                    }
                    if(session._directDraw(session._owner,id,in input,submissionOrdinal)!=0)throw session.NativeFailure();
                    if(publication is not null)sentResources=publication.Resources;
                }
                finally {if(framePin.IsAllocated)framePin.Free();if(resourcePin.IsAllocated)resourcePin.Free();}
            }
        }
        public void Dispose()
        {
            lock(session._directGate)
            {
                if(disposed)return;
                if(session._owner!=IntPtr.Zero)
                {
                    session.RequireDirectOwner();session._directRelease??=session.Export<DirectReleaseCall>("san14_effect_direct_release_v1");
                    if(session._directRelease(session._owner,id)!=0)throw session.NativeFailure();
                }
                disposed=true;
            }
        }
    }
}
