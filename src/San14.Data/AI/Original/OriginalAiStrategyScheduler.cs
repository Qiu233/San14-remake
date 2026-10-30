using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>Actual prototype resource identities, supplied by the normal
/// loaded policy. Layer numbers are not substituted for bundle data keys.</summary>
public sealed record OriginalAiStrategyRoots(uint Force, uint District, uint MilitaryGroup, uint Army);

/// <summary>The source manager enable and its existing per-Force override
/// map. A known map without an entry admits that Force; an unknown manager is
/// not constructed from an older save by this configuration.</summary>
public sealed record OriginalAiStrategyHostConfiguration(bool Enabled, ImmutableDictionary<int, bool> ForceOverrides)
{
    public bool IsForceEnabled(int force) => !ForceOverrides.TryGetValue(force, out bool enabled) || enabled;
}

/// <summary>Source Strategy entry workers. The caller supplies the real
/// current Force owner and a world-owned registered context bank. This does
/// not flatten all layers into global scope passes or run in a render update.</summary>
public sealed class OriginalAiStrategyScheduler
{
    private readonly ImmutableDictionary<uint, OriginalAiCompiledRegion> regions;
    private readonly OriginalAiStrategyRoots roots;
    private readonly OriginalAiStrategyHostConfiguration configuration;
    private readonly PlayableRuleSet rules;
    private readonly ArmyCampaignRuleAdapter adapter;

    public OriginalAiStrategyScheduler(OriginalAiBehaviorProgram program, OriginalAiStrategyRoots roots,
        OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules, ArmyCampaignRuleAdapter adapter)
    {
        this.roots = roots; this.configuration = configuration;
        this.rules = rules; this.adapter = adapter;
        ArgumentNullException.ThrowIfNull(configuration.ForceOverrides);
        regions = new[] { roots.Force, roots.District, roots.MilitaryGroup, roots.Army }.Distinct()
            .ToImmutableDictionary(key => key, key => program.Compile(key, 0));
        var unresolved = regions.Values.Where(region => !region.IsBound).SelectMany(region => region.Unresolved).ToArray();
        if (unresolved.Length != 0)
            throw new InvalidDataException(string.Join("; ", unresolved.Select(item =>
                $"original_ai.bundle.{item.BundleKey:X8}.tree.{item.TreeKey:X8}.node.{item.NodeIndex}.type.{item.TypeId:X8}: {item.Requirement}")));
    }

    public void RunComputerForce(CommandPlanningContext planning, OriginalAiStrategyContextBank contexts, int force)
    {
        RequireStrategy(planning);
        // The normal COtherStrategy entry advances past these five source
        // special Force receivers before queueing the worker.
        if (force is >= 46 and <= 50) return;
        var host = new Host(this, planning, contexts);
        if (configuration.Enabled)
        {
            var actor = new OriginalAiActor(force, null, OriginalAiAgentKind.Force);
            var forceOperation = contexts.Open(actor, roots.Force, planning, rules, adapter);
            forceOperation?.PrepareForceStrategyBlackboard();
            var current = host.Current();
            bool player = adapter.ReadOriginalAiStrategyPlayerForceCurrent(planning.Current, current) == force;
            if (configuration.IsForceEnabled(force) && (!player || adapter.ReadOriginalAiStrategyWholeControlCurrent(current)))
            {
                host.Step(actor, OriginalAiStrategyHostStage.ForcePrepare);
                // B5660 retains its initial Running status when the registered
                // map has a known absent context, but still runs both writers.
                var result = forceOperation is not null
                    ? RunRoot(forceOperation, roots.Force, contexts, maximumUpdates: null) : OriginalAiNodeStatus.Running;
                host.Step(actor, OriginalAiStrategyHostStage.ForceComplete);
                if (result != OriginalAiNodeStatus.Running) contexts.ResetNodes(OriginalAiStrategyContextBank.Identity(actor));
            }
            if (forceOperation is not null) contexts.Complete(forceOperation, clearBlackboard: false);

            // Force actions may have changed the District roster. Enter this
            // real nested traversal only after the Force root and its tail.
            foreach (int district in adapter.CaptureOriginalAiStrategyDistrictsCurrent(host.Current(), force))
                RunDistrict(host, force, district, playerDelegation: false);
            contexts.ClearBlackboard(OriginalAiStrategyContextBank.Identity(actor));
        }
        if (adapter.ReadOriginalAiStrategyPlayerForceCurrent(planning.Current, host.Current()) != force)
            host.Step(new(force, null, OriginalAiAgentKind.Force), OriginalAiStrategyHostStage.ComputerForceComplete);
    }

