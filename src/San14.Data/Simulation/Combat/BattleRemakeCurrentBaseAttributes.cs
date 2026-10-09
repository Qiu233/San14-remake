using San14.Core;

namespace San14.Data;

/// <summary>The current City receiver of TC2A9180/2B2570/2AA290. The
/// descriptor divisor is the final source byte14, not a troop-derived guess.
/// ObjectDurability14 is TC22C800's linked Object value, not Base capacity46.</summary>
public sealed record BattleRemakeCurrentBaseAttributeReceipt(
    float Common, float AttackAgainstArmy, float Defense,
    BaseReference Base, int NativeForceSlot, ushort Governor2E,
    int GovernorPhysicalSlot, int FullLeadership, int ReadyTroops3C,
    byte RawMorale44, byte Abnormal49, ushort ObjectLink4E,
    ushort ObjectDurability14, ushort CurrentHex, byte FinalDescriptorDivisor14,
    BattleRemakeCurrentRegionReceipt Region, string Revision);

/// <summary>Ordinary Force, valid-governor City statistics over the caller's
/// actual current definitions. No Army, formation, doctrine or Army status is
/// manufactured for a Base. Gate current owners and advanced morale settings
/// are outside this finite producer; omitted saved inputs remain unknown.</summary>
public sealed class BattleRemakeCurrentBaseAttributes
{
    private readonly BattleRemakeCurrentParameterSeed seed;
    private readonly SourceWorldContext definitions;
    private readonly IEffectiveOfficerAbilities abilities;
    private readonly BattleRemakeCurrentRegionFactor region;
    public string Revision { get; }

    public BattleRemakeCurrentBaseAttributes(BattleRemakeCurrentParameterSeed sourceSeed,
        SourceWorldContext currentDefinitions, IEffectiveOfficerAbilities effectiveAbilities,
        BattleRemakeCurrentRegionFactor currentRegionFactor, string revision)
    {
        seed = sourceSeed ?? throw new ArgumentNullException(nameof(sourceSeed));
        definitions = currentDefinitions ?? throw new ArgumentNullException(nameof(currentDefinitions));
        abilities = effectiveAbilities ?? throw new ArgumentNullException(nameof(effectiveAbilities));
        region = currentRegionFactor ?? throw new ArgumentNullException(nameof(currentRegionFactor));
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current Base attributes need a revision.");
        Revision = revision;
    }

