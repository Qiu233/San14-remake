using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Actual CB6510 temporary, CDCA90(root,root,zero), and null-Camera
/// v430 result. The registration belongs to Root; this is a distinct temporary
/// actor generation, not a claim that selecting root returns identical bytes.</summary>
public sealed class OriginalDirectionalLightProviderTemporary
{
    private readonly byte[] bytes;
    internal OriginalDirectionalLightProviderTemporary(OriginalDirectionalLightConstructionPacket root,byte[] value)
    {Root=root;bytes=value.ToArray();}
    public OriginalDirectionalLightConstructionPacket Root {get;}
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    public bool NativeRetainedValueIdentity=>false;
    public string SourceProvenance=>$"actual-temporary-CB6510/CDCA90/root-pair0-weight0/CB9CC0;{Root.SourceUri}#{Root.SourceObjectId:X8}@{Root.SourceSha256};owned-root={Root.PreparedSha256}";
    /// <summary>Explicit source replay for diagnostics, outside ordinary draws.</summary>
    public void ValidateSourceTemporary()
    {
        Root.ValidateSourceConstruction();
        if(!OriginalDirectionalLightConstruction.ProviderZeroPairBytes(Root).AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("Directional root-pair temporary differs from its actual original source circuit.");
    }
    public OriginalCameraDirectionalLightDataPacket ApplyCurrentCamera(ReadOnlySpan<byte> completePreparedCamera3C0)
    {
        byte[] local=bytes.ToArray();
        // CE3870 removes the temporary flag before CBC770 regardless of the
        // current Camera/context mode; the flag separately marks copy ownership.
        uint fdc=BinaryPrimitives.ReadUInt32LittleEndian(local.AsSpan(0xFDC,4));
        BinaryPrimitives.WriteUInt32LittleEndian(local.AsSpan(0xFDC,4),fdc&0x7FFFFFFFu);
        if(local[0x10B0]==2)
        {
            var value=OriginalMapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(local,completePreparedCamera3C0,Root.ContextFlags3E4);
            return new(value.Bytes,false,false);
        }
        return OriginalCameraDirectionalLightData.Apply(local,completePreparedCamera3C0,Root.ContextFlags3E4);
    }
}

public static partial class OriginalDirectionalLightConstruction
{
    public static OriginalDirectionalLightProviderTemporary ConstructProviderRootZeroPairTemporary(
        OriginalDirectionalLightConstructionPacket root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new(root,ProviderZeroPairBytes(root));
    }
    internal static byte[] ProviderZeroPairBytes(OriginalDirectionalLightConstructionPacket root)
    {
        var machine=new Machine(root.SourceCircuit,[],new(root.ContextFlags3E4,"actual CB6510 temporary",[]),new byte[LightDataBytes]);
        return machine.ProviderRootZeroPair(root);
    }
    private sealed partial class Machine
    {
        public byte[] ProviderRootZeroPair(OriginalDirectionalLightConstructionPacket root)
        {
            trackSeed=false;
            Write(Cpu+0x50,root.Bytes.Span);
            PutWord(Cpu+0x30,root.Metadata.Flags30,4);PutWord(Cpu+0x34,root.Metadata.MaximumPrimitiveCount,4);
            PutWord(Cpu+0x38,root.Metadata.SourceGroupMask,4);PutWord(Cpu+0x3C,root.Metadata.Value3C,4);
            PutWord(Cpu+0x40,root.Metadata.Value40,4);PutWord(Cpu+0x44,root.Metadata.Value44,4);
            const ulong temp=Aux+0x18000;
            Set(1,temp,8,true);Run(0xCB6510);
            // CE35F0 copies the source name, maximum, and group before D1A4B0.
            // Name is an opaque registration identifier, never followed here.
            PutWord(temp+0x34,root.Metadata.MaximumPrimitiveCount,4);PutWord(temp+0x38,root.Metadata.SourceGroupMask,4);
            Set(1,temp,8,true);Set(2,Cpu,8,true);Set(8,Cpu,8,true);Set(19,0,16,true);
            PutWord((ulong)Reg(4)+0x28,0,1);Run(0xCDCA90);
            // CDC980 chooses both nonpositive pair inputs as original receiver,
            // copies maxima3C/40/44, and invokes temporary.v430 with Camera0.
            PutWord(temp+0x3C,root.Metadata.Value3C,4);PutWord(temp+0x40,root.Metadata.Value40,4);
            PutWord(temp+0x44,root.Metadata.Value44,4);
            Set(1,temp,8,true);Set(2,Aux+0x300,8,true);Set(8,0,8,true);Run(0xCB9CC0);
            byte[] result=Read(temp+0x50,LightDataBytes);
            // Resource fields retain owned backend Data handles through the
            // original circuit; they are never interpreted as process pointers.
            foreach(var slot in root.ResourceSlots)
                if(BinaryPrimitives.ReadUInt64LittleEndian(result.AsSpan(slot.LightDataOffset,8))!=slot.EffectiveDataHandle)
                    throw new InvalidDataException("Degenerate provider interpolation changed an owned resource binding.");
            return result;
        }
    }
}
