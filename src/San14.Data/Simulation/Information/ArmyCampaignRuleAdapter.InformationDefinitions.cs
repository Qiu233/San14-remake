using System.Collections.Immutable;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //These catalogs belong to the adapter's resource generation. Mutable
    //definition values below come from the saved world/current page owner.
    private LoadedObjectDefinitions? informationFacilities;
    private LoadedPolicies? informationPolicies;
    private LoadedCreditRanks? informationCreditRanks;
    private LoadedDoctrinePanels? informationPanels;
    private LoadedForeignTechnologies? informationForeignTechnologies;

    internal InformationList ReadInformationDefinitionList(GameState state,SourceWorldContext current,
        InformationListKind kind,string title,int playerForceId)
    {
        var rows=ImmutableArray.CreateBuilder<InformationRow>();
        var issues=new HashSet<string>(StringComparer.Ordinal);
        var keys=new List<string>{"name"};
        string path=_scenario.FixedDataSource.SourcePath;
        try
        {
            switch(kind)
            {
                case InformationListKind.Facilities:
                    informationFacilities??=_repository.LoadObjectDefinitions(path);
                    keys.AddRange(["durability","gold"]);
                    Each(31,slot=>
                    {
                        var pointer=current.ReadObjectDefinitionReference(slot)??throw Missing("facility.pointer");
                        if(!Need(current.ReadObjectDefinitionValidity(pointer),"facility.validity")||
                            Need(current.ReadObjectDefinitionRaw2C(pointer),"facility.category")<=1)return;
                        int id=Need(pointer.PhysicalSlot,"facility.receiver");
                        var row=informationFacilities.Catalog.Definitions[id];
                        var cells=Cells(row.Name);
                        cells["durability"]=Number(current.ReadObjectDefinitionBaseDurability(pointer),"facility.durability");
                        cells["gold"]=Number(current.ReadFacilityConstructionDefinition(pointer)?.BaseGoldCost,"facility.gold");
                        Add(id,row.Name,cells);
                    });break;
                case InformationListKind.Formations:
                    keys.AddRange(["description","mobility","army_attack","garrison_attack","durability_attack","defense"]);
                    Each(21,slot=>
                    {
                        int id=Physical(0x77428,slot);
                        if(id==0||id is 17 or 18||Need(TacticRemakeCurrentFormations.ReadRawByte(state,id,0x7E),"formation.category")==0)return;
                        var row=state.Formations.GetValueOrDefault(id)??throw Missing("formation.current_definition");
                        var cells=Cells(row.Name);cells["description"]=new(row.Description);
                        cells["mobility"]=Number(TacticRemakeCurrentFormations.ReadParameter(state,id,0x7F),"formation.mobility");
                        cells["army_attack"]=Number(TacticRemakeCurrentFormations.ReadParameter(state,id,0x83),"formation.army_attack");
                        cells["garrison_attack"]=Number(TacticRemakeCurrentFormations.ReadParameter(state,id,0x84),"formation.garrison_attack");
                        cells["durability_attack"]=Number(TacticRemakeCurrentFormations.ReadParameter(state,id,0x85),"formation.durability_attack");
                        cells["defense"]=Number(TacticRemakeCurrentFormations.ReadParameter(state,id,0x86),"formation.defense");Add(id,row.Name,cells);
                    });break;
                case InformationListKind.Tactics:
                    keys.AddRange(["command_cost","formations"]);
                    Each(201,slot=>
                    {
                        int id=Physical(0x774D0,slot);
                        if(id==0||(((uint)(id-108)&0xfffffff9u)==0))return;
                        var pointer=new TacticReference(id);
                        byte low=Need(current.ReadSkillTacticRawByte(pointer,0x42),"tactic.cost_low");
                        byte high=Need(current.ReadSkillTacticRawByte(pointer,0x43),"tactic.cost_high");
                        int cost=unchecked((short)(low|high<<8));if(cost==0)return;
                        var eligible=ReadSpecialPersonTacticEligibilityCurrent(current,id);
                        if(eligible.Allowed is not true)
                        {if(eligible.Allowed is null)issues.Add(eligible.UnresolvedOwner??"information.tactics.special_person_application_unlock");return;}
                        var row=state.Tactics.GetValueOrDefault(id)??throw Missing("tactic.current_definition");
                        var cells=Cells(row.Name);cells["command_cost"]=new(Number:cost);
                        cells["formations"]=row.FormationIds is {} ids&&ids.All(state.Formations.ContainsKey)
                            ? new(Text:string.Join(" / ",ids.Select(f=>state.Formations[f].Name)))
                            : new(UnresolvedOwner:"information.tactic.formations");
                        Add(id,row.Name,cells);
                    });break;
                case InformationListKind.Ranks:
                    keys.AddRange(["command_limit","leadership","strength","intelligence","politics","charm"]);
                    Each(13,slot=>
                    {
                        int id=Physical(0x78490,slot);if(id==0)return;
                        if(id is 11 or 12)
                        {
                            if(state.CampaignEventWorld?.ScenarioId is not int scenario){issues.Add("information.rank.current_scenario_id");return;}
                            if(scenario!=19)return;
                        }
                        var row=state.ForceRanks.GetValueOrDefault(id)??throw Missing("rank.current_definition");
                        var cells=Cells(row.Name);cells["command_limit"]=new(Number:row.CommandLimit);
                        Bonus(cells,row.AbilityBonus);Add(id,row.Name,cells);
                    });break;
                case InformationListKind.Offices:
                    keys.AddRange(["command_limit","leadership","strength","intelligence","politics","charm"]);
                    var force=current.ReadForceTableReference(playerForceId)??throw Missing("office.player_Force_pointer");
                    int actualForce=Need(force.PhysicalSlot,"office.player_Force_receiver");
                    byte titleId=Need(current.ReadForceKingdomNameIndex(actualForce),"office.Force_title");
                    var officeNames=new HashSet<string>(StringComparer.Ordinal);
                    Each(256,slot=>
                    {
                        int id=Physical(0x784F8,slot);if(id==73||!Need(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state,id),"office.validity"))return;
                        int capacity=Need(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state,id),"office.command_limit");
                        if(capacity==0)return;
                        int flags=Need(SavedInt(OwnedArmyGovernorComparatorInputs.OfficeFlags3DKey(id)),"office.flags");
                        if((uint)flags>255)throw Missing("office.flags_range");
                        int bit=titleId switch{6=>1,44=>2,45=>4,_=>0};
                        bool special=bit!=0&&(flags&bit)!=0;
                        if(!special&&(flags&7)!=0)return;
                        var row=state.OfficerOffices.GetValueOrDefault(id)??throw Missing("office.current_definition");
                        if(special&&officeNames.Contains(row.Name))return;
                        OfficeRow(id,row,capacity);officeNames.Add(row.Name);
                    });
                    //The dispatcher appends actual table73 unconditionally,
                    //after the ordinary/special title-filtered sequence.
                    int none=Physical(0x784F8,73);
                    if(state.OfficerOffices.TryGetValue(none,out var noOffice))
                        OfficeRow(none,noOffice,OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state,none));
                    else issues.Add("information.office.none_definition");
                    break;
                case InformationListKind.Doctrines:
                    keys.Add("description");
                    var doctrineCatalog=_scenario.Snapshot.DoctrineParameters
                        ??throw Missing("doctrine.normal_definition_catalog");
                    Each(11,slot=>
                    {
                        int id=Physical(0x78CF8,slot);if(id==0)return;
                        string name,description;
                        if(state.Doctrines.TryGetValue(id,out var row))
                        {name=row.Name;description=row.Description;}
                        else
                        {
                            //The normal CDoctrineData table has11 rows. Core's
                            //person/force doctrine domain intentionally stores
                            //only1..6; the remaining table rows still belong to
                            //the loaded resource, including their empty names.
                            var native=doctrineCatalog.Doctrines[id];
                            if(native.Name.Length==0)return; //233F10 native validity.
                            if(id is >=1 and <=6)throw Missing("doctrine.current_definition");
                            name=native.Name;description=native.Description;
                        }
                        if(name.Length==0)return;
                        var cells=Cells(name);cells["description"]=new(description);Add(id,name,cells);
                    });break;
                case InformationListKind.Policies:
                    informationPolicies??=_repository.LoadPolicies(path);
                    keys.AddRange(["description","cost","department"]);
                    Each(121,slot=>
                    {
                        int id=Physical(0x78D50,slot);if(id==0)return;
                        var row=state.Policies.GetValueOrDefault(id)??throw Missing("policy.current_definition");
                        //The normal fixed reader owns raw98. The semantic
                        //saved EffectIds omit zero slots and cannot recreate
                        //this first serialized operand by taking First().
                        int raw=informationPolicies.Catalog.Policies[id].RawEffectSlots[0];
                        if(Physical(0x80880,raw<101&&raw>=0?raw:0)==0)return;
                        var cells=Cells(row.Name);cells["description"]=new(row.Description);
                        cells["cost"]=new(Number:row.CostAddition);cells["department"]=Number(row.DepartmentCode,"policy.department");
                        Add(id,row.Name,cells);
                    });break;
                case InformationListKind.Traits:
                    keys.AddRange(["description","style"]);
                    Each(356,slot=>
                    {
                        var pointer=current.ReadPersonalityTableReference(slot)??throw Missing("trait.pointer");
                        if(pointer.PhysicalSlot is not int id||id==0)return;
                        if(id is >=256 and <=355&&Need(current.ReadPersonalityByte(pointer,0xB6),"trait.custom_effect")==0)return;
                        byte style=Need(current.ReadPersonalityByte(pointer,0xBE),"trait.style");if(style==0)return;
                        var eligible=ReadSpecialPersonPersonalityEligibilityCurrent(current,id);
                        if(eligible.Allowed is not true)
                        {if(eligible.Allowed is null)issues.Add(eligible.UnresolvedOwner??"information.traits.special_person_application_unlock");return;}
                        string name=current.ReadPersonalityName(pointer);
                        var cells=Cells(name);cells["style"]=new(Number:style);
                        cells["description"]=new(CurrentText(pointer,0x3C,51));Add(id,name,cells);
                    });break;
                case InformationListKind.CreditRanks:
                    informationCreditRanks??=_repository.LoadCreditRanks(path);keys.AddRange(["merit","doctrine_points"]);
                    Each(7,slot=>
                    {
                        int id=Physical(0x85DB8,slot);if(id==0)return;
                        var row=informationCreditRanks.Catalog.Ranks[id];var cells=Cells(row.Name);
                        cells["merit"]=new(Number:row.SignedThreshold);
                        cells["doctrine_points"]=new(Number:row.DoctrinePointAddition);Add(id,row.Name,cells);
                    });break;
                case InformationListKind.DoctrinePanels:
                    informationPanels??=_repository.LoadDoctrinePanels(path);keys.Add("value");
                    Each(80,slot=>
                    {
                        int id=Physical(0x85F20,slot);if(id is 0 or 1)return;
                        var row=informationPanels.Catalog.Panels[id];
                        //23C4D0 reads the9 UTF16 name units, then field2E.
                        if(row.RawBytes[18]==7)return;
                        var cells=Cells(row.Name);cells["value"]=Number(SavedInt(OwnedBaseCapacityInputs.PanelKey(id)),"panel.value");
                        Add(id,row.Name,cells);
                    });break;
                case InformationListKind.ForeignTechnologies:
                    informationForeignTechnologies??=_repository.LoadForeignTechnologies(path);
                    keys.AddRange(["description","value"]);
                    int foreignAnchor=Need((current.ReadStaticTableReference(0x85A48,0)
                        ??throw Missing("foreign_technology.anchor_lookup")).PhysicalSlot,"foreign_technology.anchor_receiver");
                    Each(9,slot=>
                    {
                        int? physical=(current.ReadStaticTableReference(0x85A48,slot)
                            ??throw Missing("foreign_technology.lookup")).PhysicalSlot;
                        if(physical is null||physical==foreignAnchor)return;
                        int id=physical.Value;
                        if((uint)id>=informationForeignTechnologies.Definitions.Count)throw Missing("foreign_technology.receiver");
                        var row=informationForeignTechnologies.Definitions[id];
                        var cells=Cells(row.Name);cells["description"]=new(row.Description);
                        cells["value"]=new(Number:row.Value);Add(id,row.Name,cells);
                    });break;
                default:issues.Add("information.definition_kind");break;
            }
        }
        catch(InvalidDataException error){issues.Add(error.Message);}
        return new(title,keys.Select(key=>InformationColumnFor(key,title)).ToImmutableArray(),rows.ToImmutable(),
            issues.Count==0?null:string.Join(";",issues));

        void Each(int count,Action<int> read)
        {
            for(int slot=0;slot<count;slot++)
                try{read(slot);}catch(InvalidDataException error){issues.Add(error.Message);}
        }
        int Physical(int table,int lookup)
        {
            var pointer=current.ReadStaticTableReference(table,lookup)??throw Missing("table_pointer");
            return pointer.PhysicalSlot??0; //32FB90 rejects a known null receiver.
        }
        Dictionary<string,InformationCell> Cells(string name)=>new(StringComparer.Ordinal){["name"]=new(name,SortText:name)};
        void Add(int id,string name,Dictionary<string,InformationCell> cells)=>rows.Add(new((int)kind,id,name,cells.ToImmutableDictionary(StringComparer.Ordinal)));
        InformationCell Number(long? value,string missing)=>value.HasValue?new(Number:value):new(UnresolvedOwner:"information."+missing);
        int? SavedInt(string key)=>state.NativeArmySimulation?.Parameters is {} bank&&
            !bank.Float32Values.ContainsKey(key)&&bank.Int32Values.TryGetValue(key,out int value)?value:null;
        void Bonus(Dictionary<string,InformationCell> cells,OfficerAbilities bonus)
        {
            cells["leadership"]=new(Number:bonus.Leadership);cells["strength"]=new(Number:bonus.Strength);
            cells["intelligence"]=new(Number:bonus.Intelligence);cells["politics"]=new(Number:bonus.Politics);
            cells["charm"]=new(Number:bonus.Charm);
        }
        void OfficeRow(int id,OfficerOfficeDefinition row,int? capacity)
        {
            var cells=Cells(row.Name);cells["command_limit"]=Number(capacity,"office.command_limit");
            Bonus(cells,row.AbilityBonus);Add(id,row.Name,cells);
        }
        string CurrentText(PersonalityReference pointer,int start,int units)
        {
            var text=new StringBuilder(units);
            for(int i=0;i<units;i++)
            {
                ushort unit=Need(current.ReadPersonalityWord(pointer,start+i*2),"trait.description");
                if(unit==0)break;text.Append((char)unit);
            }
            return text.ToString();
        }
        static T Need<T>(T? value,string field)where T:struct=>value??throw Missing(field);
        static InvalidDataException Missing(string field)=>new("information.definitions."+field);
    }
}
