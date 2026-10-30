using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class AreaInformationCurrent
    {
        private AreaAdministrationPreviewSource? preview;
        private readonly Dictionary<(int AreaId,int Column),int> income=[];
        private PreparedCell IncomeCell(Area area,int column)=>MakeCell(()=>
        {
            if(Special(area))return Data(1,0x353);
            if(!Need(Current.ReadBaseNativeValidity(City(area)),"income_City_validity"))return "";
            return Number(Income(area,column));
        },()=>IncomeApplicable(area)?new NumberKey(Income(area,column)):new NumberKey(0,true));
        private bool IncomeApplicable(Area area)=>!Special(area)
            && Need(Current.ReadBaseNativeValidity(City(area)),"income_City_validity");
        private AreaAdministrationPreviewSource Preview
        {
            get
            {
                if(preview is not null)return preview;
                //All queries borrow this publication's actual current. The
                //normal assignment/capacity kernels are bound once; no draft,
                //World write or second full capture is created.
                rules._personAssignmentPrecallInputs.BindCurrent(state,Current);
                bool bound=rules._baseCapacityInputs.BindCurrent(state,Current);
                return preview=new(state,Current,rules._scenario,rules.Seed.Language,rules._baseCapacityInputs,bound);
            }
        }
        private int Income(Area area,int column)
        {
            if(income.TryGetValue((area.Id,column),out int saved))return saved;
            var value=column switch
            {
                15=>Preview.Gold(area.PhysicalSlot,AssignmentCount),
                16=>Preview.Food(area.PhysicalSlot,AssignmentCount),
                17=>Preview.Soldiers(area.PhysicalSlot,mode:2),
                _=>throw Missing("income_column")
            };
            if(!value.Available || value.Value is not int actual)
                throw Missing(value.UnresolvedInput??"income_current_scalar");
            return income[(area.Id,column)]=actual;
        }
        private int AssignmentCount(int person)=>(Current.ReadAssignmentPersonAreaVector(person)
            ??throw Missing("Person_task_vector")).ActiveCount1E8;
    }
}
