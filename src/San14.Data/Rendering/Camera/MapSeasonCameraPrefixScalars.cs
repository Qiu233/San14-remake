using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The direct selected package's scalar source copies at
/// CPU20/24/28/2C, CPU68/6C, adaptation74/78, climate7C..88 and CPU120. C358A0 copies CPU2C later in its full body;
/// V418 publishes that selected word as Main17C. Blended Camera inputs need their own
/// native interpolation receipt.</summary>
public sealed class MapSeasonCameraPrefixScalars
{
    private readonly byte[] words;
    private MapSeasonCameraPrefixScalars(byte[] copiedWords,string evidence)
    {words=copiedWords;SourceEvidence=evidence;}
    public uint Exposure170Bits=>BinaryPrimitives.ReadUInt32LittleEndian(words);
    public uint Gamma174Bits=>BinaryPrimitives.ReadUInt32LittleEndian(words.AsSpan(4));
    public uint Scalar178Bits=>BinaryPrimitives.ReadUInt32LittleEndian(words.AsSpan(8));
    public uint Luminance17CBits=>BinaryPrimitives.ReadUInt32LittleEndian(words.AsSpan(12));
    public uint FogMaxDistanceToSky278Bits=>BinaryPrimitives.ReadUInt32LittleEndian(words.AsSpan(16));
    public uint ExposureRangeMin1B8Bits=>BinaryPrimitives.ReadUInt32LittleEndian(words.AsSpan(20));
    public uint ExposureRangeMax1BCBits=>BinaryPrimitives.ReadUInt32LittleEndian(words.AsSpan(24));
    public string SourceEvidence{get;}
    public static MapSeasonCameraPrefixScalars ReadDirectSelection(MapSeasonCameraSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);selection.Validate();
        if(selection.SecondPackage is not null)
            throw new NotSupportedException("Source Camera scalar copies require a direct selected package, not an unresolved blended Camera.");
        var camera=selection.FirstPackage.Camera;byte[] bytes=new byte[52];
        (int Ordinal,uint Hash)[] fields=[(9,0xB90D1027),(10,0xABB91759),(11,0x3DF6821A),(88,0x462994C1),
            (934,0xA48ECB2A),(29,0xF9989A1C),(30,0x13AC572E),
            (54,0x907B67C1),(55,0x800DB8FF),(850,0x2F6A6AF9),
            (851,0xDF989EE1),(852,0xAC082408),(853,0x4669C572)];
        for(int index=0;index<fields.Length;index++)
        {
            var field=camera.Properties[fields[index].Ordinal];
            if(field.NameHash!=fields[index].Hash||field.TypeCode!=8||field.ElementCount!=1||
                field.ElementSize!=4||field.RawValue.Length!=4||
                camera.Properties.Count(p=>p.NameHash==field.NameHash)!=1)
                throw new InvalidDataException("Selected source Camera scalar lacks its exact typed ordinal/hash/word.");
            field.RawValue.CopyTo(bytes,index*4);
        }
        return new(bytes,selection.SourceEvidence+
            ";paired-TC:C358AC..C358C7/SC:C36F5C..C36F77-sourceCPU20/24/28-bitwise-copy;"+
            "TC:C367EF..C367F5/SC:C37E9F..C37EA5-selectedCPU2C-bitwise-copy;"+
            "TC:C40A3F..C40A54-embedded1330+170/174/178;"+
            "TC:C41912..C4191B/SC:C42FC2..C42FCB-CPU2C-to-embedded1330+17C;"+
            "TC:C3592C..C3593A/SC:C36FDC..C36FEA-selectedCPU68/6C-bitwise-copy;"+
            "TC:C40AF8..C40B0A/SC:C421A8..C421BA-CPU68/6C-to-embedded1330+1B8/1BC;"+
            "TC:C35B5E..C35B6B/SC:C3720E..C3721B-selectedCPU120-copy;"+
            "TC:C40D1E..C40D2A/SC:C423CE..C423DA-CPU120-to-embedded1330+278;"+
            "paired-complete-V468-copy-inventory/CPU74/78-to-prepared1C4/1C8;"+
            "TC:C40B25..C40B4C/SC:C421D5..C421FC-CPU7C/80/84/88-to-1D0/1D4/1D8/1DC;"+
            "temperature1D4-and-humidity1D8-have-independent-typed-source-domains");
    }
    public byte[] ApplyToPrefix(ReadOnlySpan<byte> actualPriorMainPrefix)
    {
        if(actualPriorMainPrefix.Length!=MapCameraConstruction.PrefixByteLength)
            throw new InvalidDataException("Season Camera scalar application requires the complete owned Main prefix.");
        byte[] result=actualPriorMainPrefix.ToArray();
        words.AsSpan(0,16).CopyTo(result.AsSpan(0x170,16));
        words.AsSpan(16,4).CopyTo(result.AsSpan(0x278,4));
        words.AsSpan(20,8).CopyTo(result.AsSpan(0x1B8,8));
        words.AsSpan(28,8).CopyTo(result.AsSpan(0x1C4,8));
        words.AsSpan(36,16).CopyTo(result.AsSpan(0x1D0,16));
        return result;
    }
}
