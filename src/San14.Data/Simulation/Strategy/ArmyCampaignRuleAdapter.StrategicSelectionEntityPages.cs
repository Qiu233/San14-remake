using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ForceInformationCurrent
    {
        internal ForceInformationPageSet StrategicPages(IReadOnlyList<int> ids)=>new(Data(1,0x58D),
            StrategicSelectionColumnsSource.Force10(Data),ids,
            (id,column)=>Cell(id,column).Display with{SortUnresolvedOwner=Cell(id,column).SortUnresolvedOwner},
            (id,column)=>Cell(id,column).SortUnresolvedOwner,
            (left,right,column,ascending)=>Before(Cell(left,column).Key??throw Missing("strategic.sort_key"),
                Cell(right,column).Key??throw Missing("strategic.sort_key"),column,ascending));
    }
    private sealed partial class AreaInformationCurrent
    {
        internal AreaInformationPageSet StrategicPages(IReadOnlyList<int> ids)=>new(Data(1,0x58D),
            StrategicSelectionColumnsSource.Area(Data),ids,
            (id,column)=>Cell(id,column).Display with{SortUnresolvedOwner=Cell(id,column).SortUnresolvedOwner},
            (id,column)=>Cell(id,column).SortUnresolvedOwner,
            (left,right,column,ascending)=>(Cell(left,column).Key??throw Missing("strategic.sort_key"))
                .Before(Cell(right,column).Key??throw Missing("strategic.sort_key"),ascending));
        internal IReadOnlyDictionary<int,AreaAdministrationQuery<int?>> StrategicIcons(IReadOnlyList<int> ids)=>
            ids.ToDictionary(id=>id,ReadIcon);
        internal AreaAdministrationQuery<AreaInformationListPage> SortStrategicTerrain(
            AreaInformationPageSet pages,IReadOnlyList<int> ids,bool ascending)
        {
            try
            {
                var keys=ids.ToDictionary(id=>id,StrategicTerrainKey);
                return pages.SortAuxiliary(0,(left,right)=>ascending?keys[left]<keys[right]:keys[right]<keys[left]);
            }
            catch(InvalidDataException error)
            { return AreaAdministrationQuery<AreaInformationListPage>.Unknown(error.Message); }
        }
        private int StrategicTerrainKey(int id)
        {
            int raw=Need(Current.ReadAreaHexIndex(ReadArea(id).Receiver),"strategic.terrain_hex3A");
            var hex=Current.ReadHexTableReference(raw<48400?raw:0)??throw Missing("strategic.terrain_Hex");
            int surface=Need(Current.ReadHexSurfaceCode(hex),"strategic.terrain_surface10");
            return surface==15?int.MaxValue:surface;
        }
        private AreaAdministrationQuery<int?> ReadIcon(int id)
        {
            try{return AreaAdministrationQuery<int?>.Known(DiplomacyAreaIcon(id));}
            catch(InvalidDataException error){return AreaAdministrationQuery<int?>.Unknown(error.Message);}
        }
    }
    private sealed partial class CityInformationCurrent
    {
        internal CityInformationPageSet StrategicPages(IReadOnlyList<int> ids)
        {
            var cache=new Dictionary<int,PreparedCell>();
            PreparedCell Read(int id,int column)
            {
                if(column!=34)return Cell(id,column);
                if(cache.TryGetValue(id,out var saved))return saved;
                try
                {
                    int person=Governor(ReadCity(id));
                    bool Valid()=>Need(Current.ReadPersonNativeValidity(person),"strategic.governor_validity");
                    byte Status()=>Need(Current.ReadPersonStatus(person),"strategic.governor_status");
                    int Loyalty()=>Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state,Current,person,rules._governorInputs),"strategic.governor_loyalty");
                    // The same356EE0/345E60 column as the Inspect selector:
                    // display qualifies/clamps; the key reads raw E8.
                    saved=MakeCell(()=>!Valid()?"":Status()==1?Data(1,0x353):Status() is >=1 and <=4?Number(Math.Min(Loyalty(),100)):"",
                        ()=>Valid()?new NumberKey(Loyalty()):new NumberKey(int.MaxValue,true));
                }
                catch(InvalidDataException error){saved=new(new(null,null,error.Message),null,error.Message);}
                return cache[id]=saved;
            }
            return new(Data(1,0x58D),StrategicSelectionColumnsSource.City11(Data),ids,
                (id,column)=>Read(id,column).Display with{SortUnresolvedOwner=Read(id,column).SortUnresolvedOwner},
                (id,column)=>Read(id,column).SortUnresolvedOwner,
                (left,right,column,ascending)=>(Read(left,column).Key??throw Missing("strategic.sort_key"))
                    .Before(Read(right,column).Key??throw Missing("strategic.sort_key"),ascending));
        }
    }
}
