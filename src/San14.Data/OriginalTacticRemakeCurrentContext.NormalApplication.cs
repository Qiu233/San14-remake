using San14.Core;

namespace San14.Data;

public sealed partial class OriginalTacticRemakeCurrentContext
{
    /// <summary>Actual17CFD0 inputs after the current target troop debit.
    /// Ordinary middle/lower difficulty never reaches233B30's contribution.
    /// Advanced difficulty player predicate remains distinct from World3A.</summary>
    internal OriginalNormalArmyCombatWoundedRatioInput? ResolveNormalWoundedRatioInput(GameState state,
        UnitState attacker, UnitState target, int selector, int difficulty)
    {
        if (ArmyValid(state, attacker) != true || ArmyValid(state, target) != true ||
            CommanderPhysical(state, attacker) is not int attackerPerson ||
            CommanderPhysical(state, target) is not int targetPerson ||
            _definitions.ReadPersonNativeValidity(attackerPerson) is not bool attackerValid ||
            _definitions.ReadPersonNativeValidity(targetPerson) is not bool targetValid ||
            ForceMoraleMaximum(state, target.ForceId) is not int cap || target.NativeMorale1A is not byte morale ||
            CurrentSurfaceCode(target) is not byte surface ||
            _definitions.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) is not { } terrain ||
            _definitions.ReadSurfaceTypeWoundedCategory42(terrain) is not byte category ||
            _definitions.ReadPersonFirstLocation(targetPerson) is not ushort first)
            return null;
        int charm = 0;
        if (targetValid)
        {
            if (_abilities?.Resolve(state, target.OfficerId) is not { } abilities) return null;
            charm = abilities.Charm;
        }
        bool special = false, forceValid = false;
        if (first is >= 1 and <= 61)
        {
            if (_definitions.ReadPersonForce(targetPerson)?.PhysicalSlot is not int force) return null;
            special = force is >= 46 and <= 50;
        }
        //The native233B30 predicate is distinct from savedWorld3A reference
        //equality. Only an actually reached contribution needs that producer.
        if (selector != 0)
        {
            if (_definitions.ReadPersonForce(targetPerson) is not { } force ||
                _definitions.ReadForceNativeValidity(force) is not bool valid) return null;
            forceValid = valid;
            if (valid && difficulty >= 3) return null;
        }
        int sourceEffect = 0, targetEffect = 0;
        if (attackerValid)
        {
            int? ReadEffect(int person) => OriginalTacticRemakeCurrentTraits.CommanderHasEffect(_definitions, person, 0x6F)
                switch { false => 0, true => OriginalTacticRemakeCurrentTraits.CommanderEffectSum(_definitions, person, 0x6F), _ => null };
            if (ReadEffect(attackerPerson) is not int source || ReadEffect(targetPerson) is not int victim) return null;
            sourceEffect = source; targetEffect = victim;
        }
        bool adjacent = attacker.HexId != target.HexId && NativeHexOrder(target.HexId, 1).Contains(attacker.HexId);
        //CTerrainData's actual V18 is MOV EAX,1;RET for the complete loaded
        //31-entry table, including terrain0. Byte42 is the current row field.
        //Below difficulty3 the helper's player boolean is an unused formal,
        //not a resolved current233B30=false or a substitutedWorld3A identity.
        return new(targetValid, charm, Math.Min((int)morale, cap), adjacent, true, category,
            category == 1 ? OriginalNormalArmyCombatApplicationSourceMath.ReadCurrentTerrain1Addend(state) : null,
            selector, difficulty, forceValid, false, true, attackerValid, targetEffect, sourceEffect, special);
    }

    internal int? ReadNormalApplicationLeadership(GameState state, UnitState target) =>
        ArmyValid(state, target) == true ? _abilities?.Resolve(state, target.OfficerId)?.Leadership : null;
}
