using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>SC178B62..178B92's complete finite Army nondamage tail.
    /// The source captures actor.word12's Person reference, tests that Person
    /// validity, then passes unchecked(BD44*10) to169320. Its kind44 argument
    /// is unused by the pending-word writer. Unknown remains atomic upstream.</summary>
    public OfficerState? ResolveOrdinaryActivationCommanderTail(GameState state, UnitState caster,
        int tacticId, Func<GameState, int, int, PersonPendingMeritResult> writePendingMerit)
    {
        ArgumentNullException.ThrowIfNull(writePendingMerit);
        if (ArmyValid(state, caster) != true || caster.NativeArmyOrigin?.NativeSlotIndex is not int armySlot ||
            tacticId is < 1 or > 200 || !state.Officers.TryGetValue(caster.OfficerId, out OfficerState? commander) ||
            ReadCurrentProgram(state, _catalog.Rows[tacticId]) is not { } program ||
            program.Instructions.Count != 2 ||
            program.Instructions.Any(instruction => instruction.Opcode > 23 || instruction.Opcode is 1 or 18))
            return null;
        ushort? word12;
        if (state.ReadArmyLifecycle() is { } arena)
        {
            if (armySlot is < 1 or > 500 || arena.Slots.Count != 501) return null;
            var source = arena.Slots[armySlot];
            var receiver = arena.Receivers.Find(source.ReceiverId);
            if (receiver?.ActiveUnitId != caster.Id) return null;
            word12 = source.Commander12;
        }
        else
        {
            //SC2D2CE3..2D2CFA / TC2D2E63..2D2E7A writes the actual creator
            //packet commander to12, clamped0..6000. Source creation binds the
            //canonical Unit.OfficerId to that packet and only admits1..6000.
            //The optional whole return arena is not an activation prerequisite.
            if (caster.OfficerId is < 1 or > 6000) return null;
            word12 = (ushort)caster.OfficerId;
        }
        _definitions.SetArmyUnitTablePointer(armySlot, armySlot);
        _definitions.SetArmyUnitCommanderId(armySlot, word12);
        if (_definitions.ReadArmyUnitCommanderId(new(armySlot)) is not ushort rawCommander ||
            _definitions.ReadPersonLookupReference(rawCommander <= 6000 ? rawCommander : 0)?.PhysicalSlot is not int personSlot ||
            _definitions.ReadPersonNativeValidity(personSlot) is not bool valid)
            return null;
        if (!valid) return commander;
        if (_definitions.ReadPersonIdentity(personSlot) != commander.Id ||
            Parameter(state, SimulationParameterKeys.Tactics.ActivationContribution) is not int contribution) return null;
        int requested = unchecked(contribution * 10);
        PersonPendingMeritResult result = writePendingMerit(state, commander.Id, requested);
        return result.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved
            ? null : result.State.Officers.GetValueOrDefault(commander.Id);
    }
}
