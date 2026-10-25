namespace San14.Data;

public enum G1EffectGpuUpdateKind : uint { Retain=0, Replace=1, Clear=2 }

/// <summary>An immutable, generation-owned CER1 upload. The encoder owns its
/// storage; callers retain this object across frames until resources change.</summary>
public sealed class G1EffectGpuResourceUpload
{
    internal G1EffectGpuResourceUpload(ulong generation, byte[] bytes)
    { Generation=generation;Bytes=bytes; }
    public ulong Generation {get;}
    public ReadOnlyMemory<byte> Bytes {get;}
}

/// <summary>A synchronous native effect publication. Its resource object is
/// reused; frame bytes contain only this accepted frame's live input.</summary>
public sealed class G1EffectGpuPublication
{
    internal G1EffectGpuPublication(G1EffectGpuResourceUpload resources, byte[] frame,
        G1EffectGpuUpdateKind updateKind, ulong sequence)
    { Resources=resources;Frame=frame;UpdateKind=updateKind;Sequence=sequence; }
    public G1EffectGpuResourceUpload Resources {get;}
    public ReadOnlyMemory<byte> Frame {get;}
    public G1EffectGpuUpdateKind UpdateKind {get;}
    public ulong Sequence {get;}
    public static G1EffectGpuPublication Clear(G1EffectGpuResourceUpload resources, ulong sequence) =>
        new(resources, [], G1EffectGpuUpdateKind.Clear, sequence);
}
