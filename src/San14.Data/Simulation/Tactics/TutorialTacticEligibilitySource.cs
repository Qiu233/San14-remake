using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The active tutorial's definition filter (TC499BC0/SC499CF0). Lesson three
/// excludes fire instructions; it does not restrict tactic identities or
/// categories. The program is decoded by the ordinary current tactic owner.</summary>
internal static class TutorialTacticEligibilitySource
{
    internal static bool? AllowsCurrent(GameState state, S14TacticEffectProgram program)
    {
        int? header = TacticRemakeCurrentBattleControl.ReadHeader(state);
        if (header is null) return null;
        //16CAC0/1753E0 do not invoke499BC0 for an inactive controller.
        if (header == 0) return true;

        int? lesson = TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(state);
        if (lesson is null) return null;
        if (lesson != 3) return true;

        if (program.Instructions.Count != 2)
            throw new InvalidDataException("Tutorial tactic filtering requires the two current instructions.");
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            S14TacticEffectInstruction instruction = program.Instructions[ordinal];
            if (instruction.Index != ordinal)
                throw new InvalidDataException("Tutorial tactic instruction order is not current.");
            if (instruction.Opcode == 17) return false;
        }
        return true;
    }
}
