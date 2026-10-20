using San14.Core;

namespace San14.Data;

public sealed record CityCaptureGarrisonFoodQuote(BaseReference Base, int Ready3C,
    int Wounded40, int BasicExpense, int GovernorPhysical, bool? GovernorBusy,
    int? GovernorEffect70, int Expense);
public sealed record CityCaptureFoodQuote(int? Value, CityCaptureGarrisonFoodQuote[] Garrisons,
    int[] HomeArmyPhysicalSlots, string? UnresolvedOwner)
{
    public ForcePolicyScratchReceipt[] QuotePolicyQueries { get; init; } = [];
}

/// <summary>The food+4 component consumed by 2FE2B0: quote-only 2DCA60,
/// followed by 2DCDB0's ordinary empty current HomeArmy leaf and the actual
/// quote tail pools with no current Area36 worker and CityCC trainer. Stock
/// posting, unused gold scalars and unresolved route/income writers remain separate.</summary>
public static class CityCaptureFoodWarningSource
{
    public const string VersionKey = "ce-city-capture-food-quote:version";
    private const int GarrisonFoodExpenseNumerator = 10;
    private const int GarrisonFoodExpenseDivisor = 30;
    private const int GovernorFoodExpenseEffectScale = 1;
    private const int CityRepairQuoteGoldFee = 100;
    private const int InitialPositiveFoodCorrection = 0;
    private const int InitialNegativeFoodCorrection = 0;
    private static readonly string[] ParameterKeys = [
        SimulationParameterKeys.CityFoodExpense.GarrisonExpenseNumerator,
        SimulationParameterKeys.CityFoodExpense.GarrisonExpenseDivisor,
        SimulationParameterKeys.CityFoodExpense.GovernorExpenseEffectScale,
        SimulationParameterKeys.CityFoodExpense.RepairGoldFee
    ];

