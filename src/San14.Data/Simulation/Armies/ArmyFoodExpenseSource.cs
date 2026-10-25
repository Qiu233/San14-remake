using San14.Core;

namespace San14.Data;

internal static class ArmyFoodExpenseSource
{
    internal static int Read(GameState state, SourceWorldContext current, UnitState army,
        IReadOnlyList<byte> terrainReductions, out ArmyLogisticsRouteResult route)
    {
        int physical = K(army.NativeArmyOrigin?.NativeSlotIndex, "receiver");
        route = new([], 0, 0, null);
        var receiver = new ArmyUnitReference(physical);
        if (!K(current.ReadArmyUnitNativeValidity(receiver), "269850_validity")) return 0;
        byte flags = army.NativeTacticRuntime?.Flags26 ?? throw Missing("269850_flags26");
        if ((flags & 2) == 0) return 0;
        //269850(mode0) always constructs this outer Hex chain before2B0010,
        //including an unsuccessful/empty route.2B0010 appends the returned
        //path in order; actual node exhaustion can shorten this count.
        var path = current.CreateTemporaryHexList() ?? throw Missing("269850_Hex_descriptor");
        try
        {
            route = ArmyLogisticsRouteSource.Read(state, current, army);
            if (route.UnresolvedOwner is string missing) throw new InvalidDataException(missing);
            if (route.Hexes is not { Length: > 0 } hexes) return 0;
            foreach (int raw in hexes)
            {
                var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("269850_path_Hex_pointer");
                current.AppendNearbyHexTemporary(path, hex);
            }
            int count = K(current.ReadTemporaryHexListCount(path), "269850_path_count");
            int steps = Math.Max(unchecked(count - 1), 1);
            int cost = Math.Max(K(route.Cost, "269850_route_cost"), 1);
            return ReadExpense(state, current, army, terrainReductions, receiver, steps, cost);
        }
        finally { current.ClearTemporaryHexList(path); current.ReleaseTemporaryHexList(path); }
    }

    //269A50, shared by the stock-posting Turn and the quote-only consumer.
    //The caller owns route/descriptors; no food, supply flag or Army is written.
    private static int ReadExpense(GameState state, SourceWorldContext current, UnitState army,
        IReadOnlyList<byte> terrainReductions, ArmyUnitReference receiver, int steps, int cost)
    {
        var resources = current.ReadArmyReturnResourceFields(receiver) ?? throw Missing("resources");
        int ready = K(resources.Ready16, "troops16");
        int wounded = K(resources.Wounded18, "wounded18");
        int basic = Math.Max(1, unchecked((ready + wounded) * P("troop-numerator")) /
            Divisor() / 10);
        ushort rawHex = K(current.ReadArmyUnitRawHex2A(receiver), "hex");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("hex_pointer");
        int at = K(hex.PhysicalSlot, "hex_receiver");
        long baseline = (long)basic * (100 - terrainReductions[at]) / 100;
        var force = current.ReadArmyUnitForceReference(receiver) ?? throw Missing("force");
        int forceId = K(current.ReadForcePhysicalSlot(force), "force_receiver");
        int policy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, 11, true).Value;
        long policyReduction = (long)policy * P("policy-scale") * baseline / 100;
        ushort commanderId = K(current.ReadArmyUnitCommanderId(receiver), "commander");
        int commander = K((current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
            ?? throw Missing("commander_pointer")).PhysicalSlot, "commander_receiver");
        int[] traits = TacticRemakeCurrentTraits.ArmyContributions(current, commander,
            current.ReadArmyReturnTraitFields(receiver)?.Traits64) ?? throw Missing("traits");
        long traitReduction = (long)P("trait-scale") * traits[20] * baseline / 100;
        byte surface = K(current.ReadHexSurfaceCode(hex), "terrain");
        int naval = K(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "naval_formation");
        long waterReduction = naval is >= 11 and <= 13 && surface is 14 or 24
            ? (long)K(current.ReadBaseForceModifier(force, 64), "water_policy64") * baseline / 100 : 0;
        var territoryForce = TerritoryForce(hex);
        int territoryId = K(current.ReadForcePhysicalSlot(territoryForce), "territory_force_receiver");
        long foreignAddition = 0;
        if (territoryId != forceId)
        {
            var row = territoryId == 0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelationsEA :
                state.NativeArmySimulation?.Forces.GetValueOrDefault(territoryId)?.RelationsEA;
            if (row is not { Length: 52 }) throw Missing("territory_relations");
            if ((row[forceId] & 127) == 0)
            {
                var player = current.ReadCurrentPlayerForce() ?? throw Missing("player");
                int playerId = K(current.ReadForcePhysicalSlot(player), "player_receiver");
                int factor = 1;
                if (playerId != 0 && playerId == forceId)
                {
                    int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "difficulty_selector");
                    int difficulty = K(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "difficulty");
                    if (difficulty == 3) factor = P("super-territory-percent") / 100;
                    else if (difficulty > 3) factor = P("top-territory-percent") / 100;
                }
                int modifier = territoryId == 0 && P("unassigned-territory-policy-enabled") == 0
                    ? 0 : K(current.ReadBaseForceModifier(territoryForce, 67), "foreign_policy67");
                foreignAddition = (long)modifier * factor * baseline / 100;
            }
        }
        var terrain = current.ReadSurfaceTypeTableReference(1) ?? throw Missing("baseline_terrain");
        int terrainCost = ArmyRouteTerrainCost.Read(state, current, terrain);
        float ratio = (float)cost / ((float)terrainCost * steps);
        float value = 1f - (1f - ratio) * P("terrain-correction") / 100f;
        value *= baseline;
        float growth = (float)P("duration-growth") * steps;
        growth += 1000f;
        value *= growth;
        value /= 1000f;
        value -= policyReduction;
        value -= traitReduction;
        value -= waterReduction;
        value += foreignAddition;
        int expense = Math.Max(0, Cvtt(value));
        if (K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector") == 0)
            expense = unchecked(K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 41), "advanced41") * expense) / 100;
        if (expense < 0) throw Missing("signed_advanced_credit");
        return expense;

        int P(string key) => ArmyFoodExpenseParameters.Read(state, key);
        int Divisor() => state.NativeArmySimulation?.Parameters.Int32Values is { } parameters &&
            parameters.GetValueOrDefault(CityCaptureFoodWarningSource.VersionKey) == 1 &&
            parameters.TryGetValue(SimulationParameterKeys.CityFoodExpense.GarrisonExpenseDivisor, out int divisor) && divisor != 0
                ? divisor : throw Missing("divisor");
        ForceReference TerritoryForce(HexReference cell)
        {
            int rawArea = K(current.ReadHexAreaSlot(cell), "area");
            var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0) ?? throw Missing("area_pointer");
            ushort representative = K(current.ReadAreaHexIndex(area), "area_hex");
            var pointer = current.ReadHexTableReference(representative < 48400 ? representative : 0) ?? throw Missing("area_hex_pointer");
            int rawForce = K(current.ReadHexForceSlot(pointer), "area_force");
            return current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("territory_force");
        }
    }
    private static int Cvtt(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f ? int.MinValue : (int)value;
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("food_upkeep.army." + name);
}
