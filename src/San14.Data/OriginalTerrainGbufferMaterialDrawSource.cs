using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalTerrainGbufferMaterialDrawPreparation(Vector4 Specular,
    uint RawMaterialFlags, uint NextCachedFlags);

/// <summary>The ordinary Landscape F8 draw's material write, after the generic
/// owner's constructor. This source is loaded once. A selected F8 call skips
/// the intervening modifier body and submits its complete cSpec float4, even
/// when the previous material flags already equal 100h. The float4 setter may
/// deduplicate an equal value; it does not rescale roughness.</summary>
public sealed class OriginalTerrainGbufferMaterialDrawSource
{
    private readonly Vector4 specular;
    private readonly uint materialFlags;
    internal OriginalTerrainGbufferMaterialDrawSource(string locale, string executableHash,
        string textHash, Vector4 value, uint flags, uint drawRva, uint setterRva)
    {
        Locale = locale; ExecutableSha256 = executableHash; DecodedTextSha256 = textHash;
        specular = value; materialFlags = flags; NativeDrawRva = drawRva; NativeMaterialSetterRva = setterRva;
        SourceProvenance = $"san14://file/SAN14CE_{locale}.exe@{executableHash};" +
            $"ordinary-F8/{drawRva:X8};flags-bit4-skips-material-modifiers;" +
            $"material+20->{setterRva:X8}/count1/spec-bit100/group-index2;" +
            $"san14://rdb/system/DC52FD1C@{OriginalTerrainGbufferSourceParameters.ShaderSha256}";
    }

    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public uint NativeDrawRva { get; }
    public uint NativeMaterialSetterRva { get; }
    public string SourceProvenance { get; }
    public uint MaterialModeCount => 1;

