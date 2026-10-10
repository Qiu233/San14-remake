using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Initial camera ABI fields imported directly from resource properties.
/// Geometry and optical owners publish their current values separately.</summary>
internal static class CameraDeclarationEncoding
{
    internal static byte[] CreateEmptyPrefix()
    {
        byte[] prefix = new byte[MapCameraConstruction.PrefixByteLength];
        BinaryPrimitives.WriteUInt64LittleEndian(prefix, (ulong)RenderStateRecordKind.PerspectiveCamera);
        prefix[0x161] = 1;
        return prefix;
    }
    internal static byte[] ImportInitialPrefix(DokObject camera)
    {
        var prefix = CreateEmptyPrefix();
        var p = new CameraNumericPropertyReader(camera);
        Write(prefix, 0x150, p.Word(7, 0x0624AD1Du, 8, 1, 0));
        Write(prefix, 0x15C, p.Word(8, 0xBAE710B0u, 8, 1, 0));
        Write(prefix, 0x170, p.Word(9, 0xB90D1027u, 8, 1, 0));
        Write(prefix, 0x174, p.Word(10, 0xABB91759u, 8, 1, 0));
        Write(prefix, 0x178, p.Word(11, 0x3DF6821Au, 8, 1, 0));
        Write(prefix, 0x17C, p.Word(88, 0x462994C1u, 8, 1, 0));
        Write(prefix, 0x180, p.Word(12, 0xC96AEEA5u, 8, 1, 0));
        Write(prefix, 0x184, p.Word(13, 0x721A508Au, 8, 1, 0));
        Write(prefix, 0x188, p.Word(14, 0xA0E5DA98u, 8, 1, 0));
        Write(prefix, 0x18C, p.Word(15, 0x16B54787u, 8, 3, 0));
        Write(prefix, 0x190, p.Word(15, 0x16B54787u, 8, 3, 1));
        Write(prefix, 0x194, p.Word(15, 0x16B54787u, 8, 3, 2));
        Write(prefix, 0x198, p.Word(16, 0xD40D74C8u, 8, 3, 0));
        Write(prefix, 0x19C, p.Word(16, 0xD40D74C8u, 8, 3, 1));
        Write(prefix, 0x1A0, p.Word(16, 0xD40D74C8u, 8, 3, 2));
        Write(prefix, 0x1A4, p.Word(17, 0x1951CDF3u, 4, 1, 0));
        Write(prefix, 0x1A8, p.Word(18, 0xDF73CE78u, 8, 1, 0));
        Write(prefix, 0x1AC, p.Word(19, 0x87454A68u, 8, 1, 0));
        Write(prefix, 0x1B0, p.Word(20, 0xA7C1A130u, 8, 1, 0));
        Write(prefix, 0x1B4, p.Word(28, 0x62AA34F8u, 8, 1, 0));
        Write(prefix, 0x1B8, p.Word(29, 0xF9989A1Cu, 8, 1, 0));
        Write(prefix, 0x1BC, p.Word(30, 0x13AC572Eu, 8, 1, 0));
        Write(prefix, 0x1C0, p.Word(31, 0xCD8B987Fu, 8, 1, 0));
        Write(prefix, 0x1C4, p.Word(54, 0x907B67C1u, 8, 1, 0));
        Write(prefix, 0x1C8, p.Word(55, 0x800DB8FFu, 8, 1, 0));
        Write(prefix, 0x1D0, p.Word(850, 0x2F6A6AF9u, 8, 1, 0));
        Write(prefix, 0x1D4, p.Word(851, 0xDF989EE1u, 8, 1, 0));
        Write(prefix, 0x1D8, p.Word(852, 0xAC082408u, 8, 1, 0));
        Write(prefix, 0x1DC, p.Word(853, 0x4669C572u, 8, 1, 0));
        Write(prefix, 0x1E0, p.Word(891, 0x90B9A1E5u, 8, 1, 0));
        Write(prefix, 0x1E4, p.Word(914, 0xEA5C2C8Du, 8, 1, 0));
        Write(prefix, 0x1E8, p.Word(923, 0x53E41154u, 8, 1, 0));
        Write(prefix, 0x1EC, p.Word(928, 0x273D81CEu, 8, 1, 0));
        Write(prefix, 0x1F0, p.Word(930, 0xBBE67AEDu, 8, 1, 0));
        Write(prefix, 0x1F4, p.Word(931, 0x0C3B06E3u, 8, 1, 0));
        Write(prefix, 0x1F8, p.Word(1047, 0x93B62B81u, 8, 1, 0));
        Write(prefix, 0x1FC, p.Word(1048, 0xF8D97B10u, 8, 1, 0));
        Write(prefix, 0x200, p.Word(826, 0x085B6991u, 8, 1, 0));
        Write(prefix, 0x204, p.Word(827, 0x1EB4D0F0u, 8, 1, 0));
        Write(prefix, 0x208, p.Word(828, 0x4170AF46u, 8, 1, 0));
        Write(prefix, 0x20C, p.Word(829, 0xFEEDD601u, 8, 1, 0));
        Write(prefix, 0x210, p.Word(830, 0xE599A7F7u, 8, 1, 0));
        Write(prefix, 0x214, p.Word(831, 0xF3554D1Du, 8, 1, 0));
        Write(prefix, 0x218, p.Word(832, 0xBF2E4097u, 8, 1, 0));
        Write(prefix, 0x21C, p.Word(939, 0x3C750447u, 4, 1, 0));
        Write(prefix, 0x220, p.Word(904, 0xC579866Bu, 8, 1, 0));
        Write(prefix, 0x224, p.Word(973, 0xC9B352ACu, 8, 1, 0));
        Write(prefix, 0x228, p.Word(974, 0x7F2CA7D3u, 8, 1, 0));
        Write(prefix, 0x230, p.Word(896, 0x49E1FAACu, 8, 1, 0));
        Write(prefix, 0x234, p.Word(897, 0x502747A5u, 8, 1, 0));
        Write(prefix, 0x238, p.Word(898, 0xFFC8C20Du, 8, 1, 0));
        Write(prefix, 0x23C, p.Word(899, 0xB7299006u, 8, 1, 0));
        Write(prefix, 0x240, p.Word(900, 0xF57EBD90u, 8, 1, 0));
        Write(prefix, 0x244, p.Word(901, 0x591F7389u, 8, 1, 0));
        Write(prefix, 0x248, p.Word(915, 0xFAF89ED8u, 8, 1, 0));
        Write(prefix, 0x24C, p.Word(919, 0x5E26AA91u, 8, 1, 0));
        Write(prefix, 0x250, p.Word(920, 0x1F9BB007u, 8, 1, 0));
        Write(prefix, 0x254, p.Word(921, 0xBC6DBC69u, 8, 1, 0));
        Write(prefix, 0x258, p.Word(924, 0x96A26C16u, 8, 1, 0));
        Write(prefix, 0x25C, p.Word(925, 0x2F330B61u, 8, 1, 0));
        Write(prefix, 0x260, p.Word(926, 0x85FCC148u, 8, 3, 0));
        Write(prefix, 0x264, p.Word(926, 0x85FCC148u, 8, 3, 1));
        Write(prefix, 0x268, p.Word(926, 0x85FCC148u, 8, 3, 2));
        Write(prefix, 0x26C, p.Word(927, 0x9BFA4BC8u, 8, 3, 0));
        Write(prefix, 0x270, p.Word(927, 0x9BFA4BC8u, 8, 3, 1));
        Write(prefix, 0x274, p.Word(927, 0x9BFA4BC8u, 8, 3, 2));
        Write(prefix, 0x278, p.Word(934, 0xA48ECB2Au, 8, 1, 0));
        Write(prefix, 0x27C, p.Word(854, 0x810B5624u, 8, 1, 0));
        Write(prefix, 0x280, p.Word(840, 0xAA84CF1Cu, 8, 1, 0));
        Write(prefix, 0x284, p.Word(841, 0x71003548u, 8, 1, 0));
        Write(prefix, 0x288, p.Word(902, 0x7C80D02Bu, 8, 1, 0));
        Write(prefix, 0x28C, p.Word(903, 0x484B95EBu, 8, 1, 0));
        Write(prefix, 0x290, p.Word(976, 0x97D65DD2u, 8, 1, 0));
        Write(prefix, 0x294, p.Word(1056, 0x7C1C051Eu, 8, 1, 0));
        Write(prefix, 0x298, p.Word(1057, 0x38E69314u, 8, 1, 0));
        Write(prefix, 0x29C, p.Word(1058, 0xC4DB66F5u, 8, 2, 0));
        Write(prefix, 0x2A0, p.Word(1058, 0xC4DB66F5u, 8, 2, 1));
        Write(prefix, 0x2A4, p.Word(1059, 0xA5B5D92Au, 8, 2, 0));
        Write(prefix, 0x2A8, p.Word(1059, 0xA5B5D92Au, 8, 2, 1));
        Write(prefix, 0x3A0, p.Word(905, 0xBE47DAC5u, 4, 1, 0));
        Write(prefix, 0x3A4, p.Word(864, 0x0B6C81F1u, 4, 1, 0));
        Write(prefix, 0x3A8, p.Word(842, 0x2F288D0Cu, 4, 1, 0));
        Write(prefix, 0x3AC, p.Word(843, 0x48457126u, 4, 1, 0));
        Write(prefix, 0x1CC, uint.MaxValue);
        return prefix;
    }
    private static void Write(Span<byte> prefix, int offset, uint bits)
        => BinaryPrimitives.WriteUInt32LittleEndian(prefix[offset..], bits);
}
