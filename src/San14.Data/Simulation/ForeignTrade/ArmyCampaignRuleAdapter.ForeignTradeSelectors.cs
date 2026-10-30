using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private ForeignTradeSelectorPublication PrepareForeignTradeSelectorCurrent(GameState state, SourceWorldContext current,
        int forceId, ForeignTradeDraft draft, ForeignTradeSelectorKind kind)
    {
        string Data(int id) => ReadHistoryDataText(1,id);
        if(kind==ForeignTradeSelectorKind.Executors)
        {
            //6A1960/6B1E30: shortest journey, effective politics, identity;
            //the common B0=-1 selection sort then brings the real initial list first.
            var people=draft.Executors.OrderBy(row=>row.TravelDays??throw ForeignTradeCommandMissing("selector_travel"))
                .ThenByDescending(row=>row.Politics??throw ForeignTradeCommandMissing("selector_politics"))
                .ThenBy(row=>row.OfficerId).ToArray();
            var reader=new OfficerInformationCurrent(this,state,forceId,rowPaletteEnabled:true,retainedCurrent:current);
            var pages=reader.ForeignTradePages(people,draft.ExecutorIds);
            var health=people.ToDictionary(row=>row.OfficerId, row=>reader.MovementAlteredHealth(row.OfficerId));
            var enabled=people.ToDictionary(row=>row.OfficerId,row=>row.Enabled==true);
            var groups=pages.Groups.Select(group=>new ForeignTradeSelectorGroup(group.Index,group.Caption,
                group.Columns.Where(column=>column.Visible).Select(column=>new ForeignTradeSelectorColumn(column.Id,column.Caption,
                    column.Width,column.CellFormat,column.InitialAscending)).ToImmutableArray())).ToImmutableArray();
            return new(kind,groups,draft.ExecutorIds,draft.MaximumExecutors,
                group=>pages.ReadPage(group).Rows.Select(row=>new ForeignTradeSelectorRow(row.OfficerId,
                    row.Cells.Select(cell=>new ForeignTradeSelectorCell(cell.Text,cell.UnresolvedOwner,cell.SortUnresolvedOwner,
                        cell.PresetIndex,cell.StatePresets,cell.SourceFormattedText,cell.MarkerColour?.CpuPacked)).ToImmutableArray(),
                    enabled[row.OfficerId],health[row.OfficerId].Available?health[row.OfficerId].Value:null)).ToImmutableArray(),
                (group,column,ascending)=>{var result=pages.Sort(group,column,ascending);return result.Available
                    ?AreaAdministrationQuery<bool>.Known(true):AreaAdministrationQuery<bool>.Unknown(result.UnresolvedInput!);},
                (selected,first)=>pages.SortSelection(selected,first),()=>pages.CurrentOfficerIds);
        }
        int[] order=kind==ForeignTradeSelectorKind.Item?draft.Items.Select(row=>row.ItemId).ToArray():draft.Countries.Where(row=>row.Enabled==true)
            .OrderByDescending(row=>ForeignTradeCurrentSource.ReadFriendship(state,forceId,row.Id)).ThenBy(row=>row.Id).Select(row=>row.Id).ToArray();
        var allowed=kind==ForeignTradeSelectorKind.Item?draft.Items.ToDictionary(row=>row.ItemId,row=>row.Enabled==true)
            :draft.Countries.ToDictionary(row=>row.Id,row=>row.Enabled==true);
        var cells=new Dictionary<(int,int),ForeignTradeSelectorCell>();
        var items=kind==ForeignTradeSelectorKind.Item?new DiplomacyItemTargetCurrent(this,state,current,forceId):null;
        var technologies=new Lazy<IReadOnlyList<Formats.S14ForeignTechnologyDefinition>>(()=>
            _repository.LoadForeignTechnologies(_scenario.FixedDataSource.SourcePath).Definitions);
        int[] columns=kind==ForeignTradeSelectorKind.Item?[0,1,8,3,4,5,6,7]:[0,1,2,3,4,5];
        int[] captions=kind==ForeignTradeSelectorKind.Item?[0x246,0x401,0x452,0x411,0x423,0x424,0x425,0x426]:[0x701,0x703,0x724,0x725,0x726,0x727];
        int[] widths=kind==ForeignTradeSelectorKind.Item?[188,104,132,132,76,160,132,76]:[104,104,188,188,188,188];
        int[] formats=kind==ForeignTradeSelectorKind.Item?[3,4,3,4,5,4,4,4]:[3,5,3,3,3,3];
        bool[] ascending=kind==ForeignTradeSelectorKind.Item?[true,true,false,true,false,true,true,true]:[true,true,true,true,true,true];
        ImmutableArray<ForeignTradeSelectorGroup> single=[new(0,Data(kind==ForeignTradeSelectorKind.Item?0x2DC:0x3CD),
            columns.Select((column,slot)=>new ForeignTradeSelectorColumn(column,Data(captions[slot]),widths[slot],formats[slot],ascending[slot])).ToImmutableArray())];
        return new(kind,single,[],1,group=>
            order.Select(id=>new ForeignTradeSelectorRow(id,columns.Select(column=>Cell(id,column)).ToImmutableArray(),allowed[id])).ToImmutableArray(),
            (group,column,asc)=>
            {
                try
                {
                    foreach(int id in order)if(Cell(id,column).SortUnresolvedOwner is {} issue)throw new InvalidDataException(issue);
                    order=order.OrderBy(id=>id,Comparer<int>.Create((a,b)=>Before(a,b,column,asc)?-1:Before(b,a,column,asc)?1:0)).ToArray();
                    return AreaAdministrationQuery<bool>.Known(true);
                }
                catch(InvalidDataException error){return AreaAdministrationQuery<bool>.Unknown(error.Message);}
            },(_,_)=>{},()=>order);

        ForeignTradeSelectorCell Cell(int id,int column)
        {
            if(cells.TryGetValue((id,column),out var saved))return saved;
            if(items is not null)
            {
                var item=items.Cell(id,column);
                saved=new(item.Text,item.UnresolvedInput,item.SortUnresolvedInput,FirstGlyphColour:item.FirstGlyphColour);
            }
            else
            {
                try
                {
                    var country=ReadForeignTradeCountry(id);int stage=ForeignTradeCurrentSource.ReadStage(state,forceId,id);
                    string value=column switch
                    {
                        0=>country.Name,
                        1=>ForeignTradeCurrentSource.ReadFriendship(state,forceId,id).ToString(CultureInfo.InvariantCulture),
                        2=>state.ItemDefinitions.GetValueOrDefault(state.Items?.GetValueOrDefault(country.FirstItemId<202?country.FirstItemId:0)?.DefinitionId
                            ??throw ForeignTradeCommandMissing("selector_country_item_owner"))?.Name
                            ??throw ForeignTradeCommandMissing("selector_country_item"),
                        3=>technologies.Value[country.FirstWeaponId<9?country.FirstWeaponId:0].Name,
                        4=>technologies.Value[country.SecondWeaponId<9?country.SecondWeaponId:0].Name,
                        5=>_skills.Gems.Gems[country.GemId<31?country.GemId:0].Name,
                        _=>throw ForeignTradeCommandMissing("selector_country_column")
                    };
                    //Original 309990 colour7 carries preset65 for an acquired reward.
                    saved=new(value,SortUnresolvedOwner:column==0&&country.NameSortKey is null?"foreign-trade.country.sort_key18":null,
                        Preset:column>=2&&stage>column-2?65:null);
                }
                catch(InvalidDataException error){saved=new(null,error.Message,error.Message);}
            }
            return cells[(id,column)]=saved;
        }
        bool Before(int a,int b,int column,bool asc)
        {
            if(items is not null)return items.Before(a,b,column,asc);
            if(column>=2)return false; //These four source headers keep direction but have no comparator.
            int compare=column==0?PersonNameOrder.Compare(ReadForeignTradeCountry(a).NameSortKey!,ReadForeignTradeCountry(b).NameSortKey!)
                :ForeignTradeCurrentSource.ReadFriendship(state,forceId,a).CompareTo(ForeignTradeCurrentSource.ReadFriendship(state,forceId,b));
            return asc?compare<0:compare>0;
        }
    }

    private sealed partial class OfficerInformationCurrent
    {
        internal OfficerInformationPageSet ForeignTradePages(IReadOnlyList<ForeignTradeExecutorChoice> candidates,IReadOnlyList<int> initial)
        {
            commandBodyPalette=true;
            var common=OfficerInformationSources.Create(Data).SelectMany(group=>group.Columns).GroupBy(column=>column.Id)
                .ToDictionary(group=>group.Key,group=>group.First());
            common[130]=new(130,0x713,Data(1,0x713),76,4,false,true,true);
            common[131]=new(131,0x712,Data(1,0x712),76,4,false,true,true);
            common[102]=new(102,0x26C,Data(1,0x26C),76,5,true,true,true);
            int[][] orders=[[0,5,35,8,9,10,11,12,130,131,102,18,194],[0,29,28,24,30,31,32,33]];
            ImmutableArray<OfficerInformationGroup> groups=orders.Select((order,index)=>new OfficerInformationGroup(index,
                index==0?0x3CD:0x451,Data(1,index==0?0x3CD:0x451),order.Select(id=>common[id]).ToImmutableArray())).ToImmutableArray();
            var rows=candidates.ToDictionary(row=>row.OfficerId);
            PreparedCell TradeCell(int id,int column)
            {
                if(column is not (102 or 130 or 131))return Cell(id,column);
                if(prepared.TryGetValue((id,column),out var saved))return saved;
                try
                {
                    if(column==102)
                    {
                        int days=rows[id].TravelDays??throw Missing("foreign_trade.travel_days");
                        saved=MakeCell(()=>Number(days),()=>new NumberKey(days));
                    }
                    else
                    {
                        int physical=ReadPerson(id).PhysicalSlot;int trait=column==130?186:185;
                        bool exists=Enumerable.Range(0,9).Any(slot=>Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,
                            physical,slot),"foreign_trade.trait_slot")==trait);
                        saved=MakeCell(()=>exists?Data(1,0x338):"",()=>new NumberKey(exists?1:0));
                    }
                }
                catch(InvalidDataException error){saved=new(new(null,null,error.Message),null,error.Message);}
                return prepared[(id,column)]=saved;
            }
            var pages=new OfficerInformationPageSet(Data(1,0x58F),groups,candidates.Select(row=>row.OfficerId).ToArray(),
                (id,column)=>TradeCell(id,column).Display with{SortUnresolvedOwner=TradeCell(id,column).SortUnresolvedOwner},
                (id,column)=>TradeCell(id,column).SortUnresolvedOwner,
                (a,b,column,asc)=>(TradeCell(a,column).Key??throw Missing("foreign_trade.sort_key"))
                    .Before(TradeCell(b,column).Key??throw Missing("foreign_trade.sort_key"),asc)){DynamicPaletteEnabled=true};
            pages.SortSelection(initial,selectedFirst:true);return pages;
        }
    }
}
