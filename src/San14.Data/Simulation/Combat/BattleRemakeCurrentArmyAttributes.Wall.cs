using San14.Core;

namespace San14.Data;

public sealed partial class BattleRemakeCurrentArmyAttributes
{
    /// <summary>Current2AA720 alone. Object attacks do not consume Common,
    /// an opponent, an accepted target count, morale or region multipliers.</summary>
    internal float? ResolveWallOffense(GameState state, UnitState army)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != source.Language ||
            parameters.RuleVersion != source.Source.RuleVersion || state.Units.GetValueOrDefault(army.Id) != army ||
            queries.ReadArmyValidity(state, army) != true ||
            army.NativeTacticRuntime is not { Status10: 1, StatModifiers4A.Length: 6 } native ||
            !ArmyConfigurationSourceAdapter.HasConfiguration(army) ||
            definitions.ResolveOfficerSource(army.OfficerId) is not int person ||
            definitions.ReadPersonNativeValidity(person) != true ||
            queries.ReadSelectedFormationPhysicalSlot(state, army) is not int formation || formation is < 1 or > 20 ||
            queries.ReadArmyForcePhysicalSlot(state, army) is not int force || force is < 1 or > 45 ||
            state.NativeArmySimulation.Forces.GetValueOrDefault(force)?.Flags193 is not byte flags)
            return null;
        int? leadership = abilities is TacticRemakeCurrentAbilities current
            ? current.ResolveDimension(state, army.OfficerId, 0)
            : abilities.Resolve(state, army.OfficerId)?.Leadership;
        var coefficient = queries.ReadFormationCoefficient(state, army, formation, BattleUnitArmyStatKind.WallAttack);
        int? percent = QueryPercent(state, army, formation);
        int? bonus = formation == 5 && (flags & 2) != 0
            ? Parameter(state, SimulationParameterKeys.BattleAttributes.WallAbilityBonus) : 0;
        int[]? traits = TacticRemakeCurrentTraits.ArmyContributions(definitions, person,
            ArmyTraitSourceAdapter.Encode(state, army));
        if (leadership is null || coefficient is null || percent is null || bonus is null || traits is not { Length: 127 })
            return null;
        int? command = traits[31] == 0 && traits[97] == 0 ? 0 : queries.ReadCommandEffect(state, army);
        int? limit = Parameter(state, SimulationParameterKeys.BattleAttributes.WallAbilityLimit),
            step = Parameter(state, SimulationParameterKeys.BattleAttributes.WallAbilityStep),
            leadershipDivisor = Parameter(state, SimulationParameterKeys.BattleAttributes.WallLeadershipDenominator),
            troopOffset = Parameter(state, SimulationParameterKeys.BattleAttributes.WallTroopOffset),
            troopDivisor = Parameter(state, SimulationParameterKeys.BattleAttributes.WallTroopDenominator);
        if (command is null || limit is null || step is null || leadershipDivisor is null || troopOffset is null || troopDivisor is null)
            return null;
        int encoded = BattleUnitSourceMath.FormationCoefficient(BattleUnitArmyStatKind.WallAttack,
            coefficient.NativeContainerByteCount, coefficient.StaticField, coefficient.EncodedQueryResult);
        return BattleUnitSourceMath.ArmyWallAttack(new(leadership.Value, army.Troops,
            encoded, bonus.Value, percent.Value, true, command.Value, traits[8], traits[19], true, native.StatModifiers4A[4]),
            new(troopOffset.Value, troopDivisor.Value, leadershipDivisor.Value,
                new(limit.Value, step.Value, step.Value, 0, source.PercentDenominator, source.One)));
    }
}
