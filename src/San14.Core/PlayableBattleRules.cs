namespace San14.Core;

/// <summary>Simultaneous losses calculated from one encounter snapshot.</summary>
public readonly record struct PlayableFieldCombatResult(int DefenderLoss, int AttackerLoss,
    int RemainingDefenders, int RemainingAttackers)
{
    public string? AttackerStrikeVetoRuleId { get; init; }
    public string? DefenderStrikeVetoRuleId { get; init; }
}

/// <summary>A resolved battle explicitly returns the next simulation random state.</summary>
public readonly record struct PlayableBattleResolution<T>(T Damage, ulong RandomState);

/// <summary>
/// Battle arithmetic is supplied independently of movement and aftermath. Null means
/// that a required original query or branch is unresolved; it never means zero damage.
/// </summary>
public interface IPlayableBattleRules
{
    PlayableBattleResolution<PlayableFieldCombatResult>? Field(GameState state,
        UnitState attacker, UnitState defender, int period);
    PlayableBattleResolution<PlayableSiegeResult>? Siege(GameState state,
        UnitState attacker, int locationId, int period);
}

/// <summary>Explicit unknown arithmetic for a caller that has no native battle adapter.</summary>
public sealed class UnresolvedPlayableBattleRules : IPlayableBattleRules
{
    public static UnresolvedPlayableBattleRules Instance { get; } = new();
    private UnresolvedPlayableBattleRules() { }
    public PlayableBattleResolution<PlayableFieldCombatResult>? Field(GameState state,
        UnitState attacker, UnitState defender, int period) => null;
    public PlayableBattleResolution<PlayableSiegeResult>? Siege(GameState state,
        UnitState attacker, int locationId, int period) => null;
}

/// <summary>
/// Preserves the explicitly provisional skeleton's former troop fractions. These
/// fractions and the tactic percentage adapter are not original battle rules.
/// </summary>
public sealed class ProvisionalPlayableBattleRules : IPlayableBattleRules
{
    public static ProvisionalPlayableBattleRules Instance { get; } = new();
    private ProvisionalPlayableBattleRules() { }

    public PlayableBattleResolution<PlayableFieldCombatResult>? Field(GameState state,
        UnitState attacker, UnitState defender, int period)
    {
        bool attackerVeto = PlayableTacticCombat.NativeConfusionBlocksOrdinaryFieldAttack(attacker);
        bool defenderVeto = PlayableTacticCombat.NativeConfusionBlocksOrdinaryFieldAttack(defender);
        int defenderLoss = attackerVeto ? 0 : Math.Min(defender.Troops, PlayableTacticCombat.AdjustLoss(
            Math.Max(1, attacker.Troops / 5), attacker, defender, period));
        int attackerLoss = defenderVeto ? 0 : Math.Min(attacker.Troops, PlayableTacticCombat.AdjustLoss(
            Math.Max(1, defender.Troops / 5), defender, attacker, period));
        var damage = new PlayableFieldCombatResult(defenderLoss, attackerLoss,
            defender.Troops - defenderLoss, attacker.Troops - attackerLoss)
        {
            AttackerStrikeVetoRuleId = attackerVeto ? PlayableTacticCombat.NativeConfusionFieldAttackRuleId : null,
            DefenderStrikeVetoRuleId = defenderVeto ? PlayableTacticCombat.NativeConfusionFieldAttackRuleId : null
        };
        PlayableBattleValidation.Validate(attacker.Troops, defender.Troops, damage);
        return new(damage, state.RandomState);
    }

    public PlayableBattleResolution<PlayableSiegeResult>? Siege(GameState state,
        UnitState attacker, int locationId, int period)
    {
        PlayableSiegeResult damage;
        if (state.Cities.TryGetValue(locationId, out CityState? city))
            damage = PlayableSiegeCombat.Resolve(attacker.Troops, city.Troops,
                city.Durability, city.ForceId is null);
        else if (state.Gates.TryGetValue(locationId, out GateState? gate))
            damage = PlayableSiegeCombat.Resolve(attacker.Troops, gate.Troops,
                gate.Durability, gate.ForceId is null);
        else throw new InvalidDataException("Battle location is missing.");
        return new(damage, state.RandomState);
    }
}

/// <summary>Rejects inconsistent provider results before battle settlement mutates the world.</summary>
public static class PlayableBattleValidation
{
    public static void Validate(int attackerTroops, int defenderTroops,
        PlayableFieldCombatResult damage)
    {
        if (attackerTroops < 1 || defenderTroops < 1 ||
            !Conserved(attackerTroops, damage.AttackerLoss, damage.RemainingAttackers) ||
            !Conserved(defenderTroops, damage.DefenderLoss, damage.RemainingDefenders) ||
            !ValidVeto(damage.AttackerStrikeVetoRuleId, damage.DefenderLoss) ||
            !ValidVeto(damage.DefenderStrikeVetoRuleId, damage.AttackerLoss))
            throw new InvalidDataException("Field battle result does not conserve its input troops.");
    }

    public static void Validate(int attackerTroops, int defenderTroops, int? durability,
        PlayableSiegeResult damage)
    {
        if (attackerTroops < 1 || defenderTroops < 0 || durability < 0 ||
            !Conserved(attackerTroops, damage.AttackerLoss, damage.RemainingAttackers) ||
            !Conserved(defenderTroops, damage.DefenderLoss, damage.RemainingDefenders) ||
            damage.DurabilityLoss < 0 ||
            durability.HasValue != damage.RemainingDurability.HasValue ||
            (durability is int wall
                ? !Conserved(wall, damage.DurabilityLoss, damage.RemainingDurability!.Value)
                : damage.DurabilityLoss != 0) ||
            (damage.Captured && damage.RemainingAttackers == 0))
            throw new InvalidDataException("Siege result does not conserve its input troops or durability.");
    }

    private static bool Conserved(int input, int loss, int remaining) =>
        loss >= 0 && loss <= input && remaining == input - loss;

    private static bool ValidVeto(string? ruleId, int damage) =>
        ruleId is null || !string.IsNullOrWhiteSpace(ruleId) && damage == 0;
}
