using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record AutomaticTacticCandidateSource(int TacticSlot,
    S14TacticEffectProgram Program, TacticInstructionTargetsSource[] Instructions)
{
    //Retain the actual two Hex vectors selected once by16BF20. Scoring must
    //not reconstruct them from Army IDs or query target selection a second time.
    internal TacticInstructionHexTargetsSource[]? HexInstructions { get; init; }
}
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
        => ResolveAutomaticCurrentCandidates(state, pair, out _);

    internal AutomaticTacticCurrentCandidates? ResolveAutomaticCurrentCandidates(GameState state,
        NormalArmyCombatPair pair, out string? missingOwner)
    {
        missingOwner = null;
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
        { missingOwner = "automatic-candidates.identity_control_or_available_tactics"; return null; }
        var retained = new List<AutomaticTacticCandidateSource>();
        foreach (int id in available)
        {
            TacticSourceRow row = _catalog.Rows[id];
            if (ReadCurrentProgram(state, row) is not { } program || program.Instructions.Count != 2 ||
                !program.Instructions.Select(instruction => instruction.Index).SequenceEqual([0, 1]))
            { missingOwner = "automatic-candidates.tactic_" + id + "_program"; return null; }
            var selections = new TacticInstructionTargetsSource[2];
            var hexSelections = new TacticInstructionHexTargetsSource[2];
            foreach (S14TacticEffectInstruction instruction in program.Instructions)
            {
                var selected = instruction.Opcode == 0
                    ? new TacticInstructionHexTargetsSource(instruction.Index, [])
                    : ResolveAutomaticInstructionHexTargets(state, caster, row, instruction,
                        pair.Attacker.NativeHexC, pair.Target.NativeHexC);
                if (selected is null)
                { missingOwner = "automatic-candidates.tactic_" + id + "_instruction_" + instruction.Index +
                    "_opcode_" + instruction.Opcode + "_hex_selection"; return null; }
                hexSelections[instruction.Index] = selected;
                var ids = new List<int>(selected.OrderedTargets.Length);
                foreach (var target in selected.OrderedTargets)
                {
                    //This Army application port cannot represent a Base,
                    //Object or territory target. Do not discard that target
                    //and turn a real nonempty instruction into an empty one.
                    if (target.Entity.NativeKind != 27 || target.Entity.GameplayArmyId is not int targetId)
                    { missingOwner = "automatic-candidates.tactic_" + id + "_instruction_" + instruction.Index +
                        "_non_army_target_kind_" + target.Entity.NativeKind; return null; }
                    ids.Add(targetId);
                }
                selections[instruction.Index] = new(ids.ToArray(), true, state.RandomState);
            }
            if (selections.All(selection => selection.OrderedArmyIds.Length == 0)) continue;
            retained.Add(new(id, program, selections) { HexInstructions = hexSelections });
        }
        return new(caster.Id, primary.Id, pair.Attacker.NativeHexC, pair.Target.NativeHexC,
            available.ToArray(), retained.ToArray());
    }
}
