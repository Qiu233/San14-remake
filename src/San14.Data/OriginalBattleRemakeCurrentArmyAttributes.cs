using San14.Core;

namespace San14.Data;

/// <summary>Actual reached common queries. FinalDescriptorDivisor14 is the
/// post169E20 result, including cache inheritance and active commander effect55.
/// RegionFactor uses Army.word152; terrain uses Army.word2A. Neither is inferred
/// from an unproved Core position/morale projection.</summary>
public sealed record OriginalBattleRemakeCurrentCommonQueries(byte FinalDescriptorDivisor14,
    int CappedNativeMorale, bool AlternateMoraleGlobals, float RegionFactor,
    byte NativeTerrainCode, float NeighborFactor, string Revision);
public sealed record OriginalBattleRemakeCurrentFormationCoefficient(long NativeContainerByteCount,
    byte StaticField, byte? EncodedQueryResult);

/// <summary>Remaining current source producers. Missing implementations return
/// null. Numeric source branches are selected by this builder; callers supply
/// actual query results, not displayed attack/defense or guessed terrain.
/// Formation coefficient receipts require a valid selected native definition;
/// an unknown/invalid definition branch is outside this finite Army contract.</summary>
public interface IOriginalBattleRemakeCurrentAttributeQueries
{
    bool? ReadArmyValidity(GameState state, UnitState army);
    int? ReadSelectedFormationPhysicalSlot(GameState state, UnitState army);
    OriginalBattleRemakeCurrentCommonQueries? ReadCommonQueries(GameState state, UnitState army, UnitState opponent);
    OriginalBattleRemakeCurrentFormationCoefficient? ReadFormationCoefficient(GameState state, UnitState army,
        int formationPhysicalSlot, OriginalBattleUnitArmyStatKind kind);
    int? ReadArmyForcePhysicalSlot(GameState state, UnitState army);
    int? ReadForceFormationEffect(GameState state, UnitState army, int nativeEffectIndex);
    int? ReadCommandEffect(GameState state, UnitState army);
    int? ReadActiveForcePolicy(GameState state, UnitState army, int nativeEffectIndex);
    bool? ReadDoctrineAttackEligibility(GameState state, UnitState army);
    ushort? ReadRawProvocationTargetWord3E(GameState state, UnitState army);
}

/// <summary>169E20→2A8FD0,16A940→2B23B0 and16A780→2AA0B0,
/// followed by the actual stage3/4 provocation multipliers. This finite builder
/// covers Army versus Army; Base statistics have a different source formula.</summary>
public sealed partial class OriginalBattleRemakeCurrentArmyAttributes
{
    private readonly OriginalBattleRemakeCurrentParameterSeed source;
    private readonly OriginalEventInitializationContext definitions;
    private readonly IEffectiveOfficerAbilities abilities;
    private readonly IOriginalBattleRemakeCurrentAttributeQueries queries;
    public string Revision { get; }
    public OriginalBattleRemakeCurrentArmyAttributes(OriginalBattleRemakeCurrentParameterSeed sourceSeed,
        OriginalEventInitializationContext currentDefinitions, IEffectiveOfficerAbilities effectiveAbilities,
        IOriginalBattleRemakeCurrentAttributeQueries currentQueries, string revision)
    {
        source = sourceSeed ?? throw new ArgumentNullException(nameof(sourceSeed));
        definitions = currentDefinitions ?? throw new ArgumentNullException(nameof(currentDefinitions));
        abilities = effectiveAbilities ?? throw new ArgumentNullException(nameof(effectiveAbilities));
        queries = currentQueries ?? throw new ArgumentNullException(nameof(currentQueries));
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current attributes need a producer revision.");
        Revision = revision;
    }
    public OriginalBattleRemakeCurrentDamageAttributes? Resolve(GameState state, UnitState caster, UnitState target)
    {
        if (state.NativeArmySimulation?.Parameters is not { } p || p.Language != source.Language ||
            p.RuleVersion != source.Source.RuleVersion || state.Units.GetValueOrDefault(caster.Id) != caster ||
            state.Units.GetValueOrDefault(target.Id) != target || caster.NativeArmyOrigin?.NativeSlotIndex is not >= 1 or > 500 ||
            target.NativeArmyOrigin?.NativeSlotIndex is not >= 1 or > 500) return null;
        Direction? a = Directional(state, caster, target), b = Directional(state, target, caster);
        if (a is null || b is null) return null;
        var result = new OriginalBattleRemakeCurrentDamageAttributes(a.Common, a.Attack, a.Defense, b.Common, b.Attack, b.Defense);
        result.Validate(); return result;
    }

