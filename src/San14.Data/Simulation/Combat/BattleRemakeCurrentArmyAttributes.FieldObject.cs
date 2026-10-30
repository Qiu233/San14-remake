using San14.Core;

namespace San14.Data;

/// <summary>The target-only TC191920 chain. Attack is the actually reached
/// TC2B2810 siege troop attack, even though TC16A260 does not consume that
/// operand. No opponent Army or normal-combat descriptor is constructed.</summary>
public sealed record BattleRemakeCurrentFieldObjectTargetReceipt(
    float Common, float SiegeTroopAttack, float Defense,
    int ArmyId, int NativeArmySlot, int SelectedFormation, int FullLeadership,
    int FormationQueryPercent, int SiegeBonus, int DefenseBonus,
    int CommandEffect, int SiegeTrait6, int DefenseTrait7, int SharedTrait19,
    BattleRemakeCurrentCommonQueries CommonQueries, string Revision);

public sealed partial class BattleRemakeCurrentArmyAttributes
{
    /// <summary>Common(Army, null Base, divisor1, modifiers1), then the actual
    /// selected formation and siege/defense getters with modifiers1, alternate0.
    /// Every operand is read from this operation's current state. Missing
    /// policies, command effects or native transport facts remain unknown.</summary>
    public BattleRemakeCurrentFieldObjectTargetReceipt? ResolveFieldObjectTarget(
        GameState state, UnitState target, BattleRemakeCurrentCommonQueries reached)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            parameters.Language != source.Language || parameters.RuleVersion != source.Source.RuleVersion ||
            state.Units.GetValueOrDefault(target.Id) != target ||
            target.NativeArmyOrigin?.NativeSlotIndex is not >= 1 or > 500 ||
            queries.ReadArmyValidity(state, target) != true ||
            ArmyMoraleSourceAdapter.Read(target) is not byte morale ||
            target.NativeTacticRuntime is not { Status10: 1 or 2, StatModifiers4A.Length: 6 } native ||
            !ArmyConfigurationSourceAdapter.HasConfiguration(target) ||
            !state.Officers.ContainsKey(target.OfficerId) ||
            definitions.ResolveOfficerSource(target.OfficerId) is not int commander ||
            definitions.ReadPersonNativeValidity(commander) != true ||
            queries.ReadSelectedFormationPhysicalSlot(state, target) is not int formation || formation is < 1 or > 20 ||
            queries.ReadArmyForcePhysicalSlot(state, target) is not int force || force is < 1 or > 51 ||
            state.NativeArmySimulation.Forces.GetValueOrDefault(target.ForceId)?.Flags193 is not byte flags ||
            reached.FinalDescriptorDivisor14 != 1 || string.IsNullOrWhiteSpace(reached.Revision) ||
            target.Troops is < 0 or > ushort.MaxValue ||
            reached.CappedNativeMorale is < 0 or > byte.MaxValue || reached.CappedNativeMorale > morale ||
            !float.IsFinite(reached.RegionFactor) || !float.IsFinite(reached.NeighborFactor)) return null;

        //2A9180 consumes only effective Leadership. Do not make unrelated
        //Intelligence/Charm doctrine panels a prerequisite of Object damage.
        int? leadership = abilities is TacticRemakeCurrentAbilities current
            ? current.ResolveDimension(state, target.OfficerId, 0)
            : abilities.Resolve(state, target.OfficerId)?.Leadership;
        BattleUnitCommonParameters? commonParameters = CommonParameters(state);
        var moraleCoefficients = reached.MoraleCoefficients;
        float? terrain = TerrainFactor(state, formation, reached.NativeTerrainCode);
        if (leadership is null || commonParameters is null || moraleCoefficients.Divisor <= 0 || terrain is null)
            return null;
        float common = BattleUnitSourceMath.CommonFactor(new(leadership.Value, target.Troops, 1,
            reached.CappedNativeMorale, true, native.Abnormal23 != 0, moraleCoefficients.Offset, moraleCoefficients.Divisor,
            reached.RegionFactor, terrain.Value, reached.NeighborFactor), commonParameters);

        //Transport also traverses26A740; only its following type1 sum is
        //skipped. Require the actual trait query rather than inventing zeros.
        int[]? traits = TacticRemakeCurrentTraits.ArmyContributions(definitions, commander,
            ArmyTraitSourceAdapter.Encode(state, target));
        if (traits is not { Length: 127 }) return null;
        int? command = native.Status10 != 1 || traits[31] == 0 && traits[97] == 0
            ? 0 : queries.ReadCommandEffect(state, target);
        var siegeSource = queries.ReadFormationCoefficient(state, target, formation, BattleUnitArmyStatKind.SiegeTroopAttack);
        var defenseSource = queries.ReadFormationCoefficient(state, target, formation, BattleUnitArmyStatKind.Defense);
        int? percent = QueryPercent(state, target, formation);
        int? siegeBonus = formation == 5 && (flags & 2) != 0
            ? Parameter(state, SimulationParameterKeys.BattleAttributes.SiegeAbilityBonus) : 0;
        int? defenseBonus = formation == 7 && (flags & 0x20) != 0
            ? Parameter(state, SimulationParameterKeys.BattleAttributes.Formation7ForceFlagBonus) : 0;
        int? siegeLimit = Parameter(state, SimulationParameterKeys.BattleAttributes.SiegeAbilityLimit),
            siegeStep = Parameter(state, SimulationParameterKeys.BattleAttributes.SiegeAbilityStep),
            attackScale = Parameter(state, SimulationParameterKeys.BattleAttributes.AttackCoefficientScale);
        BattleUnitStatParameters? defenseParameters = StatParameters(state, true);
        if (command is null || siegeSource is null || defenseSource is null || percent is null ||
            siegeBonus is null || defenseBonus is null || siegeLimit is null || siegeStep is null ||
            attackScale is null || defenseParameters is null) return null;
        int siegeCoefficient = BattleUnitSourceMath.FormationCoefficient(BattleUnitArmyStatKind.SiegeTroopAttack,
            siegeSource.NativeContainerByteCount, siegeSource.StaticField, siegeSource.EncodedQueryResult);
        int defenseCoefficient = BattleUnitSourceMath.FormationCoefficient(BattleUnitArmyStatKind.Defense,
            defenseSource.NativeContainerByteCount, defenseSource.StaticField, defenseSource.EncodedQueryResult);
        float attack = BattleUnitSourceMath.ArmyStat(BattleUnitArmyStatKind.SiegeTroopAttack,
            new(common, siegeCoefficient, siegeBonus.Value, percent.Value, native.Status10 == 1,
                command.Value, traits[6], traits[19], true, native.StatModifiers4A[2], false, 0),
            new(siegeLimit.Value, siegeStep.Value, siegeStep.Value, attackScale.Value, source.PercentDenominator, source.One));
        float defense = BattleUnitSourceMath.ArmyStat(BattleUnitArmyStatKind.Defense,
            new(common, defenseCoefficient, defenseBonus.Value, percent.Value, native.Status10 == 1,
                command.Value, traits[7], traits[19], true, native.StatModifiers4A[3], false, 0), defenseParameters);
        //Neither function has the normal directional provocation multiplier.
        //Siege attack has no FieldAttack doctrine-bonus branch either.
        return new(common, attack, defense, target.Id, target.NativeArmyOrigin.NativeSlotIndex.Value,
            formation, leadership.Value, percent.Value, siegeBonus.Value, defenseBonus.Value,
            command.Value, traits[6], traits[7], traits[19], reached, $"{Revision}:{parameters.Revision}");
    }
}
