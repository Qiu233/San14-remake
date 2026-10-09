using San14.Core;

namespace San14.Data;

/// <summary>A disabled native attack branch is distinct from an unresolved damage input.</summary>
public sealed record BattleTroopStrikeSource(bool Executes, TroopDamageSourceInput? Input);
public sealed record BattleWallStrikeSource(bool Executes, WallDamageSourceInput? Input);

/// <summary>
/// Current parameters and both attack decisions must be resolved for this encounter.
/// Image initial values alone do not establish current mutable native parameters.
/// </summary>
public sealed record FieldBattleSourceContext(BattleDamageSourceParameters? CurrentParameters,
    BattleTroopStrikeSource? AttackerStrike, BattleTroopStrikeSource? DefenderStrike,
    ulong RandomState, string Revision);

/// <summary>
/// Troop and wall channels are separate. Captured is a separately resolved native
/// aftermath decision; null cannot be inferred from an empty garrison or broken wall.
/// </summary>
public sealed record SiegeBattleSourceContext(BattleDamageSourceParameters? CurrentParameters,
    BattleTroopStrikeSource? AttackerStrike, BattleTroopStrikeSource? GarrisonStrike,
    BattleWallStrikeSource? WallStrike, bool? Captured, ulong RandomState, string Revision);

/// <summary>
/// Supplies current final native queries and branch decisions. Implementations may
/// compose OriginalBattleUnitSourceMath; base formation/officer fields are not final stats.
/// Native random queries are explicit and return their next simulation state.
/// </summary>
public interface IBattleSourceContext
{
    FieldBattleSourceContext? ResolveField(GameState state, UnitState attacker,
        UnitState defender, int period);
    SiegeBattleSourceContext? ResolveSiege(GameState state, UnitState attacker,
        int locationId, int period);
}

/// <summary>
/// Applies the inspected native troop damage curve and wall damage helper through
/// the shared period battle boundary. It supplies no default final abilities,
/// permission, transport classification, traits, capture decision or random draws.
/// </summary>
public sealed class BattleSourceRules : IPlayableBattleRules
{
    private readonly IBattleSourceContext _context;

    public BattleSourceRules(IBattleSourceContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public PlayableBattleResolution<PlayableFieldCombatResult>? Field(GameState state,
        UnitState attacker, UnitState defender, int period)
    {
        FieldBattleSourceContext? current = _context.ResolveField(state, attacker, defender, period);
        if (current is null || string.IsNullOrWhiteSpace(current.Revision) ||
            !Known(current.AttackerStrike) || !Known(current.DefenderStrike)) return null;
        BattleDamageSourceParameters? parameters = current.CurrentParameters;
        if ((current.AttackerStrike!.Executes || current.DefenderStrike!.Executes) && !Current(parameters)) return null;
        int defenderLoss = TroopDamage(parameters, current.AttackerStrike!, attacker.Troops, defender.Troops);
        int attackerLoss = TroopDamage(parameters, current.DefenderStrike!, defender.Troops, attacker.Troops);
        var damage = new PlayableFieldCombatResult(defenderLoss, attackerLoss,
            defender.Troops - defenderLoss, attacker.Troops - attackerLoss);
        PlayableBattleValidation.Validate(attacker.Troops, defender.Troops, damage);
        return new(damage, current.RandomState);
    }

    public PlayableBattleResolution<PlayableSiegeResult>? Siege(GameState state,
        UnitState attacker, int locationId, int period)
    {
        int defenders;
        int? durability;
        if (state.Cities.TryGetValue(locationId, out CityState? city))
            (defenders, durability) = (city.Troops, city.Durability);
        else if (state.Gates.TryGetValue(locationId, out GateState? gate))
            (defenders, durability) = (gate.Troops, gate.Durability);
        else throw new InvalidDataException("Original battle location is missing.");
        SiegeBattleSourceContext? current = _context.ResolveSiege(state, attacker, locationId, period);
        if (current is null || string.IsNullOrWhiteSpace(current.Revision) ||
            !Known(current.AttackerStrike) || !Known(current.GarrisonStrike) ||
            current.WallStrike is not { } wall || (wall.Executes && wall.Input is null) ||
            current.Captured is not bool captured) return null;
        BattleDamageSourceParameters? parameters = current.CurrentParameters;
        if ((current.AttackerStrike!.Executes || current.GarrisonStrike!.Executes || wall.Executes) &&
            !Current(parameters)) return null;
        if (!wall.Executes && wall.Input is not null)
            throw new InvalidDataException("A disabled original wall attack also supplied damage input.");
        //An older/unsupported source profile can still have an unknown wall.
        //A required native branch cannot invent that independent current stock.
        if (wall.Executes && durability is null) return null;
        int defenderLoss = TroopDamage(parameters, current.AttackerStrike!, attacker.Troops, defenders);
        int attackerLoss = TroopDamage(parameters, current.GarrisonStrike!, defenders, attacker.Troops);
        int wallLoss = 0;
        if (wall.Executes)
        {
            if (wall.Input!.CurrentDurability != durability)
                throw new InvalidDataException("Original wall damage input belongs to a different durability snapshot.");
            wallLoss = BattleDamageSource.CalculateWallDamage(parameters!, wall.Input);
        }
        var damage = new PlayableSiegeResult(defenderLoss, attackerLoss, wallLoss,
            defenders - defenderLoss, attacker.Troops - attackerLoss, durability - wallLoss, captured);
        PlayableBattleValidation.Validate(attacker.Troops, defenders, durability, damage);
        return new(damage, current.RandomState);
    }

    private static bool Known(BattleTroopStrikeSource? strike) =>
        strike is not null && (!strike.Executes || strike.Input is not null);

    private static bool Current(BattleDamageSourceParameters? parameters) =>
        parameters is not null && !string.IsNullOrWhiteSpace(parameters.CurrentRevision);

    private static int TroopDamage(BattleDamageSourceParameters? parameters,
        BattleTroopStrikeSource strike, int attackerTroops, int targetTroops)
    {
        if (!strike.Executes)
        {
            if (strike.Input is not null)
                throw new InvalidDataException("A disabled original troop attack also supplied damage input.");
            return 0;
        }
        TroopDamageSourceInput input = strike.Input!;
        if (input.AttackerTroops != attackerTroops || input.TargetTroops != targetTroops)
            throw new InvalidDataException("Original troop damage input belongs to a different troop snapshot.");
        return BattleDamageSource.CalculateAppliedTroopLoss(parameters!, input);
    }
}