    private sealed record Direction(float Common, float Attack, float Defense);
    private Direction? Directional(GameState state, UnitState army, UnitState opponent)
    {
        if (queries.ReadArmyValidity(state, army) != true || army.NativeMorale1A is not byte nativeMorale ||
            army.NativeTacticRuntime is not { StatModifiers4A.Length: 6 } native ||
            army.NativeArmyConfiguration is not { } configuration ||
            state.Officers.GetValueOrDefault(army.OfficerId)?.NativeAbilityRuntime is not { } commander ||
            definitions.ReadPersonIdentity(commander.PhysicalSlot) != army.OfficerId ||
            abilities.Resolve(state, army.OfficerId) is not { } full ||
            queries.ReadSelectedFormationPhysicalSlot(state, army) is not int formation || formation is < 1 or > 20 ||
            queries.ReadCommonQueries(state, army, opponent) is not { } reached || string.IsNullOrWhiteSpace(reached.Revision)) return null;
        if (army.Troops is < 0 or > ushort.MaxValue || reached.FinalDescriptorDivisor14 == 0 ||
            reached.CappedNativeMorale is < 0 or > byte.MaxValue || reached.CappedNativeMorale > nativeMorale || !float.IsFinite(reached.RegionFactor) ||
            !float.IsFinite(reached.NeighborFactor))
            throw new InvalidDataException("Current common factor query has unsupported native scalar fields.");
        OriginalBattleUnitCommonParameters? commonParameters = CommonParameters(state);
        int? moraleOffset = Parameter(state, reached.AlternateMoraleGlobals ? 0x1A4A050 : 0x1A4C6B0);
        int? moraleDivisor = Parameter(state, reached.AlternateMoraleGlobals ? 0x1A4A05C : 0x1A4C6BC);
        float? terrainFactor = TerrainFactor(state, formation, reached.NativeTerrainCode);
        if (commonParameters is null || moraleOffset is null || moraleDivisor is not > 0 || terrainFactor is null) return null;
        float common = OriginalBattleUnitSourceMath.CommonFactor(new(full.Leadership, army.Troops,
            reached.FinalDescriptorDivisor14, reached.CappedNativeMorale, true, native.Abnormal23 != 0,
            moraleOffset.Value, moraleDivisor.Value, reached.RegionFactor, terrainFactor.Value, reached.NeighborFactor), commonParameters);
        OriginalBattleRemakeCurrentFormationCoefficient? attackCoefficient = queries.ReadFormationCoefficient(state, army, formation,
            OriginalBattleUnitArmyStatKind.FieldAttack);
        OriginalBattleRemakeCurrentFormationCoefficient? defenseCoefficient = queries.ReadFormationCoefficient(state, army, formation,
            OriginalBattleUnitArmyStatKind.Defense);
        OriginalBattleUnitStatParameters? attackParameters = StatParameters(state, false), defenseParameters = StatParameters(state, true);
        int? percent = QueryPercent(state, army, formation);
        int? defenseBonus = state.NativeArmySimulation!.Forces.GetValueOrDefault(army.ForceId)?.Flags193 is byte forceFlags
            ? (forceFlags & 0x20) != 0 && formation == 7 ? Parameter(state, 0x1A4ABBC) : 0 : null;
        int? doctrine = ActivePolicy(state, army, 40); //native query0x28, decimal40
        bool? doctrineEligible = doctrine is > 0 ? queries.ReadDoctrineAttackEligibility(state, army) : false;
        if (attackCoefficient is null || defenseCoefficient is null || attackParameters is null || defenseParameters is null ||
            percent is null || defenseBonus is null || doctrine is null || doctrineEligible is null) return null;
        int command = 0, attackEffect = 0, defenseEffect = 0, shared = 0;
        if (native.Status10 == 1)
        {
            int[]? traits = OriginalTacticRemakeCurrentTraits.ArmyContributions(definitions, commander.PhysicalSlot, configuration.TraitSlots64);
            if (traits is not { Length: 127 }) return null;
            //All return branches of2A6200 are zero when both actual source
            //contributions31/97 are zero, independent of its earlier age read.
            int? currentCommand = traits[31] == 0 && traits[97] == 0 ? 0 : queries.ReadCommandEffect(state, army);
            if (currentCommand is null) return null;
            command = currentCommand.Value; attackEffect = traits[5]; defenseEffect = traits[7]; shared = traits[19];
        }
        int coefficientA = OriginalBattleUnitSourceMath.FormationCoefficient(OriginalBattleUnitArmyStatKind.FieldAttack,
            attackCoefficient.NativeContainerByteCount, attackCoefficient.StaticField, attackCoefficient.EncodedQueryResult);
        int coefficientD = OriginalBattleUnitSourceMath.FormationCoefficient(OriginalBattleUnitArmyStatKind.Defense,
            defenseCoefficient.NativeContainerByteCount, defenseCoefficient.StaticField, defenseCoefficient.EncodedQueryResult);
        float attack = OriginalBattleUnitSourceMath.ArmyStat(OriginalBattleUnitArmyStatKind.FieldAttack,
            new(common, coefficientA, 0, percent.Value, native.Status10 == 1, command, attackEffect, shared,
                true, native.StatModifiers4A[1], doctrineEligible.Value, doctrine.Value), attackParameters);
        float defense = OriginalBattleUnitSourceMath.ArmyStat(OriginalBattleUnitArmyStatKind.Defense,
            new(common, coefficientD, defenseBonus.Value, percent.Value, native.Status10 == 1, command, defenseEffect, shared,
                true, native.StatModifiers4A[3], false, 0), defenseParameters);
        if (native.Abnormal25 > 0)
        {
            ushort? rawTarget = queries.ReadRawProvocationTargetWord3E(state, army);
            if (rawTarget is null) return null;
            //The source compares the raw word only; it does not read ownerkind3C.
            if (rawTarget == opponent.NativeArmyOrigin!.NativeSlotIndex)
            {
                int? multiplier = Parameter(state, 0x1A4A930); if (multiplier is null) return null;
                attack *= (float)multiplier.Value / source.PercentDenominator;
            }
            else
            {
                int? multiplier = Parameter(state, 0x1A4A93C); if (multiplier is null) return null;
                defense *= (float)multiplier.Value / source.PercentDenominator;
            }
        }
        return new(common, attack, defense);
    }

