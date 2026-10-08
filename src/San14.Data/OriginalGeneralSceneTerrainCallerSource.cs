using System.Buffers.Binary;

namespace San14.Data;

/// <summary>D31D30's current saved-Cull, temporary Terrain draw-Cull and
/// post-call restoration. Commands are conditional on the actual prior
/// frontend state; they are not GPU submission receipts.</summary>
public readonly record struct OriginalGeneralSceneTerrainCullTransition(
    uint SavedSourceCull, uint SavedNativeCull, uint DrawSourceCull, uint DrawNativeCull,
    uint RestoredSourceCull, uint? EntryCommandWord, uint? ExitCommandWord);

/// <summary>Loaded full GeneralScene caller contract. D2D050's Flag2.bit0
/// gate is distinct from its earlier per-record mask test. Geometry depth
/// write/compare remain the actual earlier state; caller Cull does not.</summary>
public sealed class LoadedOriginalGeneralSceneTerrainCallerSource
{
    internal LoadedOriginalGeneralSceneTerrainCallerSource(string locale, string hash, uint terrainCull,
        uint[] nativeCullTable, uint factoryClassId = 0x1C8F048A)
    { Locale = locale; ExecutableSha256 = hash; TerrainDrawSourceCull = terrainCull; this.nativeCullTable = nativeCullTable;
        FactoryClassId = factoryClassId; }
    private readonly uint[] nativeCullTable;
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public uint FactoryClassId { get; }
    public uint TerrainDrawSourceCull { get; }
    public uint TerrainDrawNativeCull => NativeCull(TerrainDrawSourceCull);
    public string SourceProvenance => $"SAN14CE_{Locale}.exe@{ExecutableSha256};" +
        "GeneralScene-loader-and-CPU.v8;actual-arg12/CPU78=>Terrain-arg9=>F8-stack258-bit0-skip;" +
        "D31D30-saveE8/temporary-sourceCull2/D28C30/F8/restore-priorE8;source2=>D3DBack3;" +
        "DepthWrite-and-Compare-retained;not-GPU-receipt";

    public bool SuppressesOrdinaryTerrainF8(uint currentArgument9Flag2) => (currentArgument9Flag2 & 1) != 0;
    public OriginalGeneralSceneTerrainCullTransition PrepareTerrainCull(uint actualSavedSourceCull)
    {
        uint savedNative = NativeCull(actualSavedSourceCull), draw = TerrainDrawSourceCull;
        bool changed = actualSavedSourceCull != draw;
        return new(actualSavedSourceCull, savedNative, draw, NativeCull(draw), actualSavedSourceCull,
            changed ? (draw << 16) | 0x1Du : null,
            changed ? (actualSavedSourceCull << 16) | 0x1Du : null);
    }
    private uint NativeCull(uint source)
    {
        if (source is < 1 or > 3)
            throw new NotSupportedException("GeneralScene Terrain Cull closes the actual source codes1..3; no unknown prior raster fallback.");
        return nativeCullTable[source - 1];
    }
}

