using San14.Core;

namespace San14.Data;

public sealed record OriginalBattleRemakeCurrentParameterField(int SemanticScRva,
    int PhysicalRva, int Value, string Sha256);
public sealed record OriginalBattleRemakeCurrentLiteral(string Name, int SemanticScRva,
    int PhysicalRva, int Bytes, string Sha256);

/// <summary>A bounded source seed for a newly allocated owned parameter state.
/// Defaults are project rules. InstallFresh never restores omitted save fields
/// or replaces any existing current parameter value.</summary>
public sealed class OriginalBattleRemakeCurrentParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source => DamageSource.Source;
    public OriginalBattleDamageSourceParameters DamageSource { get; }
    public OriginalBattleUnitSourceProfile SourceCodeProfile { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentLiteral> Literals { get; }
    public double CommonPowerExponent { get; }
    public float PercentDenominator { get; }
    public float One { get; }
    public float FlagNumerator { get; }
    public float FlagDenominator { get; }

    private OriginalBattleRemakeCurrentParameterSeed(string language,
        OriginalBattleDamageSourceParameters damage,
        OriginalBattleRemakeCurrentParameterField[] fields,
        OriginalBattleRemakeCurrentLiteral[] literals,
        double power, float percent, float one, float flagNumerator, float flagDenominator)
    {
        Language = language; DamageSource = damage;
        SourceCodeProfile = OriginalBattleUnitSourceCodeSources.ForLanguage(language);
        Fields = Array.AsReadOnly(fields); Literals = Array.AsReadOnly(literals);
        CommonPowerExponent = power; PercentDenominator = percent; One = one;
        FlagNumerator = flagNumerator; FlagDenominator = flagDenominator;
    }

    public static OriginalBattleRemakeCurrentParameterSeed Load(OriginalGameRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        OriginalBattleDamageSourceParameters damage = OriginalBattleDamageSource.LoadParameters(repository, language);
        int delta = language == "TC" ? 0x1030 : 0;
        var fields = damage.Fields.Select(field => new OriginalBattleRemakeCurrentParameterField(
            field.Rva + delta, field.Rva, field.Value, string.Empty)).Concat(
                MutableDefaults.Select(field => new OriginalBattleRemakeCurrentParameterField(
                    field.SemanticScRva, field.SemanticScRva - delta, field.Value, string.Empty)))
            .OrderBy(field => field.SemanticScRva).ToArray();
        int literalDelta = language == "TC" ? 0x1000 : 0;
        OriginalBattleRemakeCurrentLiteral[] literals =
        [
            new("commonPowerExponent", 0x1356750, 0x1356750 - literalDelta, 8, string.Empty),
            new("percentDenominator", 0x1344C4C, 0x1344C4C - literalDelta, 4, string.Empty),
            new("one", 0x1344B98, 0x1344B98 - literalDelta, 4, string.Empty),
            new("flagNumerator", 0x13B08F0, 0x13B08F0 - literalDelta, 4, string.Empty),
            new("flagDenominator", 0x1344C40, 0x1344C40 - literalDelta, 4, string.Empty),
        ];
        return new(language, damage, fields, literals, CommonPowerDefault, PercentDenominatorDefault,
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
        foreach (OriginalBattleRemakeCurrentParameterField field in Fields)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (current.Int32Values.Keys.Concat(current.Float32Values.Keys)
                .Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Owned battle parameter key already has a current value; installation cannot replace it.");
        }
        var integers = new Dictionary<string, int>(current.Int32Values, current.Int32Values.Comparer);
        foreach (OriginalBattleRemakeCurrentParameterField field in Fields)
            integers.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
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

    // Keys are stable save identifiers. Reserved entries preserve the existing seed
    // schema; their values are not used as executable addresses.
    private static readonly (int SemanticScRva, int Value)[] MutableDefaults =
    [
        (0x1A4C674, CommonRootDivisor),
        (0x1A4C680, CommonLeadershipScale),
        (0x1A4C68C, CommonAdditive),
        (0x1A4C698, CommonDivisorNumerator),
        (0x1A4C6A4, CommonDivisorOffset),
        (0x1A4C6B0, MoraleOffset),
        (0x1A4C6BC, MoraleDivisor),
        (0x1A4A050, AlternateMoraleOffset),
        (0x1A4A05C, AlternateMoraleDivisor),
        (0x1A4C6D4, NormalFormationPercentScale),
        (0x1A4A068, AlternateFormationPercentScale),
        (0x1A4C668, Formation6TerrainPercent),
        (0x1A4A074, Formation14TerrainPercent),
        (0x1A4A8A0, AttackCoefficientScale),
        (0x1A49C78, AttackAbilityLimit),
        (0x1A4A900, AttackAbilityStep),
        (0x1A4A8C4, BaseAttackCoefficient),
        (0x1A4A8D0, ReservedBaseAttackSecondaryCoefficient),
        (0x1A4A930, ProvokedAttackPercent),
        (0x1A49C98, SiegeAbilityLimit),
        (0x1A4A90C, SiegeAbilityStep),
        (0x1A4ABA4, SiegeAbilityBonus),
        (0x1A4ABB0, WallAbilityBonus),
        (0x1A49CB0, WallLeadershipDenominator),
        (0x1A49CBC, WallTroopOffset),
        (0x1A49CC8, WallTroopDenominator),
        (0x1A49CEC, WallAbilityLimit),
        (0x1A4A918, WallAbilityStep),
        (0x1A4A8AC, DefenseCoefficientScale),
        (0x1A49D04, DefenseAbilityLimit),
        (0x1A4A924, DefenseAbilityStep),
        (0x1A4A8DC, BaseDefenseCoefficient),
        (0x1A4A8E8, ReservedBaseDefenseSecondaryCoefficient),
        (0x1A4A8B8, BaseWallCoefficient),
        (0x1A4ABBC, Formation7ForceFlagBonus),
        (0x1A4A93C, ProvokedOtherTargetDefensePercent),
        (0x1A4B3D8, ReservedAgeThreshold),
        (0x1A4C774, ReservedAgeTier1),
        (0x1A4C780, ReservedAgeTier2),
        (0x1A4C788, ReservedAgeTier3),
        (0x1A4C794, ReservedAgeTier4),
        (0x1A4C7A0, ReservedAgeTier5),
        (0x1A4C6F8, FormationEffect1Scale),
        (0x1A4C704, FormationEffect2Scale),
        (0x1A4C710, FormationEffect3Scale),
        (0x1A4C71C, FormationEffect4Scale),
        (0x1A4C728, FormationEffect5Scale),
        (0x1A4C734, FormationEffect6Scale),
        (0x1A4C740, FormationEffect7Scale),
        (0x1A4C74C, FormationEffect8Scale),
        (0x1A4C754, FormationEffect9Scale),
        (0x1A49C34, FormationEffect12Scale),
    ];
}
