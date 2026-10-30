using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class CurrentWorldItemSelectionCurrent
    {
        private sealed record ExtraKey(int Number,bool Empty=false);
        private sealed record ExtraPrepared(DefinitionInformationCell Cell,ExtraKey? Key);
        private readonly Dictionary<(int,int),ExtraPrepared> extra=[];
        private DefinitionInformationCell ExtraCell(int id,int column)=>ReadExtra(id,column).Cell;
        private ExtraPrepared ReadExtra(int id,int column)
        {
            if(extra.TryGetValue((id,column),out var saved))return saved;
            string? text=null,issue=null,sortIssue=null;ExtraKey? key=null;
            int Identity()=>state.Items?.GetValueOrDefault(id)?.Id??throw CurrentWorldItemEditMissing("selection.Item_identity");
            byte Flags()=>state.Items?.GetValueOrDefault(id)?.Lifecycle?.RetainedFlags
                ??throw CurrentWorldItemEditMissing("selection.Item_flags4F");
            try
            {
                text=column switch
                {
                    17=>(Flags()&8)==0?rules.ReadHistoryDataText(1,0x338):"",
                    19=>Identity()>151?rules.ReadHistoryDataText(1,0x338):"",
                    _=>throw CurrentWorldItemEditMissing("selection.column")
                };
            }
            catch(InvalidDataException error){issue=error.Message;}
            try
            {
                key=column switch
                {
                    17=>new((Flags()>>3)&1),
                    19=>Identity()>151?new(1):new(int.MaxValue,true),
                    _=>throw CurrentWorldItemEditMissing("selection.sort_column")
                };
            }
            catch(InvalidDataException error){sortIssue=error.Message;}
            return extra[(id,column)]=new(new(text,null,issue){SortUnresolvedOwner=sortIssue},key);
        }
        private bool ExtraBefore(int left,int right,int column,bool ascending)
        {
            var a=ReadExtra(left,column).Key??throw CurrentWorldItemEditMissing("selection.sort_key");
            var b=ReadExtra(right,column).Key??throw CurrentWorldItemEditMissing("selection.sort_key");
            // Both original extra columns use mode0. The tagged empty in
            // column19 follows direction; column17 always has a numeric key.
            if(a.Empty!=b.Empty)return ascending?!a.Empty:a.Empty;
            return !a.Empty&&(ascending?a.Number<b.Number:b.Number<a.Number);
        }
    }
}
