namespace San14.Core;

/// <summary>Losses and an explicit capture decision; troop and wall damage remain separate.</summary>
public readonly record struct PlayableSiegeResult(int DefenderLoss, int AttackerLoss,
    int DurabilityLoss, int RemainingDefenders, int RemainingAttackers,
    int? RemainingDurability, bool Captured);

/// <summary>Legacy provisional skeleton arithmetic. Native providers implement IPlayableBattleRules separately.</summary>
public static class PlayableSiegeCombat
{
    public static PlayableSiegeResult Resolve(int attackerTroops, int defenderTroops,
        int? durability, bool neutralBase)
    {
        if (attackerTroops < 1 || defenderTroops < 0 || durability < 0)
            throw new InvalidDataException("Siege input is invalid.");
        if (neutralBase && defenderTroops == 0)
            return new(0, 0, 0, 0, attackerTroops, durability, true);
        int defenderLoss = defenderTroops == 0 ? 0 :
            Math.Min(defenderTroops, Math.Max(1, attackerTroops / 5));
        int attackerLoss = defenderTroops == 0 ? 0 :
            Math.Min(attackerTroops, Math.Max(1, defenderTroops / 5));
        // Placeholder damage is deliberately isolated from garrison damage.
        int durabilityLoss = durability is > 0 && defenderTroops > 0
            ? Math.Min(durability.Value, Math.Max(1, attackerTroops / 10)) : 0;
        int remainingDefenders = defenderTroops - defenderLoss;
        int remainingAttackers = attackerTroops - attackerLoss;
        int? remainingDurability = durability - durabilityLoss;
        bool captured = remainingAttackers > 0 &&
            (remainingDefenders == 0 || remainingDurability == 0);
        return new(defenderLoss, attackerLoss, durabilityLoss, remainingDefenders,
            remainingAttackers, remainingDurability, captured);
    }
}
