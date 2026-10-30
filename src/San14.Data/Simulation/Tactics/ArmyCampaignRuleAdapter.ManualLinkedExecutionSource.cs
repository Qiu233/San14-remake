using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record ManualLinkedExecutionCapture(NormalCombatParticipant Primary,
    AutomaticTacticBaseCandidateSource MainSelected, int LinkedCenter);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>174020's main selection, after the independent16F230
    /// eligibility gate. Null means a known native rejection; unknown source
    /// ownership throws. This performs no effects, automatic trial, linked
    /// selection, waiting, random write or pending-command cleanup.</summary>
    internal ManualLinkedExecutionCapture? CaptureManualLinkedExecutionCurrent(GameState state,
        SourceWorldContext current, UnitState caster, int tacticId, int selectedHex)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state)) throw ManualLinkedMissing("current_context");
        if (ArmyValid(state, caster) is not bool sourceValid) throw ManualLinkedMissing("source_Army_validity");
        if (!sourceValid || !ManualLinkedKnown(ReadManualTacticValidity(state, tacticId), "tactic_validity")) return null;
        if (selectedHex is < 0 or >= 48400) return null;
        var targetHex = current.ReadHexTableReference(selectedHex) ?? throw ManualLinkedMissing("selected_Hex_pointer");
        if (targetHex.PhysicalSlot is null) return null;
        if (targetHex.PhysicalSlot != selectedHex) throw ManualLinkedMissing("selected_Hex_identity");
        int sourceHex = ManualLinkedKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, caster), "source_Army2A");
        var read = FieldHexOccupantSource.ReadCurrent(state, current, selectedHex);
        var selected = read.Receipt ?? throw ManualLinkedMissing(read.UnresolvedOwner ?? "selected_Hex_receiver");
        NormalCombatParticipant primary;
        int linkedCenter;
        if (selected.NativeKind == 27)
        {
            var receiver = current.ReadArmyUnitTableReference(selected.NativePhysicalSlot)
                ?? throw ManualLinkedMissing("target_Army_pointer");
            if (!ManualLinkedKnown(current.ReadArmyUnitNativeValidity(receiver), "target_Army_validity")) return null;
            int id = selected.GameplayArmyId ?? throw ManualLinkedMissing("target_gameplay_ArmyId");
            var target = state.Units.GetValueOrDefault(id) ?? throw ManualLinkedMissing("target_current_Army_image");
            int raw = ManualLinkedKnown(current.ReadArmyUnitRawHex2A(receiver), "target_Army2A");
            linkedCenter = ManualLinkedKnown(current.ReadHexTableReference(raw < 48400 ? raw : 0)?.PhysicalSlot, "target_Army_Hex");
            primary = new(27, selected.NativePhysicalSlot, id,
                ManualLinkedKnown(ArmyMoraleSourceAdapter.Read(target), "target_Army_morale"), checked((ushort)target.Troops),
                selectedHex, current.ReadArmyUnitRawForceSlot(receiver), 0);
        }
        else if (selected.NativeKind is 5 or 6)
        {
            var receiver = selected.NativeKind == 5 ? current.ReadCityTableReference(selected.NativePhysicalSlot)
                : current.ReadGateTableReference(selected.NativePhysicalSlot);
            if (receiver is null) throw ManualLinkedMissing("target_Base_pointer");
            if (!ManualLinkedKnown(current.ReadBaseNativeValidity(receiver), "target_Base_validity")) return null;
            int raw = current.ReadBaseCurrentHexIndex(receiver);
            linkedCenter = ManualLinkedKnown(current.ReadHexTableReference(raw < 48400 ? raw : 0)?.PhysicalSlot, "target_Base_Hex");
            int troops = ManualLinkedKnown(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "target_Base3C");
            primary = new(selected.NativeKind, selected.NativePhysicalSlot, null,
                ManualLinkedKnown(current.ReadBaseMorale44(receiver), "target_Base44"), unchecked((ushort)Math.Max(0, troops)),
                selectedHex, ManualLinkedKnown(current.ReadBaseForceSlot(receiver), "target_Base_Force"), 0);
        }
        else return null; //174020 requires actual Army or Base, not empty/facility.
        var program = ReadCurrentProgram(state, _catalog.Rows[tacticId]) ?? throw ManualLinkedMissing("current_program");
        if (!ManualProgram(program)) throw ManualLinkedMissing("two_instruction_program");
        var vectors = CaptureManualInstructionHexVectorsCurrent(state, caster, tacticId, program, sourceHex, selectedHex)
            ?? throw ManualLinkedMissing("main_instruction_Hex_vectors");
        if (vectors.All(vector => vector.OrderedTargets.Length == 0)) return null;
        return new(primary, new(tacticId, program, vectors), linkedCenter);
    }

    //16BF20 is common to manual and automatic usage. The no-primary
    //eligibility fallback keeps caster-centered instructions and has no
    //selected center for the other instruction family.
    private TacticInstructionHexTargetsSource[]? CaptureManualInstructionHexVectorsCurrent(GameState state,
        UnitState caster, int tacticId, S14TacticEffectProgram program, int sourceHex, int? selectedHex)
    {
        var vectors = new TacticInstructionHexTargetsSource[2];
        foreach (var instruction in program.Instructions)
        {
            bool selectedCentered = instruction.Opcode is 1 or 2 or 3 or 4 or 10 or 11 or 12 or 13 or 14 or 16 or 17 or 18 or 22 or 23;
            var vector = selectedHex is null && selectedCentered ? new TacticInstructionHexTargetsSource(instruction.Index, [])
                : ResolveAutomaticInstructionHexTargets(state, caster, _catalog.Rows[tacticId], instruction,
                    sourceHex, selectedHex ?? sourceHex);
            if (vector is null) return null;
            vectors[instruction.Index] = vector;
        }
        return vectors;
    }

    private static T ManualLinkedKnown<T>(T? value, string owner) where T : struct => value ?? throw ManualLinkedMissing(owner);
    private static InvalidDataException ManualLinkedMissing(string owner) => new("manual_linked_tactic." + owner);
}
