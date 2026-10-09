using San14.Core;

namespace San14.Data;

public sealed record BattleRemakeCurrentParameterField(string Key, int Value);

/// <summary>A bounded source seed for a newly allocated owned parameter state.
/// Defaults are project rules. InstallFresh never restores omitted save fields
/// or replaces any existing current parameter value.</summary>
public sealed class BattleRemakeCurrentParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source => DamageSource.Source;
    public BattleDamageSourceParameters DamageSource { get; }
    public BattleUnitSourceProfile SourceCodeProfile { get; }
    public IReadOnlyList<BattleRemakeCurrentParameterField> Fields { get; }
    public double CommonPowerExponent { get; }
    public float PercentDenominator { get; }
    public float One { get; }
    public float FlagNumerator { get; }
    public float FlagDenominator { get; }

    internal static string? FormationEffectKey(int effect) => effect switch
    {
        1 => SimulationParameterKeys.BattleAttributes.FormationEffect1Scale,
        2 => SimulationParameterKeys.BattleAttributes.FormationEffect2Scale,
        3 => SimulationParameterKeys.BattleAttributes.FormationEffect3Scale,
        4 => SimulationParameterKeys.BattleAttributes.FormationEffect4Scale,
        5 => SimulationParameterKeys.BattleAttributes.FormationEffect5Scale,
        6 => SimulationParameterKeys.BattleAttributes.FormationEffect6Scale,
        7 => SimulationParameterKeys.BattleAttributes.FormationEffect7Scale,
        8 => SimulationParameterKeys.BattleAttributes.FormationEffect8Scale,
        9 => SimulationParameterKeys.BattleAttributes.FormationEffect9Scale,
        12 => SimulationParameterKeys.BattleAttributes.FormationEffect12Scale,
        _ => null
    };

    private BattleRemakeCurrentParameterSeed(string language,
        BattleDamageSourceParameters damage,
        BattleRemakeCurrentParameterField[] fields,
        double power, float percent, float one, float flagNumerator, float flagDenominator)
    {
        Language = language; DamageSource = damage;
        SourceCodeProfile = BattleUnitSourceCodeSources.ForLanguage(language);
        Fields = Array.AsReadOnly(fields);
        CommonPowerExponent = power; PercentDenominator = percent; One = one;
        FlagNumerator = flagNumerator; FlagDenominator = flagDenominator;
    }

    public static BattleRemakeCurrentParameterSeed Load(GameResourceRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        BattleDamageSourceParameters damage = BattleDamageSource.LoadParameters(repository, language);
        BattleRemakeCurrentParameterField[] fields =
        [
            new(SimulationParameterKeys.BattleAttributes.FormationEffect12Scale, FormationEffect12Scale),
            new(SimulationParameterKeys.BattleAttributes.AttackAbilityLimit, AttackAbilityLimit),
            new(SimulationParameterKeys.BattleAttributes.SiegeAbilityLimit, SiegeAbilityLimit),
            new(SimulationParameterKeys.BattleAttributes.WallLeadershipDenominator, WallLeadershipDenominator),
            new(SimulationParameterKeys.BattleAttributes.WallTroopOffset, WallTroopOffset),
            new(SimulationParameterKeys.BattleAttributes.WallTroopDenominator, WallTroopDenominator),
            new(SimulationParameterKeys.BattleAttributes.WallAbilityLimit, WallAbilityLimit),
            new(SimulationParameterKeys.BattleAttributes.DefenseAbilityLimit, DefenseAbilityLimit),
            new(SimulationParameterKeys.BattleAttributes.AlternateMoraleOffset, AlternateMoraleOffset),
            new(SimulationParameterKeys.BattleAttributes.AlternateMoraleDivisor, AlternateMoraleDivisor),
            new(SimulationParameterKeys.BattleAttributes.AlternateFormationPercentScale, AlternateFormationPercentScale),
            new(SimulationParameterKeys.BattleAttributes.Formation14TerrainPercent, Formation14TerrainPercent),
            new(SimulationParameterKeys.BattleAttributes.AttackCoefficientScale, AttackCoefficientScale),
            new(SimulationParameterKeys.BattleAttributes.DefenseCoefficientScale, DefenseCoefficientScale),
            new(SimulationParameterKeys.BattleAttributes.BaseWallCoefficient, BaseWallCoefficient),
            new(SimulationParameterKeys.BattleAttributes.BaseAttackCoefficient, BaseAttackCoefficient),
            new(SimulationParameterKeys.BattleAttributes.ReservedBaseAttackSecondaryCoefficient, ReservedBaseAttackSecondaryCoefficient),
            new(SimulationParameterKeys.BattleAttributes.BaseDefenseCoefficient, BaseDefenseCoefficient),
            new(SimulationParameterKeys.BattleAttributes.ReservedBaseDefenseSecondaryCoefficient, ReservedBaseDefenseSecondaryCoefficient),
            new(SimulationParameterKeys.BattleAttributes.AttackAbilityStep, AttackAbilityStep),
            new(SimulationParameterKeys.BattleAttributes.SiegeAbilityStep, SiegeAbilityStep),
            new(SimulationParameterKeys.BattleAttributes.WallAbilityStep, WallAbilityStep),
            new(SimulationParameterKeys.BattleAttributes.DefenseAbilityStep, DefenseAbilityStep),
            new(SimulationParameterKeys.BattleAttributes.ProvokedAttackPercent, ProvokedAttackPercent),
            new(SimulationParameterKeys.BattleAttributes.ProvokedOtherTargetDefensePercent, ProvokedOtherTargetDefensePercent),
            new(damage.Fields[0].Key, damage.Fields[0].Value),
            new(damage.Fields[1].Key, damage.Fields[1].Value),
            new(damage.Fields[2].Key, damage.Fields[2].Value),
            new(damage.Fields[3].Key, damage.Fields[3].Value),
            new(damage.Fields[4].Key, damage.Fields[4].Value),
            new(damage.Fields[5].Key, damage.Fields[5].Value),
            new(damage.Fields[6].Key, damage.Fields[6].Value),
            new(damage.Fields[7].Key, damage.Fields[7].Value),
            new(SimulationParameterKeys.BattleAttributes.SiegeAbilityBonus, SiegeAbilityBonus),
            new(SimulationParameterKeys.BattleAttributes.WallAbilityBonus, WallAbilityBonus),
            new(SimulationParameterKeys.BattleAttributes.Formation7ForceFlagBonus, Formation7ForceFlagBonus),
            new(damage.Fields[8].Key, damage.Fields[8].Value),
            new(SimulationParameterKeys.BattleAttributes.ReservedAgeThreshold, ReservedAgeThreshold),
            new(damage.Fields[9].Key, damage.Fields[9].Value),
            new(damage.Fields[11].Key, damage.Fields[11].Value),
            new(damage.Fields[10].Key, damage.Fields[10].Value),
            new(SimulationParameterKeys.BattleAttributes.Formation6TerrainPercent, Formation6TerrainPercent),
            new(SimulationParameterKeys.BattleAttributes.CommonRootDivisor, CommonRootDivisor),
            new(SimulationParameterKeys.BattleAttributes.CommonLeadershipScale, CommonLeadershipScale),
            new(SimulationParameterKeys.BattleAttributes.CommonAdditive, CommonAdditive),
            new(SimulationParameterKeys.BattleAttributes.CommonDivisorNumerator, CommonDivisorNumerator),
            new(SimulationParameterKeys.BattleAttributes.CommonDivisorOffset, CommonDivisorOffset),
            new(SimulationParameterKeys.BattleAttributes.MoraleOffset, MoraleOffset),
            new(SimulationParameterKeys.BattleAttributes.MoraleDivisor, MoraleDivisor),
            new(SimulationParameterKeys.BattleAttributes.NormalFormationPercentScale, NormalFormationPercentScale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect1Scale, FormationEffect1Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect2Scale, FormationEffect2Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect3Scale, FormationEffect3Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect4Scale, FormationEffect4Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect5Scale, FormationEffect5Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect6Scale, FormationEffect6Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect7Scale, FormationEffect7Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect8Scale, FormationEffect8Scale),
            new(SimulationParameterKeys.BattleAttributes.FormationEffect9Scale, FormationEffect9Scale),
            new(SimulationParameterKeys.BattleAttributes.ReservedAgeTier1, ReservedAgeTier1),
            new(SimulationParameterKeys.BattleAttributes.ReservedAgeTier2, ReservedAgeTier2),
            new(SimulationParameterKeys.BattleAttributes.ReservedAgeTier3, ReservedAgeTier3),
            new(SimulationParameterKeys.BattleAttributes.ReservedAgeTier4, ReservedAgeTier4),
            new(SimulationParameterKeys.BattleAttributes.ReservedAgeTier5, ReservedAgeTier5),
        ];
        return new(language, damage, fields, CommonPowerDefault, PercentDenominatorDefault,
            OneDefault, FlagNumeratorDefault, FlagDenominatorDefault);
    }

    /// <summary>Append only before the first update and before Army allocation.
    /// A populated key of either numeric type rejects the entire installation.</summary>
    public GameState InstallFresh(GameState state, string revision)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(revision) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } clock ||
            clock.AnchorDate != state.Date || state.Units is null || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { Parameters: { } current } simulation ||
            current.Language != Language || !string.Equals(current.RuleVersion, Source.RuleVersion, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(current.Revision) || current.Int32Values is null || current.Float32Values is null)
            throw new InvalidDataException("Owned battle parameters require a fresh matching empty simulation owner.");
        foreach (BattleRemakeCurrentParameterField field in Fields)
        {
            string key = field.Key;
            if (current.Int32Values.Keys.Concat(current.Float32Values.Keys)
                .Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Owned battle parameter key already has a current value; installation cannot replace it.");
        }
        var integers = new Dictionary<string, int>(current.Int32Values, current.Int32Values.Comparer);
        foreach (BattleRemakeCurrentParameterField field in Fields)
            integers.Add(field.Key, field.Value);
        var parameters = current with
        {
            Revision = $"{current.Revision}|{revision}:battle-rule-seed",
            Int32Values = integers,
            Float32Values = new(current.Float32Values, current.Float32Values.Comparer)
        };
        GameState result = state with { NativeArmySimulation = simulation with { Parameters = parameters } };
        NativeSubdayStateValidation.Validate(result);
        return result;
    }

    private const double CommonPowerDefault = 2d;
    private const float PercentDenominatorDefault = 100f;
    private const float OneDefault = 1f;
    private const float FlagNumeratorDefault = 8f;
    private const float FlagDenominatorDefault = 10f;
    private const int CommonRootDivisor = 1;
    private const int CommonLeadershipScale = 2;
    private const int CommonAdditive = 200;
    private const int CommonDivisorNumerator = 10;
    private const int CommonDivisorOffset = 9;
    private const int MoraleOffset = 200;
    private const int MoraleDivisor = 300;
    private const int AlternateMoraleOffset = 75;
    private const int AlternateMoraleDivisor = 175;
    private const int NormalFormationPercentScale = 10;
    private const int AlternateFormationPercentScale = 5;
    private const int Formation6TerrainPercent = 150;
    private const int Formation14TerrainPercent = 150;
    private const int AttackCoefficientScale = 5;
    private const int AttackAbilityLimit = 10;
    private const int AttackAbilityStep = 5;
    private const int BaseAttackCoefficient = 34;
    private const int ReservedBaseAttackSecondaryCoefficient = 30;
    private const int ProvokedAttackPercent = 150;
    private const int SiegeAbilityLimit = 10;
    private const int SiegeAbilityStep = 5;
    private const int SiegeAbilityBonus = 3;
    private const int WallAbilityBonus = 5;
    private const int WallLeadershipDenominator = 100;
    private const int WallTroopOffset = 20000;
    private const int WallTroopDenominator = 10000;
    private const int WallAbilityLimit = 10;
    private const int WallAbilityStep = 5;
    private const int DefenseCoefficientScale = 5;
    private const int DefenseAbilityLimit = 10;
    private const int DefenseAbilityStep = 5;
    private const int BaseDefenseCoefficient = 20;
    private const int ReservedBaseDefenseSecondaryCoefficient = 16;
    private const int BaseWallCoefficient = 20;
    private const int Formation7ForceFlagBonus = 20;
    private const int ProvokedOtherTargetDefensePercent = 80;
    private const int ReservedAgeThreshold = 60;
    private const int ReservedAgeTier1 = 1;
    private const int ReservedAgeTier2 = 2;
    private const int ReservedAgeTier3 = 3;
    private const int ReservedAgeTier4 = 4;
    private const int ReservedAgeTier5 = 5;
    private const int FormationEffect1Scale = 1;
    private const int FormationEffect2Scale = 1;
    private const int FormationEffect3Scale = 1;
    private const int FormationEffect4Scale = 1;
    private const int FormationEffect5Scale = 1;
    private const int FormationEffect6Scale = 1;
    private const int FormationEffect7Scale = 1;
    private const int FormationEffect8Scale = 1;
    private const int FormationEffect9Scale = 1;
    private const int FormationEffect12Scale = 1;

}