    public void RunPlayerDelegation(CommandPlanningContext planning, OriginalAiStrategyContextBank contexts)
    {
        RequireStrategy(planning);
        if (!configuration.Enabled) return;
        var host = new Host(this, planning, contexts);
        int force = adapter.ReadOriginalAiStrategyPlayerForceCurrent(planning.Current, host.Current());
        if (adapter.CaptureOriginalAiStrategyDistrictsCurrent(host.Current(), force).Count == 0) return;
        var player = new OriginalAiActor(force, null, OriginalAiAgentKind.Force);
        // The first complete pass repairs group leadership/objectives before
        // any delegated District tree. Reset only contexts that really changed.
        foreach (var reset in host.Step(player, OriginalAiStrategyHostStage.PlayerGroupMaintenance)) contexts.ResetNodes(reset);
        foreach (int district in adapter.CaptureOriginalAiStrategyDistrictsCurrent(host.Current(), force))
        {
            int primary = adapter.ReadOriginalAiStrategyPrimaryDistrictCurrent(host.Current(), force);
            if (district != primary) RunDistrict(host, force, district, playerDelegation: true);
        }
    }

    private void RunDistrict(Host host, int force, int district, bool playerDelegation)
    {
        var actor = new OriginalAiActor(force, district, OriginalAiAgentKind.District);
        var operation = host.Contexts.Open(actor, roots.District, host.Planning, rules, adapter);
        operation?.PrepareDistrictStrategyBlackboard();
        var current = host.Current();
        bool enabled = configuration.IsForceEnabled(force) &&
            !adapter.IsOriginalAiStrategyPrimaryDistrictBlockedCurrent(host.Planning.Current, current, district);
        if (enabled && (operation is null || operation.PopulateDistrictStrategyCautionGroups()))
        {
            var result = operation is not null
                ? RunRoot(operation, roots.District, host.Contexts, maximumUpdates: null) : OriginalAiNodeStatus.Running;
            // 2338E0 is an actual pure current-player Force predicate. Its
            // ignored result is not an invented cleanup or list mutation.
            _ = adapter.ReadOriginalAiStrategyPlayerForceCurrent(host.Planning.Current, host.Current()) == force;
            // Native reset does not clear the blackboard list used by the tail.
            if (result != OriginalAiNodeStatus.Running) host.Contexts.ResetNodes(OriginalAiStrategyContextBank.Identity(actor));
            if (operation?.CaptureDistrictStrategyCautionGroups() is { } retained)
                host.Step(actor, OriginalAiStrategyHostStage.DistrictComplete, retained);
        }
        if (operation is not null) host.Contexts.Complete(operation, clearBlackboard: false);

        // This is the fixed group table filtered at the moment each source
        // slot is visited. A new later group is not lost to a stale roster.
        for (int slot = 0; slot < 501; slot++)
        {
            if (adapter.ReadOriginalAiStrategyGroupAtCurrent(host.Current(), district, slot) is { } group)
                RunGroup(host, force, group, playerDelegation);
        }
        host.Contexts.ClearBlackboard(OriginalAiStrategyContextBank.Identity(actor));
    }

