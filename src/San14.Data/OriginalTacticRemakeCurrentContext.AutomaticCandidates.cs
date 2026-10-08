using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalAutomaticTacticCandidateSource(int TacticSlot,
    S14TacticEffectProgram Program, OriginalTacticInstructionTargetsSource[] Instructions);
public sealed record OriginalAutomaticTacticCurrentCandidates(int SourceArmyId, int PrimaryArmyId,
    int SourceDescriptorHex, int PrimaryDescriptorHex, int[] ActualAvailableSlots,
    OriginalAutomaticTacticCandidateSource[] CandidatesAfterTargetFiltering)
{
    public bool RequiresNativeMultiCandidateScoring => CandidatesAfterTargetFiltering.Length > 1;
}

public sealed partial class OriginalTacticRemakeCurrentContext
{
    /// <summary>16C820 and172FDD..173104's ordinary Army→Army candidate
    /// stage. Actual available order/repeats and both independent target vectors
    /// are retained. Unknown programs/Base selectors are not silently removed.
    /// This does not perform multi-candidate scoring, rate or activation.</summary>
    public OriginalAutomaticTacticCurrentCandidates? ResolveAutomaticCurrentCandidates(GameState state,
        OriginalNormalArmyCombatPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        if (!state.Units.TryGetValue(pair.Attacker.ArmyId, out UnitState? caster) ||
            !state.Units.TryGetValue(pair.Target.ArmyId, out UnitState? primary) ||
            ArmyValid(state, caster) != true || ArmyValid(state, primary) != true ||
            caster.NativeArmyOrigin?.NativeSlotIndex != pair.Attacker.NativePoolSlot ||
            primary.NativeArmyOrigin?.NativeSlotIndex != pair.Target.NativePoolSlot ||
            state.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(
                OriginalTacticRemakeCurrentBattleControl.VersionKey) != 1 ||
            !state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(
                OriginalTacticRemakeCurrentBattleControl.HeaderKey, out int header) || header != 0 ||
            EffectiveFormation(state, caster) is not int formation ||
            AvailableTactics(state, caster, formation) is not int[] available)
            return null;
        var retained = new List<OriginalAutomaticTacticCandidateSource>();
        foreach (int id in available)
        {
            OriginalTacticSourceRow row = _catalog.Rows[id];
            if (ReadCurrentProgram(state, row) is not { } program || program.Instructions.Count != 2 ||
                !program.Instructions.Select(instruction => instruction.Index).SequenceEqual([0, 1])) return null;
            var selections = new OriginalTacticInstructionTargetsSource[2];
            foreach (S14TacticEffectInstruction instruction in program.Instructions)
            {
                OriginalTacticInstructionTargetsSource? selected = instruction.Opcode == 0
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
