using System.Runtime.InteropServices;
using Godot;
using San14.Data;

public enum D3D11UiEffectTextureUse : uint { RenderTarget,ShaderRead }

public readonly record struct D3D11UiEffectReceipt(ulong Request,ulong Sequence,uint DrawCount);

/// <summary>One independent source UI offscreen command. The caller owns the
/// Godot output RID and calls this owner exclusively on its render thread.
/// The native return follows the actual shared-queue handoff, copy and fence.</summary>
public sealed class D3D11UiEffectSession : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Size,Version;
        public ulong ResourceGeneration;
        public IntPtr Resources;
        public uint ResourceBytes;
        public IntPtr Frame;
        public uint FrameBytes,UpdateKind;
        public ulong Sequence;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeReceipt
    {
        public ulong Request,Sequence;
        public uint DrawCount,Width,Height,Selector;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateCall(ulong device,ulong queue,ulong output,uint width,uint height,uint selector,out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NextCall(IntPtr owner,ulong output,in NativeInput input,uint reverseDepth,
        D3D11UiEffectTextureUse outputUse,uint paneAlpha,out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DestroyCall(IntPtr owner);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ErrorCall();
    private IntPtr module,owner;
    private readonly CreateCall create;
    private readonly NextCall next;
    private readonly DestroyCall destroy;
    private readonly ErrorCall error;
    private G1EffectGpuResourceUpload? sentResources;
    public D3D11UiEffectSession(string nativeDllPath)
    {
        RequireRenderThread();ArgumentException.ThrowIfNullOrWhiteSpace(nativeDllPath);
        module=NativeLibrary.Load(Path.GetFullPath(nativeDllPath));
        try {
            create=Export<CreateCall>("san14_ui_effect_create_v1");
            next=Export<NextCall>("san14_ui_effect_next_v3");
            destroy=Export<DestroyCall>("san14_ui_effect_destroy_v1");
            error=Export<ErrorCall>("san14_ui_effect_error_v1");
        }catch{NativeLibrary.Free(module);module=IntPtr.Zero;throw;}
    }
    public D3D11UiEffectReceipt Create(ulong device,ulong queue,ulong output,int width,int height,int selector)
    {
        RequireRenderThread();ObjectDisposedException.ThrowIf(module==IntPtr.Zero,this);
        if(owner!=IntPtr.Zero)throw new InvalidOperationException("UI Effect owner is already open.");
        if(width<=0||height<=0||selector is not (2 or 3))throw new ArgumentOutOfRangeException(nameof(selector));
        owner=create(device,queue,output,checked((uint)width),checked((uint)height),checked((uint)selector),out var receipt);
        if(owner==IntPtr.Zero)throw Failure();
        return new(receipt.Request,receipt.Sequence,receipt.DrawCount);
    }
    /// <summary>Compatibility path for an output already submitted to Canvas.</summary>
    public D3D11UiEffectReceipt Next(ulong output,G1EffectGpuPublication publication,bool reverseDepth) =>
        Next(output,publication,reverseDepth,D3D11UiEffectTextureUse.ShaderRead);

    public D3D11UiEffectReceipt Next(ulong output,G1EffectGpuPublication publication,bool reverseDepth,
        D3D11UiEffectTextureUse outputUse,byte sourceAlpha=byte.MaxValue)
    {
        RequireRenderThread();ObjectDisposedException.ThrowIf(module==IntPtr.Zero,this);
        ArgumentNullException.ThrowIfNull(publication);
        if(owner==IntPtr.Zero)throw new InvalidOperationException("UI Effect owner has not been created.");
        if(outputUse is not (D3D11UiEffectTextureUse.RenderTarget or D3D11UiEffectTextureUse.ShaderRead))
            throw new ArgumentOutOfRangeException(nameof(outputUse));
        if(sourceAlpha==0)throw new ArgumentOutOfRangeException(nameof(sourceAlpha),"Source alpha0 does not publish an offscreen command.");
        GCHandle resources=default,frame=default;
        try {
            static IntPtr Pin(ReadOnlyMemory<byte> bytes,ref GCHandle handle)
            {
                if(bytes.IsEmpty)return IntPtr.Zero;
                if(!MemoryMarshal.TryGetArray(bytes,out ArraySegment<byte> segment)||segment.Array is null)
                    throw new InvalidOperationException("UI Effect publication requires owned contiguous storage.");
                handle=GCHandle.Alloc(segment.Array,GCHandleType.Pinned);
                return IntPtr.Add(handle.AddrOfPinnedObject(),segment.Offset);
            }
            bool replace=!ReferenceEquals(sentResources,publication.Resources);
            var input=new NativeInput {
                Size=checked((uint)Marshal.SizeOf<NativeInput>()),Version=1,
                ResourceGeneration=publication.Resources.Generation,
                Resources=replace?Pin(publication.Resources.Bytes,ref resources):IntPtr.Zero,
                ResourceBytes=replace?checked((uint)publication.Resources.Bytes.Length):0,
                Frame=Pin(publication.Frame,ref frame),FrameBytes=checked((uint)publication.Frame.Length),
                UpdateKind=(uint)publication.UpdateKind,Sequence=publication.Sequence
            };
            if(next(owner,output,in input,reverseDepth?1u:0u,outputUse,sourceAlpha,out var receipt)!=0)throw Failure();
            sentResources=publication.Resources;
            return new(receipt.Request,receipt.Sequence,receipt.DrawCount);
        }finally{if(frame.IsAllocated)frame.Free();if(resources.IsAllocated)resources.Free();}
    }
    public void Dispose()
    {
        RequireRenderThread();
        if(owner!=IntPtr.Zero){destroy(owner);owner=IntPtr.Zero;}
        if(module!=IntPtr.Zero){NativeLibrary.Free(module);module=IntPtr.Zero;}
        sentResources=null;
    }
    private T Export<T>(string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name));
    private Exception Failure()=>new InvalidOperationException(Marshal.PtrToStringUTF8(error())??"Native UI Effect failed.");
    private static void RequireRenderThread()
    {
        if(!RenderingServer.IsOnRenderThread())throw new InvalidOperationException("UI Effect ownership belongs to Godot's render thread.");
    }
}
