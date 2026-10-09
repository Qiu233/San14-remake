using San14.Core;

namespace San14.Data;

/// <summary>Battle getter receipt over the explicitly created current
/// CFormation owner. Reuses its saved A8/slot9 data; no source/static row reload.
/// Callers may supply this to their current attribute-query implementation.</summary>
public static class BattleRemakeCurrentFormationCoefficientQueries
{
    public static BattleRemakeCurrentFormationCoefficient? Read(GameState state, int physicalSlot,
        BattleUnitArmyStatKind kind)
    {
        int offset = kind switch
        {
            BattleUnitArmyStatKind.FieldAttack => 0x83,
            BattleUnitArmyStatKind.SiegeTroopAttack => 0x84,
            BattleUnitArmyStatKind.Defense => 0x86,
            BattleUnitArmyStatKind.WallAttack => 0x85,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        int? bytes = TacticRemakeCurrentFormations.ReadVectorByteCount(state, physicalSlot);
        byte? raw = TacticRemakeCurrentFormations.ReadRawByte(state, physicalSlot, offset);
        if (bytes is null || raw is null) return null;
        byte? encoded = (bytes.Value & ~3) == 32
            ? TacticRemakeCurrentFormations.ReadParameter(state, physicalSlot, offset) : null;
        if ((bytes.Value & ~3) == 32 && encoded is null) return null;
        return new(bytes.Value, raw.Value, encoded);
    }
}