    /// <summary>Explicit fresh installation of project food quote rules.
    /// Ordinary quote calls consume the current saved parameter bank.</summary>
    public static GameState InstallFreshInputs(GameState state, GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var owner = state.NativeArmySimulation?.Parameters ?? throw Missing("fresh_parameter_owner");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            owner.Int32Values.ContainsKey(VersionKey) || owner.Language is not ("SC" or "TC") ||
            owner.RuleVersion != SimulationRuleIdentity.ForLocale(owner.Language))
            throw Missing("fresh_unused_food_quote_owner");
        int[] rules = [GarrisonFoodExpenseNumerator, GarrisonFoodExpenseDivisor,
            GovernorFoodExpenseEffectScale, CityRepairQuoteGoldFee];
        var values = new Dictionary<string, int>(owner.Int32Values) { [VersionKey] = 1 };
        for (int i = 0; i < ParameterKeys.Length; i++) values.Add(ParameterKeys[i], rules[i]);
        values.Add(SimulationParameterKeys.CityFoodExpense.PositiveCorrection, InitialPositiveFoodCorrection);
        values.Add(SimulationParameterKeys.CityFoodExpense.NegativeCorrection, InitialNegativeFoodCorrection);
        return state with { NativeArmySimulation = state.NativeArmySimulation! with
            { Parameters = owner with { Int32Values = values } } };
    }

    public static CityCaptureFoodQuote TryReadFoodExpense(GameState state,
        SourceWorldContext current, BaseReference city)
    {
        var garrison = new List<CityCaptureGarrisonFoodQuote>(); var homeArmies = new List<int>();
        var policies = new List<ForcePolicyScratchReceipt>();
        try
        {
            ushort location = K(current.ReadBaseObjectLink(city), "expense_City4E");
            if (city.Kind != BaseKind.City || location > 51) return new(0, [], [], null);
            var force = Force(current, city);
            if (!K(current.ReadForceNativeValidity(force), "expense_Force_validity")) return new(0, [], [], null);
            int expense = QuoteGarrison(city);
            var garrisonGates = Temporary(current);
            try
            {
                var mapped = MappedGates(current, city).ToArray();
                foreach (var gate in mapped) AppendBase(current, garrisonGates, gate);
                foreach (var gate in Bases(current, garrisonGates)) expense = unchecked(expense + QuoteGarrison(gate));
            }
            finally { Release(current, garrisonGates); }

            //2DCDB0 allocates its two actual descriptors even for an empty
            //HomeArmy list; no current stock or logistics flag is written.
            var gates = Temporary(current); var armies = Temporary(current);
            try
            {
                foreach (var gate in MappedGates(current, city)) AppendBase(current, gates, gate);
                var admittedGates = Bases(current, gates).ToArray();
                foreach (var entry in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
                {
                    int physical = K(entry.PhysicalSlot, "expense_Army_pointer");
                    if (K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "expense_Army233560")) continue;
                    var slot = state.ReadArmyLifecycle()?.ReceiverSlots()[physical] ?? throw Missing("expense_Army_current_slot");
                    var image = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
                    byte flags = image?.NativeTacticRuntime?.Flags26 ?? throw Missing("expense_Army26");
                    if ((flags & 2) == 0) continue;
                    ushort commander = K(current.ReadArmyUnitCommanderId(new(physical)), "expense_Army12");
                    var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0) ?? throw Missing("expense_Commander_pointer");
                    int personPhysical = K(person.PhysicalSlot, "expense_Commander_identity");
                    var home = BaseLookup.Find(K(current.ReadPersonNativeValidity(personPhysical), "expense_Commander_validity")
                        ? K(current.ReadPersonFirstLocation(personPhysical), "expense_CommanderE2") : 0, current);
                    ushort homeLink = K(current.ReadBaseObjectLink(home), "expense_Home4E");
                    if (homeLink != location && !(homeLink is >= 52 and <= 61 && admittedGates.Contains(home))) continue;
                    var node = current.AllocateRegistryNode(new(armies.Identity)) ?? throw Missing("expense_Army_node");
                    if (node.Identity.HasValue)
                    { current.WriteRegistryNodePayload(node, new(RegistryKind.Army, physical)); homeArmies.Add(physical); }
                }
                if (homeArmies.Count != 0) throw Missing("Army269850_route2B0010_and269A50_current_producer");
            }
            finally { Release(current, armies); Release(current, gates); }
            ApplyEmptyAdministrationQuoteTail(state, current, city, policies, P(SimulationParameterKeys.CityFoodExpense.RepairGoldFee));
            return new(expense, garrison.ToArray(), [], null) { QuotePolicyQueries = policies.ToArray() };

            int QuoteGarrison(BaseReference target)
            {
                int ready = K(current.ReadBaseResource(target, BaseResourceKind.Troops), "garrison3C");
                int wounds = K(current.ReadArmyReturnBaseWounded40(target), "garrison40");
                int total = unchecked(ready + wounds);
                int basic = Divide(Divide(unchecked(total * P(SimulationParameterKeys.CityFoodExpense.GarrisonExpenseNumerator)), P(SimulationParameterKeys.CityFoodExpense.GarrisonExpenseDivisor)), 10);
                basic = Math.Max(basic, 1);
                ushort governor = K(current.ReadArmyReturnBaseGovernor2E(target), "garrison_Governor2E");
                var lookup = current.ReadPersonLookupReference(governor <= 6000 ? governor : 0) ?? throw Missing("garrison_Governor_pointer");
                int physical = K(lookup.PhysicalSlot, "garrison_Governor_identity");
                int adjustment = 0; bool? busy = null; int? effect = null;
                if (total != 0 && K(current.ReadPersonNativeValidity(physical), "garrison_Governor_validity"))
                {
                    busy = Busy(current, physical);
                    if (busy == false)
                    {
                        effect = TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 0x46)
                            ?? throw Missing("garrison_Governor_effect70");
                        if (effect > 0) adjustment = Math.Max(1, unchecked(Divide(unchecked(P(SimulationParameterKeys.CityFoodExpense.GovernorExpenseEffectScale) * basic * effect.Value), 100) + P(SimulationParameterKeys.CityFoodExpense.PositiveCorrection)));
                        else if (effect < 0) adjustment = Math.Min(-1, unchecked(Divide(unchecked(P(SimulationParameterKeys.CityFoodExpense.GovernorExpenseEffectScale) * basic * effect.Value), 100) - P(SimulationParameterKeys.CityFoodExpense.NegativeCorrection)));
                    }
                }
                int quote = total == 0 ? 0 : unchecked(basic - adjustment);
                garrison.Add(new(target, ready, wounds, basic, physical, busy, effect, quote)); return quote;
            }
            int P(string key) => state.NativeArmySimulation?.Parameters.Int32Values is { } values && values.GetValueOrDefault(VersionKey) == 1 &&
                values.TryGetValue(key, out int raw) ? raw : throw Missing($"parameter{key}");
        }
        catch (InvalidDataException missing) { return new(null, garrison.ToArray(), homeArmies.ToArray(), missing.Message)
            { QuotePolicyQueries = policies.ToArray() }; }
    }

    public static CityCaptureFoodQuote TryReadPredictedFoodIncome(GameState state,
        SourceWorldContext current, BaseReference city, NativeMapSeasonDate? operationThirdDate = null)
    {
        try
        {
            if (city.Kind != BaseKind.City || K(current.ReadBaseObjectLink(city), "income_City4E") > 51) return new(0, [], [], null);
            _ = operationThirdDate ?? state.NativeMapSeason?.ThirdDate ?? throw Missing("income_World34_date");
            var force = Force(current, city);
            if (!K(current.ReadForceNativeValidity(force), "income_Force_validity")) return new(0, [], [], null);
            //22D9E0 takes next month through32D690 and quote mode1. The
            //existing SC monthly settlement instead posts capped stock at a
            //month boundary; its ephemeral Area3E context is not a saved owner.
            throw Missing("income2BC080_current_Area4A_4C_4E_50_writers_and2BCD20_Area3E_income_owner");
        }
        catch (InvalidDataException missing) { return new(null, [], [], missing.Message); }
    }

    private static ForceReference Force(SourceWorldContext current, BaseReference city)
    {
        int raw = K(current.ReadBaseForceSlot(city), "City_B0");
        return current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("City_Force_pointer");
    }
    private static void ApplyEmptyAdministrationQuoteTail(GameState state, SourceWorldContext current,
        BaseReference city, List<ForcePolicyScratchReceipt> policies, int repairFee)
    {
        //2E7550(quote1): City v60=22C860->268230 reads actual41D690(32)
        //after the eager gold gate. This scratch cannot be omitted with the
        //unused gold scalar. Gate v90 forwards to its mapped City, but its
        //268230 fourth output has no policy scratch or persistent writer.
        if (K(current.ReadBaseNativeValidity(city), "quote_tail_repair_City_validity") &&
            K(current.ReadBaseResource(city, BaseResourceKind.Gold), "quote_tail_repair_City34") >= repairFee &&
            !current.ReadSpecialBaseCapacity(city))
            policies.Add(ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, Force(current, city), 32, true));
        //All further2E7550 quote1 branches are pure; actual stock/wall
        //writers require quote0. Its mapped Gate calls do not add scratch.
        //2DC710's third Gate list still traverses the shared arena.
        var gates = Temporary(current);
        try { foreach (var gate in MappedGates(current, city)) AppendBase(current, gates, gate); }
        finally { Release(current, gates); }

        //2955A0(quote1): the resident trait scan is pure, while41D690 has
        //real ForcePolicy scratch pools even for unused numeric results.
        //With no current worker, the actual Area temporary is empty and
        //local_res8 stays0; Force170 unsigned<0 cannot enter its reward.
        var areas = Temporary(current);
        try
        {
            int cityPhysical = K(city.PhysicalSlot, "quote_tail_City_identity");
            int force = K(current.ReadBaseForceSlot(city), "quote_tail_City_B0");
            foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("quote_tail_Area_managerB0"))
            {
                var area = entry ?? throw Missing("quote_tail_Area_pointer");
                if (K(current.ReadAreaCitySlot(area), "quote_tail_Area35") != cityPhysical ||
                    K(current.ReadAreaCurrentForceSlot(area), "quote_tail_Area_Force") != force) continue;
                int physical = K(area.PhysicalSlot, "quote_tail_Area_identity");
                if (K(current.ReadAssignmentArea36(physical), "quote_tail_Area36") != 0)
                    throw Missing("quote_tail2955A0_nonempty_Area36_level3C_3E_40_security54_selection_producer");
            }
            //Source24B550 is also called for the empty list. An empty
            //native stable sort has no node/allocation/state effect.
            var owner = Force(current, city);
            policies.Add(ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, owner, 0x1D, true));
            policies.Add(ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, owner, 0x1E, true));
        }
        finally { Release(current, areas); }
        //301170(quote1) calls266CD0 before its CC gate. Its unused scalar
        //still queries actual41D690(effect38), with its own scratch pool.
        policies.Add(ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, Force(current, city), 0x26, true));
        //Only a nonzero trainer can reach269320 and a further Gate temporary.
        if (K(current.ReadAssignmentCityCC(K(city.PhysicalSlot, "quote_tail_City_identity")), "quote_tail_CityCC") != 0)
            throw Missing("quote_tail301170_nonzero_CityCC_training_gate_and_Gate_temporary_producer");
    }
    private static bool Busy(SourceWorldContext current, int physical) =>
        PersonCurrentBusySource.Read(current, physical, field => Missing("Governor_" + field));
    private static IEnumerable<BaseReference> MappedGates(SourceWorldContext current, BaseReference city)
    {
        foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw Missing("expense_Gate_managerA0"))
        {
            var gate = entry ?? throw Missing("expense_Gate_payload");
            if (CurrentGateMapQueries.FindCity(gate, current) == city) yield return gate;
        }
    }
    private static HexListHandle Temporary(SourceWorldContext current) =>
        current.CreateNearbyTemporary();
    private static void Release(SourceWorldContext current, HexListHandle temporary) => current.ClearNearbyTemporary(temporary, true);
    private static void AppendBase(SourceWorldContext current, HexListHandle temporary, BaseReference target)
    {
        var node = current.AllocateBaseRegistryNode(new(temporary.Identity)) ?? throw Missing("expense_Gate_node");
        if (node.Identity.HasValue) current.WriteBaseRegistryNodePayload(node, target);
    }
    private static IEnumerable<BaseReference> Bases(SourceWorldContext current, HexListHandle temporary)
    {
        var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw Missing("Gate_temporary_head");
        while (node.Identity.HasValue)
        {
            yield return current.ReadBaseRegistryNodePayload(node) ?? throw Missing("Gate_temporary_payload");
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("Gate_temporary_next");
        }
    }
    private static int Divide(int a, int b)
    { if (b == 0 || a == int.MinValue && b == -1) throw Missing("source_IDIV"); return a / b; }
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new("current.CityCaptureFoodQuote_" + owner);
}
