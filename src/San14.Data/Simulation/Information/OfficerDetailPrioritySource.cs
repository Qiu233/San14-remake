using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IOfficerPriorityRules
{
    private OfficerDetailPriority ReadOfficerDetailPriorityCurrent(GameState state,SourceWorldContext current,
        int playerForceId,int physical)
    {
        try
        {
            var force=current.ReadPersonForce(physical) ?? throw OfficerDetailMissing("priority_force");
            int actual=DetailNeed(force.PhysicalSlot,"priority_force_receiver");
            bool editable=DetailNeed(current.ReadForceNativeValidity(force),"priority_force_validity") && actual==playerForceId;
            if(editable)
                editable=actual is not (>=46 and <=50) && !DuelAftermathSpecialPerson(current,physical) &&
                    DetailNeed(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(physical)),"priority_behavior");
            if(!editable)return new(null,[],null){Visible=false};
            byte status=DetailNeed(current.ReadPersonStatus(physical),"priority_status");
            int selected=status is 1 or 2 ? 1 : status==6 ? 0 :
                DetailNeed(_governorInputs.ReadCurrentPriority160(state,physical),"priority_current_flags")&1;
            //Values are the actual callback event values. Controller display
            //group (~flags)&1 reverses these, but is not a command payload.
            return new(selected,[new(1,ReadHistoryDataText(1,0xc7),editable&&status!=6),
                new(0,ReadHistoryDataText(1,0xc8),editable&&status is not (1 or 2))],null);
        }
        catch(InvalidDataException error){return new(null,[],error.Message);}
    }

    public CommandResult ApplyOfficerPriority(GameState state,int forceId,int officerId,int choice,CommandContext context)
    {
        if(choice is not (0 or 1))return CommandResult.Reject("Unknown officer priority.","officer-detail.priority_choice");
        try
        {
            var current=CaptureDeploymentPersonSelectionCurrent(state);
            int p=current.ResolveOfficerSource(officerId) ?? throw OfficerDetailMissing("priority_person");
            if(!DetailNeed(current.ReadPersonNativeValidity(p),"priority_person_validity"))
                return CommandResult.Reject("The officer is no longer available.","officer-detail.priority_person");
            var publication=ReadOfficerDetailPriorityCurrent(state,current,forceId,p);
            if(publication.UnresolvedOwner is {} missing)throw new InvalidDataException(missing);
            if(!publication.Choices.Any(option=>option.Value==choice&&option.Enabled))
                return CommandResult.Reject("The officer priority cannot be changed.","officer-detail.priority_permission");
            byte old=DetailNeed(_governorInputs.ReadCurrentPriority160(state,p),"priority_current_flags");
            var simulation=state.NativeArmySimulation ?? throw OfficerDetailMissing("priority_saved_owner");
            var values=new Dictionary<string,int>(simulation.Parameters.Int32Values)
                {[OwnedArmyGovernorComparatorInputs.PersonFlags160Key(p)]=(old&~1)|(choice>0?1:0)};
            return CommandResult.Accept(state with {NativeArmySimulation=simulation with
                {Parameters=simulation.Parameters with {Int32Values=values}}});
        }
        catch(InvalidDataException error){return CommandResult.RuleUnavailable(error.Message,"The current officer priority is unavailable.");}
    }

    private sealed partial class OfficerDetailsCurrentReader
    {
        private OfficerDetailPriority ReadPriority()
        {
            var result=owner.ReadOfficerDetailPriorityCurrent(state,current,playerForceId,physical);
            if(result.UnresolvedOwner is {} missing)issues.Add(missing);
            return result;
        }
    }
}
