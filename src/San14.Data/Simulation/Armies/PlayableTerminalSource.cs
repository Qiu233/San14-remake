using San14.Core;

namespace San14.Data;

/// <summary>State is the same input for a continuing branch, a new terminal
/// state for a source stop, or null with the actual unresolved owner.</summary>
public sealed record PlayableTerminalResolution(GameState? State,
    NativePlayableTerminalState? Terminal, string? UnresolvedOwner);

/// <summary>The ordinary owned client's27A6B0 player-failure gate and
/// 429070/288800 unification gate. It owns no original TaskScene or tutorial
/// receiver, changes no Person/Army/Force/pool and invokes no presentation.</summary>
public sealed class PlayableTerminalSource(ArmyCampaignRuleAdapter package)
{
    public const string PlayerDefeatRuleId = "ce-27A6B0-player-defeat-terminal";
    public const string OrdinaryUnificationRuleId = "ce-429070-288800-ordinary-unification-terminal";

    public PlayableTerminalResolution TryApplyForceDefeat(GameState input, int victim)
    {
        try
        {
            NativePlayableTerminalStateValidation.Validate(input);
            if (input.NativePlayableTerminal is { } old) return new(input, old, null);
            var current = OrdinaryCurrent(input);
            int player = Player(input, current);
            if (victim is < 1 or > 45 || !Known(current.ReadForceNativeValidity(new(victim)), "victim_ruler10"))
                throw Missing("branch.ordinary_native_valid_victim");
            int remaining = 0, remainingPlayers = 0;
            //27A6B0 uses the physical table, notC0 or Core active/city counts.
            for (int force = 0; force < 52; force++)
            {
                if (!Known(current.ReadForceNativeValidity(new(force)), "Force_table_ruler10")) continue;
                if (force is >= 46 and <= 51 || force == victim) continue;
                remaining++;
                if (force == player) remainingPlayers++;
            }
            //This is the distinct no-ordinary-Force2B39 exit, not the normal
            //player's conquest victory. Keep it outside the two admitted kinds.
            if (remaining == 0) throw Missing("branch.no_remaining_ordinary_force_exit_2B39");
            if (victim != player) return new(input, null, null);
            if (remainingPlayers != 0) throw Missing("branch.multiplayer_terminal");
            return Stop(input, new(NativePlayableTerminalKind.PlayerDefeated, player, victim, PlayerDefeatRuleId));
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    public PlayableTerminalResolution TryApplyOrdinaryUnification(GameState input)
    {
        try
        {
            NativePlayableTerminalStateValidation.Validate(input);
            if (input.NativePlayableTerminal is { } old) return new(input, old, null);
            var current = OrdinaryCurrent(input);
            int player = Player(input, current);
            var saved = input.NativeArmyLifecycle ?? throw Missing("saved_C0_primary_owner");
            if (saved.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Force) is not { } order)
                throw Missing("saved_Force_index");
            int count = 0, only = 0;
            //288800 counts actualC0 nodes, without validity filtering or
            //deduplication. The saved pool has already been admitted on load.
            foreach (var entity in order)
            {
                if (entity.Kind != WorldEntityKind.Force || entity.Id is not int physical)
                    throw Missing("saved_Force_index_value");
                int force = current.ReadForcePhysicalSlot(new(physical)) ?? throw Missing("C0_Force_identity60");
                if (force is >= 46 and <= 51) continue;
                only = force; count++;
                if (count > 1) return new(input, null, null);
            }
            //29EBB0's invalid-player/observer branch is not this finite leaf.
            //A sole foreign/placeholder node is not awarded to theCore player.
            if (count != 1 || only != player) throw Missing("branch.no_single_current_player_C0");
            return Stop(input, new(NativePlayableTerminalKind.OrdinaryUnified, player, null, OrdinaryUnificationRuleId));
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private SourceWorldContext OrdinaryCurrent(GameState input)
    {
        if (input.Phase == TurnPhase.Initializing || input.NativeArmySimulation?.Parameters.Int32Values is not { } bank ||
            bank.GetValueOrDefault(TacticRemakeCurrentBattleControl.VersionKey) != 1 ||
            !bank.TryGetValue(TacticRemakeCurrentBattleControl.HeaderKey, out int header))
            throw Missing("ordinary_application_Tutorial_header0");
        if (header != 0) throw Missing("branch.tutorial_Task_terminal_service");
        //The same independent ordinary application's known absentTaskScene
        //and570850receiver used by the existing capture/return adapters. NoBC
        //default is read or constructed on this absent-receiver branch.
        var current = package.CaptureCurrentDefinitions(input) ?? throw Missing("current_Force_table");
        //Core null ruler cannot encode the actual invalid table row's saved0.
        //Bind that existing detached owner, as the complete return capture does.
        OwnedArmyLifecycleDetachedInputs.BindCurrent(input, current);
        return current;
    }

    private static int Player(GameState input, SourceWorldContext current)
    {
        byte raw = OwnedCurrentPlayerForce.ReadRawForce3A(input) ?? throw Missing("World_byte3A");
        var reference = current.ReadCurrentPlayerForce();
        if (raw is < 1 or > 45 || reference?.PhysicalSlot != raw ||
            !Known(current.ReadForceNativeValidity(reference), "player_ruler10"))
            throw Missing("branch.observer_or_invalid_current_player");
        return raw;
    }

    private static PlayableTerminalResolution Stop(GameState input, NativePlayableTerminalState terminal)
    {
        var next = input with { NativePlayableTerminal = terminal };
        NativePlayableTerminalStateValidation.Validate(next);
        return new(next, terminal, null);
    }
    private static bool Known(bool? value, string field) => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.PlayableTerminal_" + field);
}
