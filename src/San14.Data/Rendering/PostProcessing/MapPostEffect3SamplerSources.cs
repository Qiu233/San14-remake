using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The two real manager wrapper key recipes. These values establish
/// sampler creation, not installation in a particular shader stage or frame.</summary>
public sealed class MapPostEffect3ManagerSamplerSources
{
    private readonly byte[] linear,point;
    internal MapPostEffect3ManagerSamplerSources(byte[] linearKey,byte[] pointKey,string evidence,string locale="TC")
        =>(linear,point,SourceEvidence,Locale)=(linearKey,pointKey,evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public byte[] LinearKey=>linear.ToArray();
    public byte[] PointKey=>point.ToArray();
}

public static partial class RenderSamplerSourceAdapters
{
    /// <summary>Create two eight by eight native RT wrappers,
    /// then edit wrapper+48 and install them into manager+30/+38.
    /// The currently verified SC/TC source profiles are admitted. No RDEF sampler name
    /// or backend null/default state participates in this recipe.</summary>
    public static MapPostEffect3ManagerSamplerSources ReadPostEffect3ManagerSamplers(
        LoadedDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        return LoadedPostEffect3ManagerSamplers.GetValue(native, ReadPostEffect3ManagerSamplersAtImport);
    }

    private static MapPostEffect3ManagerSamplerSources ReadPostEffect3ManagerSamplersAtImport(
        LoadedDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("3SPK manager sampler writers are verified for SC/TC.");

        // Creator requests level1; preserve constructor high word and bias.
        byte[] linear=RenderTextureKey(native,1),point=RenderTextureKey(native,1);
        void Write(byte[] key,ushort filter)
        {
            ushort lo=BinaryPrimitives.ReadUInt16LittleEndian(key);
            lo=(ushort)((lo&0x03FF)|filter);
            lo=(ushort)((lo&0xFFF5)|5);
            BinaryPrimitives.WriteUInt16LittleEndian(key,lo);
        }
        Write(linear,0x5400);Write(point,0x4000);
        return new(linear,point,native.ResourceUri+$";{native.Locale}:manager-sampler-rules;"+"level1;wrapper48;linear30/point38;slot-installation-not-established",native.Locale);
    }
}
