using System.Numerics;

namespace San14.Data;

public sealed partial class OrdinarySdfModelShapeCatalog
{
    // Joint keys index the resource bone-name mapping; they are not pose-array indices.
    internal static OrdinarySdfModelShapeCatalog ConstructImported() => new(
        [
            new(12, null, 1.10000002f, 0.400000006f, new(0f, -5f, 0f, 1f), new(2f, 1.5f, 0f, 1f)), // shape 0
            new(9, 3, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 1
            new(3, 4, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 2
            new(4, 9, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 3
            new(3, 5, 1.29999995f, 0.600000024f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 4
            new(4, 6, 1.29999995f, 0.600000024f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 5
            new(5, 7, 0.800000012f, 0.699999988f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 6
            new(6, 8, 0.800000012f, 0.699999988f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 7
            new(9, 10, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 8
            new(10, 11, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 9
            new(10, 15, 1f, 1f, new(1f, 0f, 0f, 0f), new(0f, 0f, 0f, 1f)), // shape 10
            new(15, 16, 0.800000012f, 0.800000012f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 11
            new(10, 16, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 12
            new(15, 17, 0.600000024f, 0.5f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 13
            new(16, 18, 0.600000024f, 0.5f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 14
            new(17, 19, 0.600000024f, 0.5f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 15
            new(18, 20, 0.600000024f, 0.5f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 16
            new(7, 23, 0.800000012f, 0.5f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 17
            new(8, 24, 0.800000012f, 0.5f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 0f)), // shape 18
            new(13, 10, 1f, 1f, new(2.5f, 0f, 0f, 1f), new(0f, 0f, 2.5f, 1f)), // shape 19
            new(14, 10, 1f, 1f, new(2.5f, 0f, 0f, 1f), new(0f, 0f, -2.5f, 1f)), // shape 20
            new(10, 10, 1f, 1f, new(15f, 0f, 5f, 1f), new(15f, 0f, -5f, 1f)), // shape 21
            new(10, 10, 1f, 1f, new(0f, 0f, 5f, 1f), new(0f, 0f, -5f, 0f)), // shape 22
            new(10, 10, 1f, 1f, new(-10f, 0f, 5f, 1f), new(-10f, 0f, -5f, 1f)), // shape 23
            new(13, null, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 24
            new(14, null, 1f, 1f, new(0f, 0f, 0f, 1f), new(0f, 0f, 0f, 1f)), // shape 25
            new(10, null, 1f, 1f, new(0f, 0f, 5f, 1f), new(0f, 0f, 0f, 1f)), // shape 26
            new(10, null, 1f, 1f, new(0f, 0f, -5f, 1f), new(0f, 0f, 0f, 1f)), // shape 27
            new(9, null, 1f, 1f, new(-5f, 0f, 5f, 1f), new(0f, 0f, 0f, 1f)), // shape 28
            new(9, null, 1f, 1f, new(-5f, 0f, -5f, 1f), new(0f, 0f, 0f, 1f)), // shape 29
        ],
        [
            new(1, 16, 0x0007FFF5u, 1f), // profile 0
            new(1, 8, 0x0001FE01u, 1f), // profile 1
            new(0, 10, 0x000601FEu, 1f), // profile 2
            new(1, 18, 0x0007FFFFu, 0.800000012f), // profile 3
            new(1, 14, 0x0007EBF5u, 1f), // profile 4
            new(1, 14, 0x0007EBF5u, 0.800000012f), // profile 5
            new(1, 18, 0x0007FFFFu, 1f), // profile 6
            new(1, 13, 0x001FE0F5u, 1f), // profile 7
            new(1, 15, 0x001FE0FFu, 1f), // profile 8
            new(1, 13, 0x00E7E0F1u, 1f), // profile 9
            new(7, 10, 0x3F07E0F1u, 1f), // profile 10
        ],
        // Resource bone-name hashes used by the actual skeleton lookup.
        [0x0696CA22u, 0x7AEE68CCu, 0x8630DCF5u, 0xF6BB7F79u, 0xF7100C7Fu, 0xFE30A4D4u, 0xFE8531DAu, 0x2301BC18u, 0x2356491Eu, 0xE59784DDu, 0x7A7C379Eu, 0x90F79E3Au, 0x4FAE6099u, 0x702951A6u, 0x707DDEACu, 0x7855FA62u, 0x78AA8768u, 0x58783486u, 0x58CCC18Cu, 0x05A23D77u, 0x05F6CA7Du, 0x881AA4AAu, 0x886F31B0u, 0x48A3327Du, 0x48F7BF83u, 0x9E74C0A1u]);
}
