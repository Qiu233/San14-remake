using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerDetailsCurrentReader
    {
        private DetailMiniMapValues? ReadMiniMap()
        {
            //907EA0 intentionally marks the person's current home base E2,
            //not the E4 Army or an associated journey's interpolated position.
            if(ForceId==51)return null; //232500.
            int home=DetailNeed(current.ReadPersonFirstLocation(physical),"minimap_home_location");
            var location=BaseLookup.Find(home,current);
            int at=current.ReadBaseCurrentHexIndex(location);
            if(at is <0 or >=48400)throw OfficerDetailMissing("minimap_invalid_base_point");
            var bases=ReadDetailMiniMapBases(current) ?? throw OfficerDetailMissing("minimap_base_publication");
            byte color=ReadMiniMapColor(current,ForceId) ?? throw OfficerDetailMissing("minimap_force_color");
            return new(bases,DetailMiniMapProjection.Project(DetailMiniMapIconKind.Person,person.Id,at,color));
        }
    }
}
