using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>TC16B910's ordinary source-Army / actual Base-target rate
    /// trial after real candidate filtering and ordering. This overload does
    /// not select candidates, authorize use, dispatch effects or alter RNG.
    /// Null is unresolved current data, not a rejected candidate.</summary>
    public TacticAutomaticActivationCurrentTrial? ResolveAutomaticTacticTrial(GameState state,
        UnitState caster, BaseReference target, int tacticSlot, int candidateOrdinal,
        BattleRemakeCurrentRegionFactor region)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(region);
        if (candidateOrdinal < 5) throw new ArgumentOutOfRangeException(nameof(candidateOrdinal));
        if (!Bound(state) || ArmyValid(state, caster) != true ||
            _definitions.ReadBaseNativeValidity(target) != true) return null;

        //22D640 follows the Base's current object4E link. Invalid linked
        //Object or out-of-map object10 returns the real Hex-table slot0.
        //ReadBaseCurrentHexIndex preserves unknown current getters as errors;
        //48400 is its known-invalid Object sentinel, not a Hex-table index.
        int rawHex = _definitions.ReadBaseCurrentHexIndex(target);
        HexReference? hex = _definitions.ReadHexTableReference(rawHex < 48400 ? rawHex : 0);
        if (hex is null || _definitions.ReadHexPhysicalSlot(hex) is not int targetHex) return null;
        return ResolveAutomaticArmyTrialAtTargetHex(state, caster, targetHex, tacticSlot,
            candidateOrdinal, region);
    }
}
