using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record AutomaticTacticCandidateSource(int TacticSlot,
    S14TacticEffectProgram Program, TacticInstructionTargetsSource[] Instructions);
public sealed record AutomaticTacticCurrentCandidates(int SourceArmyId, int PrimaryArmyId,
    int SourceDescriptorHex, int PrimaryDescriptorHex, int[] ActualAvailableSlots,
    AutomaticTacticCandidateSource[] CandidatesAfterTargetFiltering)
{
    public bool RequiresNativeMultiCandidateScoring => CandidatesAfterTargetFiltering.Length > 1;
}

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>16C820 and172FDD..173104's ordinary Army→Army candidate
    /// stage. Actual available order/repeats and both independent target vectors
    /// are retained. Unknown programs/Base selectors are not silently removed.
    /// This does not perform multi-candidate scoring, rate or activation.</summary>
    public AutomaticTacticCurrentCandidates? ResolveAutomaticCurrentCandidates(GameState state,
        NormalArmyCombatPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        if (!state.Units.TryGetValue(pair.Attacker.ArmyId, out UnitState? caster) ||
            !state.Units.TryGetValue(pair.Target.ArmyId, out UnitState? primary) ||
            ArmyValid(state, caster) != true || ArmyValid(state, primary) != true ||
            caster.NativeArmyOrigin?.NativeSlotIndex != pair.Attacker.NativePoolSlot ||
            primary.NativeArmyOrigin?.NativeSlotIndex != pair.Target.NativePoolSlot ||
            state.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(
                TacticRemakeCurrentBattleControl.VersionKey) != 1 ||
            !state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(
                TacticRemakeCurrentBattleControl.HeaderKey, out int header) || header != 0 ||
            EffectiveFormation(state, caster) is not int formation ||
            AvailableTactics(state, caster, formation) is not int[] available)
            return null;
        var retained = new List<AutomaticTacticCandidateSource>();
        foreach (int id in available)
        {
            TacticSourceRow row = _catalog.Rows[id];
            if (ReadCurrentProgram(state, row) is not { } program || program.Instructions.Count != 2 ||
                !program.Instructions.Select(instruction => instruction.Index).SequenceEqual([0, 1])) return null;
            var selections = new TacticInstructionTargetsSource[2];
            foreach (S14TacticEffectInstruction instruction in program.Instructions)
            {
                TacticInstructionTargetsSource? selected = instruction.Opcode == 0
                    ? new([], true, state.RandomState)
                    : ResolveInstructionTargetsAtCapturedHexes(state, caster, primary, row, instruction,
                        pair.Attacker.NativeHexC, pair.Target.NativeHexC);
                if (selected is null || !selected.NoSelectedBaseTargets || selected.OrderedArmyIds is null)
                    return null;
                selections[instruction.Index] = selected with { OrderedArmyIds = selected.OrderedArmyIds.ToArray() };
            }
            if (selections.All(selection => selection.OrderedArmyIds.Length == 0)) continue;
            retained.Add(new(id, program, selections));
        }
        return new(caster.Id, primary.Id, pair.Attacker.NativeHexC, pair.Target.NativeHexC,
            available.ToArray(), retained.ToArray());
    }
}
