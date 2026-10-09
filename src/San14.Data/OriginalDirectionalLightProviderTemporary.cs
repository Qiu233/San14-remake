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
        byte[] light=ProviderZeroPairOwned(root.Bytes.ToArray());
        PrepareNullCamera(light,root.ContextFlags3E4);
        return light;
    }
}
