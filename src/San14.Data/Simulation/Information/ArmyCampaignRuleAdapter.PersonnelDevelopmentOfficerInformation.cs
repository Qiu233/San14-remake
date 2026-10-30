using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The learning and mediation callers retain their own ordered
    /// candidate sets, authored groups and command-list colour context.</summary>
    public OfficerInformationPageSet PreparePersonnelDevelopmentOfficerPages(GameState state,int forceId,
        IReadOnlyList<int> ids,bool mediation)
    {
        var source=new OfficerInformationCurrent(this,state,forceId,rowPaletteEnabled:false);
        return source.PersonnelDevelopmentPages(ids,mediation,false);
    }

    /// <summary>Hire rows are independent generated template records. Their
    /// keys must never resolve an overlapping world Person.</summary>
    public OfficerInformationPageSet PreparePersonnelHireTemplatePages(GameState state,int forceId,
        IReadOnlyList<int> templateIds)
        =>new OfficerInformationCurrent(this,state,forceId,rowPaletteEnabled:false)
            .PersonnelDevelopmentPages(templateIds,false,true);

    private sealed partial class OfficerInformationCurrent
    {
        private readonly Dictionary<(int Template,int Column),PreparedCell> hireTemplatePrepared=[];

        internal OfficerInformationPageSet PersonnelDevelopmentPages(IReadOnlyList<int> ids,bool mediation,bool hire)
        {
            var columns=OfficerInformationSources.Create(Data).SelectMany(group=>group.Columns)
                .GroupBy(column=>column.Id).ToDictionary(group=>group.Key,group=>group.First());
            for(int dimension=0;dimension<5;dimension++)
                columns[13+dimension]=new(13+dimension,0x238+dimension,Data(1,0x238+dimension),76,5,false,true,true);
            int[][] groups=hire?[[0,13,14,15,16,17,29,28],[0,61,62,63],[0,40,41,42,43,44,45,46],[0,51,52,53]]:
                mediation?[[0,5,29,28,6,7,32,33,8,9,10,11,12]]:
                [[0,5,3,8,9,10,11,12],[0,40,41,42,43,44,45,46,47,48,49,50],[0,51,52,53,54,55],[0,56,57,58,59,60]];
            int[] captions=hire?[0x3CD,0x23D,0x22B,0x234]:mediation?[0x3CD]:[0x3CD,0x22B,0x3DD,0x3DE];
            var descriptors=groups.Select((group,index)=>new OfficerInformationGroup(index,captions[index],Data(1,captions[index]),
                group.Select(id=>columns[id]).ToImmutableArray())).ToImmutableArray();
            if(!hire)return Pages(ids,Data(1,mediation?0x58F:0x32E),descriptors);
            PreparedCell Read(int id,int column)=>HireTemplateCell(id,column);
            return new(Data(1,0x32E),descriptors,ids,
                (id,column)=>Read(id,column).Display with{SortUnresolvedOwner=Read(id,column).SortUnresolvedOwner},
                (id,column)=>Read(id,column).SortUnresolvedOwner,
                (left,right,column,ascending)=>(Read(left,column).Key??throw Missing("hire.sort_key"))
                    .Before(Read(right,column).Key??throw Missing("hire.sort_key"),ascending));
        }

        private PreparedCell HireTemplateCell(int id,int column)
        {
            if(hireTemplatePrepared.TryGetValue((id,column),out var retained))return retained;
            PreparedCell value;
            try
            {
                var template=state.PersonnelHire?.Templates?.GetValueOrDefault(id)??throw Missing("hire.template_owner");
                var person=template.Person;
                if(column is >=13 and <=17)
                {
                    var basic=person.BaseAbilities??throw Missing("hire.base_abilities");
                    int number=(column-13) switch{0=>basic.Leadership,1=>basic.Strength,2=>basic.Intelligence,3=>basic.Politics,_=>basic.Charm};
                    value=MakeCell(()=>Number(number),()=>new NumberKey(number));
                }
                else if(column is >=40 and <=46)
                {
                    bool enabled=DeploymentFormationEligibility.Allows(Need(person.CurrentFormationMask,"hire.formation_mask"),column-39,person.Id);
                    value=MakeCell(()=>enabled?Data(1,0x338):null,()=>new NumberKey(0,!enabled));
                }
                else if(column is >=51 and <=53)
                {
                    var slots=person.CurrentTacticSlots??throw Missing("hire.tactic_slots");
                    byte raw=slots[column-51];
                    var tactic=DeploymentTacticSelectionSource.ReadQualifiedSlot(current,person.Id,raw);
                    bool valid=Need(current.ReadSkillTacticValidity(tactic),"hire.tactic_validity");
                    int physical=Need(tactic.PhysicalSlot,"hire.tactic_receiver");
                    value=MakeCell(()=>valid?state.Tactics.GetValueOrDefault(physical)?.Name??throw Missing("hire.tactic_name"):null,
                        ()=>new NumberKey(valid?physical:0,!valid));
                }
                else if(column is >=61 and <=63)
                {
                    int slot=column-61;var traits=person.CurrentTraitIds??throw Missing("hire.trait_slots");
                    ushort lookup=Need(TacticRemakeCurrentTraits.SelectPersonalityLookup(current,checked((ushort)person.Id),slot,
                        ()=>slot<traits.Length&&traits[slot] is >=0 and <=ushort.MaxValue?(ushort)traits[slot]:null),"hire.trait_lookup");
                    var trait=current.ReadPersonalityTableReference(lookup<=355?lookup:0)??throw Missing("hire.trait_pointer");
                    bool valid=Need(current.ReadSkillPersonalityValidity(trait),"hire.trait_validity");
                    value=MakeCell(()=>valid?MilitaryTraitText(trait,true):null,
                        ()=>valid?new MilitaryTraitKey(true,MilitaryTraitText(trait,true),MilitaryTraitText(trait,false)):
                            new MilitaryTraitKey(false,"",""));
                }
                else value=column switch
                {
                    0=>MakeCell(()=>person.Name,()=>new NameKey(person.CurrentNameSortKey??throw Missing("hire.name_sort_key"))),
                    29=>MakeCell(()=>person.Sex switch{OfficerSex.Male=>Data(1,0x495),OfficerSex.Female=>Data(1,0x496),_=>""},
                        ()=>new NumberKey(person.Sex is {} sex?(int)sex:throw Missing("hire.sex"))),
                    28=>MakeCell(()=>HireTemplateAge(person,true) is int age?Number(age):Data(1,0x353),
                        ()=>HireTemplateAge(person,false) is int age?new NumberKey(age):new NumberKey(0,true)),
                    _=>throw Missing("hire.column."+column)
                };
            }
            catch(InvalidDataException error){value=new(new(null,null,error.Message),null,error.Message);}
            return hireTemplatePrepared[(id,column)]=value;
        }

        private int? HireTemplateAge(OfficerState person,bool display)
        {
            int mode=state.CampaignEventWorld?.ScenarioMode??throw Missing("hire.scenario_mode");
            ushort flags=Need(OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior),"hire.flags");
            if(mode==2||(flags&0x400)!=0)return null;
            bool timeless=mode==3&&PersonCurrentAgeSource.UsesTimelessAges(state);
            if(display&&timeless)return null;
            int age;
            if(timeless)
            {
                age=person.Id is 231 or 247 or 271 or 631 or 5121?80:20;
                var traits=person.CurrentTraitIds??throw Missing("hire.age_trait_slots");
                for(int index=0;index<9;index++)
                {
                    int slot=index;
                    ushort lookup=Need(TacticRemakeCurrentTraits.SelectPersonalityLookup(current,checked((ushort)person.Id),slot,
                        ()=>slot<traits.Length&&traits[slot] is >=0 and <=ushort.MaxValue?(ushort)traits[slot]:null),"hire.age_trait_lookup");
                    if(lookup==0x90)return 80;
                }
            }
            else age=Math.Max(0,unchecked((short)(state.NativeMapSeason?.ThirdDate.Year??throw Missing("hire.current_year"))-
                (person.BirthYear??throw Missing("hire.birth_year"))+1));
            return age==0?null:age;
        }
    }
}