    private int? QueryPercent(GameState state, UnitState army, int formation)
    {
        if (formation is >= 14 and <= 16 or 19 or 20 || formation is < 1 or > 20) return 100;
        if (queries.ReadArmyForcePhysicalSlot(state, army) is not int force) return null;
        if (force is >= 46 and <= 51) return 100;
        int? effect = formation switch { >= 1 and <= 7 => formation, >= 8 and <= 10 => 8,
            >= 11 and <= 13 => 9, >= 14 and <= 16 => 0, 17 or 18 => 12, _ => null };
        if (effect == 0) return 100;
        int? coefficientRva = effect switch { >= 1 and <= 8 => 0x1A4C6F8 + (effect.Value - 1) * 12,
            9 => 0x1A4C754, 12 => 0x1A49C34, _ => null };
        int? coefficient = coefficientRva is null ? null : Parameter(state, coefficientRva.Value);
        if (effect is null || coefficient is null || queries.ReadForceFormationEffect(state, army, effect.Value) is not int value) return null;
        return unchecked(100 + unchecked(value * coefficient.Value));
    }
    private int? ActivePolicy(GameState state, UnitState army, int effect)
    {
        var force = state.NativeArmySimulation!.Forces.GetValueOrDefault(army.ForceId);
        if (force?.ActivePolicyBitmap1CB is not { Length: 10 } active) return null;
        int index = effect - 1, mask = 1 << (index & 7);
        if ((active[index >> 3] & mask) == 0) return 0;
        if (force.SuppressedPolicyBitmap1D5 is not { Length: 10 } suppressed) return null;
        if ((suppressed[index >> 3] & mask) != 0) return 0;
        return queries.ReadActiveForcePolicy(state, army, effect);
    }
    private float? TerrainFactor(GameState state, int formation, byte terrain)
    {
        if (terrain is not (5 or 6 or >= 8 and <= 11) || formation is not (6 or 14)) return source.One;
        int? amount = Parameter(state, formation == 6 ? 0x1A4C668 : 0x1A4A074);
        return amount is null ? null : (float)amount.Value / source.PercentDenominator;
    }
    private OriginalBattleUnitCommonParameters? CommonParameters(GameState state)
    {
        int? root = Parameter(state, 0x1A4C674), leadership = Parameter(state, 0x1A4C680), additive = Parameter(state, 0x1A4C68C),
            numerator = Parameter(state, 0x1A4C698), offset = Parameter(state, 0x1A4C6A4);
        return root is null or 0 || leadership is null || additive is null || numerator is null || offset is null ? null :
            new(source.CommonPowerExponent, root.Value, leadership.Value, additive.Value, source.FlagNumerator,
                source.FlagDenominator, numerator.Value, offset.Value);
    }
    private OriginalBattleUnitStatParameters? StatParameters(GameState state, bool defense)
    {
        int? limit = Parameter(state, defense ? 0x1A49D04 : 0x1A49C78),
            step = Parameter(state, defense ? 0x1A4A924 : 0x1A4A900), scale = Parameter(state, defense ? 0x1A4A8AC : 0x1A4A8A0);
        return limit is null || step is null || scale is null ? null :
            new(limit.Value, step.Value, step.Value, scale.Value, source.PercentDenominator, source.One);
    }
    private static int? Parameter(GameState state, int rva) => state.NativeArmySimulation!.Parameters.Int32Values
        .TryGetValue(OriginalTacticRemakeParameterSeed.Key(rva), out int value) ? value : null;
}

