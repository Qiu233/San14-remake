using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

public enum OriginalAiStrategyInvocationKind { ComputerForce, PlayerDelegation, SpecialForce }
public readonly record struct OriginalAiStrategyInvocation(OriginalAiStrategyInvocationKind Kind, int ForceId);

/// <summary>Optional ICommandPlanner consumer of the actual Strategy caller
/// queue. The queue source must capture the real admitted invocation sequence;
/// this adapter never derives a global order from World.Forces or tree scopes.</summary>
public sealed class OriginalAiStrategyQueuedPlanner : ICommandPlanner
{
    private readonly OriginalAiStrategyScheduler scheduler;
    private readonly ArmyCampaignRuleAdapter adapter;
    private readonly OriginalAiPolicySource source;
    private readonly Func<GameState, ImmutableArray<OriginalAiStrategyInvocation>>? readInvocations;
    private readonly OriginalAiStrategyQueueReader? readBatch;
    private readonly OriginalAiStrategyQueueStage queueStage;
    private readonly OriginalAiPolicyCaptureRules captureRules = new();

    public static OriginalAiStrategyQueuedPlanner CreateForRules(LoadedOriginalAiPolicy policy,
        OriginalAiStrategyRoots roots, OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules,
        Func<GameState, ImmutableArray<OriginalAiStrategyInvocation>> readInvocations)
        => new(policy, roots, configuration, rules,
            rules.OrdinaryDeploymentRules as ArmyCampaignRuleAdapter
                ?? throw Missing("current_rules_army_adapter"), readInvocations);

    public OriginalAiStrategyQueuedPlanner(LoadedOriginalAiPolicy policy, OriginalAiStrategyRoots roots,
        OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules, ArmyCampaignRuleAdapter adapter,
        Func<GameState, ImmutableArray<OriginalAiStrategyInvocation>> readInvocations)
    {
        ArgumentNullException.ThrowIfNull(readInvocations);
        this.adapter = adapter; this.readInvocations = readInvocations;
        // These normal-loader hashes are already known. No actor operation
        // rereads resources or computes a container fingerprint.
        source = new(policy.Behavior.Source.ResourceUri, policy.Behavior.Source.Sha256,
            policy.Scripts.Source.ResourceUri, policy.Scripts.Source.Sha256);
        // The scheduler resolves all four real roots once and refuses any
        // unbound region before this planner can be installed.
        scheduler = new(policy.Program, roots, configuration, rules, adapter);
    }

    public static OriginalAiStrategyQueuedPlanner CreateBeforePlayerForRules(LoadedOriginalAiPolicy policy,
        OriginalAiStrategyRoots roots, OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules)
        => CreateScheduledForRules(policy, roots, configuration, rules, OriginalAiStrategyQueueStage.BeforePlayer);

    public static OriginalAiStrategyQueuedPlanner CreateAfterPlayerForRules(LoadedOriginalAiPolicy policy,
        OriginalAiStrategyRoots roots, OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules)
        => CreateScheduledForRules(policy, roots, configuration, rules, OriginalAiStrategyQueueStage.AfterPlayer);

    private static OriginalAiStrategyQueuedPlanner CreateScheduledForRules(LoadedOriginalAiPolicy policy,
        OriginalAiStrategyRoots roots, OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules,
        OriginalAiStrategyQueueStage stage)
    {
        var adapter = rules.OrdinaryDeploymentRules as ArmyCampaignRuleAdapter ?? throw Missing("current_rules_army_adapter");
        return new(policy, roots, configuration, rules, adapter, adapter.ReadOriginalAiStrategyQueueBatchCurrent, stage);
    }

    public OriginalAiStrategyQueuedPlanner(LoadedOriginalAiPolicy policy, OriginalAiStrategyRoots roots,
        OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules, ArmyCampaignRuleAdapter adapter,
        OriginalAiStrategyQueueReader readBatch, OriginalAiStrategyQueueStage stage)
        : this(policy, roots, configuration, rules, adapter, static _ => [])
    {
        ArgumentNullException.ThrowIfNull(readBatch);
        if (stage is not (OriginalAiStrategyQueueStage.BeforePlayer or OriginalAiStrategyQueueStage.AfterPlayer))
            throw new ArgumentOutOfRangeException(nameof(stage));
        this.readBatch = readBatch; queueStage = stage; readInvocations = null;
    }

