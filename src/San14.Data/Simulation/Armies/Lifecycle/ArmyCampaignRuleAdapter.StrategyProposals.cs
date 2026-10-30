using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableStrategyEntryRules, IPlayableStrategyExitRules
{
    /// <summary>44B8E0's proposal boundary, separate from the initial event
    /// history writers. Restore entry carries the actual load gate set by the
    /// load callback, so it retains saved offers and consumes no App draws.</summary>
    public GameState EnterStrategy(GameState state, CampaignEntryKind entryKind)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(entryKind)) throw new ArgumentOutOfRangeException(nameof(entryKind));
        if (state.Phase != TurnPhase.Strategy) throw new InvalidDataException("proposal.strategy_entry_phase");
        //44B070 maintains claims before44B8E0's independent proposal load gate.
        state = RefreshAreaOccupationClaimsCurrent(state, CaptureCurrentCampaignEventQueries(state));
        if (entryKind == CampaignEntryKind.RestoreCampaign)
            return originalAiStrategyInstallation?.RequireRestoredOwner(state) ?? state;
        if (originalAiStrategyInstallation is { } policy) state = policy.EnterNewStrategy(state);
        return GenerateStrategyProposalsCurrent(state);
    }

    /// <summary>429F30 cleared the load gate when this real Progress began.
    /// The next completed report therefore enters44B8E0's regeneration branch.
    /// Budget refreshes and UI refreshes do not call this boundary.</summary>
    public GameState EnterStrategyAfterReport(GameState state)
    {
        if (state.Phase != TurnPhase.Strategy) throw new InvalidDataException("proposal.report_strategy_entry_phase");
        var application = applicationGameplay ?? throw new InvalidDataException("proposal.strategy_application_lifecycle");
        //A project save may resume while Reporting. The actual load callback
        //set the gate, and no new Progress initializer has cleared it yet.
        state = RefreshAreaOccupationClaimsCurrent(state, CaptureCurrentCampaignEventQueries(state));
        // This is a new Strategy parent even when a saved Reporting phase
        // resumes. Its actual2220 load gate also selects the starting player
        // cursor; the saved queue is not reconstructed on Strategy restore.
        state = AppendInitialStrategyChronicleCurrent(state);
        if (originalAiStrategyInstallation is { } policy) state = policy.EnterNewStrategy(state);
        if (application.StrategyLoadResume) return state;
        return GenerateStrategyProposalsCurrent(state);
    }

    /// <summary>The strategy state executes41DAB0 before leaving its stage6
    /// for stage3. The normal period calls this once before deployment and
    /// waiting preparation; resuming that period never repeats it.</summary>
    public GameState ExitStrategyForProgress(GameState state)
    {
        if (state.Phase != TurnPhase.Strategy) throw new InvalidDataException("proposal.strategy_exit_phase");
        var application = applicationGameplay ?? throw new InvalidDataException("proposal.strategy_application_lifecycle");
        if (state.Proposals is not { } board) throw new InvalidDataException("proposal.board_owner");
        var result = board.Slots.Any(entry => entry.Status == ProposalStatus.Pending)
            ? ((IProposalExecution)this).ExecutePendingProposals(state, StrategyProposalPlayer(state)) : state;
        //41DAB0 precedes the reached Progress initializer429F30. A missing
        //proposal dependency must not pretend that the latter was entered.
        // 41DAB0 completes before the actual440120 worker. The ordinary
        // BeginProgress planner seam runs earlier and must not duplicate this.
        if (originalAiStrategyInstallation is { } policy) result = policy.ExitPlayerStrategy(result);
        application.SetStrategyLoadResume(false);
        return result;
    }

    private GameState GenerateStrategyProposalsCurrent(GameState state)
    {
        var proposals = CreateProposalLifecycle(state, StrategyProposalPlayer(state));
        proposals.Generate();
        return proposals.State;
    }

    private static int StrategyProposalPlayer(GameState state) =>
        OwnedCurrentPlayerForce.ReadRawForce3A(state) is byte player ? player
        : throw new InvalidDataException("proposal.strategy_current_player_Force");
}
