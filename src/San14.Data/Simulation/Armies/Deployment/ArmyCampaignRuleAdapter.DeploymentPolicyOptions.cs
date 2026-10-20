using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed record DeploymentPolicyOptions(bool HasSelection,
    bool? AttackNearbyEnemiesAllowed, bool? IgnoreNearbyEnemiesAllowed,
    bool? RetreatAllowed, bool? RetreatForbiddenAllowed,
    bool? PursuitAllowed, bool? PursuitForbiddenAllowed,
    bool? RetreatAfterActionAllowed, bool? StayAfterActionAllowed,
    string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
}

public sealed record DeploymentDefaultBehaviorsQuery(
    IReadOnlyDictionary<int, DeploymentBehavior>? Behaviors, string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The union of real current trait restrictions across the selected
    /// ordinary officers, independently of display groups and row settings.</summary>
    public DeploymentPolicyOptions QueryDeploymentPolicyOptions(GameState state,
        IReadOnlyList<int> selectedOfficerIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selectedOfficerIds);
        if (selectedOfficerIds.Count == 0) return new(false, false, false, false, false, false, false, false, false, null);
        try
        {
            var traits = ReadDeploymentPolicyTraits(state, selectedOfficerIds, false);
            bool first = traits.Values.Any(t => t.First);
            bool second = traits.Values.Any(t => t.Second);
            bool third = traits.Values.Any(t => t.Third);
            return new(true, !second, !first, !first, !second, !second, !third, !third, true, null);
        }
        catch (InvalidDataException error)
        {
            return new(true, null, null, null, null, null, null, null, null, error.Message);
        }
    }

    /// <summary>Prepare the complete reset of the caller's non-dirty rows before
    /// saving a proposed global preference. Only four behaviors are returned;
    /// formation, tactics, waypoints, target and ETA are never regenerated.</summary>
    public DeploymentDefaultBehaviorsQuery QueryDeploymentDefaultBehaviors(GameState state,
        IReadOnlyList<int> officerIds, DeploymentBehavior proposedDefaults)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(officerIds);
        try
        {
            if (proposedDefaults is null || !Enum.IsDefined(proposedDefaults.AfterAction))
                throw PolicyMissing("proposed_defaults");
            var traits = ReadDeploymentPolicyTraits(state, officerIds, true);
            var behaviors = new Dictionary<int, DeploymentBehavior>();
            foreach (var (id, trait) in traits)
                behaviors.Add(id, trait.ApplyDefaults(proposedDefaults));
            return new(new ReadOnlyDictionary<int,DeploymentBehavior>(behaviors), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private IReadOnlyDictionary<int, DeploymentPolicyTraits> ReadDeploymentPolicyTraits(GameState state,
        IReadOnlyList<int> ids, bool requireValid)
    {
        var traits = new Dictionary<int, DeploymentPolicyTraits>();
        if (ids.Count == 0) return traits;
        var parameters = state.NativeArmySimulation?.Parameters;
        if (parameters is null || parameters.Language != Seed.Language ||
            parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language)) throw PolicyMissing("rule_profile");
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        foreach (int id in ids)
        {
            if (traits.ContainsKey(id)) continue;
            if (id < 0) throw PolicyMissing("negative_person_identity");
            int physical = current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot ?? throw PolicyMissing("person_pointer");
            bool valid = current.ReadPersonNativeValidity(physical) ?? throw PolicyMissing("person_validity");
            if (!valid)
            {
                if (requireValid) throw PolicyMissing("invalid_reset_person");
                traits.Add(id, new(false, false, false));
                continue;
            }
            if (current.ReadPersonIdentity(physical) != id || _definitions.ResolveOfficerSource(id) != physical || !state.Officers.ContainsKey(id))
                throw PolicyMissing("current_person_owner");
            traits.Add(id, ReadCurrentDeploymentPolicyTraits(current, physical));
        }
        return traits;
    }

    private static DeploymentPolicyTraits ReadCurrentDeploymentPolicyTraits(SourceWorldContext current, int physical)
    {
        int first = TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 1) ?? throw PolicyMissing("current_trait_effect1");
        int second = TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 2) ?? throw PolicyMissing("current_trait_effect2");
        int third = TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 3) ?? throw PolicyMissing("current_trait_effect3");
        return new(first != 0, second != 0, third != 0);
    }

    private sealed record DeploymentPolicyTraits(bool First, bool Second, bool Third)
    {
        public DeploymentBehavior ApplyDefaults(DeploymentBehavior behavior)
        {
            if (First) behavior = behavior with { AttackNearbyEnemies = true, AllowRetreat = false };
            if (Second) behavior = behavior with { AttackNearbyEnemies = false, AllowRetreat = true, PursueRetreatingEnemies = false };
            if (Third) behavior = behavior with { PursueRetreatingEnemies = true, AfterAction = DeploymentAfterAction.Stay };
            return behavior;
        }
    }
    private static InvalidDataException PolicyMissing(string field) => new("deployment.policy." + field);
}
