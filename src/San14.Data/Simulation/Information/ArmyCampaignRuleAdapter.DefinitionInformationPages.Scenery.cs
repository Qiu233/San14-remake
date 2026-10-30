using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private int[] SceneryCandidates()
        {
            current.MaterializeFixedScenery(rules._landmarkDefinitions);
            current.BindFixedConstructionSceneryLinks();
            for(int id=0;id<101;id++)current.SetSceneryFlags47(id,state.LandmarkInvestments?.GetValueOrDefault(id)?.Flags);
            var ids=new List<int>();
            foreach(var receiver in current.ReadRegistryReferences(RegistryKind.Scenery)??throw Missing("scenery.registry"))
            {
                if(receiver is not {Kind:RegistryKind.Scenery,PhysicalSlot:int id})throw Missing("scenery.registry_payload");
                var scenery=InvestmentScenery(current,id);
                if(!Need(current.ReadSceneryNativeValidity(scenery),"scenery.validity") ||
                    (Need(current.ReadSceneryFlags47(scenery),"scenery.flags")&1)==0)continue;
                if(ownOnly && Need(ReadLandmarkMapOwner(current,scenery),"scenery.owner")!=playerForceId)continue;
                ids.Add(id);
            }
            return ids.ToArray();
        }
        private AreaReference SceneryArea(int id)=>TalentSceneryArea(current,InvestmentScenery(current,id));
        private bool SceneryAreaValid(int id)=>Need(current.ReadAreaNativeValidity(SceneryArea(id)),"scenery.Area_validity");
        private int SceneryCity(int id)
        {
            int raw=Need(current.ReadAreaCitySlot(SceneryArea(id)),"scenery.Area_city");
            return raw<52?raw:0;
        }
        private bool SceneryCityValid(int id)=>SceneryAreaValid(id)&&Need(current.ReadBaseNativeValidity(
            current.ReadCityTableReference(SceneryCity(id))??throw Missing("scenery.City_pointer")),"scenery.City_validity");
        private int SceneryCityPhysical(int id)=>Need((current.ReadCityTableReference(SceneryCity(id))??throw Missing("scenery.City_pointer")).PhysicalSlot,"scenery.City_identity");
        private ForceReference SceneryForceReference(int id)
        {
            int raw=SceneryForce(id);return current.ReadForceTableReference(raw<52?raw:0)??throw Missing("scenery.Force_pointer");
        }
        private int SceneryForcePhysical(int id)=>Need(SceneryForceReference(id).PhysicalSlot,"scenery.Force_identity");
        private int SceneryForce(int id)=>Need(current.ReadConditionSceneryForceSlot(InvestmentScenery(current,id)),"scenery.Force");
        private int SceneryDays(int id)
        {
            var owner=InvestmentOwner(state,id);
            if(owner.InvestmentDays==0&&owner.CompletionDay<1)return 0;
            var date=state.NativeMapSeason?.ThirdDate??throw Missing("scenery.current_date");
            int now=unchecked(date.Day-31+(date.Month+date.Year*12)*30);
            return unchecked(owner.CompletionDay-now);
        }
        private bool SceneryStatusValid(int id)=>SceneryAreaValid(id)&&SceneryForce(id)!=0&&
            SceneryForce(id)==Need(current.ReadAreaCurrentForceSlot(SceneryArea(id)),"scenery.Area_force");
        private PreparedCell SceneryCell(int id,int column)
        {
            var definition=rules._landmarkDefinitions.Catalog.Sceneries[id];
            var owner=new Lazy<LandmarkInvestmentState>(()=>InvestmentOwner(state,id));
            var development=new Lazy<LandmarkDevelopmentState>(()=>owner.Value.Development??throw Missing("scenery.development"));
            if(column==5)
            {
                CityDomesticPersonForceName? appearance=null;
                var result=MakeCell(()=>
                {
                    var receiver=SceneryForceReference(id);int force=SceneryForcePhysical(id);
                    if(!Need(current.ReadForceNativeValidity(receiver),"scenery.Force_validity"))return "";
                    appearance=AdvantageForceName(force);return appearance?.Text??"";
                },()=>Need(current.ReadForceNativeValidity(SceneryForceReference(id)),"scenery.Force_validity")
                    ?new ModeZeroNameKey(AdvantageForceSort(SceneryForcePhysical(id))):new ModeZeroNameKey("",true));
                return appearance is null?result:result with{Display=result.Display with{MarkerColour=appearance.MarkerColour}};
            }
            return column switch
            {
                0=>MakeCell(()=>definition.Name,()=>new ModeZeroNameKey(definition.SecondaryText)),
                1=>MakeCell(()=>SceneryAreaValid(id)?state.Areas[Need(SceneryArea(id).PhysicalSlot,"scenery.Area_identity")].Name:"",
                    ()=>SceneryAreaValid(id)?new ModeZeroNameKey(rules._scenario.Snapshot.Areas[Need(SceneryArea(id).PhysicalSlot,"scenery.Area_identity")].SecondaryText):new ModeZeroNameKey("",true)),
                2=>MakeCell(()=>SceneryCityValid(id)?state.Cities[SceneryCityPhysical(id)].Name:"",
                    ()=>SceneryCityValid(id)?new ModeZeroNameKey(rules.ReadCityDetailNameSortKeyCurrent(current,SceneryCityPhysical(id))):new ModeZeroNameKey("",true)),
                6=>MakeCell(()=>Number(development.Value.Level),()=>new NumberKey(development.Value.Level)),
                7=>MakeCell(()=>development.Value.EffectKind is >=1 and <=3?FormatSceneryPanelNumber(Data(1,0x842+development.Value.EffectKind),development.Value.Effect):Data(1,0x353),
                    ()=>SceneryAreaValid(id)&&development.Value.EffectKind is >=1 and <=3?new NumberKey(development.Value.Effect+(4-development.Value.EffectKind)*1000):new NumberKey(0,true)),
                8=>MakeCell(()=>!SceneryAreaValid(id)?Data(1,0x353):SceneryStatusValid(id)?Data(0,0x1760+owner.Value.Phase):Data(1,0x89F),
                    ()=>SceneryStatusValid(id)?new NumberKey(owner.Value.Phase):new NumberKey(0,true)),
                9=>MakeCell(()=>development.Value.Level>5?Data(1,0x353):owner.Value.InvestmentDays==0&&owner.Value.CompletionDay<1?"":Number(SceneryDays(id)),
                    ()=>development.Value.Level>5?new NumberKey(0,true):new NumberKey(SceneryDays(id))),
                _=>throw Missing("scenery.column."+column)
            };
        }
    }
}