    public void Plan(CommandPlanningContext planning)
    {
        if (planning.Current.NativePlayableTerminal is not null || planning.Current.CampaignTurnStop is not null) return;
        if (planning.Current.Phase != TurnPhase.Strategy || planning.Current.RulesetId != PlayableSkeleton.RulesetId)
            throw new InvalidOperationException("Original policy requires its ordinary Strategy candidate.");
        var installed = planning.Current.OriginalAiPolicyExecution ?? throw Missing("installed_owner");
        if (installed.Source != source) throw Missing("installed_source_identity");
        OriginalAiStrategySchedule? expectedSchedule = null, capturedSchedule = null;
        if (readBatch is not null)
        {
            expectedSchedule = planning.Current.OriginalAiStrategySchedule ?? throw Missing("installed_strategy_schedule");
            // Only the actual completed User entry may dispatch440120. A
            // repeated after-player call is not a fresh before-player pass.
            if (queueStage == OriginalAiStrategyQueueStage.AfterPlayer && !expectedSchedule.AwaitingPlayerCompletion) return;
        }
        var groups = planning.Current.ReadArmyLifecycle()?.ArmyGroups ?? throw Missing("military_group_owner");
        var contexts = new OriginalAiStrategyContextBank(installed, groups.OriginalAiContexts);
        var invoked = new List<OriginalAiStrategyInvocation>();
        GameState? capturedWorld = null;
        SourceWorldContext? capturedCurrent = null;
        SourceWorldContext Current()
        {
            if (!ReferenceEquals(capturedWorld, planning.Current))
            {
                capturedCurrent = adapter.CaptureOriginalAiDomesticCurrent(planning.Current);
                capturedWorld = planning.Current;
            }
            return capturedCurrent!;
        }
        void Invoke(OriginalAiStrategyInvocation invocation)
        {
            switch (invocation.Kind)
            {
                case OriginalAiStrategyInvocationKind.ComputerForce:
                    scheduler.RunComputerForce(planning, contexts, invocation.ForceId); break;
                case OriginalAiStrategyInvocationKind.PlayerDelegation:
                    if (adapter.ReadOriginalAiStrategyPlayerForceCurrent(planning.Current, Current()) != invocation.ForceId)
                        throw Missing("invocation_player_Force");
                    scheduler.RunPlayerDelegation(planning, contexts); break;
                case OriginalAiStrategyInvocationKind.SpecialForce:
                    OriginalAiSpecialForceStrategy.Dispatch(planning, adapter, invocation.ForceId);
                    break;
                default: throw Missing("invocation_kind");
            }
            invoked.Add(invocation);
        }

        if (readBatch is null)
        {
            // The existing explicit invocation callback remains compatible.
            // It does not construct, move or reconstruct a saved schedule.
            var invocations = readInvocations!(planning.Current);
            if (invocations.IsDefault) throw Missing("actual_invocation_queue");
            if (invocations.IsEmpty) return;
            foreach (var invocation in invocations) Invoke(invocation);
        }
        else
        {
            var cursor = expectedSchedule!;
            var stage = queueStage;
            while (planning.Current.NativePlayableTerminal is null && planning.Current.CampaignTurnStop is null)
            {
                var batch = readBatch(planning.Current, Current(), cursor, stage);
                if (batch.Invocations.IsDefault || batch.Invocations.Length > 1) throw Missing("single_source_row_batch");
                if (batch.Invocations.IsEmpty && batch.NextSchedule == cursor && !batch.Stop)
                    throw Missing("source_cursor_progress");
                foreach (var invocation in batch.Invocations) Invoke(invocation);
                // Move only after this actual row completed. There is no
                // speculative cursor for rows beyond a terminal transition.
                cursor = batch.NextSchedule;
                stage = OriginalAiStrategyQueueStage.BeforePlayer;
                if (batch.Stop) break;
            }
            capturedSchedule = cursor;
            if (invoked.Count == 0 && capturedSchedule == expectedSchedule) return;
        }

        var actual = planning.Current.OriginalAiPolicyExecution ?? throw Missing("completed_owner");
        var actualGroups = planning.Current.ReadArmyLifecycle()?.ArmyGroups ?? throw Missing("completed_military_group_owner");
        var captured = contexts.Capture(actual);
        var capturedGroups = contexts.CaptureMilitaryGroups(actualGroups.OriginalAiContexts);
        var actualCautions = planning.Current.ReadArmyLifecycle()?.ArmyCautions ?? throw Missing("completed_caution_owner");
        var capturedCautionPlanning = contexts.CautionGroups.Capture(actualCautions.GroupPlanning);
        int? commitForce = null;
        for (int index = invoked.Count - 1; index >= 0; index--)
            if (planning.Current.Forces.TryGetValue(invoked[index].ForceId, out var force) && force.IsActive)
            { commitForce = force.Id; break; }
        // Actual skips/player barriers may move a cursor without an AI row.
        // Use the current active player authority rather than fabricateForce0.
        if (commitForce is null && capturedSchedule is not null)
        {
            int player = adapter.ReadOriginalAiStrategyPlayerForceCurrent(planning.Current, Current());
            if (planning.Current.Forces.TryGetValue(player, out var force) && force.IsActive) commitForce = force.Id;
        }
        if (commitForce is not int owner) throw Missing("capture_invocation_Force");
        string actorId = "original-ai-policy-capture:" + owner;
        var result = planning.Apply(new OriginalAiPolicyCaptureOrder(owner, captured, capturedGroups, capturedCautionPlanning)
        {
            ArmyEvaluations = contexts.Evaluations.CaptureArmyReceiverChanges(),
            Schedule = capturedSchedule, ExpectedSchedule = expectedSchedule
        }, captureRules, new(actorId), new SingleForceAuthority(owner, actorId));
        if (!result.Accepted) throw new InvalidDataException(result.MissingRuleKey ?? result.ErrorCode ?? result.Error);
    }

