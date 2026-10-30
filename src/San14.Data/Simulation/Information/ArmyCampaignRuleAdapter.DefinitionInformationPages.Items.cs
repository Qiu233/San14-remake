using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private DiplomacyItemTargetCurrent? items;
        private int[] ItemCandidates()
        {
            var ids=new List<int>();
            var player=current.ReadForceTableReference(playerForceId is >=0 and <52?playerForceId:0)
                ??throw Missing("Item.player_Force_pointer");
            for(int lookup=0;lookup<202;lookup++)
            {
                try
                {
                    var item=current.ReadRegistryTableReference(RegistryKind.Item,lookup)??throw Missing("Item.table_pointer");
                    if(!Need(current.ReadRegistryObjectNativeValidity(item),"Item.validity"))continue;
                    int id=Need(item.PhysicalSlot,"Item.physical_identity");
                    ushort raw=Need(current.ReadCaptureItemOwner4A(id),"Item.holder4A");
                    var holder=(raw<=6000?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0))
                        ??throw Missing("Item.holder_pointer");
                    int person=Need(holder.PhysicalSlot,"Item.holder_physical");
                    if(ownOnly&&(!Need(current.ReadPersonNativeValidity(person),"Item.holder_validity")||
                        (current.ReadPersonForce(person)??throw Missing("Item.holder_Force_pointer"))!=player))continue;
                    byte status=Need(current.ReadPersonStatus(person),"Item.holder_status");
                    if(status==255||status is >=7 and <=9)continue;
                    ids.Add(id);
                }
                catch(InvalidDataException error){candidateIssues.Add(error.Message);}
            }
            return ids.ToArray();
        }
        private PreparedCell ItemCell(int id,int column)
        {
            // The diplomacy receiver already implements these same eight
            // native Item getters and tagged keys. Only admission differs.
            items??=new(rules,state,current,playerForceId);
            var cell=items.Cell(id,column);
            return new(new(cell.Text,null,cell.UnresolvedInput),new ItemKey(items,id,column),cell.SortUnresolvedInput);
        }
        private sealed record ItemKey(DiplomacyItemTargetCurrent Reader,int Id,int Column):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)=>
                Reader.Before(Id,((ItemKey)other).Id,Column,ascending);
        }
    }
}
