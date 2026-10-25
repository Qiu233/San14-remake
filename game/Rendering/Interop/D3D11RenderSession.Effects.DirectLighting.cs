using System.Runtime.InteropServices;
using San14.Data;

public sealed partial class D3D11RenderSession : IG1EffectDirectLightingDevice
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DirectLightingCall(IntPtr owner,ulong allocation,[In] byte[] bytes,uint count,long ordinal);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LightingClusterCall(IntPtr owner,[In] byte[] bytes,uint count);
    private DirectLightingCall? _directLighting;
    private LightingClusterCall? _lightingCluster;
    public void SetLightingCluster(G1EffectDirectLightingClusterPrepared frame,uint indexFormat)
    {
        lock(_directGate)
        {
            RequireDirectOwner();
            byte[] bytes=G1EffectDirectLightingPublication.EncodeCluster(frame,indexFormat);
            _lightingCluster??=Export<LightingClusterCall>("san14_effect_lighting_cluster_v1");
            if(_lightingCluster(_owner,bytes,checked((uint)bytes.Length))!=0)throw NativeFailure();
        }
    }
    private sealed partial class DirectAllocation : IG1EffectDirectLightingAllocation
    {
        public void SetLighting(G1EffectDirectLightingSource? source,long submissionOrdinal)
        {
            lock(session._directGate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);session.RequireDirectOwner();
                byte[] bytes=source?.Encode()??Array.Empty<byte>();
                session._directLighting??=session.Export<DirectLightingCall>("san14_effect_direct_lighting_v1");
                if(session._directLighting(session._owner,id,bytes,checked((uint)bytes.Length),submissionOrdinal)!=0)
                    throw session.NativeFailure();
            }
        }
    }
}
