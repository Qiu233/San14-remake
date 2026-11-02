using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Actual17CFD0 inputs after the current target troop debit.
    /// Ordinary middle/lower difficulty never reaches the player contribution.
    /// The native predicate compares Force identities from the saved World player.</summary>
    internal NormalArmyCombatWoundedRatioInput? ResolveNormalWoundedRatioInput(GameState state,
        UnitState? attacker, UnitState target, int selector, int difficulty)
    {
        bool attackerValid = false;
        int attackerPerson = 0;
        if (attacker is not null && ArmyValid(state, attacker) != true || ArmyValid(state, target) != true) return null;
        if (attacker is not null)
        {
            if (CommanderPhysical(state, attacker) is not int actualAttacker) return null;
            attackerPerson = actualAttacker;
        }
        if (CommanderPhysical(state, target) is not int targetPerson) return null;
        if (attacker is not null)
        {
            if (_definitions.ReadPersonNativeValidity(attackerPerson) is not bool actualValid) return null;
            attackerValid = actualValid;
        }
        if (
            _definitions.ReadPersonNativeValidity(targetPerson) is not bool targetValid ||
            ForceMoraleMaximum(state, target.ForceId) is not int cap || ArmyMoraleSourceAdapter.Read(target) is not byte morale ||
            CurrentSurfaceCode(target) is not byte surface ||
            _definitions.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) is not { } terrain ||
            _definitions.ReadSurfaceTypeWoundedCategory42(terrain) is not byte category ||
            _definitions.ReadPersonFirstLocation(targetPerson) is not ushort first)
            return null;
        int charm = 0;
        if (targetValid)
        {
            int? value = attacker is null && _abilities is TacticRemakeCurrentAbilities dimension
                ? dimension.ResolveDimension(state, target.OfficerId, 4)
                : _abilities?.Resolve(state, target.OfficerId)?.Charm;
            if (value is null) return null;
            charm = value.Value;
        }
        bool special = false, forceValid = false, forceIsPlayer = false;
        if (first is >= 1 and <= 61)
        {
            if (_definitions.ReadPersonForce(targetPerson)?.PhysicalSlot is not int force) return null;
            special = force is >= 46 and <= 50;
        }
        //SC233B30/TC2338E0 compare native Force identities, with the
        //current World player identity0 returning false before the target read.
        if (selector != 0)
        {
            if (_definitions.ReadPersonForce(targetPerson) is not { } force ||
                _definitions.ReadForceNativeValidity(force) is not bool valid) return null;
            forceValid = valid;
            if (valid && difficulty >= 3)
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, _definitions);
                if (player is null || _definitions.ReadForcePhysicalSlot(player) is not int playerIdentity) return null;
                if (playerIdentity != 0)
                {
                    if (_definitions.ReadForcePhysicalSlot(force) is not int targetIdentity) return null;
                    forceIsPlayer = playerIdentity == targetIdentity;
                }
            }
        }
        int sourceEffect = 0, targetEffect = 0;
        if (attackerValid)
        {
            int? ReadEffect(int person) => TacticRemakeCurrentTraits.CommanderHasEffect(_definitions, person, 0x6F)
                switch { false => 0, true => TacticRemakeCurrentTraits.CommanderEffectSum(_definitions, person, 0x6F), _ => null };
            if (ReadEffect(attackerPerson) is not int source || ReadEffect(targetPerson) is not int victim) return null;
            sourceEffect = source; targetEffect = victim;
        }
        bool adjacent = attacker is not null && attacker.HexId != target.HexId && NativeHexOrder(target.HexId, 1).Contains(attacker.HexId);
        //CTerrainData's actual V18 is MOV EAX,1;RET for the complete loaded
        //31-entry table, including terrain0. Byte42 is the current row field.
        //Below difficulty3 the player boolean remains an unused formal;
        //that branch performs no current-player owner query.
        return new(targetValid, charm, Math.Min((int)morale, cap), adjacent, true, category,
            category == 1 ? NormalArmyCombatApplicationSourceMath.ReadCurrentTerrain1Addend(state) : null,
            selector, difficulty, forceValid, forceIsPlayer, attacker is not null, attackerValid, targetEffect, sourceEffect, special);
    }

    internal int? ReadNormalApplicationLeadership(GameState state, UnitState target, bool singleDimension = false) =>
        ArmyValid(state, target) != true ? null : singleDimension && _abilities is TacticRemakeCurrentAbilities current
            ? current.ResolveDimension(state, target.OfficerId, 0)
            : _abilities?.Resolve(state, target.OfficerId)?.Leadership;
}
