using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IGovernanceExpansionRules
{
    public GovernanceExpansionEvaluation EvaluateGovernanceExpansion(GameState state, ForceState force,
        int group, CommandContext context)
    {
        if (context.DelegatedDistrictId is not null || force.Id is < 1 or > 51 ||
            !state.Forces.TryGetValue(force.Id, out var owner) || !owner.IsActive)
            return new(false, null, null, null, null, "Expansion requires direct active force control.", null);
        if ((uint)group >= 5)
            return new(false, null, null, null, null, "The department is invalid.", null);
        try
        {
            int remaining = _force164Inputs.ReadAdditionalDepartmentSlots(state, owner.Id)
                ?? throw ExpansionMissing("experience_slots");
            for (int department = 0; department < 5; department++)
                remaining = unchecked(remaining - ReadGovernanceExpansionByte(state, owner.Id, department));
            var doctrine = GovernanceRosterSource.ReadDoctrine(state, owner, _scenario.Snapshot.DoctrineParameters)
                ?? throw ExpansionMissing("current_ruler_doctrine");
            int expansion = ReadGovernanceExpansionByte(state, owner.Id, group);
            int capacity = doctrine.RawBytes[91 + group] + expansion;
            int maximum = doctrine.RawBytes[96 + group];
            bool allowed = remaining > 0 && capacity < maximum;
            return new(allowed, remaining, capacity, maximum, expansion,
                allowed ? null : remaining <= 0 ? "No unallocated expansion points remain." : "The doctrine department limit is reached.", null);
        }
        catch (InvalidDataException error)
        { return new(null, null, null, null, null, null, error.Message); }
    }

    public CommandResult ApplyGovernanceExpansion(GameState state, ForceState force,
        ExpandGovernanceDepartmentOrder order, CommandContext context)
    {
        if (order.ForceId != force.Id)
            return CommandResult.Reject("The force does not own this expansion.", "governance.expansion_force");
        var evaluation = EvaluateGovernanceExpansion(state, force, order.Group, context);
        if (evaluation.Allowed == false)
            return CommandResult.Reject(evaluation.Reason ?? "Department expansion is unavailable.", "governance.expansion");
        if (evaluation.Allowed != true || evaluation.Expansion is not int previous)
            return CommandResult.RuleUnavailable(evaluation.UnresolvedInput ?? "governance.expansion_inputs",
                "Department expansion requires its current doctrine and saved allocation inputs.");
        // GameEngine supplies this detached command transaction. The existing
        // five saved bytes own allocation; no second balance or fee is created.
        state.NativeArmySimulation!.Parameters.Int32Values[
            BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(force.Id, order.Group)] = Math.Min(previous + 1, 7);
        return CommandResult.Accept(state);
    }

    private static int ReadGovernanceExpansionByte(GameState state, int forceId, int group)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw ExpansionMissing("parameter_owner");
        string key = BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(forceId, group);
        return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value) &&
            value is >= 0 and <= byte.MaxValue ? value : throw ExpansionMissing("department_owner");
    }

    private static InvalidDataException ExpansionMissing(string input) => new("governance.expansion." + input);
}
