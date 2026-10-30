using San14.Core;

namespace San14.Data;

/// <summary>The food output of the City's next-month quote (22D9E0/2BC080).
/// Uses current Area administration and the existing income arithmetic. This
/// does not perform monthly stock posting, population loss or recruitment;
/// the original's other output scalars and derived UI caches are not owners
/// of the food value.</summary>
public static class CityFoodIncomeSource
{
    //22A800's independent landmark/officer coefficient, retained in the
    //campaign parameter owner. Historical missing values remain unknown.
    public const string LandmarkOfficerEffectScaleKey = "simulation.landmark.area-officer-income-effect-scale";

    internal static GameState InstallFreshParameters(GameState state)
    {
        if (state.NativeArmySimulation is not { } native ||
            native.Parameters.Language is not ("SC" or "TC")) return state;
        if (native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(native.Parameters.Language) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 })
            throw new InvalidDataException("Landmark income parameters require a fresh CE campaign.");
        if (native.Parameters.Float32Values.ContainsKey(LandmarkOfficerEffectScaleKey))
            throw new InvalidDataException("Landmark income parameter has a different numeric owner.");
        if (native.Parameters.Int32Values.ContainsKey(LandmarkOfficerEffectScaleKey)) return state;
        // Independent SC and TC initial images both establish this value.
        // Install only at campaign creation; reads and old saves never fill it.
        var values = new Dictionary<string, int>(native.Parameters.Int32Values)
            { [LandmarkOfficerEffectScaleKey] = 2 };
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }

    internal static int Read(GameState state, SourceWorldContext current, BaseReference city,
        NativeMapSeasonDate date, IEffectiveOfficerAbilities? abilities, LoadedSceneries? sceneries)
    {
        int month = date.Month == 12 ? 1 : date.Month + 1;
        if (month is < 1 or > 12) throw Missing("next_month");
        int cityPhysical = K(city.PhysicalSlot, "City_pointer");
        int cityForce = K(current.ReadBaseForceSlot(city), "City_B0");
        var force = current.ReadForceTableReference(cityForce <= 51 ? cityForce : 0)
            ?? throw Missing("City_Force_pointer");
        int sum = 0;
        //CCityAreaIterator follows manager B0, preserving its order and
        //membership. Ownership is the separate representative-Hex test.
        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("Area_managerB0"))
        {
            var area = entry ?? throw Missing("Area_pointer");
            if (K(current.ReadAreaCitySlot(area), "Area35") != cityPhysical) continue;
            if (K(current.ReadAreaCurrentForceSlot(area), "Area_Force") != cityForce) continue;
            sum = unchecked(sum + ReadArea(state, current, area, city, force, month, abilities));
        }
        int forcePhysical = K(current.ReadForcePhysicalSlot(force), "Force_identity");
        if (forcePhysical != 51 && forcePhysical is not (>= 46 and <= 50))
        {
            int percent = ReadLandmarkPercent(state, current, city, sceneries);
            if (percent > 0 && sum > 0)
            {
                float value = sum;
                sum = Truncate((percent / 100f) * value + value);
            }
        }
        return sum;
    }

    internal static int ReadArea(GameState state, SourceWorldContext current, AreaReference area,
        BaseReference city, ForceReference force, int month, IEffectiveOfficerAbilities? abilities,
        int? personOverride = null)
    {
        if (!K(current.ReadBaseNativeValidity(city), "Area_City_validity")) return 0;
        if (current.ReadSpecialBaseCapacity(city))
            return unchecked(BasePostLoad.ReadSuppliesCapacity(city, current)
                - K(current.ReadBaseResource(city, BaseResourceKind.Supplies), "special_City_food"));

        int physical = K(area.PhysicalSlot, "Area_identity");
        int person = personOverride ?? PersonAreaAppointmentSource.Resolve(current,
            K(current.ReadAssignmentArea36(physical), "Area36"));
        var owner = state.Areas.GetValueOrDefault(physical) ?? throw Missing("Area_saved_owner");
        var basis = owner.EconomySource ?? throw Missing("Area_fixed_income");
        ushort rawBasis = U16(basis.RawField46, "Area46");
        ushort population = owner.NativeNormalAftermath?.Quantity52 ?? throw Missing("Area52");
        bool personValid = K(current.ReadPersonNativeValidity(person), "Person_validity");
        int task = personValid ? TaskPercent(current, person) : 0;
        int politics = 0;
        if (personValid)
        {
            int id = K(current.ReadPersonIdentity(person), "Person_identity");
            politics = abilities is TacticRemakeCurrentAbilities actual
                ? K(actual.ResolveDimension(state, id, 3), "Person_current_politics")
                : abilities?.Resolve(state, id)?.Politics ?? throw Missing("Person_current_politics");
        }
        ushort progress = U16(owner.Development?.Agriculture ?? throw Missing("Area_agriculture"), "Area3E");
        byte security = owner.Security is >= 0 and <= 255 ? (byte)owner.Security : throw Missing("Area54");
        byte bonus = K(current.ReadAssignmentArea6D(physical), "Area6D");
        var statistics = state.RegionStatistics?.Entries.GetValueOrDefault(physical)
            ?? throw Missing("Area_statistics");
        ushort terrain = U16(statistics.TerrainCellCount, "Area76");
        ushort owned = statistics.IsOwnedCountCurrent
            ? U16(statistics.OwnedCellCount, "Area68") : throw Missing("Area68_current");
        int trait = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x25), "Person_trait25");
        bool forceValid = K(current.ReadForceNativeValidity(force), "Force_validity");
        int panel = forceValid ? K(current.ReadBaseForceModifier(force, 0x34), "Force_panel34") : 0;
        byte? flags = month is >= 7 and <= 9 ? K(current.ReadAreaFlags38(area), "Area38") : null;
        int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        int difficulty = selector != 0
            ? K(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "difficulty") : 0;
        var player = current.ReadCurrentPlayerForce() ?? throw Missing("player_Force_pointer");
        int playerId = K(current.ReadForcePhysicalSlot(player), "player_Force_identity");
        bool own = playerId != 0 && playerId == K(current.ReadForcePhysicalSlot(force), "Force_identity");
        int rate = selector == 0 ? 100 : own
            ? difficulty switch { 0 => 200, 1 => 150, _ => 100 }
            : difficulty == 3 ? 150 : difficulty >= 4 ? 200 : 100;
        int? advanced = selector == 0
            ? K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, own ? 0x20 : 0x23), "advanced_food_percent") : null;
        return CeAreaIncomeMath.CalculateSupplies(new(rawBasis, population, security, progress, bonus,
            owned, terrain, politics, task, personValid, trait, panel, month, flags, rate, advanced, forceValid)).Supplies;
    }

    private static int TaskPercent(SourceWorldContext current, int person)
    {
        int effect = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7E), "Person_trait126");
        int? count = effect > 0 ? null : (current.ReadAssignmentPersonAreaVector(person)
            ?? throw Missing("Person_task_vector")).ActiveCount1E8;
        // Ordinary simulation is outside the temporary full-effort display scope.
        return CeEconomyRuntimeContext.ReadPersonTaskPercent(false, effect, count, null)
            ?? throw Missing("Person_task_count_fallback");
    }

    private static int ReadLandmarkPercent(GameState state, SourceWorldContext current,
        BaseReference city, LoadedSceneries? definitions)
    {
        if (!K(current.ReadBaseNativeValidity(city), "landmark_City_validity")) return 0;
        int cityId = K(city.PhysicalSlot, "landmark_City_identity");
        int cityForce = K(current.ReadBaseForceSlot(city), "landmark_City_Force");
        int sum = 0;
        for (int slot = 0; slot < 101; slot++)
        {
            //Hex44 is fixed scenery data, while flags47 and effect55/56 are
            //the existing saved construction/development owner.
            int physical;
            ushort rawHex;
            if (definitions is not null)
            {
                var row = definitions.Catalog.Sceneries[slot];
                physical = row.Slot;
                rawHex = row.RawHex44;
            }
            else
            {
                var scenery = current.ReadSceneryTableReference(slot) ?? throw Missing("Scenery_table");
                physical = K(scenery.PhysicalSlot, "Scenery_pointer");
                rawHex = K(current.ReadSceneryHexIndex(scenery), "Scenery44");
            }
            if (physical == 0) continue;
            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("Scenery_Hex_pointer");
            int areaRaw = K(current.ReadHexAreaSlot(hex), "Scenery_Hex12");
            if (areaRaw == 0) continue; //CScenery's native validity.
            var landmark = state.LandmarkInvestments?.GetValueOrDefault(physical)
                ?? throw Missing("Scenery_saved_development");
            if ((K(landmark.Flags, "Scenery47") & 1) == 0) continue;
            var area = current.ReadAreaTableReference(areaRaw < 501 ? areaRaw : 0) ?? throw Missing("Scenery_Area_pointer");
            int hexForce = K(current.ReadHexForceSlot(hex), "Scenery_Hex14");
            int mappedCity = K(current.ReadAreaCurrentForceSlot(area), "Scenery_Area_Force") == hexForce
                ? K(current.ReadAreaCitySlot(area), "Scenery_Area35") : 0;
            if (mappedCity == 0 || hexForce == 0 || mappedCity != cityId || hexForce != cityForce) continue;
            var development = landmark.Development ?? throw Missing("Scenery55_56");
            if (development.EffectKind != 2) continue;
            int effect = development.Effect;
            if (K(current.ReadAreaNativeValidity(area), "Scenery_Area_validity"))
            {
                ushort rawPerson = K(current.ReadAssignmentArea36(K(area.PhysicalSlot, "Scenery_Area_identity")), "Scenery_Area36");
                if (rawPerson != 0)
                {
                    int person = PersonAreaAppointmentSource.Resolve(current, rawPerson);
                    if (K(current.ReadPersonNativeValidity(person), "Scenery_Person_validity"))
                    {
                        //22CF60 returns the current District's raw Force byte,
                        //not the identity of the Force table lookup receiver.
                        if (K(current.ReadPersonOldForceSlot(person), "Scenery_Person_Force_byte") == hexForce)
                        {
                            int trait = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7B), "Scenery_Person_trait123");
                            if (trait != 0 && effect != 0)
                            {
                                var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("Scenery_parameter_owner");
                                if (!bank.Int32Values.TryGetValue(LandmarkOfficerEffectScaleKey, out int scale))
                                    throw Missing(LandmarkOfficerEffectScaleKey);
                                float extra = (float)unchecked(scale * trait) * effect / 100f;
                                effect = unchecked(effect + (extra > 0 && extra < 1 ? 1
                                    : extra < 0 && extra > -1 ? -1 : Truncate(extra)));
                            }
                        }
                    }
                }
            }
            sum = unchecked(sum + effect);
        }
        return sum;
    }

    private static ushort U16(int value, string field) => value is >= 0 and <= 65535
        ? (ushort)value : throw Missing(field);
    private static int Truncate(float value) => value >= -2147483648f && value < 2147483648f ? (int)value : int.MinValue;
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.CityFoodIncome." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal LoadedSceneries FoodIncomeSceneries => _landmarkDefinitions;
}
