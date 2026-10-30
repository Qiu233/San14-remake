using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual first-course load callback's player confirmation,
    /// followed by the world-facing prefix of4FAE70. This does not enter an
    /// Opening paragraph, complete a UI selection or grant lesson progress.</summary>
    internal GameState InitializeSelectedTutorialWorldCurrent(GameState state)
    {
        RequireSelectedTutorialControl(state);
        var current = CaptureCurrentDefinitions(state) ?? throw FirstTutorialMissing("entry.current_world");
        //533A20 ->5641A0 ->533960 selects World's actual Force table row2.
        //520820 reads its ruler;5218F0's completed load callback5409F0 then
        //invokes33CCA0. City7's ownership is not a player-selection input.
        var force = current.ReadForceTableReference(2) ?? throw FirstTutorialMissing("entry.Force2_pointer");
        if (!(current.ReadForceNativeValidity(force) ?? throw FirstTutorialMissing("entry.Force2_validity")))
            throw FirstTutorialMissing("entry.Force2_invalid");
        ushort rulerId = current.ReadForceRecord(2)?.RawRuler10 ?? throw FirstTutorialMissing("entry.Force2_ruler");
        var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0))
            ?? throw FirstTutorialMissing("entry.ruler_lookup");
        if (ruler.PhysicalSlot is not int person ||
            !(current.ReadPersonNativeValidity(person) ?? throw FirstTutorialMissing("entry.ruler_validity")))
            throw FirstTutorialMissing("entry.ruler_invalid");
        var confirmed = OwnedCurrentPlayerForce.ConfirmNewGamePlayerPerson(state, current, rulerId);
        //The existing33CCA0 tail owns2EE410's actual RankB3 capacity store.
        confirmed = ApplyConfirmedPlayerCommanderCapacity(confirmed, current);
        confirmed = WriteFirstTutorialControl(confirmed, 60, true);
        return WriteFirstTutorialControl(confirmed, 67, true);
    }

    /// <summary>Source-time world writes only. Phase0 is paragraph entry,
    /// phase-1 is its actual completed tail. Progress paragraph7 additionally
    /// uses phase1 after its opening narration, before550180. Paragraph5's
    /// phase2 is its unconfirmed-priority retry branch. The host owns all
    /// predicates, UI receipts, narration waits and temporary control63.</summary>
    internal GameState ApplyFirstTutorialParagraphWorldCurrent(GameState state, int paragraphIndex, int phase)
    {
        RequireFirstTutorialControl(state);
        if ((uint)paragraphIndex >= 22 || phase < -1) throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        var progress = state.TutorialLessonProgress ?? throw FirstTutorialMissing("paragraph.progress_owner");
        if (progress.CourseIndex != 0) throw FirstTutorialMissing("paragraph.first_course");
        if (phase == 0)
        {
            switch (paragraphIndex)
            {
                case 0: //4BAC50, before its first100 narration.
                    state = WriteFirstTutorialControl(state, 78, true);
                    state = WriteFirstTutorialControl(state, 79, true);
                    state = WriteFirstTutorialControl(state, 81, true);
                    state = WriteFirstTutorialControl(state, 62, true);
                    state = ApplyFirstTutorialOpeningResources(state);
                    state = SetFirstTutorialCommandMask(state, 0);
                    return WriteFirstTutorialControl(state, 58, true);
                case 2: //4E7080/4E7CA0 ->54FA60: command3 and its parent2.
                    state = SetFirstTutorialCommandMask(state, (1 << 3) | (1 << 2));
                    return WriteFirstTutorialControl(state, 60, false);
                case 11: //4E68F0 ->54FA60: command4 and its parent2.
                    state = SetFirstTutorialCommandMask(state, (1 << 4) | (1 << 2));
                    return WriteFirstTutorialControl(state, 60, false);
                case 13: //4AC970 clears the command mask, then enters550180.
                    state = SetFirstTutorialCommandMask(state, 0);
                    return WriteFirstTutorialControl(state, 4, false);
                case 18: //4BF270's first550180 call, before real progress input.
                    return WriteFirstTutorialControl(state, 4, false);
            }
        }
        else if (paragraphIndex == 7 && phase == 1)
            return WriteFirstTutorialControl(state, 4, false); //4BEAF0 ->550180, after515C60 callback.
        else if (paragraphIndex == 5 && phase == 2)
            return WriteFirstTutorialControl(state, 12, false); //4C1730's unconfirmed-priority retry.
        else if (phase == -1)
        {
            switch (paragraphIndex)
            {
                case 2: //4C0A60, only after54FA60 actually returns1.
                    state = SetFirstTutorialCommandMask(state, 0);
                    return WriteFirstTutorialControl(state, 66, true);
                case 5: //4C1730, only after the actual decision bit13.
                    return WriteFirstTutorialControl(state, 66, true);
                case 6: //4B3620, only after the actual commit bit14.
                    return WriteFirstTutorialControl(state, 66, false);
                case 18: //4BF270, only after550180 actually returns1.
                    return WriteFirstTutorialControl(state, 16, false);
                case 20: //4B3A40, after126 narration and UI retirement.
                    return state with { TutorialLessonProgress = progress with { CompletionCourseIndex = progress.CourseIndex } };
            }
        }
        return state;
    }

    private GameState ApplyFirstTutorialOpeningResources(GameState state)
    {
        var current = CaptureCurrentDefinitions(state) ?? throw FirstTutorialMissing("opening.current_world");
        var city = current.ReadCityTableReference(7) ?? throw FirstTutorialMissing("opening.City7_pointer");
        //4BAC50 requests the immutable first-lesson differences3000-2000
        //gold and10000-7000 food. These are setup inputs, not a fabricated
        //domestic-income result; the normal virtual setters still bound them.
        int gold = BoundFirstTutorialResource(1000, BasePostLoad.ReadGoldCapacity(city, current));
        current.SetBaseResource(city, BaseResourceKind.Gold, gold);
        int food = BoundFirstTutorialResource(3000, BasePostLoad.ReadSuppliesCapacity(city, current));
        current.SetBaseResource(city, BaseResourceKind.Supplies, food);
        var owner = state.Cities.GetValueOrDefault(7) ?? throw FirstTutorialMissing("opening.City7_owner");
        return state with { Cities = new(state.Cities) { [7] = owner with { Gold = gold, Supplies = food } } };
    }

    private static int BoundFirstTutorialResource(int requested, int maximum) => requested < 0 ? 0 : Math.Min(requested, maximum);

    private static GameState SetFirstTutorialCommandMask(GameState state, int mask)
    {
        var progress = state.TutorialLessonProgress ?? throw FirstTutorialMissing("commands.progress_owner");
        return state with { TutorialLessonProgress = progress with { EnabledCommandMask = checked((ushort)mask) } };
    }

    private static void RequireFirstTutorialControl(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (TacticRemakeCurrentBattleControl.ReadHeader(state) is not int header || header == 0 ||
            TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(state) != 0 ||
            TacticRemakeCurrentBattleControl.ReadBitmapPresence(state) != true)
            throw FirstTutorialMissing("selected_first_lesson_control");
    }

    private static void RequireSelectedTutorialControl(GameState state)
    {
        if (TacticRemakeCurrentBattleControl.ReadHeader(state) is not int header || header == 0 ||
            TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(state) is not (>= 0 and <= 7) ||
            TacticRemakeCurrentBattleControl.ReadBitmapPresence(state) != true)
            throw FirstTutorialMissing("selected_supported_lesson_control");
    }

    private static GameState WriteFirstTutorialControl(GameState state, int index, bool enabled)
    {
        var simulation = state.NativeArmySimulation ?? throw FirstTutorialMissing("control.simulation");
        string key = TacticRemakeCurrentBattleControl.BitmapKey(index >> 3);
        if (simulation.Parameters.Float32Values.ContainsKey(key) ||
            !simulation.Parameters.Int32Values.TryGetValue(key, out int before) || before is < 0 or > 255)
            throw FirstTutorialMissing("control.bitmap_byte");
        int bit = 1 << (index & 7);
        int after = enabled ? before | bit : before & ~bit;
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
            { Int32Values = new(simulation.Parameters.Int32Values) { [key] = after } } } };
    }
    private static InvalidDataException FirstTutorialMissing(string name) => new("tutorial.first_lesson.world." + name);
}
