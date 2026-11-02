using San14.Core;
namespace San14.Data;

public sealed record DispatchHudRow(int OfficerId,string Name,ushort? Appearance,int Kind,string Caption,
    string? Target,int Remaining,int? HexId);
public sealed record DispatchHudPublication(GameState Source,int ForceId,IReadOnlyList<DispatchHudRow> Rows,string? UnresolvedInput);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One current publication of CMainJump's dispatched people.
    /// Called when the world's immutable publication changes, never during draw.</summary>
    public DispatchHudPublication QueryDispatchHud(GameState state,int forceId)
    {
        var rows=new List<DispatchHudRow>();
        try
        {
            var current=CaptureOwnedArmyReturnWorld(state).Context;
            var player=OwnedCurrentPlayerForce.ReadCurrentReference(state,current)??throw Missing("player");
            int own=Need(current.ReadForcePhysicalSlot(player),"player_identity");
            if(own!=forceId)throw Missing("player_binding");
            foreach(int? entry in current.ReadRegisteredPersonPhysicalSlots()??throw Missing("Person_registry"))
            {
                int physical=Need(entry,"Person_payload");
                if(current.ReadPersonNativeValidity(physical)!=true)continue;
                int identity=Need(current.ReadPersonIdentity(physical),"Person_identity");
                var person=state.Officers.GetValueOrDefault(identity)??throw Missing("Person_owner");
                var force=current.ReadPersonForce(physical)??throw Missing("person_force");
                if(current.ReadForcePhysicalSlot(force)!=own)continue;
                var linked=current.ReadPersonLinkedEntity(physical)??throw Missing("linked_command");
                if(linked.Identity is null)continue;
                var task=new CurrentPersonTaskSource(state,current,physical,_repository,_scenario,state.NativeArmySimulation?.Parameters.Language).ReadDispatchTask();
                if(task is null)continue;
                int raw=Need(current.ReadPersonDistrictSlot(physical),"district");
                bool allowed=PlayerDistrict(raw);
                if(!allowed&&task.Value.Kind==1&&state.PersonnelCommands?.Returns.GetValueOrDefault(linked.Identity.Value) is {} moving&&moving.RouteDestinations.Length>0)
                {
                    var destination=moving.RouteDestinations[0];
                    int id=destination.BaseId;
                    var location=BaseLookup.Find(id,current);
                    if(current.ReadBaseNativeValidity(location)==true)
                        allowed=PlayerDistrict(Need(current.ReadBaseDistrictSlot(location),"destination_district"));
                }
                if(!allowed)continue;
                int kind=task.Value.Kind;
                int[] captions=[0,0x270,0x277,0x276,0x288,0x289,0x266,0x3B4,0x3B5,0x3B6,0x3B7,0x3B8,0x2A7,0x3B9,0x3BA,0x3BB,0,0x700,0x3BB,0x70A,0x738,0x838,0x277,0x3B8,0xC5,0xC6];
                if(kind<0||kind>=captions.Length)throw Missing("command_kind");
                string caption=kind==16?ReadHistoryDataText(0,0x10):ReadHistoryDataText(1,captions[kind]);
                rows.Add(new(person.Id,person.Name,current.ReadPersonPortraitAppearance(physical),kind,caption,task.Value.Target,
                    task.Value.Remaining,ReadCommanderCardLocation(current,physical).HexId));
            }
            // 87F3E0 compares command total-minus-elapsed; equal remaining
            // commands retain their current source order.
            return new(state,forceId,rows.OrderBy(row=>row.Remaining).ToArray(),null);
            bool PlayerDistrict(int raw)
            {
                var district=current.ReadDistrictTableReference(raw<52?raw:0)??throw Missing("district_pointer");
                int p=Need(district.PhysicalSlot,"district_receiver");
                if(current.ReadDistrictForceId(p)!=own)return false;
                int leader=Need(current.ReadDistrictLeader(p),"district_leader");
                var reference=current.ReadPersonLookupReference(leader<6001?leader:0)??throw Missing("leader_pointer");
                return reference.PhysicalSlot is int l&&current.ReadPersonNativeValidity(l)==true&&current.ReadPersonStatus(l)==1;
            }
        }
        catch(InvalidDataException error){return new(state,forceId,rows.AsReadOnly(),error.Message);}
        static T Need<T>(T? value,string field) where T:struct=>value??throw Missing(field);
        static InvalidDataException Missing(string field)=>new("dispatch-hud."+field);
    }
}
