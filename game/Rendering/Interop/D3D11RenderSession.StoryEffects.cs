using System.Runtime.InteropServices;

public sealed partial class D3D11RenderSession
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RetireEffectOwnerCall(IntPtr owner, ulong sourceOwner);
    private RetireEffectOwnerCall? retireEffectOwner;

    internal void RetireStoryEffectOwner(ulong sourceOwner)
    {
        lock(_directGate)
        {
        RequireRenderThread();
        ObjectDisposedException.ThrowIf(_module==IntPtr.Zero,this);
        if (sourceOwner == 0) throw new ArgumentOutOfRangeException(nameof(sourceOwner));
        retireEffectOwner ??= Export<RetireEffectOwnerCall>("san14_effect_retire_owner_v1");
        if (retireEffectOwner(_owner, sourceOwner) != 0) throw NativeFailure();
        }
    }
}
