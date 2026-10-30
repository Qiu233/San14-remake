using San14.Core;

namespace San14.Data;

/// <summary>The selected-lesson/control-state projections of the actual
/// tutorial lifecycle. They are called by real selection/entry/reset hosts;
/// none starts a tutorial UI, selects a lesson on the player's behalf, or
/// claims the rest of a tutorial's camera and paragraph setup completed.</summary>
public static class TutorialLessonLifecycleSource
{
    /// <summary>50D600's confirmed selection writes header1 and the actual
    /// selected row0..7. The bitmap is not allocated by this selection.</summary>
    public static GameState SelectCurrent(GameState state, int selectedLessonIndex)
    {
        if ((uint)selectedLessonIndex >= 8) throw new ArgumentOutOfRangeException(nameof(selectedLessonIndex));
        var simulation = RequireController(state);
        RequireIntegerField(simulation.Parameters, TacticRemakeCurrentBattleControl.HeaderKey);
        RequireIntegerField(simulation.Parameters, TacticRemakeCurrentBattleControl.LessonIndexKey);
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        {
            [TacticRemakeCurrentBattleControl.HeaderKey] = 1,
            [TacticRemakeCurrentBattleControl.LessonIndexKey] = selectedLessonIndex
        };
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    /// <summary>4FACD0 always writes inactive/no selection. Only its active
    /// branch releases the13-byte bitmap; inactive reset leaves it alone.
    /// Freed knowledge is removed, not replaced by invented zero bytes.</summary>
    public static GameState ResetCurrent(GameState state)
    {
        var simulation = RequireController(state);
        int header = TacticRemakeCurrentBattleControl.ReadHeader(state)
            ?? throw Missing("reset_controller_header");
        RequireIntegerField(simulation.Parameters, TacticRemakeCurrentBattleControl.LessonIndexKey);
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        {
            [TacticRemakeCurrentBattleControl.HeaderKey] = 0,
            [TacticRemakeCurrentBattleControl.LessonIndexKey] = -1
        };
        var floats = simulation.Parameters.Float32Values;
        if (header != 0)
        {
            values[TacticRemakeCurrentBattleControl.BitmapPresenceKey] = 0;
            floats = new(floats);
            for (int index = 0; index < 13; index++)
            {
                string key = TacticRemakeCurrentBattleControl.BitmapKey(index);
                values.Remove(key); floats.Remove(key);
            }
            floats.Remove(TacticRemakeCurrentBattleControl.BitmapPresenceKey);
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values, Float32Values = floats } } };
    }

    /// <summary>Only4FAE70's actual bitmap-allocation prefix: an active
    /// controller with a known absent bitmap allocates and clears13 bytes.
    /// Existing allocation is retained. Later lesson-specific bit writers,
    /// paragraph setup and camera/UI activation belong to their real hosts.</summary>
    public static GameState InitializeSelectedLessonBitmapCurrent(GameState state)
    {
        var simulation = RequireController(state);
        int header = TacticRemakeCurrentBattleControl.ReadHeader(state)
            ?? throw Missing("entry_controller_header");
        if (header == 0) return state;
        bool present = TacticRemakeCurrentBattleControl.ReadBitmapPresence(state)
            ?? throw Missing("entry_bitmap_presence");
        if (present) return state;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
            { [TacticRemakeCurrentBattleControl.BitmapPresenceKey] = 1 };
        var floats = new Dictionary<string, float>(simulation.Parameters.Float32Values);
        for (int index = 0; index < 13; index++)
        {
            string key = TacticRemakeCurrentBattleControl.BitmapKey(index);
            values[key] = 0; floats.Remove(key);
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values, Float32Values = floats } } };
    }

    private static NativeArmySimulationState RequireController(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.NativeClock is null || state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Language is not ("TC" or "SC") ||
            string.IsNullOrWhiteSpace(simulation.Parameters.Revision) ||
            simulation.Parameters.Float32Values.ContainsKey(TacticRemakeCurrentBattleControl.VersionKey) ||
            simulation.Parameters.Int32Values.GetValueOrDefault(TacticRemakeCurrentBattleControl.VersionKey) != 1)
            throw Missing("controller_owner");
        return simulation;
    }

    private static void RequireIntegerField(NativeArmyParameterState parameters, string key)
    {
        if (parameters.Float32Values.ContainsKey(key)) throw Missing("noninteger_" + key);
    }
    private static InvalidDataException Missing(string field) => new("tutorial.lesson-lifecycle." + field);
}
