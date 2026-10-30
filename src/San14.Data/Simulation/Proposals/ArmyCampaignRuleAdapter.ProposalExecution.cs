using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IProposalExecution
{
    CommandResult IProposalExecution.GenerateProposals(GameState state, GenerateProposalsOrder order, CommandContext context)
    {
        try
        {
            var rules = CreateProposalLifecycle(state, order.ForceId);
            if (rules.PlayerForce.PhysicalSlot != order.ForceId)
                return CommandResult.Reject("Only the current player's force can regenerate proposals.", "proposal.player_force");
            rules.Generate();
            return CommandResult.Accept(rules.State);
        }
        catch (InvalidDataException missing)
        { return CommandResult.RuleUnavailable(missing.Message, "The current proposal generation inputs are unavailable."); }
    }

    CommandResult IProposalExecution.AdoptProposal(GameState state, AdoptProposalOrder order, CommandContext context)
        => ApplyProposalAdoption(state, order.ForceId, [order.SlotId]);

    CommandResult IProposalExecution.AdoptProposalBatch(GameState state, AdoptProposalBatchOrder order, CommandContext context)
        => ApplyProposalAdoption(state, order.ForceId, order.SlotIds);

    private CommandResult ApplyProposalAdoption(GameState state, int forceId, IReadOnlyList<int> slots)
    {
        try
        {
            var rules = CreateProposalLifecycle(state, forceId);
            if (rules.PlayerForce.PhysicalSlot != forceId)
                return CommandResult.Reject("Only the current player's force can adopt proposals.", "proposal.player_force");
            rules.Adopt(slots);
            return CommandResult.Accept(rules.State);
        }
        catch (InvalidDataException invalid) when (invalid.Message is "proposal.selection" or "proposal.slot" or
            "proposal.not_adoptable" or "proposal.insufficient_resources")
        { return CommandResult.Reject("The proposal selection is no longer available or affordable.", invalid.Message); }
        catch (InvalidDataException missing)
        { return CommandResult.RuleUnavailable(missing.Message, "The selected proposal's current rules are unavailable."); }
    }

    GameState IProposalExecution.ExecutePendingProposals(GameState state, int forceId)
    {
        if (state.Proposals is null) throw ProposalLifecycleSource.Missing("board_owner");
        if (!state.Proposals.Slots.Any(entry => entry.Status == ProposalStatus.Pending)) return state;
        var rules = CreateProposalLifecycle(state, forceId);
        if (rules.PlayerForce.PhysicalSlot != forceId) return state;
        rules.ExecutePending();
        return rules.State;
    }

    internal ProposalLifecycleSource CreateProposalLifecycle(GameState state, int forceId)
        => new(state, forceId, this, _repository, _scenario, _map, Seed.Language,
            _baseCapacityInputs, _normalAftermathInputs, CaptureProposalCurrent);

    private SourceWorldContext CaptureProposalCurrent(GameState state)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        if (!_baseCapacityInputs.BindCurrent(state, current))
            throw ProposalLifecycleSource.Missing("capacity_owner");
        return current;
    }
}