    private static InvalidDataException Missing(string field) => new("original_ai.queued_planner." + field);
}

/// <summary>Only typed execution snapshots cross this command boundary. No
/// caller World, borrowed query, delegate or executable resource is accepted.</summary>
internal sealed record OriginalAiPolicyCaptureOrder(int ForceId, OriginalAiPolicyExecutionState Execution,
    ImmutableDictionary<int, OriginalAiPolicyActorState> MilitaryGroups,
    ImmutableDictionary<int, ArmyCautionGroupPlanningState>? CautionPlanning) : GameOrder(ForceId)
{
    public ImmutableArray<OriginalAiArmyEvaluationChange> ArmyEvaluations { get; init; } = [];
    public OriginalAiStrategySchedule? Schedule { get; init; }
    public OriginalAiStrategySchedule? ExpectedSchedule { get; init; }
}

internal sealed class OriginalAiPolicyCaptureRules : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not OriginalAiPolicyCaptureOrder capture || capture.ForceId != force.Id ||
            state.Phase != TurnPhase.Strategy || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Policy capture requires its actual Force Strategy owner.", "original-ai.capture.scope");
        var installed = state.OriginalAiPolicyExecution;
        if (installed is null || installed.Source != capture.Execution.Source)
            return CommandResult.RuleUnavailable("original_ai.capture.installed_source", "Installed policy owner is unknown or changed.");
        if (capture.Schedule is not null && !ReferenceEquals(state.OriginalAiStrategySchedule, capture.ExpectedSchedule))
            return CommandResult.RuleUnavailable("original_ai.capture.strategy_schedule_owner", "Actual Strategy cursor was replaced.");
        var lifecycle = state.ReadArmyLifecycle();
        if (lifecycle is null || lifecycle.ArmyGroups is not { OriginalAiContexts: not null } groups)
            return CommandResult.RuleUnavailable("original_ai.capture.group_owner", "Registered military-group context owner is unknown.");
        if (lifecycle.ArmyCautions is not { } cautions)
            return CommandResult.RuleUnavailable("original_ai.capture.caution_owner", "Caution-group owner is unknown.");
        var next = state.WithArmyLifecycle(lifecycle with
        { ArmyGroups = groups with { OriginalAiContexts = capture.MilitaryGroups },
          ArmyCautions = cautions with { GroupPlanning = capture.CautionPlanning } }) with
        { OriginalAiPolicyExecution = capture.Execution,
          OriginalAiStrategySchedule = capture.Schedule ?? state.OriginalAiStrategySchedule };
        return CommandResult.Accept(ArmyAiEvaluationSource.Apply(next, capture.ArmyEvaluations));
    }
}
