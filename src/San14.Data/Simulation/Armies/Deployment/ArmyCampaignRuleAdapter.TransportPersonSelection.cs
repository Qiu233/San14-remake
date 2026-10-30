using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record TransportPersonSelection(OfficerInformationPageSet Pages,
    IReadOnlyDictionary<int,bool> Enabled,
    IReadOnlyDictionary<int,AreaAdministrationQuery<bool>> AlteredHealth,
    int? InitialOfficerId,string GuideText);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The transport caller's one authored Person page. Candidates,
    /// disabled membership, initial order and cells share one current owner.</summary>
    public AreaAdministrationQuery<TransportPersonSelection> PrepareTransportPersonSelection(
        GameState state,int sourceCityId,int? selectedOfficerId)
    {
        try
        {
            var current=CaptureDeploymentPersonSelectionCurrent(state);
            var source=TransportSource(state,current,sourceCityId);
            var candidates=BuildTransportPersonSelection(state,current,sourceCityId).Candidates!.ToList();
            for(int index=1;index<candidates.Count;index++)
            {
                var row=candidates[index];int previous=index-1;
                while(previous>=0&&CompareTransportPersons(current,row,candidates[previous])<0)
                {candidates[previous+1]=candidates[previous];previous--;}
                candidates[previous+1]=row;
            }
            if(selectedOfficerId is int chosen&&!candidates.Any(row=>row.OfficerId==chosen))
                throw TM("person_selection.initial_member");
            int force=state.Cities[sourceCityId].ForceId??throw TM("person_selection.source_force");
            var reader=new OfficerInformationCurrent(this,state,force,rowPaletteEnabled:true,retainedCurrent:current);
            return AreaAdministrationQuery<TransportPersonSelection>.Known(reader.TransportPages(candidates,selectedOfficerId,
                PrepareCommanderSelectionGuide()));
        }
        catch(InvalidDataException error){return AreaAdministrationQuery<TransportPersonSelection>.Unknown(error.Message);}
    }

    private sealed partial class OfficerInformationCurrent
    {
        internal TransportPersonSelection TransportPages(IReadOnlyList<DeploymentPersonCandidate> candidates,
            int? selected,string guide)
        {
            commandBodyPalette=true;
            var common=OfficerInformationSources.Create(Data).SelectMany(group=>group.Columns)
                .GroupBy(column=>column.Id).ToDictionary(group=>group.Key,group=>group.First());
            common[39]=new(39,0x3D4,Data(1,0x3D4),104,4,false,true,true);
            common[123]=new(123,0x50E,Data(1,0x50E),76,4,false,true,true);
            common[124]=new(124,0x50F,Data(1,0x50F),76,4,false,true,true);
            int[] order=[0,5,39,8,9,10,11,12,123,124,18,194];
            ImmutableArray<OfficerInformationGroup> groups=[new(0,0x3CD,Data(1,0x3CD),
                order.Select(column=>common[column]).ToImmutableArray())];
            var ids=candidates.Select(row=>row.OfficerId).ToArray();
            var pages=new OfficerInformationPageSet(Data(1,0x32E),groups,ids,
                (id,column)=>TransportCell(id,column).Display with{SortUnresolvedOwner=TransportCell(id,column).SortUnresolvedOwner},
                (id,column)=>TransportCell(id,column).SortUnresolvedOwner,
                (left,right,column,ascending)=>(TransportCell(left,column).Key??throw Missing("transport.sort_key"))
                    .Before(TransportCell(right,column).Key??throw Missing("transport.sort_key"),ascending))
                {DynamicPaletteEnabled=true};
            //251280 B0=-1: initialization applies the common selected-first
            //sort to the caller's real current Person, without a checked front.
            pages.SortSelection(selected is int chosen?[chosen]:[],selectedFirst:true);
            return new(pages,candidates.ToDictionary(row=>row.OfficerId,
                    row=>row.DisabledReasons==DeploymentPersonDisabledReason.None),
                ids.ToDictionary(id=>id,MovementAlteredHealth),selected,guide);
        }

        private PreparedCell TransportCell(int id,int column)
        {
            if(column is not (123 or 124))return Cell(id,column);
            if(prepared.TryGetValue((id,column),out var saved))return saved;
            try
            {
                var person=ReadPerson(id);
                bool HasTrait()
                {
                    //232E30 reads the same qualified nine current slots as
                    //230730, including original/custom-person exclusions.
                    int trait=column==123?63:64;
                    for(int slot=0;slot<9;slot++)
                        if(Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,
                            person.PhysicalSlot,slot),"transport.trait_slot")==trait)return true;
                    return false;
                }
                saved=MakeCell(()=>HasTrait()?Data(1,0x338):"",()=>new NumberKey(HasTrait()?1:0));
            }
            catch(InvalidDataException error){saved=new(new(null,null,error.Message),null,error.Message);}
            return prepared[(id,column)]=saved;
        }
    }
}
