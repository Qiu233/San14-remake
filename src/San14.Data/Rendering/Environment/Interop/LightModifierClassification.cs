using System.Buffers.Binary;

namespace San14.Data;

// Project-owned typed default classifiers for the packed light modifiers.
// No runtime original code, descriptor mirror, PE reader or interpreter is used.
internal static class LightModifierClassification
{
    internal static void Apply(byte[] light, uint contextFlags3E4)
    {
        const int origin = 0x1B0;
        float F(int index) => BinaryPrimitives.ReadSingleLittleEndian(light.AsSpan(origin + index * 4, 4));
        uint U(int index) => BinaryPrimitives.ReadUInt32LittleEndian(light.AsSpan(origin + index * 4, 4));
        byte B(int offset) => light[origin + offset];
        bool All(int first, int last, float value)
        {
            for (int i = first; i <= last; i++) if (F(i) != value) return false;
            return true;
        }
        bool Indices(float value, params int[] indices)
        {
            foreach (int i in indices) if (F(i) != value) return false;
            return true;
        }
        float Bits(uint value) => BitConverter.Int32BitsToSingle(unchecked((int)value));

        bool groupA = All(0x13, 0x15, 0) && All(0x16, 0x18, 1)
            && All(0x19, 0x1B, 0) && All(0x1C, 0x1E, 1)
            && All(0x1F, 0x21, 0) && All(0x22, 0x24, 1)
            && All(0x25, 0x2A, 0) && F(0x2B) == 32000 && F(0x2C) == 32000
            && F(0x2D) == 1 && F(0x2E) == 1 && F(0x2F) == 0;
        bool groupB = true;
        for (int i = (contextFlags3E4 & 3) == 1 ? 1 : 0; i < 16 && groupB; i++)
        {
            groupB = All(0x76 + i * 4, 0x78 + i * 4, 1) && F(0x79 + i * 4) == 2
                && B(0x2D8 + i) == 0 && B(0x2E8 + i) == 2 && B(0x2F8 + i) == 0xFF
                && All(0xC2 + i * 4, 0xC5 + i * 4, 0)
                && All(0x102 + i * 4, 0x105 + i * 4, 0)
                && All(0x142 + i * 8, 0x149 + i * 8, 1)
                && All(0x1C2 + i * 4, 0x1C5 + i * 4, 1);
        }
        bool groupC = Indices(0, 0x202, 0x20A, 0x210, 0x213, 0x21D, 0x21E, 0x224, 0x225,
                0x22B, 0x22C, 0x23C, 0x23E, 0x23F, 0x244, 0x247)
            && All(0x205, 0x208, 0) && All(0x215, 0x217, 0) && All(0x232, 0x239, 0)
            && Indices(1, 0x209, 0x211, 0x212, 0x214, 0x23A, 0x23B, 0x23D, 0x248)
            && All(0x20B, 0x20F, 1) && All(0x21A, 0x21C, 1) && All(0x221, 0x223, 1)
            && All(0x228, 0x22A, 1) && All(0x22F, 0x231, 1) && All(0x240, 0x243, 1)
            && F(0x203) == 20 && F(0x204) == 45
            && Indices(15, 0x218, 0x21F, 0x226, 0x22D)
            && Indices(10, 0x219, 0x220, 0x227, 0x22E) && B(0x924) == 0;
        bool groupD = All(0x257, 0x25E, 0) && All(0x25F, 0x265, 1)
            && F(0x266) == 0 && F(0x267) == 0 && F(0x268) == 350 && All(0x269, 0x26D, 0)
            && B(0x9B8) == 0 && B(0x9B9) == 0 && B(0x9BA) == 0 && B(0x9BB) == 1
            && B(0x9BC) == 0 && B(0x9BD) == 0 && B(0x9BE) == 0;
        bool groupE = All(0x275, 0x278, 0) && B(0x9E4) == 0 && F(0x27A) == 0
            && All(0x27B, 0x27E, 0) && All(0x27F, 0x282, 1) && All(0x283, 0x2C2, 0)
            && U(0x2C3) == 0 && F(0x2C4) == 0;
        bool groupF = F(0x2F1) == Bits(0x40AAAAA4) && F(0x2F2) == Bits(0x40AAAAA4)
            && F(0x2F3) == Bits(0x41CCCCCD) && F(0x2F4) == Bits(0x41CCCCCD)
            && F(0x2F5) == Bits(0x3F555550) && F(0x2F6) == -.5f
            && F(0x2F7) == Bits(0xC0E66666) && F(0x2F8) == -12
            && All(0x2F9, 0x2FB, .5f) && All(0x2FC, 0x301, 1)
            && F(0x302) == 10 && F(0x303) == 1
            && F(0x304) == Bits(0x41C0CCCD) && F(0x305) == Bits(0xC27D999A)
            && B(0xC18) == 0 && F(0x307) == Bits(0x4230CCCD) && F(0x308) == Bits(0xC2DE999A)
            && B(0xC24) == 0 && F(0x30A) == 0 && F(0x30B) == 0 && B(0xC30) == 0;
        bool groupG = F(0x24A) == 0 && F(0x24B) == -1 && All(0x24C, 0x250, 0)
            && F(0x251) == 1 && F(0x252) == 1 && All(0x253, 0x256, 0);

        uint firstMask = 0;
        void Add(uint bit, bool value) { if (value) firstMask |= bit; }
        Add(0x00000001, All(0, 0xE, 1) && U(0xF) == 0xFFFFFFFF && U(0x10) == 0);
        Add(0x00000002, groupA);
        Add(0x00000004, F(0x30) == Bits(0x3C23D70A) && All(0x31, 0x33, 1));
        Add(0x00000008, All(0x38, 0x3B, 0) && F(0x3C) == 32000 && F(0x3D) == 32000 && All(0x3E, 0x41, 0));
        Add(0x00000010, F(0x44) == Bits(0x3C449BA6) && F(0x45) == 0 && F(0x46) == Bits(0x38D1B717));
        Add(0x00000020, All(0x47, 0x51, 1) && F(0x52) == 0);
        Add(0x00000040, F(0x53) == 1);
        Add(0x00000080, All(0x54, 0x64, 1));
        Add(0x00000100, All(0, 3, 1));
        Add(0x00000200, All(7, 0xA, 1));
        Add(0x00000400, All(4, 6, 1));
        Add(0x00000800, All(0xB, 0xD, 1));
        Add(0x00001000, F(0x65) == 0 && F(0x66) == 0 && F(0x67) == 1);
        Add(0x00002000, All(0x69, 0x6D, 1));
        Add(0x00004000, F(0x6E) == 1);
        Add(0x00008000, F(0x72) == 1);
        Add(0x00010000, F(0x73) == 1 && F(0x74) == 1);
        Add(0x00020000, All(0x34, 0x37, 1));
        Add(0x00040000, groupB);
        Add(0x00080000, groupC);
        Add(0x00100000, F(0x6F) == 1);
        Add(0x00200000, F(0x68) == 1);
        Add(0x00400000, groupD);
        Add(0x00800000, F(0x270) == 1 && F(0x271) == 1);
        Add(0x01000000, F(0x273) == 0);
        Add(0x02000000, F(0x274) == 0);
        Add(0x04000000, groupE);
        Add(0x08000000, All(0x2C5, 0x2F0, 1));
        Add(0x10000000, groupF);
        Add(0x20000000, All(0x30E, 0x310, 1));
        Add(0x40000000, F(0x272) == 0);
        Add(0x80000000, F(0x315) == 0 && F(0x316) == 0 && F(0x31A) == 0);

        uint secondMask = 0;
        void AddSecond(uint bit, bool value) { if (value) secondMask |= bit; }
        AddSecond(0x001, F(0x31B) == 0);
        AddSecond(0x002, F(0x31C) == 5000 && F(0x31D) == .5f && F(0x31E) == 1);
        AddSecond(0x004, F(0x31F) == 0 && F(0x320) == 0 && F(0x321) == 0);
        AddSecond(0x008, All(0x322, 0x339, 1) && All(0x33A, 0x351, 0));
        AddSecond(0x010, All(0x352, 0x354, 1));
        AddSecond(0x020, F(0x70) == 1 && F(0x71) == 1);
        AddSecond(0x040, groupG);
        AddSecond(0x080, F(0x75) == 1);
        AddSecond(0x100, All(0x355, 0x35A, 0));
        BinaryPrimitives.WriteUInt32LittleEndian(light.AsSpan(0x1F4, 4), firstMask);
        BinaryPrimitives.WriteUInt32LittleEndian(light.AsSpan(0x1F8, 4), secondMask);
    }
}
