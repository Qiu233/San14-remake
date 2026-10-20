using San14.Core;

namespace San14.Data;

/// <summary>Actual ordinary2704F0 two-way Relations13 mutation. Directional
/// hostilityEA is a separate owner. Each call retains its own level notice.</summary>
internal static class ForceRelationChangeCurrent
{
    internal static GameState Apply(GameState state, SourceWorldContext current,
        int first, int second, Func<int> adjustment, Func<string, int> parameter)
    {
        var own = current.ReadForceTableReference(first <= 51 ? first : 0) ?? throw Missing("fire_relation_attacker");
        if (!Known(current.ReadForceNativeValidity(own), "fire_relation_attacker_validity") || first == second) return state;
        if (first is < 1 or > 45 || second is < 1 or > 45) throw Missing("fire_relation_special_force_tail");
        var mode = current.ArmyReturnTransferMode ?? throw Missing("fire_relation_mode");
        bool show = !mode.Present || Known(mode.Value, "fire_relation_mode_value") == 0;
        var simulation = state.NativeArmySimulation ?? throw Missing("fire_relation_state");
        var forces = new Dictionary<int, NativeArmyForceState>(simulation.Forces);
        byte[] FirstValues() => forces.GetValueOrDefault(first)?.NormalAftermath?.Relations13 is { Length: 52 } values
            ? values : throw Missing("fire_relation_values");
        int Level(int value)
        {
            for (int level = 1; level <= 5; level++)
                if (value < parameter(SimulationParameterKeys.ForceRelations.LevelThreshold(level))) return level - 1;
            return 5;
        }
        int before = Level(FirstValues()[second]);
        foreach (var (self, other) in new[] { (first, second), (second, first) })
        {
            var force = forces.GetValueOrDefault(self) ?? throw Missing("fire_relation_force_owner");
            var fields = force.NormalAftermath ?? throw Missing("fire_relation_fields");
            var values = fields.Relations13 is { Length: 52 } relation ? relation.ToArray() : throw Missing("fire_relation_values");
            values[other] = (byte)Math.Clamp(unchecked(values[other] + adjustment()), 0, 200);
            forces[self] = force with { NormalAftermath = fields with { Relations13 = values } };
        }
        int after = Level(FirstValues()[second]);
        var result = state with { NativeArmySimulation = simulation with { Forces = forces } };
        //Fire passes param5=1: unchanged categories never announce. Changed
        //categories announce only when either force is current player and the
        //actual challenge/tutorial mode did not suppress param4.
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("fire_relation_player");
        int playerId = Known(player.PhysicalSlot, "fire_relation_player_identity");
        if (show && before != after && playerId != 0 && (playerId == first || playerId == second))
            result = result with { PendingForceRelationAnnouncements = [.. state.PendingForceRelationAnnouncements, new(first, second, after)] };
        return result;
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("force.relation-current." + field);
}
