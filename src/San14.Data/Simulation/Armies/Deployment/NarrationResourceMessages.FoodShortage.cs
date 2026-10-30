namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //Finite installed TC/SC GameMsg closure for both reached Army and
    //garrison food-shortage reports. Their four actual roots have no J/C
    //children; the live normal formatter still owns all current operands.
    private static readonly HashSet<int> FoodShortageReportRoots =
    [
        0x5E3D, 0x5E3E, 0x5E41, 0x5E42,
    ];
    private static readonly HashSet<int> FoodShortageReportMessages =
    [
        0x5E3D, 0x5E3E, 0x5E41, 0x5E42,
    ];
}