    private void RunGroup(Host host, int force, OriginalAiTroopsReference group, bool playerDelegation)
    {
        var current = host.Current();
        // This source probe's return is deliberately ignored by the caller.
        _ = adapter.ReadOriginalAiStrategyHasActiveMemberCurrent(host.Planning.Current, current, group);
        if (adapter.ShouldSkipOriginalAiStrategyGroupCurrent(host.Planning.Current, current, force, group, playerDelegation)) return;

        int actorDistrict = adapter.ReadOriginalAiTroopsDistrictCurrent(current, group);
        int actorForce = adapter.ReadOriginalAiTroopsForceCurrent(current, group);
        var actor = new OriginalAiActor(actorForce, actorDistrict, OriginalAiAgentKind.Troops) { Troops = group };
        bool enabled = configuration.IsForceEnabled(actorForce) &&
            !adapter.IsOriginalAiStrategyPrimaryDistrictBlockedCurrent(host.Planning.Current, current, actorDistrict);
        if (enabled)
        {
            var registrations = host.Planning.Current.ReadArmyLifecycle()?.ArmyGroups?.OriginalAiContexts
                ?? throw Missing("military_group_context_owner");
            host.Contexts.RefreshMilitaryGroupRegistration(group.GroupId, registrations.GetValueOrDefault(group.GroupId));
            var operation = host.Contexts.Open(actor, roots.MilitaryGroup, host.Planning, rules, adapter);
            OriginalAiNodeStatus? result = null;
            if (operation is not null)
            {
                host.Step(actor, OriginalAiStrategyHostStage.GroupPrepare);
                result = RunRoot(operation, roots.MilitaryGroup, host.Contexts, maximumUpdates: 10);
            }
            // The actual group post-business also runs with a known absent
            // registered context. Absence is not reported as a successful tree.
            host.Step(actor, OriginalAiStrategyHostStage.GroupComplete);
            if (operation is not null)
            {
                if (result != OriginalAiNodeStatus.Running) host.Contexts.ResetNodes(OriginalAiStrategyContextBank.Identity(actor));
                host.Contexts.Complete(operation, clearBlackboard: false);
            }
        }

        // The source captures a list for whole-group skip checks, then starts
        // a NEW member traversal after group business. Never use the old list
        // to invoke member trees after group commands have changed assignments.
        var members = adapter.ReadOriginalAiTroopsMembersCurrent(host.Current(), group)
            .Select(member => ArmyDestructionQueue.ResolveUnitId(host.Planning.Current, member.PhysicalSlot)
                ?? throw Missing("member_domain_identity")).ToArray();
        foreach (int unitId in members)
        {
            current = host.Current();
            int? physical = WorldIndexContext.FindArmyPhysical(host.Planning.Current, unitId);
            if (physical is not int receiver) continue; // Actually retired by an earlier member's command.
            var actualGroup = current.ReadOriginalAiArmyTroopsCurrent(new(receiver));
            if (actualGroup != group) continue;
            RunArmy(host, force, unitId, receiver);
        }
    }

    private void RunArmy(Host host, int force, int unitId, int physical)
    {
        var current = host.Current();
        var receiver = new OriginalAiActor(force, null, OriginalAiAgentKind.Army)
        { ArmyId = unitId, ArmyPhysicalSlot = physical };
        int actorForce = (adapter.ReadOriginalAiMyselfForce(current, receiver) ?? throw Missing("Army_Force_pointer")).Id;
        int actorDistrict = (adapter.ReadOriginalAiMyselfDistrict(current, receiver) ?? throw Missing("Army_District_pointer")).Id;
        var actor = receiver with { ForceId = actorForce, DistrictId = actorDistrict };
        if (current.ReadArmyReturnUnitStatus10(new(physical)) == 2)
            host.Step(actor, OriginalAiStrategyHostStage.TransportArmyPrepare);
        current = host.Current();
        if (!adapter.IsOriginalAiStrategyArmyEligibleCurrent(host.Planning.Current, current, physical)) return;

        var identity = OriginalAiStrategyContextBank.Identity(actor);
        // 43F9D0/440120 lazily register an absent Army context immediately
        // before its actual first invocation; other layers are not rebuilt here.
        host.Contexts.Register(identity, roots.Army);
        var operation = host.Contexts.Open(actor, roots.Army, host.Planning, rules, adapter)
            ?? throw Missing("registered_Army_context");
        operation.PrepareArmyStrategyBlackboard();
        int groupForce = adapter.ReadOriginalAiTroopsForceCurrent(current, current.ReadOriginalAiArmyTroopsCurrent(new(physical)));
        if (configuration.IsForceEnabled(groupForce) &&
            !adapter.IsOriginalAiStrategyPrimaryDistrictBlockedCurrent(host.Planning.Current, current, actorDistrict))
        {
            host.Step(actor, OriginalAiStrategyHostStage.ArmyPrepare);
            current = host.Current();
            if (adapter.ShouldRunOriginalAiArmyStrategyTreeCurrent(host.Planning.Current, current, unitId, physical))
            {
                operation.BeginStrategyTreeUpdate();
                var result = regions[roots.Army].Invoke(operation);
                if (result != OriginalAiNodeStatus.Running) host.Contexts.ResetNodes(identity);
            }
        }
        host.Contexts.Complete(operation, clearBlackboard: true);
        // The actual outer target/changed-list tail follows the wrapper even
        // when the primary-District gate did not execute the Army tree.
        host.Step(actor, OriginalAiStrategyHostStage.ArmyComplete);
    }

