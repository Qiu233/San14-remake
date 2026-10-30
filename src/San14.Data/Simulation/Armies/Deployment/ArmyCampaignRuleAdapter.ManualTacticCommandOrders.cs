using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IManualTacticCommandRules
{
    public CommandResult IssueManualTacticCommand(GameState state,UnitState actor,int tacticId,
        int targetHexId,bool? progressAllowsReplacement)
    {
        try
        {
            var publication=QueryManualTacticCommands(state,actor.ForceId,actor.Id);
            if(publication.UnresolvedInput is string missing)throw ManualMissing(missing);
            var caster=publication.Casters.FirstOrDefault(row=>row.UnitId==actor.Id);
            if(caster?.Enabled is null)throw ManualMissing(caster?.UnresolvedInput??"command_caster");
            if(caster.Enabled!=true)return CommandResult.Reject("The commander cannot intervene.","manual-tactic.caster");
            var tactic=publication.Tactics.FirstOrDefault(row=>row.TacticId==tacticId&&row.Enabled==true);
            if(tactic is null)
            {
                var unknown=publication.Tactics.FirstOrDefault(row=>row.TacticId==tacticId&&row.Enabled is null);
                if(unknown is not null)throw ManualMissing(unknown.UnresolvedInput??"command_tactic");
                return CommandResult.Reject("The tactic cannot be selected.","manual-tactic.tactic");
            }
            if(tactic.TargetHexIds is null)throw ManualMissing("command_target_chain");
            if(!tactic.TargetHexIds.Contains(targetHexId))
                return CommandResult.Reject("The selected Hex is not a manual tactic target.","manual-tactic.target");
            var arena=state.ReadArmyLifecycle()??throw ManualMissing("command_lifecycle");
            int physical=actor.NativeArmyOrigin?.NativeSlotIndex??throw ManualMissing("command_actor_slot");
            if((uint)physical>=arena.Slots.Count||arena.Receivers.Find(arena.Slots[physical].ReceiverId)?.ActiveUnitId!=actor.Id)
                return CommandResult.Reject("The acting Army is no longer active.","manual-tactic.actor");
            //88DB00 checks the actual clicked pointer in the UI chain first,
            //then normalizes a City/Gate footprint to its current center.
            int selectedHex=NormalizeManualCommandTargetHexCurrent(
                CaptureOwnedArmyReturnWorld(state).Context,targetHexId);
            var request=new ManualTacticRequest(arena.Slots[physical].ReceiverId,tacticId,
                new(ManualTacticTargetKind.Hex,selectedHex));
            var queued=TryQueueManualTacticIntervention(state,ReadManualCommandCurrentPeriod(state),request,progressAllowsReplacement);
            if(queued.UnresolvedOwner is string unresolved)throw ManualMissing(unresolved);
            if(queued.State is null||ReferenceEquals(queued.State,state))
                return CommandResult.Reject("The pending intervention cannot be replaced.","manual-tactic.pending");
            return CommandResult.Accept(queued.State);
        }
        catch(InvalidDataException error)
        {return CommandResult.RuleUnavailable("rules.manual_tactic",error.Message);}
    }

    private static int NormalizeManualCommandTargetHexCurrent(SourceWorldContext current,int selectedHex)
    {
        var hex=current.ReadHexTableReference(selectedHex)??throw ManualMissing("command_selected_Hex");
        int link=current.ReadHexObjectLink(hex)??throw ManualMissing("command_selected_Base_link");
        var city=current.ReadCityTableReference(link<=51?link:0)??throw ManualMissing("command_selected_City");
        BaseReference? target=null;
        if(current.ReadBaseNativeValidity(city)??throw ManualMissing("command_selected_City_validity"))target=city;
        else
        {
            var gate=current.ReadGateTableReference(link is >=52 and <=61?link-51:0)
                ??throw ManualMissing("command_selected_Gate");
            if(current.ReadBaseNativeValidity(gate)??throw ManualMissing("command_selected_Gate_validity"))target=gate;
        }
        if(target is null)return selectedHex;
        int raw=current.ReadBaseCurrentHexIndex(target);
        return current.ReadHexTableReference(raw is >=0 and <48400?raw:0)?.PhysicalSlot
            ??throw ManualMissing("command_selected_Base_Hex");
    }
}
