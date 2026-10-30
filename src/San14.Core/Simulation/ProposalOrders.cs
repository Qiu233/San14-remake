namespace San14.Core;

/// <summary>Runs the real proposal regeneration boundary, not a list refresh.</summary>
public sealed record GenerateProposalsOrder(int ForceId) : GameOrder(ForceId);
public sealed record AdoptProposalOrder(int ForceId, int SlotId) : GameOrder(ForceId);
/// <summary>One confirmation, preserving the source selection order and its
/// shared command expense. It is not a sequence of separately issued orders.</summary>
public sealed record AdoptProposalBatchOrder(int ForceId, IReadOnlyList<int> SlotIds) : GameOrder(ForceId);

public interface IProposalExecution
{
    CommandResult GenerateProposals(GameState state, GenerateProposalsOrder order,
        CommandContext context);
    CommandResult AdoptProposal(GameState state, AdoptProposalOrder order,
        CommandContext context);
    CommandResult AdoptProposalBatch(GameState state, AdoptProposalBatchOrder order,
        CommandContext context);

    /// <summary>Called at the reached strategy transition. Each accepted pending
    /// offer is rechecked and executed or failed by its actual handler.</summary>
    GameState ExecutePendingProposals(GameState state, int forceId);
}

/// <summary>Uses the same command authority and owned transaction as other
/// campaign orders. Handler validity, resource cost and execution remain in
/// the supplied campaign rules.</summary>
public sealed class ProposalOrders(IOrderRules next, IProposalExecution? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context)
    {
        if (order is not (GenerateProposalsOrder or AdoptProposalOrder or AdoptProposalBatchOrder))
            return next.Apply(state, force, order, context);
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Proposals require direct control of the current force.",
                "proposal.direct_control");
        if (rules is null)
            return CommandResult.RuleUnavailable("proposal.execution",
                "The campaign proposal rules are unavailable.");
        if (order is GenerateProposalsOrder generate)
            return rules.GenerateProposals(state, generate, context);
        if (state.Proposals is not { } board)
            return CommandResult.RuleUnavailable("proposal.current_board",
                "The current proposal board is unresolved.");
        IReadOnlyList<int>? slots = order is AdoptProposalBatchOrder batch
            ? batch.SlotIds : [((AdoptProposalOrder)order).SlotId];
        if (slots is null || slots.Count == 0 || slots.Count >= ProposalBoard.SlotCount)
            return CommandResult.Reject("No usable proposal selection was supplied.", "proposal.selection");
        uint selected = 0;
        foreach (int slot in slots)
        {
            if (slot is <= 0 or >= ProposalBoard.SlotCount || (selected & (1u << slot)) != 0)
                return CommandResult.Reject("This proposal slot cannot be adopted.", "proposal.slot");
            selected |= 1u << slot;
            ProposalEntry entry = board.Read(slot);
            if (entry.Kind == ProposalKind.None || entry.Status != ProposalStatus.Offered)
                return CommandResult.Reject("This proposal is not awaiting adoption.", "proposal.not_offered");
        }
        return order is AdoptProposalBatchOrder group
            ? rules.AdoptProposalBatch(state, group, context)
            : rules.AdoptProposal(state, (AdoptProposalOrder)order, context);
    }
}
