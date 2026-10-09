using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMainMapRawCameraPreparationSource LoadMainMapRawCameraPreparationSource(
        string directory,string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("raw-camera-preparation",locale);

        return new(locale,$"{ruleVersion};C409C0-1186-bulk+126C-byte;"+
            "loop7B8-16words;current-null/actualData-Lens;conditional262C-tail;geometry/C44480/forced-AB5BD0;"+
            "same-currentCPU-header-lower31;no-V460;no-resource-key-to-pointer",RawMainCameraNumericRanges);
    }

    // Exact original instruction order, grouped only where both offsets
    // advance by4. The loaded plan is retained without per getter/frame source reads.
    private static readonly OriginalMainMapRawCameraWordRange[] RawMainCameraNumericRanges=
    [
        new(0x18,0x1480,1),
        new(0x1C,0x148C,1),
        new(0x20,0x14A0,3),
        new(0x30,0x14B0,19),
        new(0x7C,0x1500,23),
        new(0xD8,0x1560,31),
        new(0x154,0x16D0,4),
        new(0x164,0x16F0,55),
        new(0x24C,0x17D8,1),
        new(0x25C,0x17E8,1),
        new(0x26C,0x17F8,1),
        new(0x240,0x17CC,1),
        new(0x250,0x17DC,1),
        new(0x260,0x17EC,1),
        new(0x270,0x17FC,1),
        new(0x244,0x17D0,1),
        new(0x254,0x17E0,1),
        new(0x264,0x17F0,1),
        new(0x274,0x1800,1),
        new(0x248,0x17D4,1),
        new(0x258,0x17E4,1),
        new(0x268,0x17F4,1),
        new(0x278,0x1804,41),
        new(0x324,0x18B0,2),
        new(0x31C,0x18A8,2),
        new(0x32C,0x18B8,24),
        new(0x3A0,0x192C,3),
        new(0x38C,0x1918,5),
        new(0x3AC,0x1938,8),
        new(0x3CC,0x195C,5),
        new(0x3E0,0x1974,2),
        new(0x3E8,0x1980,2),
        new(0x3F0,0x198C,23),
        new(0x44C,0x19F4,11),
        new(0x478,0x1A2C,19),
        new(0x4C4,0x1A7C,2),
        new(0x4CC,0x1A88,20),
        new(0x2C,0x14AC,1),
        new(0x51C,0x1AD8,3),
        new(0x544,0x1B00,28),
        new(0x528,0x1AE4,7),
        new(0x5B4,0x1B70,129),
        new(0x7B8,0x1D74,16),
        new(0x7F8,0x1DB4,14),
        new(0xB2C,0x1DEC,1),
        new(0x830,0x1DF0,52),
        new(0x910,0x1EC0,1),
        new(0x900,0x1EC4,4),
        new(0x914,0x1ED4,8),
        new(0x934,0x1F04,4),
        new(0x944,0x1EF4,4),
        new(0x954,0x1F14,4),
        new(0xA88,0x2044,1),
        new(0xA8C,0x204C,1),
        new(0xA08,0x2048,1),
        new(0xA90,0x2050,5),
        new(0x964,0x1F24,41),
        new(0xA0C,0x1FC8,31),
        new(0xAA4,0x2064,6),
        new(0xABC,0x2080,28),
        new(0xB30,0x20F0,12),
        new(0xB60,0x2124,2),
        new(0xB68,0x2120,1),
        new(0xB6C,0x212C,68),
        new(0xC7C,0x2240,1),
        new(0xC80,0x2248,3),
        new(0xC8C,0x2258,62),
        new(0xD84,0x2354,8),
        new(0xDAC,0x237C,3),
        new(0xDA4,0x2374,2),
        new(0xDC0,0x2390,4),
        new(0xDB8,0x2388,2),
        new(0xDD0,0x23A0,51),
        new(0xEA0,0x2478,3),
        new(0xEAC,0x2488,12),
        new(0xE9C,0x2474,1),
        new(0xEDC,0x24B8,41),
        new(0xF84,0x255C,1),
        new(0xF80,0x2560,1),
        new(0xF88,0x2564,7),
        new(0xFA4,0x2584,1),
        new(0xFA8,0x258C,34),
        new(0x1038,0x261C,4),
        new(0x104C,0x2630,35),
        new(0x10D8,0x26CC,65),
        new(0x11E0,0x27D4,1),
        new(0x11DC,0x27D0,1),
        new(0x11E4,0x27D8,13),
        new(0x1318,0x280C,3),
        new(0x1218,0x2818,21),
        new(0x1270,0x2870,4),
        new(0x1284,0x2888,25),
    ];
}
