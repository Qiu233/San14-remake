using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

/// <summary>Source FPSManager/FrameSync admission for the normal playable Map.
/// A late poll accepts one callback, never elapsed-time catch-up callbacks.</summary>
public sealed class D3D11AnimationPacer
{
    private readonly uint[] _fps;
    private readonly bool _qpc;
    private readonly ulong _frequency;
    private ulong _prior;
    public uint FpsMode {get;private set;}
    [DllImport("ucrtbase",CallingConvention=CallingConvention.Cdecl)]
    private static extern int clock();

    public D3D11AnimationPacer(string fpsSource)
    {
        var bytes=File.ReadAllBytes(fpsSource);
        if(bytes.Length!=8)throw new InvalidDataException("Actual source FPS table extent.");
        _fps=[BinaryPrimitives.ReadUInt32LittleEndian(bytes),BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))];
        if(_fps.Any(value=>value==0))throw new InvalidDataException("Actual source FPS table contains zero.");
        _qpc=X86Base.IsSupported&&X86Base.CpuId(0,0).Eax>0&&(X86Base.CpuId(1,0).Edx&0x10)!=0&&Stopwatch.IsHighResolution;
        _frequency=_qpc?(ulong)Stopwatch.Frequency:1000;
        _prior=Sample();
    }
    public void ApplyLoadedOption31(uint selectedSettings84)
    {
        if(selectedSettings84<2)FpsMode=selectedSettings84;
    }
    private ulong Sample()=>_qpc?(ulong)Stopwatch.GetTimestamp():unchecked((ulong)clock());
    public bool TryAccept()=>TryAccept(Sample());
    internal bool TryAccept(ulong currentCounter)
    {
        if(_prior<currentCounter&&currentCounter-_prior<_frequency/_fps[FpsMode])return false;
        _prior=currentCounter;return true;
    }
}
