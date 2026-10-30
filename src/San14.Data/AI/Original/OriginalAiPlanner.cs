using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>A resource root and its actual host scope. The composition owner
/// supplies this only when that original scheduler entry has been restored.</summary>
public readonly record struct OriginalAiPlannerEntry(uint BundleKey, WorldEntityKind Scope);

/// <summary>Strict ICommandPlanner adapter for fully bound synchronous roots.
/// Partial trees cannot be installed as the original policy or silently fall
/// back to the provisional AI. Normal command permissions remain authoritative.</summary>
public sealed class OriginalAiPlanner : ICommandPlanner
{
    private readonly ImmutableArray<(OriginalAiPlannerEntry Entry, OriginalAiCompiledRegion Region)> entries;
    private readonly PlayableRuleSet rules;
    private readonly ArmyCampaignRuleAdapter adapter;
    private readonly ImmutableHashSet<int> humanForces;

    /// <summary>Resolve the adapter from this session's freshly prepared rules,
    /// including worker forks. The factory may retain immutable policy data,
    /// but must not retain a previous composition's mutable adapter.</summary>
    public static OriginalAiPlanner CreateForRules(OriginalAiBehaviorProgram program,
        IReadOnlyList<OriginalAiPlannerEntry> entries, PlayableRuleSet rules, int humanForceId) =>
        new(program, entries, rules,
            rules.OrdinaryDeploymentRules as ArmyCampaignRuleAdapter
                ?? throw new InvalidDataException("original_ai.current_rules.army_adapter"),
            new HashSet<int> { humanForceId });

    public OriginalAiPlanner(OriginalAiBehaviorProgram program, IReadOnlyList<OriginalAiPlannerEntry> entries,
        PlayableRuleSet rules, ArmyCampaignRuleAdapter adapter, IReadOnlySet<int> humanForceIds)
    {
        if (entries.Count == 0) throw new ArgumentException("An original policy needs an actual scheduler entry.", nameof(entries));
        this.rules = rules; this.adapter = adapter; humanForces = humanForceIds.ToImmutableHashSet();
        var bound = ImmutableArray.CreateBuilder<(OriginalAiPlannerEntry, OriginalAiCompiledRegion)>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Scope is not (WorldEntityKind.Force or WorldEntityKind.District))
                throw new InvalidDataException("original_ai.scheduler_scope_requires_normal_period_continuation");
            var region = program.Compile(entry.BundleKey, 0);
            if (!region.IsBound) throw new InvalidDataException(string.Join("; ", region.Unresolved.Select(item =>
                $"original_ai.bundle.{item.BundleKey:X8}.node.{item.NodeIndex}.type.{item.TypeId:X8}: {item.Requirement}")));
            bound.Add((entry, region));
        }
        this.entries = bound.MoveToImmutable();
    }

    public void Plan(CommandPlanningContext planning)
    {
        if (planning.Current.NativePlayableTerminal is not null) return;
        if (planning.Current.RulesetId != PlayableSkeleton.RulesetId || planning.Current.Phase != TurnPhase.Strategy)
            throw new InvalidOperationException("Original AI planning requires the ordinary strategy command operation.");
        foreach (var (entry, region) in entries)
        {
            var indexes = planning.Current.ReadArmyLifecycle()?.Indexes ?? throw Missing("world_indexes");
            if (!indexes.Registries.TryGetValue(entry.Scope, out var registry) || registry is null)
                throw Missing("registry." + entry.Scope);
            // Keep the source registry sequence. It is not a score ordering,
            // nor a dictionary enumeration used as an invented tie-breaker.
            foreach (var reference in registry)
            {
                if (reference.Kind != entry.Scope || reference.Id is not int id)
                    throw Missing("registry_identity." + entry.Scope);
                OriginalAiActor actor;
                if (entry.Scope == WorldEntityKind.Force)
                {
                    if (!planning.Current.Forces.TryGetValue(id, out var force)) throw Missing("force." + id);
                    if (!force.IsActive || humanForces.Contains(id)) continue;
                    actor = new(id, null, OriginalAiAgentKind.Force);
                }
                else
                {
                    if (!planning.Current.Districts.TryGetValue(id, out var district)) throw Missing("district." + id);
                    // Player delegation is a distinct source scheduler contract,
                    // not inferred merely from district.Number>1.
                    if (humanForces.Contains(district.ForceId)) continue;
                    bool valid = NativeValidity.District(id,
                        district.ForceId is >= 0 and <= byte.MaxValue ? (byte)district.ForceId : null,
                        district.Number is >= 0 and <= byte.MaxValue ? (byte)district.Number : null,
                        ForceUnregisterInputs.ReadDistrictLeader(planning.Current, district))
                        ?? throw Missing("district_validity." + id);
                    if (!valid) continue;
                    actor = new(district.ForceId, id, OriginalAiAgentKind.District);
                }
                var operation = new OriginalAiOperation(planning, rules, adapter, actor, OriginalAiInitialBindings.Empty);
                OriginalAiNodeStatus result = region.Invoke(operation);
                if (result == OriginalAiNodeStatus.Running)
                    throw Missing("running_continuation");
            }
        }
    }
    private static InvalidDataException Missing(string input) => new("original_ai.planner." + input);
}