public static class OriginalGeneralSceneTerrainCallerSource
{
    public static LoadedOriginalGeneralSceneTerrainCallerSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadGeneralSceneTerrainCallerSource(repository.SourceDirectory, locale);
    }
    // Canonical RGK Read accepts the caller's already verified executable
    // identity, as its pre-existing API did. No original file reads occur in
    // this path; native constants are the paired, hash-pinned source contract.
    internal static LoadedOriginalGeneralSceneTerrainCallerSource ReadForVerifiedExecutable(string locale, string sha256)
    {
        string required = locale switch
        {
            "TC" => "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            "SC" => "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            _ => throw new InvalidDataException("GeneralScene caller requires paired TC or SC source.")
        };
        if (sha256 != required) throw new InvalidDataException("GeneralScene caller executable identity differs.");
        return new(locale, sha256, 2, [1, 3, 2]);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalGeneralSceneTerrainCallerSource LoadGeneralSceneTerrainCallerSource(string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] executable = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(executable, profile.ExeHash, "GeneralScene full caller executable");
        var image = new NativeImage(executable);
        byte[] text = image.DecodeText(out int start);
        VerifyHash(text, profile.TextHash, "GeneralScene full caller decoded text");
        bool tc = locale == "TC";
        void Check(int rva, int size, string tcHash, string scHash, string name)
            => VerifyHash(text.AsSpan(rva - start, size), tc ? tcHash : scHash, name);
        Check(tc ? 0xE7EF10 : 0xE7FFC0, 997,
            "28D4CBC448CCBF9A64F2E497BA5A077812D3467E3EE6B6153A93B2E57053BDAC",
            "A73A971F4CA8F2A6300329B63D980C49527B6C7A7AFF718358887B9672F831C5", "complete GeneralScene loader");
        Check(tc ? 0xEA4520 : 0xEA5510, 1391,
            "91EF472D7BA14B78D61822538FAFA686911AE703A452D85F2C00B277D97CC904",
            "D08D8F89900888DF261B72DD5F413D5C0314B2990E3A60AAED6BCF2F9E2901CF", "complete GeneralScene execution");
        Check(tc ? 0xE8DE30 : 0xE8EEE0, 492,
            "4A6D9F76359C609DE8922E7D526F5EB6CE73C9676BA2A8630FEB44297E0679EC",
            "02AD677AFC47DAD3590B620F5FB6149531366C3AE6EAB619C0ADDA25E64D9BB8", "complete GeneralScene argument schema");
        Check(tc ? 0xB3E980 : 0xB3F9B0, 283,
            "A4AE7616D99EFDB43C7F533F0DA504851DD5B31BE32BD7D2F0AD20F8FA01D27E",
            "17198EFDA7349258307B44CA6926E11EBE6023D6C14E0325A4296ECBCF579084", "complete GeneralScene argument copy");
        int classGetter = tc ? 0xE8F7D0 : 0xE908F0;
        Check(classGetter, 6,
            "23AB3ABABD442CB2343F309444ABC6FEDFC201A21CEE49149789E8C8DF89BBA1",
            "23AB3ABABD442CB2343F309444ABC6FEDFC201A21CEE49149789E8C8DF89BBA1", "GeneralScene actual factory class getter");
        int terrainRva = tc ? 0xD31D30 : 0xD32A60;
        Check(terrainRva, 2783,
            "C9340350B1B8140129645B1EDCAF2238DDD57C9D335323B057BA828F0CB8E9F0",
            "3B24BBBCBCF7AB9C29048B41E09AC147D1063C27051670723921419E7D796E83", "complete Terrain caller save/temporary/restore");
        Check(tc ? 0xD28C30 : 0xD29960, 627,
            "48DC3B51C93BFA9E99283900D31016F1B852FD93153F7E6A02A76BE1AE85BE02",
            "48DC3B51C93BFA9E99283900D31016F1B852FD93153F7E6A02A76BE1AE85BE02", "complete recursive Terrain dispatch");
        Check(tc ? 0xD2D050 : 0xD2DD80, 5372,
            "7B4AE1D8F6B786F42E14CF41357F03E8F7F4AF429D44693EEF9C65052A98B9DB",
            "9C1EF4EA25E8F4C6C4E0A8CE9AAA2AFA6B1212159CC924DBBCCB53DADB062A0E", "complete F8 ordinary consumer and Flag2 bit0 gate");
        Check(tc ? 0x9668D0 : 0x967A50, 40,
            "96E2F887F9B0DBB39FA9691F69E603E78F1903F0DFD828E6C6B1483EC2AE4F01",
            "96E2F887F9B0DBB39FA9691F69E603E78F1903F0DFD828E6C6B1483EC2AE4F01", "native source Cull index packing");
        Check(tc ? 0x94E315 : 0x94F3E5, 117,
            "7B6BE7BA3F568E094F15BEDBAE4AAF5D936AFCA2A0EE07105093B087D1998249",
            "D66E61D7F53FBB0F86224954C502B95D83E9A4AB88D38399D7DFA44C06017DA6", "native FrontCounterClockwise and Cull table consumer");
        int vtable = tc ? 0x16E68D8 : 0x16E7508;
        if (BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(vtable + 8, 8)) != 0x140000000UL + (uint)(tc ? 0xEA4520 : 0xEA5510))
            throw new InvalidDataException("GeneralScene CPU.v8 differs from the accepted normal execution function.");
        uint temporaryCull = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(
            terrainRva - start + (0xD323E4 - 0xD31D30) + 7, 4));
        var tableBytes = image.Raw(tc ? 0x14BF850 : 0x14C0730, 12);
        uint[] table = new uint[3];
        for (int i = 0; i < table.Length; i++)
            table[i] = BinaryPrimitives.ReadUInt32LittleEndian(tableBytes.Slice(i * 4, 4));
        if (temporaryCull != 2 || !table.SequenceEqual(new uint[] { 1, 3, 2 }))
            throw new InvalidDataException("Terrain caller temporary Cull or native table changed.");
        uint factoryClass = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(classGetter - start + 1, 4));
        return new(locale, profile.ExeHash, temporaryCull, table, factoryClass);
    }
}
