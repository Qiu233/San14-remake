using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2A2620(recipient,captured,0), at the initial City callback in2851B0.
    //An invalid recipient (including Force0) still cancels other forces'
    //matching requests. The callback owns neither a new current nor a batch.
    internal GameState ApplyCityCaptureAttackRequestsCurrent(GameState state, SourceWorldContext current,
        BaseReference captured, ForceReference recipient, List<CampaignEffectRequest> effects, bool showRelationChange = true)
    {
        int recipientId = TalentKnown(recipient.PhysicalSlot, "capture_request_recipient_Force");
        if (!TalentKnown(current.ReadBaseNativeValidity(captured), "capture_request_target_validity"))
        {
            if (TalentKnown(AttackRequestObjective(state, recipientId).RemainingMonths,
                "capture_request_remaining_months161") == 0)
                return TerminateDiplomacyAttackRequestCurrent(state, current, recipient, captured, 2, effects, showRelationChange);
            return state;
        }

        ushort location = TalentKnown(current.ReadBaseObjectLink(captured), "capture_request_target_location4E");
        foreach (int physical in ReadAttackRequestForceOrder(current))
        {
            //Read the latest saved objective each time. An earlier callback
            //may have cleared a different holder selected by2E98E0 itself.
            var objective = AttackRequestObjective(state, physical);
            if (TalentKnown(objective.TargetLocationId, "capture_request_Force_target160") != location) continue;
            int kind = physical == recipientId ? 1 :
                TalentKnown(objective.RequestingForceId, "capture_request_Force_requester15F") == recipientId ? 3 : 4;
            state = TerminateDiplomacyAttackRequestCurrent(state, current, recipient, captured, kind, effects, showRelationChange);
        }
        return state;
    }

    private static ForceCombatObjective AttackRequestObjective(GameState state, int force)
        => state.Forces.GetValueOrDefault(force)?.CombatObjective
            ?? throw DiplomacyMissing("attack_request_Force_objective_" + force);

    private static IEnumerable<int> ReadAttackRequestForceOrder(SourceWorldContext current)
    {
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
            ?? throw DiplomacyMissing("attack_request_ForceC0"))
        {
            if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical })
                throw DiplomacyMissing("attack_request_ForceC0_payload");
            yield return physical;
        }
    }
}