    private OriginalAiNodeStatus RunRoot(OriginalAiOperation operation, uint bundle,
        OriginalAiStrategyContextBank contexts, int? maximumUpdates)
    {
        var region = regions[bundle];
        int updates = 0;
        OriginalAiNodeStatus result;
        do
        {
            operation.BeginStrategyTreeUpdate();
            result = region.Invoke(operation);
            updates++;
            if (!operation.StrategyTreeRestartRequested) return result;
            // Reset occurs even after the tenth group update; the cap prevents
            // only the eleventh invocation, not that final actual reset.
            contexts.ResetNodes(OriginalAiStrategyContextBank.Identity(operation.Actor));
        } while (maximumUpdates is not int maximum || updates < maximum);
        return result;
    }

    private static void RequireStrategy(CommandPlanningContext planning)
    {
        if (planning.Current.Phase != TurnPhase.Strategy || planning.Current.RulesetId != PlayableSkeleton.RulesetId)
            throw new InvalidOperationException("Original AI host requires its ordinary Strategy candidate transaction.");
    }

    private sealed class Host(OriginalAiStrategyScheduler scheduler, CommandPlanningContext planning,
        OriginalAiStrategyContextBank contexts)
    {
        internal readonly CommandPlanningContext Planning = planning;
        internal readonly OriginalAiStrategyContextBank Contexts = contexts;
        private readonly OriginalAiStrategyHostRules stepRules = new(scheduler.adapter);
        private GameState? capturedWorld;
        private SourceWorldContext? capturedCurrent;
        internal SourceWorldContext Current()
        {
            if (!ReferenceEquals(capturedWorld, Planning.Current))
            {
                capturedCurrent = scheduler.adapter.CaptureOriginalAiDomesticCurrent(Planning.Current);
                capturedWorld = Planning.Current;
            }
            return capturedCurrent!;
        }

        internal ImmutableArray<OriginalAiPolicyActor> Step(OriginalAiActor actor, OriginalAiStrategyHostStage stage,
            ImmutableArray<int>? retainedCautionGroups = null)
        {
            string name = "original-ai-host:" + actor.ForceId;
            var context = new CommandContext(name, actor.DistrictId);
            var authority = new SingleForceAuthority(actor.ForceId, name, actor.DistrictId is int district ? [district] : []);
            var order = new OriginalAiStrategyHostOrder(actor.ForceId, stage, actor, retainedCautionGroups);
            var result = Planning.Apply(order, stepRules, context, authority);
            if (!result.Accepted) throw new InvalidDataException(result.MissingRuleKey ?? result.ErrorCode ?? result.Error);
            return stepRules.ResetContexts;
        }
    }

    private static InvalidDataException Missing(string field) => new("original_ai.strategy_host." + field);
}

internal enum OriginalAiStrategyHostStage
{
    ForcePrepare, ForceComplete, DistrictComplete, GroupPrepare, GroupComplete,
    TransportArmyPrepare, ArmyPrepare, ArmyComplete, PlayerGroupMaintenance, ComputerForceComplete
}

