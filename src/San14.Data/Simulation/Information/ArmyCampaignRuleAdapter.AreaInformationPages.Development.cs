using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class AreaInformationCurrent
    {
        private OwnedAreaDevelopmentContext? development;
        private PreparedCell DevelopmentCell(Area area,int column)=>column switch
        {
            >=6 and <=11=>MakeCell(()=>Special(area)?Data(1,0x353):Number(DevelopmentValue(area,column)),
                ()=>Special(area)?new NumberKey(0,true):new NumberKey(DevelopmentValue(area,column))),
            12=>MakeCell(()=>FocusText(area),()=>new NumberKey(Focus(area)==0?4:Focus(area))),
            13=>MakeCell(()=>Special(area)?Data(1,0x353):Number(Population(area)),
                ()=>Special(area)?new NumberKey(0,true):new NumberKey(Population(area))),
            14=>MakeCell(()=>Number(Security(area)),()=>new NumberKey(Security(area))),
            _=>throw Missing("development_column."+column)
        };
        private int DevelopmentValue(Area area,int column)
        {
            var field=column switch
            {6 or 7=>CeAreaDevelopmentField.Commerce,8 or 9=>CeAreaDevelopmentField.Agriculture,
                10 or 11=>CeAreaDevelopmentField.Barracks,_=>throw Missing("development_field")};
            development??=new(state,Current,rules._normalAftermathInputs);
            int progress=Need(development.ReadAreaProgress(area.Receiver,field),"Area_current_"+field);
            //The list's odd columns are the accumulated native words, not
            //the detail panel's within-level display remainder.
            return (column&1)!=0?progress:rules._normalAftermathInputs.Development.ReadLevel((ushort)progress);
        }
        private int Focus(Area area)=>Need(Current.ReadAssignmentArea72(area.PhysicalSlot),"Area72_focus");
        private string FocusText(Area area)=>Focus(area) switch
        {0=>"",1=>Data(1,0x2B3),2=>Data(1,0x2B4),3=>Data(1,0x2B5),_=>throw Missing("Area72_focus_text_range")};
        private static int Population(Area area)=>Need(area.Owner.NativeNormalAftermath?.Quantity52,"Area52_population");
        private static int Security(Area area)
        {
            int value=Need(area.Owner.Security,"Area54_security");
            return value is >=0 and <=255?value:throw Missing("Area54_byte_domain");
        }
    }
}
