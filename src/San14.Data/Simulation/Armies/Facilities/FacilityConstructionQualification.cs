using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

/// <summary>The ordinary2C4E30 Person qualification. The caller owns the
/// distinct selected-group intrinsic precheck and actual tutorial shortcut.</summary>
internal static class FacilityConstructionQualification
{
    internal static bool PersonQualifies(GameState state,SourceWorldContext current,int person,
        FacilityConstructionDefinition definition,bool tutorialShortcut=false)
    {
        if(!K(current.ReadPersonNativeValidity(person),"qualification_Person_validity"))return false;
        var sourceForce=current.ReadPersonForce(person)??throw Missing("qualification_Person_Force");
        if(!K(current.ReadForceNativeValidity(sourceForce),"qualification_Force_validity"))return false;
        if(tutorialShortcut)return true;
        bool policy=true;
        if(definition.RequiredPolicyEffect!=0)
        {
            if(!state.PolicyEffects.ContainsKey(definition.RequiredPolicyEffect))throw Missing("qualification_policy_definition");
            int forceId=K(sourceForce.PhysicalSlot,"qualification_Force_identity");
            if(!state.Forces.TryGetValue(forceId,out var force))throw Missing("qualification_Force_owner");
            policy=K(BattleRemakeCurrentForcePolicies.ReadForceLevel(state,force,definition.RequiredPolicyEffect),
                "qualification_policy_current_level")>=definition.RequiredPolicyLevel;
        }
        //The source still constructs/scans its personality descriptor when
        //the independent policy result is false; preserve its allocator order.
        bool trait=definition.RequiredTrait==0||HasCollectedRequirement(current,person,definition.RequiredTrait);
        return policy&&trait;
    }

    private static bool HasCollectedRequirement(SourceWorldContext current,int person,ushort required)
    {
        var temporary=current.CreatePersonInjuryTemporary();
        try
        {
            current.ClearPersonInjuryTemporary(temporary,false);
            //2307D0 appends only valid selected personalities whose nodes
            //actually allocate. It is not the intrinsic2C5830 precheck.
            for(int slot=0;slot<9;slot++)
            {
                var trait=TacticRemakeCurrentTraits.ResolveCommanderPersonality(current,person,slot)
                    ??throw Missing("qualification_personality_selection");
                if(K(current.ReadSkillPersonalityValidity(trait),"qualification_personality_validity"))
                    current.AppendSkillTemporaryPersonality(temporary,trait);
            }
            var node=current.ReadSkillTemporaryHead(temporary)??throw Missing("qualification_personality_head");
            while(node.Identity.HasValue)
            {
                var trait=current.ReadSkillTemporaryPayload(node)??throw Missing("qualification_personality_payload");
                if(K(current.ReadSkillPersonalityValidity(trait),"qualification_collected_validity")&&
                    (K(current.ReadPersonalityByte(trait,0xB6),"qualification_first_effect")==required||
                     K(current.ReadPersonalityByte(trait,0xBA),"qualification_second_effect")==required))return true;
                node=current.ReadHexListNodeNext(node)??throw Missing("qualification_personality_next");
            }
            return false;
        }
        finally {current.ClearPersonInjuryTemporary(temporary,true);}
    }
}