internal sealed record OriginalAiStrategyHostOrder(int ForceId, OriginalAiStrategyHostStage Stage,
    OriginalAiActor Actor, ImmutableArray<int>? RetainedCautionGroups) : GameOrder(ForceId);

/// <summary>Specific source business steps. Orders carry actual identities
/// and source arguments, never a caller-supplied World or result snapshot.</summary>
internal sealed class OriginalAiStrategyHostRules(ArmyCampaignRuleAdapter adapter) : IOrderRules
{
    internal ImmutableArray<OriginalAiPolicyActor> ResetContexts { get; private set; } = [];

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        ResetContexts = [];
        if (order is not OriginalAiStrategyHostOrder step || step.ForceId != force.Id ||
            step.Actor.ForceId != force.Id || state.Phase != TurnPhase.Strategy)
            return CommandResult.Reject("Source strategy step has an invalid actor.", "original-ai.host.actor");
        try
        {
            var current = adapter.CaptureOriginalAiDomesticCurrent(state);
            var actualForce = adapter.ReadOriginalAiMyselfForce(current, step.Actor);
            if (actualForce is not { } actual || actual.Id != force.Id)
                return CommandResult.Reject("Source strategy actor belongs to another force.", "original-ai.host.force");
            if (context.DelegatedDistrictId is int delegated && step.Actor.DistrictId != delegated)
                return CommandResult.Reject("Source strategy actor is outside its district.", "original-ai.host.district");
            int army = step.Actor.ArmyId ?? 0;
            int physical = step.Actor.ArmyPhysicalSlot ?? 0;
            GameState next;
            switch (step.Stage)
            {
                case OriginalAiStrategyHostStage.ForcePrepare:
                    next = adapter.PrepareOriginalAiForceStrategyCurrent(state, current, force.Id); break;
                case OriginalAiStrategyHostStage.ForceComplete:
                    next = adapter.CompleteOriginalAiForceStrategyCurrent(state, current, force.Id); break;
                case OriginalAiStrategyHostStage.ComputerForceComplete:
                    next = adapter.CompleteOriginalAiComputerStrategyCurrent(state, current, force.Id); break;
                case OriginalAiStrategyHostStage.DistrictComplete:
                    next = adapter.CompleteOriginalAiDistrictStrategyCurrent(state, current,
                        step.Actor.DistrictId ?? throw new InvalidDataException("original_ai.strategy_host.District_identity"),
                        step.RetainedCautionGroups); break;
                case OriginalAiStrategyHostStage.GroupPrepare:
                    next = adapter.PrepareOriginalAiTroopsStrategyCurrent(state, current,
                        step.Actor.Troops ?? throw new InvalidDataException("original_ai.strategy_host.group_identity")); break;
                case OriginalAiStrategyHostStage.GroupComplete:
                    next = adapter.CompleteOriginalAiTroopsStrategyCurrent(state, current,
                        step.Actor.Troops ?? throw new InvalidDataException("original_ai.strategy_host.group_identity")); break;
                case OriginalAiStrategyHostStage.TransportArmyPrepare:
                    next = adapter.PrepareOriginalAiTransportArmyStrategyCurrent(state, current, army, physical,
                        current.ReadOriginalAiArmyTroopsCurrent(new(physical))); break;
                case OriginalAiStrategyHostStage.ArmyPrepare:
                    next = adapter.PrepareOriginalAiArmyStrategyCurrent(state, current, army, physical); break;
                case OriginalAiStrategyHostStage.ArmyComplete:
                    next = adapter.CompleteOriginalAiArmyStrategyCurrent(state, current, army, physical); break;
                case OriginalAiStrategyHostStage.PlayerGroupMaintenance:
                    var result = adapter.MaintainOriginalAiPlayerStrategyGroupsCurrent(state, current, force.Id);
                    next = result.State;
                    ResetContexts = result.ResetGroupIds.Select(id => new OriginalAiPolicyActor(OriginalAiPolicyActorKind.MilitaryGroup, id)).ToImmutableArray();
                    break;
                default: throw new InvalidDataException("original_ai.strategy_host.step");
            }
            return CommandResult.Accept(next);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }
}
