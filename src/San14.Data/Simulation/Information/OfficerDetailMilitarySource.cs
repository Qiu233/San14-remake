using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerDetailsCurrentReader
    {
        private OfficerDetailMilitary ReadMilitary()
        {
            var doctrine=Q<OfficerDetailEntity?>(() =>
            {
                if(!OrdinaryDisplay)return null;
                int raw=person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId ?? throw OfficerDetailMissing("doctrine");
                var reference=current.ReadStaticTableReference(0x78cf8,raw<7?raw:0) ?? throw OfficerDetailMissing("doctrine_lookup");
                int id=DetailNeed(reference.PhysicalSlot,"doctrine_receiver");
                if(id==0)return null;
                return new(OfficerDetailEntityKind.Doctrine,id,state.Doctrines.GetValueOrDefault(id)?.Name
                    ?? throw OfficerDetailMissing("doctrine_name"));
            });
            var policy=Q<OfficerDetailEntity?>(() =>
            {
                if(!OrdinaryDisplay)return null;
                int raw=person.PolicyId ?? (person.PolicyBaseCost.HasValue ? 0 : throw OfficerDetailMissing("policy_owner"));
                var reference=current.ReadStaticTableReference(0x78d50,raw<121?raw:0) ?? throw OfficerDetailMissing("policy_lookup");
                int id=DetailNeed(reference.PhysicalSlot,"policy_receiver");
                if(id==0)return null;
                string name=state.Policies.GetValueOrDefault(id)?.Name ?? throw OfficerDetailMissing("policy_name");
                //The same current personal byte is used by230A50 as its base
                //cost and by this detail consumer as the policy level.
                int level=Math.Min(10,(person.PolicyBaseCost ?? throw OfficerDetailMissing("policy_personal_level")) + Cells.CreditBonus(candidate,3));
                string suffix=Data(1,0x384).Replace("%d",level.ToString(System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal);
                return new(OfficerDetailEntityKind.Policy,id,name+" "+suffix);
            });
            var cost=Q<int?>(()=>ReadPolicyCost(false));
            var nominal=Q<int?>(()=>ReadPolicyCost(true));
            var tactics=ImmutableArray.CreateBuilder<OfficerDetailTactic>(10);
            for(int slot=0;slot<10;slot++)
            {
                int? id=null;
                try
                {
                    var reference=DeploymentTacticSelectionSource.ReadCurrentSlot(current,person.Id,physical,slot);
                    if(!DetailNeed(current.ReadSkillTacticValidity(reference),"tactic_validity"))
                    { tactics.Add(new(slot,null,null,[],null));continue; }
                    id=DetailNeed(reference.PhysicalSlot,"tactic_receiver");
                    string name=state.Tactics.GetValueOrDefault(id.Value)?.Name ?? throw OfficerDetailMissing("tactic_name");
                    var effects=Q(()=>owner.ReadPersonnelLearnEffectsCurrent(state,current,id.Value).ToImmutableArray());
                    tactics.Add(new(slot,id,name,effects.Available?effects.Value:[],effects.UnresolvedInput));
                }
                catch(InvalidDataException error)
                {issues.Add(error.Message);tactics.Add(new(slot,id,null,[],error.Message));}
            }
            var formations=ImmutableArray.CreateBuilder<OfficerDetailFormation>();
            try
            {
                bool special=DuelAftermathSpecialForce(current,physical)||Cells.Special(candidate);
                int[] rawChoices=special?[14]:Enumerable.Range(1,10).ToArray();
                foreach(int raw in rawChoices)
                {
                    var reference=current.ReadStaticTableReference(0x77428,raw) ?? throw OfficerDetailMissing("formation_lookup");
                    int id=DetailNeed(reference.PhysicalSlot,"formation_receiver");
                    string name=state.Formations.GetValueOrDefault(id)?.Name ?? throw OfficerDetailMissing("formation_name");
                    formations.Add(new(id,name,special ? AreaAdministrationQuery<bool?>.Known(true) : Q<bool?>(()=>
                        DeploymentFormationEligibility.Allows(DetailNeed(current.ReadPersonFormationMask164(physical),"formation_mask"),id,person.Id))));
                }
            }
            catch(InvalidDataException error){issues.Add(error.Message);}
            return new(doctrine,policy,cost,tactics.ToImmutable(),formations.ToImmutable())
                {PolicyCostBeforeDiscount=nominal};
        }

        private int? ReadPolicyCost(bool ignoreDiscount)
        {
            if(!OrdinaryDisplay)return null;
            int raw=person.PolicyId ?? (person.PolicyBaseCost.HasValue ? 0 : throw OfficerDetailMissing("policy_owner"));
            var pointer=current.ReadStaticTableReference(0x78d50,raw<121?raw:0) ?? throw OfficerDetailMissing("cost_policy_lookup");
            int id=DetailNeed(pointer.PhysicalSlot,"cost_policy_receiver");
            if(id==0)return null;
            var policy=state.Policies.GetValueOrDefault(id) ?? throw OfficerDetailMissing("cost_policy_definition");
            int basis=person.PolicyBaseCost ?? throw OfficerDetailMissing("policy_personal_cost");
            bool same=false,rulerIsPerson=false;
            if(!ignoreDiscount && DetailNeed(current.ReadForceNativeValidity(Force),"cost_force_validity"))
            {
                int rulerRaw=DetailNeed(current.ReadForceRecord(ForceId)?.RawRuler10,"cost_force_ruler");
                var ruler=current.ReadPersonLookupReference(rulerRaw<6001?rulerRaw:0) ?? throw OfficerDetailMissing("cost_ruler_lookup");
                int rulerPhysical=DetailNeed(ruler.PhysicalSlot,"cost_ruler_receiver");
                if(DetailNeed(current.ReadPersonNativeValidity(rulerPhysical),"cost_ruler_validity"))
                {
                    int rulerId=DetailNeed(current.ReadPersonIdentity(rulerPhysical),"cost_ruler_identity");
                    rulerIsPerson=rulerId==person.Id;
                    if(!rulerIsPerson)
                    {
                        var rulerOwner=state.Officers.GetValueOrDefault(rulerId) ?? throw OfficerDetailMissing("cost_ruler_owner");
                        same=DoctrineReceiver(person)==DoctrineReceiver(rulerOwner);
                    }
                }
            }
            return PolicyCosts.Calculate(basis,policy.CostAddition,same,rulerIsPerson,ignoreDiscount).Total;

            int DoctrineReceiver(OfficerState officer)
            {
                int requested=officer.DoctrineId ?? officer.AbilityState?.ObservedDoctrineId ?? throw OfficerDetailMissing("cost_doctrine");
                return DetailNeed((current.ReadStaticTableReference(0x78cf8,requested<7?requested:0)
                    ?? throw OfficerDetailMissing("cost_doctrine_lookup")).PhysicalSlot,"cost_doctrine_receiver");
            }
        }
    }
}
