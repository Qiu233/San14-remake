using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerInformationCurrent
    {
        internal OfficerInformationPageSet StrategicPages(IReadOnlyList<int> ids,int kind,bool executor,
            IReadOnlyDictionary<int,StrategicCommandCandidate>? temporary)
        {
            commandBodyPalette=executor;
            var groups=executor?(kind==14?StrategicSelectionColumnsSource.Executor14(Data):StrategicSelectionColumnsSource.Executor(Data)):
                kind==12?StrategicSelectionColumnsSource.Person12(Data):StrategicSelectionColumnsSource.Person24(Data);
            var cache=new Dictionary<(int,int),PreparedCell>();
            PreparedCell Read(int id,int column)
            {
                if(column is not(75 or 102 or 106 or 107 or 108 or 109 or 110 or 111 or 112))return Cell(id,column);
                if(cache.TryGetValue((id,column),out var saved))return saved;
                try{saved=StrategicCell(ReadPerson(id),column,temporary);}
                catch(InvalidDataException error){saved=new(new(null,null,error.Message),null,error.Message);}
                return cache[(id,column)]=saved;
            }
            return new(Data(1,executor?0x58F:0x58D),groups,ids,
                (id,column)=>Read(id,column).Display with{SortUnresolvedOwner=Read(id,column).SortUnresolvedOwner},
                (id,column)=>Read(id,column).SortUnresolvedOwner,
                (left,right,column,ascending)=>(Read(left,column).Key??throw Missing("strategic.sort_key"))
                    .Before(Read(right,column).Key??throw Missing("strategic.sort_key"),ascending))
                {DynamicPaletteEnabled=executor};
        }
        internal IReadOnlyDictionary<int,AreaAdministrationQuery<bool>> StrategicHealth(IReadOnlyList<int> ids)
            =>ids.ToDictionary(id=>id,MovementAlteredHealth);
        private PreparedCell StrategicCell(Person person,int column,IReadOnlyDictionary<int,StrategicCommandCandidate>? temporary)
        {
            StrategicCommandCandidate Prepared()=>temporary?.GetValueOrDefault(person.Id)??throw Missing("strategic.prepared_Person");
            bool Recommended()=>Need(Prepared().Recommended,"strategic.recommendation");
            int Days()=>Need(Prepared().TravelDays,"strategic.travel_days");
            bool Trait(int effect)
            {
                if(!InformationNativeValid(person))return false;
                for(int slot=0;slot<9;slot++)
                    if(Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,person.PhysicalSlot,slot),
                        "strategic.trait_slot")==effect)return true;
                return false;
            }
            if(column==75)return MakeCell(()=>Recommended()?Data(1,0x338):"",()=>new NumberKey(Recommended()?1:0));
            if(column==102)return MakeCell(()=>InformationNativeValid(person)?Number(Days()):"",
                ()=>new NumberKey(InformationNativeValid(person)?Days():int.MaxValue),statePalette:()=>BasicRowPalette(person,5));
            int effect=column switch{106=>0x7A,107=>0xA5,108=>0x7C,109=>0x7D,110=>0x7E,111=>0x7F,112=>0x80,
                _=>throw Missing("strategic.column")};
            return MakeCell(()=>Trait(effect)?Data(1,0x338):"",()=>new NumberKey(Trait(effect)?1:0));
        }
    }
}
