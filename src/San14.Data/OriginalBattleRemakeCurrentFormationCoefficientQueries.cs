using San14.Core;

namespace San14.Data;

/// <summary>Battle getter receipt over the explicitly created current
/// CFormation owner. Reuses its saved A8/slot9 data; no source/static row reload.
/// Callers may supply this to their current attribute-query implementation.</summary>
public static class OriginalBattleRemakeCurrentFormationCoefficientQueries
{
    public static OriginalBattleRemakeCurrentFormationCoefficient? Read(GameState state, int physicalSlot,
        OriginalBattleUnitArmyStatKind kind)
    {
        int offset = kind switch
        {
            OriginalBattleUnitArmyStatKind.FieldAttack => 0x83,
            OriginalBattleUnitArmyStatKind.SiegeTroopAttack => 0x84,
            OriginalBattleUnitArmyStatKind.Defense => 0x86,
            OriginalBattleUnitArmyStatKind.WallAttack => 0x85,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        int? bytes = OriginalTacticRemakeCurrentFormations.ReadVectorByteCount(state, physicalSlot);
        byte? raw = OriginalTacticRemakeCurrentFormations.ReadRawByte(state, physicalSlot, offset);
        if (bytes is null || raw is null) return null;
        byte? encoded = (bytes.Value & ~3) == 32
            ? OriginalTacticRemakeCurrentFormations.ReadParameter(state, physicalSlot, offset) : null;
        if ((bytes.Value & ~3) == 32 && encoded is null) return null;
        return new(bytes.Value, raw.Value, encoded);
    }
}
