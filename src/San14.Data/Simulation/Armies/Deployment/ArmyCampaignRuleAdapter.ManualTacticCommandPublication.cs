using San14.Core;

namespace San14.Data;

public sealed record ManualTacticCasterChoice(int UnitId,int OfficerId,string Name,
    bool? Enabled,string? UnresolvedInput);
public sealed record ManualTacticCommandChoice(int Slot,int TacticId,string Name,
    bool? Enabled,IReadOnlyList<int>? TargetHexIds,string? UnresolvedInput);
public sealed record ManualTacticCommandPublication(GameState World,int ForceId,
    IReadOnlyList<ManualTacticCasterChoice> Casters,int? SelectedUnitId,
    IReadOnlyList<ManualTacticCommandChoice> Tactics,string? UnresolvedInput);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The current command cards and the selected card's three ordered
    /// available tactics. This query neither advances the calendar nor changes
    /// the singleton intervention owner.</summary>
    public ManualTacticCommandPublication QueryManualTacticCommands(GameState state,int actorForceId,
        int? selectedUnitId=null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var casters=new List<ManualTacticCasterChoice>();
        var tactics=new List<ManualTacticCommandChoice>();
        try
        {
            var cards=QueryCommanderCards(state,actorForceId);
            if(cards.UnresolvedOwner is string cardMissing)throw ManualMissing(cardMissing);
            var current=CaptureOwnedArmyReturnWorld(state).Context;
            DeploymentTacticCurrentInputs.BindCurrent(state,current);
            var context=Context(current);
            int periods=ReadManualCommandCurrentPeriod(state);
            foreach(var card in cards.Cards)
            {
                int unitId=0;bool? enabled=false;string? missing=null;
                try
                {
                    int person=current.ResolveOfficerSource(card.OfficerId)??throw ManualMissing("command_Person");
                    var reference=PersonArmyLocationSource.ReadCommanderArmy(current,person)
                        ??throw ManualMissing("command_Army_pointer");
                    bool valid=current.ReadArmyUnitNativeValidity(reference)??throw ManualMissing("command_Army_validity");
                    if(valid)
                    {
                        int physical=reference.PhysicalSlot??throw ManualMissing("command_Army_slot");
                        var arena=state.ReadArmyLifecycle()??throw ManualMissing("command_lifecycle");
                        if((uint)physical>=arena.Slots.Count||arena.Receivers.Find(arena.Slots[physical].ReceiverId)?.ActiveUnitId is not int active||
                            !state.Units.TryGetValue(active,out var unit))throw ManualMissing("command_active_Army");
                        unitId=unit.Id;
                        if(unit.ForceId==actorForceId)
                        {
                            var equipped=ReadManualCommandEquippedTacticsCurrent(state,current,unit);
                            var rows=new List<ManualTacticCommandChoice>(3);
                            for(int slot=0;slot<3;slot++)
                            {
                                if(slot>=equipped.Count){rows.Add(new(slot,0,"",false,[],null));continue;}
                                int id=equipped[slot];
                                string name=state.Tactics.GetValueOrDefault(id)?.Name??throw ManualMissing("command_tactic_name");
                                bool? eligible=context.ReadManualInterventionEligibility(state,unit,id,periods);
                                IReadOnlyList<int>? hexes=[];string? rowMissing=null;
                                if(eligible is null)rowMissing="manual_tactic.command_eligibility";
                                else if(eligible==true&&selectedUnitId==unit.Id)
                                {
                                    hexes=context.ReadManualCommandTargetHexes(state,unit,id);
                                    if(hexes is null){eligible=null;rowMissing="manual_tactic.command_target_chain";}
                                }
                                rows.Add(new(slot,id,name,eligible,hexes,rowMissing));
                            }
                            enabled=rows.Any(row=>row.Enabled==true)?true:rows.Any(row=>row.Enabled is null)?null:false;
                            if(enabled is null)missing=rows.First(row=>row.Enabled is null).UnresolvedInput;
                            if(selectedUnitId==unit.Id)tactics.AddRange(rows);
                        }
                    }
                }
                catch(InvalidDataException error){enabled=null;missing=error.Message;}
                casters.Add(new(unitId,card.OfficerId,card.Name,enabled,missing));
            }
            return new(state,actorForceId,casters.AsReadOnly(),selectedUnitId,tactics.AsReadOnly(),null);
        }
        catch(InvalidDataException error)
        {return new(state,actorForceId,casters.AsReadOnly(),selectedUnitId,tactics.AsReadOnly(),error.Message);}
    }

    // 2AE180/2314C0: compact valid equipped entries; at sea, select the
    // first three qualified person tactics matching the current naval form.
    private IReadOnlyList<int> ReadManualCommandEquippedTacticsCurrent(GameState state,
        SourceWorldContext current,UnitState unit)
    {
        int hex=ArmyMarchSourceAdapter.ReadCurrentHex(state,unit)??throw ManualMissing("command_Army_hex");
        var pointer=current.ReadHexTableReference(hex)??throw ManualMissing("command_Hex_pointer");
        byte surface=current.ReadHexSurfaceCode(pointer)??throw ManualMissing("command_surface");
        var result=new List<int>(3);
        if((_terrain.Catalog.Types[surface<=30?surface:0].RawField2C&2)==0)
        {
            if(unit.TacticIds is not {Length:3} slots)throw ManualMissing("command_three_equipped_slots");
            foreach(int requested in slots)
            {
                var tactic=current.ReadSkillTacticReference(requested is >=0 and <=200?requested:0)
                    ??throw ManualMissing("command_equipped_tactic");
                if(current.ReadSkillTacticValidity(tactic)??throw ManualMissing("command_equipped_validity"))
                    result.Add(tactic.PhysicalSlot??throw ManualMissing("command_tactic_identity"));
            }
        }
        else
        {
            int person=current.ResolveOfficerSource(unit.OfficerId)??throw ManualMissing("command_naval_Person");
            int formation=ArmyConfigurationSourceAdapter.ReadNaval(state,unit)??throw ManualMissing("command_naval_formation");
            for(int slot=0;slot<10&&result.Count<3;slot++)
            {
                var tactic=DeploymentTacticSelectionSource.ReadCurrentSlot(current,unit.OfficerId,person,slot);
                if(!(current.ReadSkillTacticValidity(tactic)??throw ManualMissing("command_naval_tactic_validity")))continue;
                uint mask=0;
                for(int part=0;part<4;part++)mask|=(uint)(current.ReadSkillTacticRawByte(tactic,0x6C+part)
                    ??throw ManualMissing("command_naval_tactic_mask"))<<(8*part);
                if(formation is >=0 and <21&&(mask&(1u<<formation))!=0)
                    result.Add(tactic.PhysicalSlot??throw ManualMissing("command_naval_tactic_identity"));
            }
        }
        return result.AsReadOnly();
    }

    private static int ReadManualCommandCurrentPeriod(GameState state)
    {
        var world=state.CampaignEventWorld??throw ManualMissing("command_period_offset");
        var season=state.NativeMapSeason??throw ManualMissing("command_calendar");
        return unchecked(world.ElapsedPeriodOffset+(int)season.AdvancedPeriods);
    }
}