    public static OriginalTerrainGbufferMaterialDrawSource Load(OriginalGameRepository repository,
        string locale, LoadedTerrainGbiMapResources material)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(material);
        var shader = material.Shader.Shader.Source;
        if (shader.FileId != 0xDC52FD1C ||
            Convert.ToHexString(SHA256.HashData(shader.Bytes)) != OriginalTerrainGbufferSourceParameters.ShaderSha256)
            throw new InvalidDataException("Ordinary F8 material draw requires its inspected DC52 shader source.");
        var container = G2ShaderContainerReader.Read(shader.Bytes);
        // Module+84 is a straight copy of source file+54, not a mode inferred
        // from selected shader names. Count1 enables the generic material setter.
        if (BinaryPrimitives.ReadUInt32LittleEndian(container.RawHeader.AsSpan(0x54, 4)) != 1)
            throw new InvalidDataException("Ordinary F8 material draw requires the source single material capability.");
        foreach (int pixel in new[] { 52, 54 })
        {
            var rdef = DxbcResourceDefinitionReader.Read(container.GetProgramBytes(shader.Bytes, pixel));
            var variable = rdef.Buffers.Single().Variables.Single(v => v.Name.Text == "cSpec");
            var type = rdef.Types.Single(t => t.Offset == variable.TypeOffset);
            if (variable.StartOffset != (pixel == 52 ? 16 : 0) || variable.ByteSize != 16 ||
                type.Class != 1 || type.BaseType != 3 || type.Rows != 1 || type.Columns != 4 || type.Elements != 0)
                throw new InvalidDataException("Ordinary near/coarse cSpec is not the source material float4.");
        }
        return OriginalDeferredSamplerSources.ReadOrdinaryTerrainMaterialDrawSource(repository.SourceDirectory, locale);
    }

    /// <summary>For a successfully selected ordinary F8 renderer, with the
    /// caller's actual current call flags. The renderer selection/override
    /// lifecycle belongs to its caller. Other routes can execute named/material
    /// modifiers and require their own source producer.</summary>
    public OriginalTerrainGbufferMaterialDrawPreparation PrepareSelectedOrdinaryF8(byte currentCallFlags)
    {
        if ((currentCallFlags & 0x10) == 0 || (currentCallFlags & (4 | 8)) != 0)
            throw new NotSupportedException("Material producer covers the selected ordinary F8 bit4-set/bit2-bit3-clear route.");
        return new(specular, materialFlags, materialFlags);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    // Reuse the existing inert PE/text decoder. It reads source bytes only;
    // no game process, executable code, GPU or per-request hash is involved.
    internal static OriginalTerrainGbufferMaterialDrawSource ReadOrdinaryTerrainMaterialDrawSource(
        string sourceDirectory, string locale)
    {
        NativeProfile profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "ordinary terrain material executable");
        var image = new NativeImage(exe);
        byte[] text = image.DecodeText(out int start);
        VerifyHash(text, profile.TextHash, "ordinary terrain material decoded text");
        int ordinaryShift = locale == "SC" ? 0xD30 : 0;
        int setterShift = locale == "SC" ? 0x1190 : 0;
        void Check(int rva, int count, string hash)
            => VerifyHash(text.AsSpan(rva - start, count), hash, "ordinary terrain material source window");
        // The complete body covers renderer selection, source stack vectors,
        // bit4 modifier skip, the unconditional material call and group draws.
        Check(0xD2D050 + ordinaryShift, 5372, locale == "SC"
            ? "9C1EF4EA25E8F4C6C4E0A8CE9AAA2AFA6B1212159CC924DBBCCB53DADB062A0E"
            : "7B4AE1D8F6B786F42E14CF41357F03E8F7F4AF429D44693EEF9C65052A98B9DB");
        Check(0x982357 + setterShift, 60, "2226C52ECC8D840E7ACB8DC5EB761EF3CA4F4BEC7BA8C0B9E968C889038B3779");
        Check(0x9800DD + setterShift, 37, "54A5647AD3B0742E7E7B75A13E435336DB9ED83F9F265A67A01ADE730E6713B1");
        Check(0x9801A9 + setterShift, 14, "30AA1FD476CAE349537807AC35D19695C3C901674455AFEAEE857A723B82AB83");
        Check(0x9801D0 + setterShift, 103, locale == "SC"
            ? "5395E73FBC87D1F47EE4E798C31A487F16E76F6826F5E2A5FF7E14F9D8CA049E"
            : "1359B3DC952D73D512CAA19AD9BF565A20FFB0F5D6BE24C4834FC7DC1F82965E");
        Check(0x9805D5 + setterShift, 110, "45DE690E9FB1DD51681414B9C73CADE8C23E83180A1E2821B28CD7FA2C4E7C6A");
        Check(0x975F90 + setterShift, 128, "36D2F6647B65CD49E551D09B4152A778149CAD666814D7096F93E460ECA3CDF3");
        int vectorLoad = 0xD2D3B9 + ordinaryShift;
        int displacement = BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(vectorLoad - start + 4, 4));
        var words = image.Raw(checked(vectorLoad + 8 + displacement), 16);
        if (!words.SequenceEqual(Convert.FromHexString("0000803F0000803F0000803F00000040")))
            throw new InvalidDataException("Ordinary terrain material cSpec literal changed.");
        Vector4 value = new(BinaryPrimitives.ReadSingleLittleEndian(words),
            BinaryPrimitives.ReadSingleLittleEndian(words[4..]), BinaryPrimitives.ReadSingleLittleEndian(words[8..]),
            BinaryPrimitives.ReadSingleLittleEndian(words[12..]));
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(0xD2DDC8 + ordinaryShift - start, 4));
        if (flags != 0x100)
            throw new InvalidDataException("Ordinary terrain material call no longer enables its exact specular write.");
        return new(locale, profile.ExeHash, profile.TextHash, value, flags,
            (uint)(0xD2D050 + ordinaryShift), (uint)(0x9800D0 + setterShift));
    }
}
