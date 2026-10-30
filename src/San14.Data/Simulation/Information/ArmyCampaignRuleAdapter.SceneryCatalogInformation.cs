using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The normal information row opens the Collection scenery layout,
    /// while every place and mini-map owner remains the current campaign.</summary>
    public AreaAdministrationQuery<SceneryCatalogInformation> PrepareSceneryCatalogInformation(GameState state,int sceneryId)
    {
        try
        {
            var current=CaptureSceneryMapCurrent(state);
            var scenery=InvestmentScenery(current,sceneryId);
            if(!IK(current.ReadSceneryNativeValidity(scenery),"catalog_validity"))throw InvestmentMissing("catalog_invalid_site");
            var definition=_landmarkDefinitions.Catalog.Sceneries[sceneryId];
            var area=TalentSceneryArea(current,scenery);
            bool valid=IK(current.ReadAreaNativeValidity(area),"catalog_Area_validity");
            int cityId=valid?IK(current.ReadAreaCitySlot(area),"catalog_City"):0;
            if(cityId>=52)cityId=0;
            var city=current.ReadCityTableReference(cityId)??throw InvestmentMissing("catalog_City_pointer");
            int cityPhysical=IK(city.PhysicalSlot,"catalog_City_identity");
            string cityName=valid&&IK(current.ReadBaseNativeValidity(city),"catalog_City_validity")
                ?state.Cities.GetValueOrDefault(cityPhysical)?.Name??throw InvestmentMissing("catalog_City_name"):"";
            string areaName=valid?state.Areas.GetValueOrDefault(IK(area.PhysicalSlot,"catalog_Area_identity"))?.Name
                ??throw InvestmentMissing("catalog_Area_name"):"";
            ushort hex=IK(current.ReadSceneryHexIndex(scenery),"catalog_hex");
            var bases=ReadDetailMiniMapBases(current);
            byte? colour=ReadMiniMapColor(current,0);
            string suffix=definition.RawField48.ToString("D3",System.Globalization.CultureInfo.InvariantCulture);
            var detail=new CollectionSceneryDetail(sceneryId,definition.Name,cityName,areaName,hex,
                ReadHistoryDataText(0,0x16F6+sceneryId),new("texture_stl_scenery_"+suffix,"sti_scenery_"+suffix,27,definition.RawField48))
            {
                MiniMap=bases is not null&&colour is byte known?new(bases,DetailMiniMapProjection.Project(DetailMiniMapIconKind.Area,sceneryId,hex,known)):null,
                MiniMapUnresolvedOwner=bases is null||colour is null?"scenery.catalog.minimap_owner":null
            };
            return AreaAdministrationQuery<SceneryCatalogInformation>.Known(new(ReadHistoryDataText(1,0xB7),ReadHistoryDataText(1,0xAD),
                ReadHistoryDataText(1,0x201),ReadHistoryDataText(1,0x202),ReadHistoryDataText(1,0x400),detail,detail.MiniMapUnresolvedOwner));
        }
        catch(InvalidDataException error){return AreaAdministrationQuery<SceneryCatalogInformation>.Unknown(error.Message);}
    }
}
