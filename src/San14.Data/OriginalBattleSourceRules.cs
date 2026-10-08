using San14.Core;

namespace San14.Data;

/// <summary>A disabled native attack branch is distinct from an unresolved damage input.</summary>
public sealed record OriginalBattleTroopStrikeSource(bool Executes, OriginalTroopDamageSourceInput? Input);
public sealed record OriginalBattleWallStrikeSource(bool Executes, OriginalWallDamageSourceInput? Input);

/// <summary>
/// Current parameters and both attack decisions must be resolved for this encounter.
/// Image initial values alone do not establish current mutable native parameters.
/// </summary>
public sealed record OriginalFieldBattleSourceContext(OriginalBattleDamageSourceParameters? CurrentParameters,
    OriginalBattleTroopStrikeSource? AttackerStrike, OriginalBattleTroopStrikeSource? DefenderStrike,
    ulong RandomState, string Revision);

/// <summary>
/// Troop and wall channels are separate. Captured is a separately resolved native
/// aftermath decision; null cannot be inferred from an empty garrison or broken wall.
/// </summary>
public sealed record OriginalSiegeBattleSourceContext(OriginalBattleDamageSourceParameters? CurrentParameters,
    OriginalBattleTroopStrikeSource? AttackerStrike, OriginalBattleTroopStrikeSource? GarrisonStrike,
    OriginalBattleWallStrikeSource? WallStrike, bool? Captured, ulong RandomState, string Revision);

/// <summary>
/// Supplies current final native queries and branch decisions. Implementations may
/// compose OriginalBattleUnitSourceMath; base formation/officer fields are not final stats.
/// Native random queries are explicit and return their next simulation state.
/// </summary>
public interface IOriginalBattleSourceContext
{
    OriginalFieldBattleSourceContext? ResolveField(GameState state, UnitState attacker,
        UnitState defender, int period);
    OriginalSiegeBattleSourceContext? ResolveSiege(GameState state, UnitState attacker,
        int locationId, int period);
}

/// <summary>
/// Applies the inspected native troop damage curve and wall damage helper through
/// the shared period battle boundary. It supplies no default final abilities,
/// permission, transport classification, traits, capture decision or random draws.
/// </summary>
public sealed class OriginalBattleSourceRules : IPlayableBattleRules
{
    private readonly IOriginalBattleSourceContext _context;

    public OriginalBattleSourceRules(IOriginalBattleSourceContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public PlayableBattleResolution<PlayableFieldCombatResult>? Field(GameState state,
        UnitState attacker, UnitState defender, int period)
    {
        OriginalFieldBattleSourceContext? current = _context.ResolveField(state, attacker, defender, period);
        if (current is null || string.IsNullOrWhiteSpace(current.Revision) ||
            !Known(current.AttackerStrike) || !Known(current.DefenderStrike)) return null;
        OriginalBattleDamageSourceParameters? parameters = current.CurrentParameters;
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
        OriginalSiegeBattleSourceContext? current = _context.ResolveSiege(state, attacker, locationId, period);
        if (current is null || string.IsNullOrWhiteSpace(current.Revision) ||
            !Known(current.AttackerStrike) || !Known(current.GarrisonStrike) ||
            current.WallStrike is not { } wall || (wall.Executes && wall.Input is null) ||
            current.Captured is not bool captured) return null;
        OriginalBattleDamageSourceParameters? parameters = current.CurrentParameters;
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
            wallLoss = OriginalBattleDamageSource.CalculateWallDamage(parameters!, wall.Input);
        }
        var damage = new PlayableSiegeResult(defenderLoss, attackerLoss, wallLoss,
            defenders - defenderLoss, attacker.Troops - attackerLoss, durability - wallLoss, captured);
        PlayableBattleValidation.Validate(attacker.Troops, defenders, durability, damage);
        return new(damage, current.RandomState);
    }

    private static bool Known(OriginalBattleTroopStrikeSource? strike) =>
        strike is not null && (!strike.Executes || strike.Input is not null);

    private static bool Current(OriginalBattleDamageSourceParameters? parameters) =>
        parameters is not null && !string.IsNullOrWhiteSpace(parameters.CurrentRevision);

    private static int TroopDamage(OriginalBattleDamageSourceParameters? parameters,
        OriginalBattleTroopStrikeSource strike, int attackerTroops, int targetTroops)
    {
        if (!strike.Executes)
        {
            if (strike.Input is not null)
                throw new InvalidDataException("A disabled original troop attack also supplied damage input.");
            return 0;
        }
        OriginalTroopDamageSourceInput input = strike.Input!;
        if (input.AttackerTroops != attackerTroops || input.TargetTroops != targetTroops)
            throw new InvalidDataException("Original troop damage input belongs to a different troop snapshot.");
        return OriginalBattleDamageSource.CalculateAppliedTroopLoss(parameters!, input);
    }
}
