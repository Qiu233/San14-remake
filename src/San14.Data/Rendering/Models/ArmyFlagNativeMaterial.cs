using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record ArmyFlagNativeMaterial(byte[] MaterialRecord64,
    uint RawFlags, Vector4 Specular, byte AlphaReference, int MaterialId,
    LoadedArmyFlagMaterialVariation Source, string ExplicitSpecularSource)
{
    public bool FreshSourceConstruction => true;
    public bool MaterialStateFidelityVerified => false;
}

public sealed class ArmyFlagNativeMaterialSource
{
    public LoadedArmyFlagMaterialVariation Variation { get; }
    public int MaterialId { get; }
    public byte AlphaReference { get; }
    internal ArmyFlagNativeMaterialSource(LoadedArmyFlagMaterialVariation variation,int materialId,byte alphaReference)
        =>(Variation,MaterialId,AlphaReference)=(variation,materialId,alphaReference);
}

/// <summary>D9D280 constructs the complete original 64-byte material record.
/// The fifth caller argument supplies specular W; it is never inferred from a
/// neighboring G1M material. The typed UC_AM_ parameters belong to the actual
/// selected ColorVariation material and are routed by D9D4F0.</summary>
public static class ArmyFlagNativeMaterials
{
    public static ArmyFlagNativeMaterial Construct(
        LoadedArmyFlagMaterialVariation source, float explicitSpecularW,
        string explicitSpecularSource)
        =>Construct(ReadSource(source),explicitSpecularW,explicitSpecularSource);

    /// <summary>Read the imported actual Material parameters once per source
    /// load. Render frames reuse the resulting numeric constructor inputs.</summary>
    public static ArmyFlagNativeMaterialSource ReadSource(LoadedArmyFlagMaterialVariation source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var parameters=source.UserParameters;
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

    public static ArmyFlagNativeMaterial Construct(ArmyFlagNativeMaterialSource source,
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
