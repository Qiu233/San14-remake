using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState CompleteCampaignExpeditionsTurn(GameState state)
        => UpdateCampaignExpeditionsCurrent(state, CaptureWritableTransportRouteCurrent(state));

    /// <summary>2FDC70: update expedition retreat state from the actual Army
    /// manager, then reset expedition records which no longer have members.
    /// The caller supplies its current pool view and owns stage scheduling.</summary>
    internal GameState UpdateCampaignExpeditionsCurrent(GameState state, SourceWorldContext current)
    {
        var lifecycle = state.ReadArmyLifecycle() ?? throw ExpeditionLifecycleMissing("Army_owner");
        var present = new HashSet<int>();
        var active = new HashSet<int>();
        var retreating = new HashSet<int>();
        foreach (var pointer in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            int physical = pointer.PhysicalSlot ?? throw ExpeditionLifecycleMissing("manager_Army_pointer");
            if ((uint)physical >= (uint)lifecycle.Slots.Count) throw ExpeditionLifecycleMissing("manager_Army_slot");
            var source = lifecycle.Slots[physical];
            var receiver = lifecycle.Receivers.Find(source.ReceiverId)
                ?? throw ExpeditionLifecycleMissing("manager_Army_receiver");
            UnitState? image;
            if (receiver.ActiveUnitId is int id)
                image = state.Units.GetValueOrDefault(id) ?? throw ExpeditionLifecycleMissing("manager_Army_active_image");
            else image = receiver.PendingImage;
            byte group = (image is null ? source.MapUiByte13C : image.NativeMapUiByte13C)
                ?? throw ExpeditionLifecycleMissing("Army_expedition_reference");
            if (group == 0) continue;
            present.Add(group);
            byte action = image is not null ? ArmyConfigurationSourceAdapter.ReadAction(state, image)
                ?? throw ExpeditionLifecycleMissing("Army_action") : throw ExpeditionLifecycleMissing("Army_action_image");
            if (action is 11 or 12) retreating.Add(group);
            else active.Add(group);
        }

        var before = state.CampaignExpeditions;
        if (before is null && present.Any(group => group is >= 1 and <= 50))
            throw ExpeditionLifecycleMissing("referenced_expedition_owner");
        Dictionary<int, CampaignExpedition>? expeditions = null;
        foreach (int group in retreating)
        {
            if (active.Contains(group)) continue;
            if (group is < 1 or > 50)
                throw ExpeditionLifecycleMissing("retreat_invalid_expedition_receiver");
            if (before?.Expeditions.GetValueOrDefault(group) is not { } expedition)
                throw ExpeditionLifecycleMissing("retreat_reset_expedition_receiver");
            if (expedition.IsWithdrawing) continue;
            expeditions ??= new(before.Expeditions);
            expeditions[group] = expedition with { IsWithdrawing = true };
        }
        if (before is not null)
        {
            foreach (int group in before.Expeditions.Keys)
            {
                if (present.Contains(group)) continue;
                expeditions ??= new(before.Expeditions);
                expeditions.Remove(group);
            }
            return expeditions is null ? state : state with
                { CampaignExpeditions = before with { Expeditions = expeditions } };
        }
        // Every real table receiver has reached226B90. This is an actual reset
        // writer, not a load-time inference of unknown expedition contents.
        return state with { CampaignExpeditions = new(new(), null) };
    }

    private static InvalidDataException ExpeditionLifecycleMissing(string field) =>
        new("campaign.expedition.lifecycle." + field);
}