public sealed record OriginalBattleRemakeCurrentCityDamageBranch(bool Present, OriginalAttackingCityDamageSourceInput? Input);
public interface IOriginalBattleRemakeCurrentDamageBodyQueries
{
    bool? ReadCurvePermission(GameState state, UnitState caster, UnitState target);
    OriginalBattleRemakeCurrentCityDamageBranch? ReadAttackingCityBranch(GameState state, UnitState caster);
    bool? ReadTargetHalfDamageTrait(GameState state, UnitState target);
}
/// <summary>Composes complete current attributes and actual late native body
/// queries into the receipt provider. No absent branch is inferred from null.</summary>
public sealed class OriginalBattleRemakeCurrentArmyDamageQueries(
    OriginalBattleRemakeCurrentArmyAttributes attributes, IOriginalBattleRemakeCurrentDamageBodyQueries body,
    string revision) : IOriginalBattleRemakeCurrentDamageQueries
{
    public OriginalBattleRemakeCurrentNormalAttackInput? ResolveNormalArmy(GameState state, UnitState caster, UnitState target)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current normal-attack queries need a revision.");
        if (target.NativeTacticRuntime is not { } native || body.ReadCurvePermission(state, caster, target) is not bool permit) return null;
        if (!permit) return new(caster.Id, target.Id, caster.Troops, target.Troops, false, null, native.Status10 == 2,
            null, false, state.RandomState, revision);
        var final = attributes.Resolve(state, caster, target);
        var city = body.ReadAttackingCityBranch(state, caster);
        bool? half = body.ReadTargetHalfDamageTrait(state, target);
        if (final is null || city is null) return null;
        if (city.Present != (city.Input is not null)) throw new InvalidDataException("Current attacking-city branch is inconsistent.");
        return new(caster.Id, target.Id, caster.Troops, target.Troops, true, final, native.Status10 == 2, city.Input,
            half, state.RandomState, revision);
    }
}
