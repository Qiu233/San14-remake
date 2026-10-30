using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The explicit construction of an original-policy manager.
    /// Loading a legacy save never calls this to replace unknown state.
    /// Normal resource provenance is retained without hashing it again.</summary>
    public GameState ConstructOriginalAiPolicyCurrent(GameState state, LoadedOriginalAiPolicy policy,
        OriginalAiStrategyRoots roots)
    {
        if (state.OriginalAiPolicyExecution is not null)
            throw AiPolicyRegistrationMissing("manager_already_constructed");
        var source = new OriginalAiPolicySource(policy.Behavior.Source.ResourceUri,
            policy.Behavior.Source.Sha256, policy.Scripts.Source.ResourceUri, policy.Scripts.Source.Sha256);
        var lifecycle = state.ReadArmyLifecycle() ?? throw AiPolicyRegistrationMissing("Army_lifecycle");
        var groups = lifecycle.ArmyGroups ?? throw AiPolicyRegistrationMissing("military_group_owner");
        var next = state.WithArmyLifecycle(lifecycle with { ArmyGroups = groups with
        {
            OriginalAiContexts = ImmutableDictionary<int, OriginalAiPolicyActorState>.Empty,
            OriginalAiContextPrototype = OriginalAiStrategyContextBank.CreateMilitaryGroupPrototype(roots.MilitaryGroup)
        } }) with { OriginalAiPolicyExecution = new(source, [], FreshOriginalAiDefinitionHeaderEvaluations())
            { RootBundles = new(roots.Force, roots.District, roots.MilitaryGroup, roots.Army) } };
        return RegisterOriginalAiPolicyContextsCurrent(next, CaptureOriginalAiDomesticCurrent(next), roots,
            resetMilitaryGroups: false);
    }

    /// <summary>D2A40: a real manager registration, in registered Force,
    /// District, military-group and member order. It is not a per-query or
    /// per-frame reconstruction. Existing contexts retain aliases and cursors.
    /// The explicit reset branch is the source A1B40 destructive registration.</summary>
    internal GameState RegisterOriginalAiPolicyContextsCurrent(GameState state, SourceWorldContext current,
        OriginalAiStrategyRoots roots, bool resetMilitaryGroups)
    {
        var top = state.OriginalAiPolicyExecution ?? throw AiPolicyRegistrationMissing("manager_owner");
        var lifecycle = state.ReadArmyLifecycle() ?? throw AiPolicyRegistrationMissing("Army_lifecycle");
        var groups = lifecycle.ArmyGroups ?? throw AiPolicyRegistrationMissing("military_group_owner");
        var registrations = groups.OriginalAiContexts ?? throw AiPolicyRegistrationMissing("military_group_context_owner");
        if (groups.OriginalAiContextPrototype is not { } prototype || prototype.BundleKey != roots.MilitaryGroup)
            throw AiPolicyRegistrationMissing("military_group_prototype");
        if (resetMilitaryGroups) registrations = registrations.Clear();
        var actors = top.Actors.ToDictionary(row => row.Actor);
        var groupBuilder = registrations.ToBuilder();
        foreach (var forceReference in current.ReadStartupForces() ?? throw AiPolicyRegistrationMissing("Force_registry"))
        {
            int force = AiTroopsIdentity(forceReference, StartupModelKind.Force, 52);
            Register(new(OriginalAiPolicyActorKind.Force, force), roots.Force);
            foreach (var districtReference in current.ReadStartupDistricts(new(StartupModelKind.Force, force)))
            {
                int district = AiTroopsIdentity(districtReference, StartupModelKind.District, 52);
                Register(new(OriginalAiPolicyActorKind.District, district), roots.District);
                foreach (var groupReference in current.ReadStartupTroops(new(StartupModelKind.District, district)))
                {
                    int group = AiTroopsIdentity(groupReference, StartupModelKind.Troops, 501);
                    if (!groupBuilder.ContainsKey(group))
                        groupBuilder.Add(group, new(new(OriginalAiPolicyActorKind.MilitaryGroup, group),
                            prototype.BundleKey, [], prototype.InitialBlackboard));
                    foreach (var member in ReadOriginalAiTroopsMembersCurrent(current, new(group)))
                    {
                        int unit = ArmyDestructionQueue.ResolveUnitId(state, member.PhysicalSlot)
                            ?? throw AiPolicyRegistrationMissing("member_domain_identity");
                        Register(new(OriginalAiPolicyActorKind.Army, unit), roots.Army);
                    }
                }
            }
        }
        return state.WithArmyLifecycle(lifecycle with { ArmyGroups = groups with
        { OriginalAiContexts = groupBuilder.ToImmutable() } }) with
        { OriginalAiPolicyExecution = top with { Actors = actors.Values.ToImmutableArray() } };

        void Register(OriginalAiPolicyActor actor, uint bundle)
        {
            if (actors.TryGetValue(actor, out var registered))
            {
                if (registered.BundleKey != bundle) throw AiPolicyRegistrationMissing("registered_bundle");
                return;
            }
            actors.Add(actor, OriginalAiStrategyContextBank.CreateRegistration(actor, bundle));
        }
    }

    // Actual2D7A70 successful creation -> D2E30. This is registration of
    // the new District, not a presentation notification or a roster rescan.
    private static GameState RegisterOriginalAiDistrictContextCurrent(GameState state, int district)
    {
        if (state.OriginalAiPolicyExecution is not { } manager) return state;
        var actor = new OriginalAiPolicyActor(OriginalAiPolicyActorKind.District, district);
        if (manager.Actors.Any(row => row.Actor == actor)) return state;
        uint bundle = manager.RootBundles?.District ?? throw AiPolicyRegistrationMissing("District_prototype");
        return state with { OriginalAiPolicyExecution = manager with { Actors = manager.Actors.Add(
            OriginalAiStrategyContextBank.CreateRegistration(actor, bundle)) } };
    }

    //221040/221340 ->31B280 initializes the actual common evaluation word8
    //to zero. Only real fresh policy installation publishes this knowledge;
    //worker/load restores and definition reset never recreate these values.
    private ImmutableArray<OriginalAiBlackboardEvaluation> FreshOriginalAiDefinitionHeaderEvaluations()
    {
        if (Seed.Language != "TC") return []; //No unproven SC constructor defaults.
        var values = ImmutableArray.CreateBuilder<OriginalAiBlackboardEvaluation>(62);
        for (int slot = 0; slot < 31; slot++)
        {
            values.Add(new(new OriginalAiFacilityDefinitionValue(slot), 0));
            values.Add(new(new OriginalAiGemDefinitionValue(slot), 0));
        }
        return values.ToImmutable();
    }

    private static InvalidDataException AiPolicyRegistrationMissing(string name) =>
        new("original_ai.policy_registration." + name);
}
