using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyIndirectPatternMapSource LoadEarlyIndirectPatternMapSource(string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "Early68 actual native source");
        var image = new NativeImage(exe); byte[] text = image.DecodeText(out int start);
        VerifyHash(text, profile.TextHash, "Early68 actual decoded source");
        bool tc = locale == "TC";
        int init = tc ? 0x99FE10 : 0x9A0DB0, writer = tc ? 0xD74F30 : 0xD75CF0;
        VerifyHash(text.AsSpan(init - start, 224), tc
            ? "4AAABEC21D44A7243619F90DE18909D3090D98E66F974C5A8D21DD369B717079"
            : "7C56080B242AAE13B5716670466A91AE2FCBC655570F7C63DC56E80B0DD29724", "PatternMap full initializer");
        VerifyHash(text.AsSpan(writer - start, 1652), tc
            ? "AA539C077606C6E2B1B264FF975092796D463200B58F57CC1B6D8E26B8EF9794"
            : "EA8151F75A3C23E3EFBE66EFF417C43ADEEF666BB8171A157B4CFD6DF3CE15A8", "PatternMap full current writer");
        VerifyHash(text.AsSpan((tc ? 0xC38E90 : 0xC3A540) - start, 91),
            "0E6BBCE7040C1CBF0AF57EB26BF8D72E91EDC5CB21E5808EBC73D7906BBACDF6", "Selected PatternMap seven numeric copies");
        VerifyHash(text.AsSpan((tc ? 0xD76780 : 0xD77540) - start, 3996), tc
            ? "B979E3FB8131971F7B952ECF2AAF860755BF1A79E0F14525DA9D9080ABA4C04D"
            : "A1149A7CEA0D54225C9A025CA15FA8749FA6FE9FF346D71E4C98464852718580", "Complete current D76780 writer");
        VerifyHash(text.AsSpan((tc ? 0xD77940 : 0xD78700) - start, 262), tc
            ? "F6708D52B22C4D0600606346817D6AB496EE280703E752029F95E54A79A6BC78"
            : "639BEA82FAB44A7D0D2EADAD9F5A4DD89A3466D37DC319FBE0C854B6F07ECDCF", "Complete D77940 shadow-manager writer");
        // Complete paired source bodies, verified at import. Current group,
        // cache, Camera and Generic time remain per-request owned inputs.
        VerifyHash(text.AsSpan((tc ? 0xE69C70 : 0xE6ADE0) - start, 1029), tc
            ? "433E21CEB35D0245772CA664442D473654B4B802C086BBECDE459C10D24F2F37"
            : "A7E141A7C942E3C19455B1B84018A428742A774D9ABA251DAA81522337D4186A", "Actual93FF pass constructor");
        VerifyHash(text.AsSpan((tc ? 0xE77220 : 0xE782D0) - start, 2170), tc
            ? "4A06DED509671FCCD45DAC40F577269299EDC55E8262DD6A1AF1AF1BBAD89239"
            : "BA9500EA4F08F89480366F7F4911D1051C6C89E3AB74E0BAB93119546FF9B353", "Actual93FF graph loader");
        VerifyHash(text.AsSpan((tc ? 0xEC6F10 : 0xEC7AC0) - start, 145), tc
            ? "53548CEA6A9505B7DA1FB34D417D69BC25E233DF3E15CC18FBDB5C1EB5357B9B"
            : "547AFFF6F1D1832FDC8C45FC351D85CAB3DEC808DF986E6F274C01E91C21D357", "Source component write mask helper");
        VerifyHash(text.AsSpan((tc ? 0xE726E0 : 0xE73790) - start, 365),
            "6EA0389E6EC879938F1F5E8CBF8699497926EBBB32138FBE380C2771333E9224", "Source per-group stencil helper");
        VerifyHash(text.AsSpan((tc ? 0xCE0B30 : 0xCE2190) - start, 95),
            "DD05BA1F92FB267E8112A7E616F32052FCC8D4CC6E2669EAB1335D6A330EFF9B", "Source seven-float pattern comparison");
        VerifyHash(text.AsSpan((tc ? 0xCB6C90 : 0xCB8380) - start, 35),
            "A86829D14AE6D571EBB603A98CF2A1A39571C9FB2C3A277445FE20D28B78368F", "Source empty-group local key constructor");
        VerifyHash(text.AsSpan((tc ? 0x9FE4D0 : 0x9FF4F0) - start, 15),
            "F1F1E41DE3A62B50E343C350045C326A38FD7182F8905A86FFF782BE37B0DC96", "Source receiver-bias record reset");
        VerifyHash(text.AsSpan((tc ? 0xE2F20 : 0xE2CB0) - start, 1142), tc
            ? "744C93CDDB22918F7AD459424242DBB691F8397752577DB589B639A9B687E580"
            : "96B5D4F81FA11A5287014FACD5222DA96C0C2DBA9FF41228060DCA6EA229991C", "Actual2939 creator");
        VerifyHash(text.AsSpan((tc ? 0xA52890 : 0x1194AE0) - start, 49), tc
            ? "B6B068CA7788DF28A4682ED299724A224E8AECB506687813CAA6B2ECCB5BD34E"
            : "BD45D253FAE8021AEBE804BD85DB72F555F1DFB92CD9B400285001413D20E0B5", "Actual2939 full variant compare");
        int Rip(int at) => checked(at + 7 + BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(at - start + 3, 4)));
        byte[] matrix = new byte[64]; int index = 0;
        foreach (int offset in new[] { 0x12, 0x26, 0x32, 0x3E })
            image.Raw(Rip(init + offset), 16).CopyTo(matrix.AsSpan(index++ * 16, 16));
        VerifyHash(matrix, "5998E9D1DD9BF48AF6AF87A1EF8C7783B818AC8329637ADA08CC5FB329042376", "Actual PatternMap four constructor vectors");
        string Name(int rva) => Encoding.ASCII.GetString(image.Raw(rva, 64)[..image.Raw(rva, 64).IndexOf((byte)0)]);
        if (Name(Rip(writer + 0x5ED)) != "mW2Pt" || Name(Rip(writer + 0x261)) != "fPtn")
            throw new InvalidDataException("Actual PatternMap named setters differ.");
        int descriptor = tc ? 0x1700A48 : 0x1701698, nameRecord = tc ? 0x1700A38 : 0x1701688;
        var row = image.Raw(descriptor, 24);
        if (U32(row, 8) != 8 || U32(row, 12) != 1 || U64(row, 16) != 0x140000000UL + (uint)nameRecord ||
            Name(checked((int)(U64(image.Raw(nameRecord, 16), 0) - 0x140000000UL))) != "fDOFade")
            throw new InvalidDataException("Actual2939 parameter8 is not its imported fDOFade base directory.");
        byte[] affineRow1 = image.Raw(Rip(writer + 0x3BB), 16).ToArray(); // D752EB MOVAPS literal, stored to global row1.
        float FloatLiteral(int offset)
        {
            int at = writer + offset;
            int rva = checked(at + 8 + BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(at - start + 4, 4)));
            return BinaryPrimitives.ReadSingleLittleEndian(image.Raw(rva, 4));
        }
        float unchangedScale = FloatLiteral(0x2DF), numerator = FloatLiteral(0x3AE);
        int reset = tc ? 0xC13360 : 0xC14890;
        VerifyHash(text.AsSpan(reset - start, 146), tc
            ? "67FB22229384EF0E44218130CE2C04691DFAA24EBA0D18A70968FC247CB535F4"
            : "4CA72C01C7021E1306E54C809488473AE4C14CFA57BD59C951E92C16209F4969", "PatternMap changed-record reset");
        float resetPattern = BinaryPrimitives.ReadSingleLittleEndian(image.Raw(Rip(reset + 0x24), 4));
        byte[] resetExtra = image.Raw(Rip(reset + 0x48), 32).ToArray(), resetMatrix = image.Raw(Rip(reset + 0x6D), 64).ToArray();
        return new(locale, $"SAN14CE_{locale}.exe@{profile.ExeHash};text@{profile.TextHash};" +
            $"PatternMap-init{init:X}/writer{writer:X}/record-reset{reset:X};actual2939-base8+1-fDOFade/complete96-byte-predicate;" +
            "source-only-no-current-selection-or-pixels", matrix, affineRow1, unchangedScale, numerator, resetMatrix, resetPattern, resetExtra);
    }
}
