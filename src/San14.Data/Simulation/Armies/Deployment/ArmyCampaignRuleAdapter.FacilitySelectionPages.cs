using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private FacilitySelectionPageSet PrepareFacilitySelectionPages(GameState state,SourceWorldContext current,
        IReadOnlyList<S14ObjectDefinition> catalog,IReadOnlyList<UnitFacilityDirectoryRow> rows,
        int?[]? people,int?[]? selectedTroops)
    {
        string Data(int id)=>ReadHistoryDataText(1,id);
        var groups=FacilitySelectionColumnsSource.Groups.Select((group,index)=>
            new FacilitySelectionGroup(index,Data(group.Caption),group.Columns.Select(id=>
            {
                var c=FacilitySelectionColumnsSource.Columns.Single(c=>c.Id==id);
                int format=(c.Flags&1)!=0?3:(c.Flags&2)!=0?5:4;
                return new FacilitySelectionColumn(c.Id,Data(c.Caption),c.Width,format,
                    (c.Flags&0x20000000)==0,c.Sortable);
            }).ToImmutableArray())).ToImmutableArray();
        var abilities=new Lazy<TacticRemakeCurrentAbilities>(()=>new(current,_scenario,Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language)));
        return new(rows,groups,ReadCell);

        ObjectDefinitionReference Ref(UnitFacilityDirectoryRow row)=>row.DefinitionPhysicalSlot is int physical
            ?new(physical):throw FacilityListMissing("definition_receiver");
        FacilityConstructionDefinition Construction(UnitFacilityDirectoryRow row)=>
            current.ReadFacilityConstructionDefinition(Ref(row))??throw FacilityListMissing("definition_fields");
        FacilityEffectDefinition Effects(UnitFacilityDirectoryRow row)=>
            current.ReadFacilityEffectDefinition(Ref(row))??throw FacilityListMissing("definition_effect_fields");
        byte Category(UnitFacilityDirectoryRow row)=>row.Category??throw FacilityListMissing("definition_category");
        int Kind(UnitFacilityDirectoryRow row)=>row.DefinitionPhysicalSlot??throw FacilityListMissing("definition_kind");
        string NameKey(UnitFacilityDirectoryRow row)
        {
            int index=Kind(row);
            if((uint)index>=catalog.Count)throw FacilityListMissing("definition_catalog_identity");
            return catalog[index].NameSortKey??throw FacilityListMissing("definition_name_sort_key");
        }
        ForceReference Player()=>OwnedCurrentPlayerForce.ReadCurrentReference(state,current)
            ??throw FacilityListMissing("current_player_Force");
        int Capacity(UnitFacilityDirectoryRow row)=>FacilityRepairCapacity.ReadCapacity(state,current,Ref(row),Player(),_baseCapacityInputs)
            ??throw FacilityListMissing("current_capacity");
        int Days(UnitFacilityDirectoryRow row)
        {
            if(people is null)throw FacilityListMissing("selected_People");
            if(people.Length==0)return 0;
            if(selectedTroops is null||selectedTroops.Length!=people.Length)throw FacilityListMissing("selected_troops");
            int sum=0;
            for(int i=0;i<people.Length;i++)
                if(people[i] is int physical)
                    sum=unchecked(sum+FacilityRepairRate.ReadForDefinition(state,current,Ref(row),physical,
                        selectedTroops[i],abilities.Value,_baseCapacityInputs));
            return sum!=0?Capacity(row)/sum:0;
        }
        int Maximum(UnitFacilityDirectoryRow row)=>Effects(row).MaximumFor(state,Kind(row));
        string Requirement(UnitFacilityDirectoryRow row)
        {
            var definition=Construction(row);
            if(definition.RequiredTrait!=0)
            {
                var personality=current.ReadPersonalityTableReference(81)??throw FacilityListMissing("fixed_personality81_pointer");
                bool valid=FacilityListNeed(current.ReadSkillPersonalityValidity(personality),"fixed_personality81_validity");
                string personalityName=valid?current.ReadPersonalityName(personality)??throw FacilityListMissing("fixed_personality81_name"):"";
                return FacilityListFormat(Data(0x398),personalityName);
            }
            var policy=current.ReadStaticTableReference(0x80880,definition.RequiredPolicyEffect<=100?definition.RequiredPolicyEffect:0)
                ??throw FacilityListMissing("policy_pointer");
            var anchor=current.ReadStaticTableReference(0x80880,0)??throw FacilityListMissing("policy_anchor");
            if(policy.PhysicalSlot is not int id||policy.PhysicalSlot==anchor.PhysicalSlot)return Data(0x353);
            string policyName=state.PolicyEffects.TryGetValue(id,out var policyDefinition)?policyDefinition.Name:throw FacilityListMissing("policy_name");
            return FacilityListFormat(Data(0x399),policyName,definition.RequiredPolicyLevel);
        }
        string Text(UnitFacilityDirectoryRow row,int column)=>column switch
        {
            0=>row.Name??throw FacilityListMissing("definition_name"),
            1=>Category(row)==0?"":Category(row)<=4?Data(0x40C+Category(row)):"",
            2=>FacilityListFormat(Data(0x324),Days(row)),
            12=>FacilityConstructionCost.Read(state,current,Player(),Construction(row),_baseCapacityInputs).ToString(CultureInfo.InvariantCulture),
            13=>Capacity(row).ToString(CultureInfo.InvariantCulture),
            5=>Category(row) is 2 or 3?Data(0x353):
                (Effects(row).TriggerRange??throw FacilityListMissing("trigger_range")).ToString(CultureInfo.InvariantCulture),
            6 or 7=>Effects(row).Channel(column-6).Effect is >=1 and <=18
                ?ReadHistoryDataText(0,0x805+Effects(row).Channel(column-6).Effect):Data(0x353),
            8=>Kind(row) is 9 or 10 or 16?Data(0x353):FacilityListFormat(Data(0x46B),Effects(row).Minimum,Maximum(row)),
            11=>Requirement(row),
            _=>throw FacilityListMissing("column")
        };
        FacilitySelectionKey Key(UnitFacilityDirectoryRow row,int column)=>column switch
        {
            0=>new(0,NameKey(row)),
            1=>new(Category(row)),2=>new(Days(row)),
            12=>new(Construction(row).BaseGoldCost),13=>new(FacilityListNeed(current.ReadObjectDefinitionBaseDurability(Ref(row)),"base_capacity")),
            5=>Category(row) is 2 or 3?new(int.MaxValue,Empty:true):new(Effects(row).TriggerRange??throw FacilityListMissing("trigger_range")),
            6 or 7=>new(Effects(row).Channel(column-6).Effect),
            8=>Kind(row) is 9 or 10 or 16?new(int.MaxValue,Empty:true):new(unchecked(Maximum(row)+Effects(row).Minimum*0xBD0F)),
            _=>throw FacilityListMissing("sort_column")
        };
        PreparedFacilitySelectionCell ReadCell(UnitFacilityDirectoryRow row,int column)
        {
            string? text=null,displayMissing=null,sortMissing=null;FacilitySelectionKey? key=null;
            if(column==2)
            {
                int? days=null;
                try{days=Days(row);key=new(days.Value);}
                catch(InvalidDataException error){displayMissing=sortMissing=error.Message;}
                if(days.HasValue)
                    try{text=FacilityListFormat(Data(0x324),days.Value);}
                    catch(InvalidDataException error){displayMissing=error.Message;}
            }
            else
            {
                try{text=Text(row,column);}catch(InvalidDataException error){displayMissing=error.Message;}
                if(column!=11)
                    try{key=Key(row,column);}catch(InvalidDataException error){sortMissing=error.Message;}
            }
            return new(new(text,displayMissing,sortMissing),key);
        }
    }
    private static T FacilityListNeed<T>(T? value,string field)where T:struct=>value??throw FacilityListMissing(field);
    private static InvalidDataException FacilityListMissing(string field)=>new("facility.selection."+field);
    private static string FacilityListFormat(string format,params object[] arguments)
    {
        var result=new StringBuilder();int argument=0;
        for(int i=0;i<format.Length;i++)
        {
            if(format[i]!='%'){result.Append(format[i]);continue;}
            if(++i==format.Length)throw FacilityListMissing("format");
            char code=format[i];if(code=='%'){result.Append('%');continue;}
            if(argument==arguments.Length||code is not ('s' or 'd' or 'u'))throw FacilityListMissing("format");
            object value=arguments[argument++];
            if(code=='s'&&value is string text)result.Append(text);
            else if((code is 'd' or 'u')&&value is IConvertible)result.Append(Convert.ToString(value,CultureInfo.InvariantCulture));
            else throw FacilityListMissing("format_operand");
        }
        if(argument!=arguments.Length)throw FacilityListMissing("format_arguments");
        return result.ToString();
    }
}
