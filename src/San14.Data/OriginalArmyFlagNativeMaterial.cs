using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalArmyFlagNativeMaterial(byte[] MaterialRecord64,
    uint RawFlags, Vector4 Specular, byte AlphaReference, int MaterialId,
    LoadedOriginalArmyFlagMaterialVariation Source, string ExplicitSpecularSource)
{
    public bool FreshSourceConstruction => true;
    public bool OriginalCurrentMaterialStateVerified => false;
}

public sealed class OriginalArmyFlagNativeMaterialSource
{
    public LoadedOriginalArmyFlagMaterialVariation Variation { get; }
    public int MaterialId { get; }
    public byte AlphaReference { get; }
    internal OriginalArmyFlagNativeMaterialSource(LoadedOriginalArmyFlagMaterialVariation variation,int materialId,byte alphaReference)
        =>(Variation,MaterialId,AlphaReference)=(variation,materialId,alphaReference);
}

/// <summary>D9D280 constructs the complete original 64-byte material record.
/// The fifth caller argument supplies specular W; it is never inferred from a
/// neighboring G1M material. The typed UC_AM_ parameters belong to the actual
/// selected ColorVariation material and are routed by D9D4F0.</summary>
public static class OriginalArmyFlagNativeMaterials
{
    public static OriginalArmyFlagNativeMaterial Construct(
        LoadedOriginalArmyFlagMaterialVariation source, float explicitSpecularW,
        string explicitSpecularSource)
        =>Construct(ReadSource(source),explicitSpecularW,explicitSpecularSource);

    /// <summary>Canonicalize the actual Material declaration once per source
    /// load. Render frames reuse the resulting numeric constructor inputs.</summary>
    public static OriginalArmyFlagNativeMaterialSource ReadSource(LoadedOriginalArmyFlagMaterialVariation source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.Materials.Source.DecodedSha256!=OriginalArmyFlagMaterialVariations.MaterialDictionarySha256||
            Convert.ToHexString(SHA256.HashData(source.Materials.Source.Bytes))!=OriginalArmyFlagMaterialVariations.MaterialDictionarySha256)
            throw Error("requires the exact selected source Material declaration");
        var declaration=DokReader.Read(source.Materials.Source.Bytes).Objects.SingleOrDefault(o=>o.ObjectId==source.SelectedMaterialObjectId)
            ??throw Error("selected source Material declaration is absent");
        var parameters=OriginalArmyFlagMaterialVariations.ReadUserParameters(declaration);
        if(parameters.DeclaredMaterialId is not int materialId)
            throw Error("requires a declared typed source UC_AM_nMtrID parameter");
        byte alpha=0;
        if(parameters.DeclaredOpacityThreshold is float opacity)
        {
            float clamped=Math.Clamp(opacity,0,1);
            alpha=checked((byte)(long)Multiply(clamped,255));
        }
        return new(source,materialId,alpha);
    }

    public static OriginalArmyFlagNativeMaterial Construct(OriginalArmyFlagNativeMaterialSource source,
        float explicitSpecularW,string explicitSpecularSource)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!float.IsFinite(explicitSpecularW)||string.IsNullOrWhiteSpace(explicitSpecularSource))
            throw Error("requires its finite explicit caller specular W and provenance");
        byte[] bytes=new byte[64];
        foreach(int offset in new[]{0,4,8,12,16,20,24,32,36,40,48,52,56})
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset,4),0x3F800000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28,4),0x100);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(44,4),explicitSpecularW);
        bytes[62]=source.AlphaReference;
        return new(bytes,0x100,new(1,1,1,explicitSpecularW),source.AlphaReference,source.MaterialId,source.Variation,explicitSpecularSource);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left,float right)=>left*right;
    private static InvalidDataException Error(string message)=>new("Original Army flag native material "+message+".");
}
