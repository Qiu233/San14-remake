using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<CurrentWorldItemEditSelection> QueryCurrentWorldItemEditSelection(
        GameState state,bool batch,IReadOnlyList<int>? retainedOrder=null)
    {
        try
        {
            RequireCurrentWorldItemEditProfile(state);
            var current=CaptureCurrentWorldItemEdit(state);
            var rows=ReadCurrentWorldItemEditRows(state,current);
            int force=CurrentWorldItemEditNeed(OwnedCurrentPlayerForce.ReadCurrentReference(state,current)?.PhysicalSlot,
                "selection.current_player");
            var reader=new CurrentWorldItemSelectionCurrent(this,state,current,force);
            int[] ids=rows.Select(row=>row.ItemId).ToArray();
            if(retainedOrder is not null)
            {
                var members=ids.ToHashSet();
                var next=new List<int>(ids.Length);
                foreach(int id in retainedOrder)if(members.Remove(id))next.Add(id);
                foreach(int id in ids)if(members.Remove(id))next.Add(id);
                ids=next.ToArray();
            }
            var pages=reader.Pages(ids);
            return AreaAdministrationQuery<CurrentWorldItemEditSelection>.Known(new(pages,rows,batch,0x5AA,
                CurrentWorldItemSelectionGuide())
            {
                ReadSelectionSort=(selectedIds,selectedFirst)=>
                {
                    var selected=selectedIds.ToHashSet();
                    return pages.SortAuxiliary(0,(left,right)=>selectedFirst
                        ?selected.Contains(left)&&!selected.Contains(right)
                        :!selected.Contains(left)&&selected.Contains(right));
                }
            });
        }
        catch(InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldItemEditSelection>.Unknown(error.Message); }
    }

    private string CurrentWorldItemSelectionGuide()=>new NarrationResourceMessages(
        _repository.LoadMessages(Seed.Language,Formats.MessageKind.Game).Messages)
        .ResolvePersonSelectorHint(ReadHistoryDataText(1,0x246));

    private sealed partial class CurrentWorldItemSelectionCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly DiplomacyItemTargetCurrent ordinary;
        internal CurrentWorldItemSelectionCurrent(ArmyCampaignRuleAdapter rules,GameState state,
            SourceWorldContext current,int playerForce)
        { this.rules=rules;this.state=state;ordinary=new(rules,state,current,playerForce); }
        internal DefinitionInformationPageSet Pages(IReadOnlyList<int> ids)=>new(rules.ReadHistoryDataText(1,0x5AA),
            CurrentWorldItemSelectionColumnsSource.Items(rules.ReadHistoryDataText),ids,
            (id,column)=>Cell(id,column),
            (id,column)=>Cell(id,column).SortUnresolvedOwner,
            (left,right,column,ascending)=>column is 17 or 19?ExtraBefore(left,right,column,ascending):
                ordinary.Before(left,right,column,ascending)) {Kind=InformationListKind.Items};
        private DefinitionInformationCell Cell(int id,int column)
        {
            if(column is 17 or 19)return ExtraCell(id,column);
            var cell=ordinary.Cell(id,column);
            return new(cell.Text,null,cell.UnresolvedInput){SortUnresolvedOwner=cell.SortUnresolvedInput};
        }
    }
}