    public BattleRemakeCurrentBaseAttributeReceipt? Resolve(GameState state,
        BaseReference actualCity, byte finalDescriptorDivisor14)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            parameters.Language != seed.Language || parameters.RuleVersion != seed.Source.RuleVersion ||
            definitions.StaticSource is not { } source ||
            state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256 ||
            actualCity.Kind != BaseKind.City || actualCity.PhysicalSlot is not >= 1 or > 51 ||
            !state.Cities.ContainsKey(actualCity.PhysicalSlot.Value) ||
            definitions.ReadBaseNativeValidity(actualCity) != true) return null;
        //TC2A935A performs IDIV with this byte; zero is not normalized here.
        if (finalDescriptorDivisor14 == 0) return null;
        if (definitions.ReadBaseForceSlot(actualCity) is not int forceSlot || forceSlot is < 1 or > 45 ||
            definitions.ReadForceTableReference(forceSlot) is not { } force ||
            definitions.ReadForceNativeValidity(force) != true ||
            definitions.ReadArmyReturnBaseGovernor2E(actualCity) is not ushort governor ||
            definitions.ReadPersonLookupReference(governor <= 6000 ? governor : 0)?.PhysicalSlot is not int person ||
            definitions.ReadPersonNativeValidity(person) != true ||
            definitions.ReadPersonIdentity(person) is not ushort personId ||
            abilities.Resolve(state, personId) is not { } full ||
            definitions.ReadBaseResource(actualCity, BaseResourceKind.Troops) is not int troops ||
            definitions.ReadBaseMorale44(actualCity) is not byte morale ||
            definitions.ReadCaptureBaseThreatFields(actualCity)?.Byte49 is not byte abnormal ||
            definitions.ReadBaseObjectLink(actualCity) is not ushort link || link > 51 ||
            definitions.ReadObjectTableReference(link <= 3000 ? link : 0) is not { } linked ||
            definitions.ReadObjectNativeValidity(linked) is not bool objectValid) return null;
        ushort? durability = objectValid ? definitions.ReadObjectRawField14(linked) : (ushort)6000;
        //Valid City requires the same linked Object; this reads its actual Hex10.
        ushort? hex = objectValid ? definitions.ReadObjectHexIndex(linked) : (ushort)48400;
        BattleRemakeCurrentRegionReceipt? reachedRegion = region.ResolveHexForceReceipt(state, forceSlot, hex);
        bool? alternate = BattleRemakeCurrentSettings.ReadAlternateMoraleGlobals(state);
        if (durability is null || hex is null || reachedRegion is null || alternate is null) return null;

        int? root = Parameter(SimulationParameterKeys.BattleAttributes.CommonRootDivisor), leadership = Parameter(SimulationParameterKeys.BattleAttributes.CommonLeadershipScale), additive = Parameter(SimulationParameterKeys.BattleAttributes.CommonAdditive),
            divisorNumerator = Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorNumerator), divisorOffset = Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorOffset),
            moraleOffset = Parameter(alternate.Value ? SimulationParameterKeys.BattleAttributes.AlternateMoraleOffset : SimulationParameterKeys.BattleAttributes.MoraleOffset),
            moraleDivisor = Parameter(alternate.Value ? SimulationParameterKeys.BattleAttributes.AlternateMoraleDivisor : SimulationParameterKeys.BattleAttributes.MoraleDivisor),
            attackCoefficient = Parameter(SimulationParameterKeys.BattleAttributes.BaseAttackCoefficient), defenseCoefficient = Parameter(SimulationParameterKeys.BattleAttributes.BaseDefenseCoefficient),
            attackScale = Parameter(SimulationParameterKeys.BattleAttributes.AttackCoefficientScale), defenseScale = Parameter(SimulationParameterKeys.BattleAttributes.DefenseCoefficientScale),
            wallCoefficient = Parameter(SimulationParameterKeys.BattleAttributes.BaseWallCoefficient);
        if (root is null || leadership is null || additive is null || divisorNumerator is null || divisorOffset is null ||
            moraleOffset is null || moraleDivisor is null || attackCoefficient is null || defenseCoefficient is null ||
            attackScale is null || defenseScale is null || wallCoefficient is null) return null;
        float common = BattleUnitSourceMath.CommonFactor(new(full.Leadership, troops,
            finalDescriptorDivisor14, morale, true, abnormal != 0, moraleOffset.Value, moraleDivisor.Value,
            reachedRegion.Factor, seed.One, seed.One),
            new(seed.CommonPowerExponent, root.Value, leadership.Value, additive.Value, seed.FlagNumerator,
                seed.FlagDenominator, divisorNumerator.Value, divisorOffset.Value));
        //TC2B26ED..2773 and2AA3E0..557 use distinct coefficient and scale operands.
        float attackScaleFactor = (float)attackScale.Value / seed.PercentDenominator;
        float attack = (float)attackCoefficient.Value * common;
        attack *= attackScaleFactor; attack *= seed.One; attack *= seed.One;
        float defenseScaleFactor = (float)defenseScale.Value / seed.PercentDenominator;
        float extra = (float)wallCoefficient.Value * (float)durability.Value;
        extra /= seed.PercentDenominator;
        float defense = (float)defenseCoefficient.Value * common;
        defense *= defenseScaleFactor; defense *= seed.One; defense *= seed.One;
        defense += extra;
        return new(common, attack, defense, actualCity, forceSlot, governor, person, full.Leadership,
            troops, morale, abnormal, link, durability.Value, hex.Value, finalDescriptorDivisor14,
            reachedRegion, $"{Revision}:{parameters.Revision}");

        int? Parameter(string key) => parameters.Int32Values.TryGetValue(key, out int value)
            ? value : null;
    }
}
