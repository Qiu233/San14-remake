using System.Buffers.Binary;

namespace San14.Data;

public sealed class OriginalMapPostEffect3ParameterCompositeMathSources
{
    private readonly byte[] temperatureTable,defaultTemperature;
    internal OriginalMapPostEffect3ParameterCompositeMathSources(OriginalSkyWeatherNativePowerSource power,
        LoadedOriginalNativeCameraTangentSource tangent,byte[] table,byte[] fallback,string evidence)
    {Power=power;Tangent=tangent;temperatureTable=table;defaultTemperature=fallback;SourceEvidence=evidence;}
    public OriginalSkyWeatherNativePowerSource Power{get;}
    public LoadedOriginalNativeCameraTangentSource Tangent{get;}
    public string Locale=>Power.Locale;
    public string SourceEvidence{get;}
    public static OriginalMapPostEffect3ParameterCompositeMathSources Load(OriginalGameRepository repository,string locale)
        =>OriginalDeferredSamplerSources.ReadCompositeMathSources(repository,locale);
    /// <summary>Native1147EE0: unsigned Kelvin/100 -10, rows0..387,
    /// otherwise actual magenta fallback. Table RGB plus source alpha1.</summary>
    public ReadOnlyMemory<byte> LookupColorTemperature(uint actualKelvin)
    {
        uint index=unchecked(actualKelvin/100-10);
        if(index>387)return defaultTemperature.ToArray();
        byte[] result=new byte[16];temperatureTable.AsSpan(checked((int)index*16+4),12).CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12),0x3F800000);return result;
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalMapPostEffect3ParameterCompositeMathSources ReadCompositeMathSources(
        OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);var profile=Profile(locale);
        var power=OriginalSkyWeatherNativePowerSource.Load(repository,locale);
        var tangent=OriginalNativeCameraTangentSource.Load(repository,locale);
        byte[] exe=File.ReadAllBytes(tangent.PhysicalPath);VerifyHash(exe,profile.ExeHash,"Composite executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"Composite decoded text");
        int init=locale=="TC"?0x1153390:0x11544D0,temperature=locale=="TC"?0x1147EE0:0x1148F60;
        VerifyHash(code.AsSpan(init-textRva,0xA51),locale=="TC"
            ?"8BC45B064BAAB066DC7A0B7D968E85A47744B8284A6BF21FC505CDFE01C87CA5"
            :"79C16BBD2779F43E121A79165B564CC09C7C2BC199DE80F660EAEF3C8CF318B4","Composite complete init");
        VerifyHash(code.AsSpan(temperature-textRva,0x79),locale=="TC"
            ?"CED4B818069BD6450B850641A52DE0D039EFC2B131BADB38C68CBC43326BDAF0"
            :"3BF3827C5B9E29F3BF1789DA690C824C657F210615BB1BFB8D04C8DF99577DCB","Composite native temperature lookup");
        byte[] table=image.Raw(locale=="TC"?0x1738B30:0x1739960,388*16).ToArray();
        VerifyHash(table,"F7C93B73464C8007311DAB79D60E67C3D4C11395544549370552922255E666CF","Composite actual temperature table");
        byte[] fallback=image.Raw(locale=="TC"?0x16B3C30:0x16B48E0,16).ToArray();
        if(!fallback.AsSpan().SequenceEqual(Convert.FromHexString("0000803F000000000000803F0000803F")))
            throw new InvalidDataException("Composite source temperature fallback changed.");
        return new(power,tangent,table,fallback,
            tangent.ResourceUri+";paired-fullCompositeInitA51;native1147EE0-388row-table;pow101EC10/tanf101FB90-SSE-source");
    }
}
