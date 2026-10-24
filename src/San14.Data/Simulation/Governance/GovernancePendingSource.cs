using San14.Core;

namespace San14.Data;

/// <summary>The Turn action's saved governance transition words. The caller
/// owns this operation's current world and rejects its transaction on unknown.</summary>
internal static class GovernancePendingSource
{
    internal const string WorldRefresh1698Key = "ce-force-policy-current:world:1698";

    internal static GameState ApplyCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("saved_parameter_owner");
        int? step = null;

        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        var working = state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };

        // No validity filter or deduplication here: a repeated live Force
        // pointer consumes the words written by its preceding occurrence.
        foreach (var entry in Forces())
        {
            foreach (string key in PendingKeys(Physical(entry))) Reduce(key);
        }

        // The following refresh starts a fresh traversal of the same live C0.
        // Only this second traversal filters on actual Force validity.
        foreach (var entry in Forces())
        {
            var force = new ForceReference(Physical(entry));
            if (!(current.ReadForceNativeValidity(force) ?? throw Missing("Force_validity"))) continue;
            var refreshed = PersonAdvisorResetSource.RefreshGovernanceCurrent(working, current, pool, force,
                morale: false, policyCache: true, cities: false);
            working = refreshed.State ?? throw Missing(refreshed.UnresolvedOwner ?? "policy_cache_refresh");
        }

        // The initial clone, or the most recent refresh's private clone, owns
        // this dictionary. Do not write the original input's parameter bank.
        working.NativeArmySimulation!.Parameters.Int32Values[WorldRefresh1698Key] = 0;
        return working;

        IReadOnlyList<RegistryObjectReference?> Forces() =>
            current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("Force_C0");

        static int Physical(RegistryObjectReference? entry) =>
            entry is { Kind: RegistryKind.Force, PhysicalSlot: int physical }
                ? physical : throw Missing("Force_C0_payload");

        static IEnumerable<string> PendingKeys(int physical)
        {
            yield return BattleRemakeCurrentForcePolicies.ForceFieldKey(physical, 0x16A);
            yield return BattleRemakeCurrentForcePolicies.ForceFieldKey(physical, 0x16C);
            for (int group = 0; group < 5; group++)
                for (int ordinal = 0; ordinal < 7; ordinal++)
                    yield return BattleRemakeCurrentForcePolicies.DepartmentFilterKey(physical, group, ordinal);
        }

        int ReadWord(Dictionary<string, int> bank, string key)
        {
            if (simulation.Parameters.Float32Values.ContainsKey(key) ||
                !bank.TryGetValue(key, out int before) || before is < 0 or > ushort.MaxValue)
                throw Missing("pending_word:" + key);
            return before;
        }

        int ReadStep()
        {
            if (step is int known) return known;
            string key = GovernanceRosterParameters.PendingReductionPerTurn;
            if (simulation.Parameters.Float32Values.ContainsKey(key) ||
                !simulation.Parameters.Int32Values.TryGetValue(key, out int amount))
                throw Missing("pending_reduction_per_turn");
            step = amount;
            return amount;
        }

        void Reduce(string key)
        {
            int before = ReadWord(values, key);
            if (before == 0) return;
            // Read the one independent parameter before the first actual
            // store; zero words do not require it. Later missing ownership
            // discards this local bank, rather than requiring a duplicate full
            // Force/37-word admission scan ahead of the real consumer.
            int reduced = unchecked(before - ReadStep());
            values[key] = Math.Clamp(Math.Max(0, reduced), 0, ushort.MaxValue);
        }
    }

    private static InvalidDataException Missing(string name) =>
        new("governance.turn_pending." + name);
}
