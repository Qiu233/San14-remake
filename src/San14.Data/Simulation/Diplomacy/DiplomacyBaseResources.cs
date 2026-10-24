using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Gate90/A0 both delegate through22A940 to its current mapped City's
    //stock; Gate34/38 are not separate diplomatic treasuries. Troops3C
    //remain owned by the actual Base and are deliberately not routed here.
    internal static int ReadDiplomacyBaseResourceCurrent(SourceWorldContext current,
        BaseReference home, BaseResourceKind resource)
    {
        if (resource is not (BaseResourceKind.Gold or BaseResourceKind.Supplies))
            throw new ArgumentOutOfRangeException(nameof(resource));
        var city = BaseCurrencyAdditionSource.CurrencyCity(home, current);
        return DNeed(current.ReadBaseResource(city, resource), "diplomacy_Base_virtual_resource");
    }
}
