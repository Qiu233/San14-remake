using San14.Data.Formats;

namespace San14.Data;

/// <summary>Effect input selected through the shared current tactic getters.
/// Capturing a program is a read, and does not establish its usage eligibility.</summary>
public static class TacticEffectSourceProgram
{
    public static S14TacticEffectProgram ReadFixed(TacticSourceRow tactic)
    {
        ArgumentNullException.ThrowIfNull(tactic);
        return S14TacticEffectProgramReader.Read(tactic.Slot,
            tactic.RawBytes.AsSpan(80, 8));
    }

    public static S14TacticEffectProgram ReadCurrent(int tacticSlot,
        TacticReference receiver, ITacticParameterQueryState state)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(state);
        if (tacticSlot is < 0 or > 200)
            throw new InvalidDataException("Current tactic identity exceeds the CE table.");
        byte[] pairs = new byte[8];
        for (int index = 0; index < 2; index++)
        {
            pairs[index] = TacticParameterQueries.ReadEffectOpcode(receiver, index, state);
            pairs[2 + index] = TacticParameterQueries.ReadRaw64(receiver, index, state);
            pairs[4 + index] = TacticParameterQueries.ReadEffectValue(receiver, index, state);
            pairs[6 + index] = TacticParameterQueries.ReadRaw68(receiver, index, state);
        }
        return S14TacticEffectProgramReader.Read(tacticSlot, pairs);
    }
}
