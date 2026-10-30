using San14.Core;

namespace San14.Data;

/// <summary>The current City/Gate receiver of TC2A9180/2B2570/2AA290. The
/// descriptor divisor is the final source byte14, not a troop-derived guess.
/// ObjectDurability14 is TC22C800's linked Object value, not Base capacity46.</summary>
public sealed record BattleRemakeCurrentBaseAttributeReceipt(
    float Common, float AttackAgainstArmy, float Defense,
    BaseReference Base, int NativeForceSlot, ushort Governor2E,
    int GovernorPhysicalSlot, int? FullLeadership, int ReadyTroops3C,
    byte RawMorale44, byte Abnormal49, ushort ObjectLink4E,
    ushort ObjectDurability14, ushort CurrentHex, byte FinalDescriptorDivisor14,
    BattleRemakeCurrentRegionReceipt Region, string Revision);

/// <summary>Current Base statistics over the caller's actual definitions.
/// A known invalid governor skips the ability getter and contributes the
/// source literal zero leadership. City and Gate share the Base owner;
/// their attack/defense coefficients are selected by actual Base4E. No Army,
/// formation or doctrine is manufactured. Omitted saved inputs remain unknown.</summary>
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
            !(actualCity.Kind == BaseKind.City && actualCity.PhysicalSlot is >= 1 and <= 51 &&
                state.Cities.ContainsKey(actualCity.PhysicalSlot.Value) ||
              actualCity.Kind == BaseKind.Gate && actualCity.PhysicalSlot is >= 1 and <= 10 &&
                state.Gates.ContainsKey(actualCity.PhysicalSlot.Value + 51)) ||
            definitions.ReadBaseNativeValidity(actualCity) != true) return null;
        //TC2A935A performs IDIV with this byte; zero is not normalized here.
        if (finalDescriptorDivisor14 == 0) return null;
        //The Base branch of2A9180 uses its actual virtualB0 Force. Unlike
        //the Army commander's region shortcut, it has no ordinary-ID gate.
        if (definitions.ReadBaseForceSlot(actualCity) is not int forceSlot || forceSlot is < 1 or > 51 ||
            definitions.ReadForceTableReference(forceSlot) is not { } force ||
            definitions.ReadForceNativeValidity(force) != true ||
            definitions.ReadArmyReturnBaseGovernor2E(actualCity) is not ushort governor ||
            (governor <= 6000 ? definitions.ReadPersonLookupReference(governor)
                : definitions.ReadTransferPersonTableReference(0))?.PhysicalSlot is not int person ||
            definitions.ReadPersonNativeValidity(person) is not bool governorValid) return null;
        int leadershipInput;
        int? fullLeadership;
        if (governorValid)
        {
            if (definitions.ReadPersonIdentity(person) is not ushort personId ||
                abilities.Resolve(state, personId) is not { } full) return null;
            leadershipInput = full.Leadership;
            fullLeadership = full.Leadership;
        }
        else
        {
            //2A9180 initializes its leadership local to0 and calls22FE40
            //only for a native-valid22ACF0 receiver. Null records the actual
            //unread full ability; it is not an unknown or fake Person result.
            leadershipInput = 0;
            fullLeadership = null;
        }
        if (definitions.ReadBaseResource(actualCity, BaseResourceKind.Troops) is not int troops ||
            definitions.ReadBaseMorale44(actualCity) is not byte morale ||
            definitions.ReadCaptureBaseThreatFields(actualCity)?.Byte49 is not byte abnormal ||
            definitions.ReadBaseObjectLink(actualCity) is not ushort link || link > 61 ||
            definitions.ReadObjectTableReference(link <= 3000 ? link : 0) is not { } linked ||
            definitions.ReadObjectNativeValidity(linked) is not bool objectValid) return null;
        ushort? durability = objectValid ? definitions.ReadObjectRawField14(linked) : (ushort)6000;
        //The shared Base getter reads the same linked Object's actual Hex10.
        ushort? hex = objectValid ? definitions.ReadObjectHexIndex(linked) : (ushort)48400;
        BattleRemakeCurrentRegionReceipt? reachedRegion = region.ResolveHexForceReceipt(state, forceSlot, hex);
        var moraleCoefficients = BattleRemakeCurrentSettings.ReadCommonMoraleCoefficients(state);
        if (durability is null || hex is null || reachedRegion is null || moraleCoefficients is null) return null;

        int? root = Parameter(SimulationParameterKeys.BattleAttributes.CommonRootDivisor), leadership = Parameter(SimulationParameterKeys.BattleAttributes.CommonLeadershipScale), additive = Parameter(SimulationParameterKeys.BattleAttributes.CommonAdditive),
            divisorNumerator = Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorNumerator), divisorOffset = Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorOffset),
            attackCoefficient = Parameter(link <= 51 ? SimulationParameterKeys.BattleAttributes.BaseAttackCoefficient
                : SimulationParameterKeys.BattleAttributes.ReservedBaseAttackSecondaryCoefficient),
            defenseCoefficient = Parameter(link <= 51 ? SimulationParameterKeys.BattleAttributes.BaseDefenseCoefficient
                : SimulationParameterKeys.BattleAttributes.ReservedBaseDefenseSecondaryCoefficient),
            attackScale = Parameter(SimulationParameterKeys.BattleAttributes.AttackCoefficientScale), defenseScale = Parameter(SimulationParameterKeys.BattleAttributes.DefenseCoefficientScale),
            wallCoefficient = Parameter(SimulationParameterKeys.BattleAttributes.BaseWallCoefficient);
        if (root is null || leadership is null || additive is null || divisorNumerator is null || divisorOffset is null ||
            attackCoefficient is null || defenseCoefficient is null ||
            attackScale is null || defenseScale is null || wallCoefficient is null) return null;
        float common = BattleUnitSourceMath.CommonFactor(new(leadershipInput, troops,
            finalDescriptorDivisor14, morale, true, abnormal != 0, moraleCoefficients.Value.Offset, moraleCoefficients.Value.Divisor,
            reachedRegion.Factor, seed.One, seed.One),
            new(seed.CommonPowerExponent, root.Value, leadership.Value, additive.Value, seed.FlagNumerator,
                seed.FlagDenominator, divisorNumerator.Value, divisorOffset.Value));
        //TC2B26ED..2773 and2AA3E0..557 select the current City/Gate
        //coefficient by4E. Both branches retain their distinct scale operands;
        //the shared wall coefficient is independent of this selection.
        float attackScaleFactor = (float)attackScale.Value / seed.PercentDenominator;
        float attack = (float)attackCoefficient.Value * common;
        attack *= attackScaleFactor; attack *= seed.One; attack *= seed.One;
        float defenseScaleFactor = (float)defenseScale.Value / seed.PercentDenominator;
        float extra = (float)wallCoefficient.Value * (float)durability.Value;
        extra /= seed.PercentDenominator;
        float defense = (float)defenseCoefficient.Value * common;
        defense *= defenseScaleFactor; defense *= seed.One; defense *= seed.One;
        defense += extra;
        return new(common, attack, defense, actualCity, forceSlot, governor, person, fullLeadership,
            troops, morale, abnormal, link, durability.Value, hex.Value, finalDescriptorDivisor14,
            reachedRegion, $"{Revision}:{parameters.Revision}");

        int? Parameter(string key) => parameters.Int32Values.TryGetValue(key, out int value)
            ? value : null;
    }
}
