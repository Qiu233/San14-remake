using System.Buffers.Binary;
using System.Numerics;
using System.Collections.ObjectModel;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalWorldGeometryLateHostNativeSource LoadWorldGeometryLateHostNativeSource(
        string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "Late Host executable");
        var image = new NativeImage(exe);
        byte[] code = image.DecodeText(out int codeRva);
        VerifyHash(code, profile.TextHash, "Late Host decoded text");
        bool tc = locale == "TC";
        int vtable = tc ? 0x13C4ED0 : 0x13C5CF0;
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        void Check(string name, int tcRva, int scRva, int count, string tcHash, string scHash)
        {
            string hash = tc ? tcHash : scHash;
            VerifyHash(code.AsSpan((tc ? tcRva : scRva) - codeRva, count), hash, name);
            hashes.Add(name, hash);
        }
        Check("hostBaseConstructor", 0x00BF58A0, 0x00BF6DC0, 1837,
            "EEFB20465AA0E0C1A201F35E0C25078514469D562675FF20B62EF84D964D1105", "73B0DA72E31B0A773F2AF02B203D05F53AD924A15D38C976D426CF162148A02C");
        Check("rendererCreatorColourAllocation", 0x0012FD9B, 0x0012FB3B, 21,
            "7C8DC200EA4889AD5020245A9C5309D80CBFE467F0664B20B26648F347FDE8A3", "B500C8873AA4122FB76CB345C57A93248CCEDD64EC16187F09F8C3A7E33A3480");
        Check("rendererCreatorColourArgument", 0x0012FF65, 0x0012FD05, 18,
            "1D90BF49E7442F2AB25A35DAC920A98963EBE3E83E620B28E9D01C0DBFDCE568", "1D90BF49E7442F2AB25A35DAC920A98963EBE3E83E620B28E9D01C0DBFDCE568");
        Check("factoryColourArgument", 0x000D553B, 0x000D52CB, 13,
            "BF5B106ABCDD95EE812DF70DA645D4924076DCBB0318D8C977F18FE280812CDA", "BF5B106ABCDD95EE812DF70DA645D4924076DCBB0318D8C977F18FE280812CDA");
        Check("wrapperColourArgument", 0x004007EE, 0x00400D1E, 12,
            "DAF901E9332E0F0F5D3A1F45D532E62103D89B474366AB463FBEAB546DE51B5C", "DAF901E9332E0F0F5D3A1F45D532E62103D89B474366AB463FBEAB546DE51B5C");
        Check("subWrapperColourArgument", 0x0040058E, 0x00400ABE, 12,
            "75A42CDB91658857B1C099BC78D13C7A7700DF4E139E9C7EB9A8F822580BC00C", "75A42CDB91658857B1C099BC78D13C7A7700DF4E139E9C7EB9A8F822580BC00C");
        Check("hostColourStore", 0x00BF592C, 0x00BF6E4C, 12,
            "B832EBFE2D5CBE1BBB50DE8AFD59294A8C398E42D4C3580555481DD086D49829", "B832EBFE2D5CBE1BBB50DE8AFD59294A8C398E42D4C3580555481DD086D49829");
        Check("hostResetLoans", 0x00BF8730, 0x00BF9C20, 28,
            "DBB5E6DF14D0102FDEAF45287042187EFB61117B895F418E254A94349EDD7D31", "DBB5E6DF14D0102FDEAF45287042187EFB61117B895F418E254A94349EDD7D31");
        Check("colourOwnerCreate", 0x00985740, 0x00986880, 149,
            "51DDF746B262885619C30BD316E790671333C7FF429B7F333FA7064A27B1A60B", "96CFF1238F7C74E8C67660D3F69546C79476C3BEEFF168D137CFFE1C35B2C14F");
        Check("cameraAmbientZeroKey", 0x00C57B59, 0x00C59159, 74,
            "53763CB07901EA851C848D58C7125F258386772E99572E87695E6C5AFC1783B8", "2E126FE1DCFF0F7EAACA82315EAC936E89249116A60F3C48B9FB593F1D5CFA6C");
        Check("cameraAmbientReferenceCopy", 0x00CAB228, 0x00CAC8F8, 14,
            "6B1AEABEF78B32D865CD8BF449E0A54B0BE19A8BD0102E12AFE7ABC3FB6AD381", "6B1AEABEF78B32D865CD8BF449E0A54B0BE19A8BD0102E12AFE7ABC3FB6AD381");
        Check("cameraAmbientViewCopy", 0x00D0623B, 0x00D06EFB, 14,
            "EEBF41AB742ECA979A61E20D74D95FA01155784637EBEE16B5C2C208FC444BFA", "EEBF41AB742ECA979A61E20D74D95FA01155784637EBEE16B5C2C208FC444BFA");
        Check("skyPublication", 0x00EB5A8E, 0x00EB6A7E, 16,
            "D53AF6FB6925FBC202F0C4590ECFF17B3F78FBD50C1BE58D36BA8E7CD0D9FED9", "D53AF6FB6925FBC202F0C4590ECFF17B3F78FBD50C1BE58D36BA8E7CD0D9FED9");
        void Accessor(int slot, int tcRva, int scRva, int count, string hash)
        {
            int rva = tc ? tcRva : scRva;
            if (BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(vtable + slot, 8)) !=
                0x140000000UL + (uint)rva)
                throw new InvalidDataException("The Main renderer late parameter getter slot changed.");
            VerifyHash(code.AsSpan(rva - codeRva, count), hash, $"Host virtual{slot:X}");
            hashes.Add($"virtual{slot:X}", hash);
        }
        Accessor(0x120, 0x00A244E0, 0x00BAA820, 5, "091F5E45F0BE74D7EE4478154027E057C5C9D8B9D8F3CE62E44700EE355C23DE");
        Accessor(0x140, 0x00F21680, 0x00A63DB0, 3, "4BC724F3B1D0CAF4FE369C18CBA3102E6C4EA057F63FE1587E3973134A7F755E");
        Accessor(0x190, 0x00BFEAB0, 0x00BFFF90, 8, "A1FED5E056C58799E3B9C2E03CF27CB23821971DECFCDFE11E15F1D7479DA093");
        Accessor(0x198, 0x00BAAAE0, 0x00EDD850, 8, "FE07572F58FCC553C9CCD7AF94DC3F3763F89C2AC85E6467C42DC47295D22005");
        Accessor(0x1A0, 0x00BFEA90, 0x00BFFF70, 8, "8622F2826F6EFE5AB35C9BF54D69FAFFE7F12907ED0451AC4CE31906EEE8677B");
        Accessor(0x1A8, 0x00BAAAA0, 0x00BFA140, 8, "F2F31F708BAC409FFC68AB28B6C5B7B7EEDF0564B482C1B8955DAB692380EAD8");
        var vectorBytes = image.Raw(tc ? 0x14BB8B0 : 0x14BC790, 16);
        VerifyHash(vectorBytes, "F6BB1294DA2F78CD935B01C7656280DF5EAA0439E9D97BC03775825A41A508E4",
            "Q58 creator two initialized vectors");
        static float Read(ReadOnlySpan<byte> bytes, int offset) => BitConverter.UInt32BitsToSingle(
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)));
        var one = new Vector4(Read(vectorBytes, 0), Read(vectorBytes, 4),
            Read(vectorBytes, 8), Read(vectorBytes, 12));
        OriginalWorldGeometryLateHostGetter[] getters =
        [
            new(0x120, tc ? 0xA244E0 : 0xBAA820, 0x58, false),
            new(0x140, tc ? 0xF21680 : 0xA63DB0, null, true),
            new(0x198, tc ? 0xBAAAE0 : 0xEDD850, 0x88, false),
            new(0x1A8, tc ? 0xBAAAA0 : 0xBFA140, 0x80, false),
        ];
        return new(locale, profile.ExeHash, profile.TextHash, vtable,
            Array.AsReadOnly(getters), new ReadOnlyDictionary<string, string>(hashes),
            new(Vector4.One, one, one));
    }
}
