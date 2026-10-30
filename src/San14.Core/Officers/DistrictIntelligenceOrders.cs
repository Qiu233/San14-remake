namespace San14.Core;

/// <summary>One district intelligence slot. Slot is zero-based; this operation
/// retains its own admission and payment rules rather than the three-slot page.</summary>
public sealed record RemoveDistrictIntelligenceAssignmentOrder(int ForceId, int DistrictId, int Slot)
    : GameOrder(ForceId);
public sealed record SetDistrictIntelligenceAssignmentOrder(int ForceId, int DistrictId, int Slot,
    int TargetForceId, int OfficerId) : GameOrder(ForceId);

public interface IIntelligenceSlotCommandRules
{
    CommandResult RemoveDistrictIntelligenceAssignment(GameState state, ForceState force,
        RemoveDistrictIntelligenceAssignmentOrder order, CommandContext context);
    CommandResult SetDistrictIntelligenceAssignment(GameState state, ForceState force,
        SetDistrictIntelligenceAssignmentOrder order, CommandContext context);
}

/// <summary>A current stock transfer, not district reorganization or personnel
/// assignment. Its provider resolves actual military intelligence stocks and caps.</summary>
public sealed record TransferDistrictMilitaryIntelligenceOrder(int ForceId, int SourceDistrictId,
    int TargetDistrictId, int Amount) : GameOrder(ForceId);

public interface IDistrictMilitaryIntelligenceTransferRules
{
    CommandResult Transfer(GameState state, ForceState force,
        TransferDistrictMilitaryIntelligenceOrder order, CommandContext context);
}

/// <summary>District intelligence commands share normal actor authority.
/// Current eligibility, exact primary selection, accounting and publication
/// remain with the actual command providers.</summary>
public sealed class DistrictIntelligenceOrders(IOrderRules next, IIntelligenceSlotCommandRules? slots,
    IDistrictMilitaryIntelligenceTransferRules? transfers) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (RemoveDistrictIntelligenceAssignmentOrder or SetDistrictIntelligenceAssignmentOrder or
            TransferDistrictMilitaryIntelligenceOrder)) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || order.ForceId != force.Id)
            return CommandResult.Reject("District intelligence requires active strategy control.", "district-intelligence.scope");
        if (order is TransferDistrictMilitaryIntelligenceOrder transfer)
        {
            if (context.DelegatedDistrictId is not null)
                return CommandResult.Reject("Military intelligence transfer requires direct force control.", "district-intelligence.transfer_scope");
            if (transfer.Amount <= 0 || transfer.SourceDistrictId == transfer.TargetDistrictId ||
                !state.Districts.TryGetValue(transfer.SourceDistrictId, out var source) || source.ForceId != force.Id ||
                !state.Districts.TryGetValue(transfer.TargetDistrictId, out var target) || target.ForceId != force.Id)
                return CommandResult.Reject("Military intelligence transfer selection is invalid.", "district-intelligence.transfer_selection");
            return transfers?.Transfer(state, force, transfer, context) ??
                CommandResult.RuleUnavailable("district-intelligence.transfer_rules", "Military intelligence transfer rules are unavailable.");
        }

        (int districtId, int slot) = order switch
        {
            RemoveDistrictIntelligenceAssignmentOrder remove => (remove.DistrictId, remove.Slot),
            SetDistrictIntelligenceAssignmentOrder set => (set.DistrictId, set.Slot),
            _ => throw new InvalidOperationException("The district intelligence order changed during dispatch.")
        };
        if (!state.Districts.TryGetValue(districtId, out var district) || district.ForceId != force.Id ||
            context.DelegatedDistrictId is int delegated && delegated != districtId)
            return CommandResult.Reject("The intelligence slot is outside the authorized district.", "district-intelligence.district_scope");
        if (slot is < 0 or > 2 || order is SetDistrictIntelligenceAssignmentOrder selection &&
            (selection.TargetForceId is < 1 or > 51 || selection.OfficerId is < 1 or > 6000))
            return CommandResult.Reject("The intelligence slot selection is invalid.", "district-intelligence.slot_selection");
        if (slots is null)
            return CommandResult.RuleUnavailable("district-intelligence.slot_rules", "District intelligence assignment rules are unavailable.");
        return order switch
        {
            RemoveDistrictIntelligenceAssignmentOrder remove => slots.RemoveDistrictIntelligenceAssignment(state, force, remove, context),
            SetDistrictIntelligenceAssignmentOrder set => slots.SetDistrictIntelligenceAssignment(state, force, set, context),
            _ => throw new InvalidOperationException("The district intelligence order changed during dispatch.")
        };
    }
}
