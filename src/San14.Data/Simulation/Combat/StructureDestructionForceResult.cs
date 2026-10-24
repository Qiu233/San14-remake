using San14.Core;

namespace San14.Data;

/// <summary>The generic-Object branch of the Force result callback.
/// The enclosing attack owns its transaction, actual Object removal and effects.</summary>
internal static class StructureDestructionForceResult
{
    internal const string Kind11DestroyBonus =
        "simulation.force-combat-result.field-object-destroy-bonus";
    internal const string Kind11LossScale =
        "simulation.force-combat-result.field-object-loss-scale";

    internal static GameState Apply(GameState state, SourceWorldContext current,
        ForceReference attacker, int oldObjectForce, int appliedLoss, bool destroyed)
    {
        if (!(current.ReadForceNativeValidity(attacker) ?? throw Missing("attacker_validity")))
            return state;
        int physical = current.ReadForcePhysicalSlot(attacker) ?? throw Missing("attacker_physical");
        if (oldObjectForce is < 0 or > byte.MaxValue) throw Missing("old_Object_force_byte");
        if (appliedLoss is < 0 or > ushort.MaxValue) throw Missing("applied_loss");

        // Fatal calls pass loss0/destroyed1; nonfatal calls pass the actual
        // capped loss/destroyed0. Only a matched objective reads its applicable
        // independent parameter; selected-Force tracking consumes neither.
        var resolved = ForceCombatObjectiveRules.Resolve(new ForceCombatObjectiveCurrent(
            state, physical, current, new NormalArmyForceResultDefinitions(current),
            () => new(appliedLoss > 0 ? ReadParameter(Kind11LossScale) : 0,
                destroyed ? ReadParameter(Kind11DestroyBonus) : 0), knownAttackerValidity: true),
            oldObjectForce, appliedLoss, destroyed)
            ?? throw Missing("current_objective");
        if (!resolved.Triggered) return state;

        var simulation = state.NativeArmySimulation;
        if (resolved.MarkSelectedForceDefeated)
        {
            if (simulation?.Forces.GetValueOrDefault(physical) is not { Flags12: byte flags } force)
                throw Missing("attacker_flags12");
            simulation = simulation with { Forces = new(simulation.Forces)
                { [physical] = force with { Flags12 = (byte)(flags | 4) } } };
        }

        var forces = state.Forces;
        if (resolved.ProgressAfter is int progress)
        {
            if (forces.GetValueOrDefault(physical) is not { CombatObjective: { } objective } force)
                throw Missing("attacker_objective_owner");
            forces = new(forces) { [physical] = force with
                { CombatObjective = objective with { Progress = progress } } };
        }
        return state with { NativeArmySimulation = simulation, Forces = forces };

        int ReadParameter(string key)
        {
            var parameters = state.NativeArmySimulation?.Parameters;
            if (parameters is null || parameters.Float32Values.ContainsKey(key) ||
                !parameters.Int32Values.TryGetValue(key, out int value))
                throw Missing(key);
            return value;
        }
    }

    private static InvalidDataException Missing(string field) =>
        new("combat.structure_force_result." + field);
}
