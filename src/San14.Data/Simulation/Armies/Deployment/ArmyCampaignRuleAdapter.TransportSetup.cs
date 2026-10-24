using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public TransportSetupView QueryTransportSetup(GameState state, int sourceCityId, int? selectedOfficerId = null)
    {
        var missing = new List<string>();
        int? hex = null, troops = null, gold = null, food = null, books = null, selected = selectedOfficerId, naval = null;
        bool? returns = null; int? behavior = null;
        IReadOnlyList<DeploymentPersonCandidate>? people = null; TransportQuantityView? quantities = null;
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            var source = TransportSource(state, current, sourceCityId);
            hex = Read(() => TransportSourceHex(current, source));
            troops = Read(() => TK(current.ReadBaseResource(source, BaseResourceKind.Troops), "troop-stock"));
            gold = Read(() => TK(current.ReadBaseResource(source, BaseResourceKind.Gold), "gold-stock"));
            food = Read(() => TK(current.ReadBaseResource(source, BaseResourceKind.Supplies), "food-stock"));
            books = Read(() => TransportBooks(state, current, source));
            try
            {
                var result = BuildTransportPersonSelection(state, current, sourceCityId);
                var list = result.Candidates!.ToList();
                for (int index = 1; index < list.Count; index++)
                {
                    var row = list[index]; int previous = index - 1;
                    while (previous >= 0 && CompareTransportPersons(current, row, list[previous]) < 0)
                    { list[previous + 1] = list[previous]; previous--; }
                    list[previous + 1] = row;
                }
                people = list.AsReadOnly();
                selected ??= list.FirstOrDefault(item => item.DisabledReasons == DeploymentPersonDisabledReason.None)?.OfficerId;
            }
            catch (InvalidDataException error) { missing.Add(error.Message); }
            if (selected is int id)
            {
                naval = Read(() => TK(TransportArmyPacketSource.ReadDefaultNaval(state, current,
                    TransportPersonForce(current, id)), "naval-force"));
                int? initial = Read(() => TK(TransportSetupCurrentInputs.Read(state, TransportSetupCurrentInputs.DefaultTroopsKey), "default-troops"));
                if (initial is int initialTroops) quantities = ReadTransportQuantities(state, current, source, id, initialTroops, 0, 0);
            }
            int? returnValue = TransportSetupCurrentInputs.Read(state, TransportSetupCurrentInputs.ReturnOfficerPreferenceKey);
            if (returnValue is 0 or 1) returns = returnValue == 1; else missing.Add("transport.setup.return-preference");
            int? behaviorValue = TransportSetupCurrentInputs.Read(state, TransportSetupCurrentInputs.BehaviorPreferenceKey);
            if (behaviorValue is >= 0 and <= 2) behavior = behaviorValue; else missing.Add("transport.setup.behavior-preference");
        }
        catch (InvalidDataException error) { missing.Add(error.Message); }
        int? cost = ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.TransportCommandCost);
        if (cost is null) missing.Add("transport.setup.command-book-cost");
        return new(sourceCityId, hex, troops, gold, food, books, people, selected, naval,
            returns, behavior, quantities, missing.AsReadOnly())
        {
            TroopsMinimum = TransportSetupCurrentInputs.Read(state, TransportSetupCurrentInputs.DefaultTroopsKey),
            AmountStep = TransportSetupCurrentInputs.Read(state, TransportSetupCurrentInputs.AmountStepKey),
            CommandBookCost = cost, CommandBooksAfter = books.HasValue && cost.HasValue ? unchecked(books.Value - cost.Value) : null,
            DefaultHintMessageId = 0x57AA
        };
        int? Read(Func<int> read) { try { return read(); } catch (InvalidDataException error) { missing.Add(error.Message); return null; } }
    }

    public TransportQuantityView QueryTransportQuantities(GameState state, int sourceCityId,
        int officerId, int troops, int gold, int supplies)
    {
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            return ReadTransportQuantities(state, current, TransportSource(state, current, sourceCityId), officerId, troops, gold, supplies);
        }
        catch (InvalidDataException error) { return new(null, null, null, null, null, null, null, null, null, null, [error.Message]); }
    }

    public DeploymentPersonBasicListQuery QueryTransportPersonBasicList(GameState state, int sourceCityId)
        => QueryPersonBasicList(state, sourceCityId, 0, transport: true);

    /// <summary>Lazy Make-decision57AC query. The page may retain Expense
    /// while editing cargo, matching source4C0. No slider invokes this quote.</summary>
    public TransportFoodWarning QueryTransportFoodWarning(GameState state, int sourceCityId, int cargoFood)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var source = TransportSource(state, current, sourceCityId);
            var city = CurrentGateMapQueries.FindCity(source, current);
            var quote = CityCaptureFoodWarningSource.TryReadFoodExpense(state, current, city);
            int expense = quote.Value ?? throw new InvalidDataException(quote.UnresolvedOwner);
            //The existing helper's known leaf has no HomeArmy. The season
            //flag is reached only by its unresolved nonempty Army branch.
            int stock = TK(current.ReadBaseResource(source, BaseResourceKind.Supplies), "warning-current-food");
            return new(unchecked(stock - expense * 3 - cargoFood) < 1, expense, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryTransportOfficerCard(
        GameState state, int? officerId)
    {
        try
        {
            var current = CaptureOfficerCommandCapacityCurrent(state);
            int p = TK(current.ReadPersonLookupReference(officerId ?? 0)?.PhysicalSlot, "card-person-pointer");
            bool valid = TK(current.ReadPersonNativeValidity(p), "card-person-validity");
            var card = new AreaAdministrationOfficerCardSource(state, current, _definitions,
                _scenario, Seed.Language, _baseCapacityInputs, _repository);
            return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(card.ReadTransport(0, p, valid));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(error.Message); }
    }

    private DeploymentPersonSelectionQuery BuildTransportPersonSelection(GameState state, SourceWorldContext current, int sourceCityId)
    {
        var source = TransportSource(state, current, sourceCityId);
        ushort link = TK(current.ReadBaseObjectLink(source), "source-link");
        var people = new List<DeploymentPersonCandidate>();
        foreach (int p in EnumerateBaseResidents(current, link, TM))
        {
            if (TK(current.ReadPersonSecondLocation(p), "person-position") != link ||
                (TK(current.ReadArmyReturnPersonFlags196(p), "person-flags") & 1) != 0) continue;
            if ((current.ReadPersonLinkedEntity(p) ?? throw TM("person-associated-command")).Identity.HasValue) continue;
            int id = TK(current.ReadPersonIdentity(p), "person-identity");
            if (_definitions.ResolveOfficerSource(id) != p || !state.Officers.ContainsKey(id)) throw TM("person-owner");
            bool rumor = (TK(current.ReadPersonResetWord1FF(p), "person-rumor") & 255) != 0;
            people.Add(new(id, p, rumor ? DeploymentPersonDisabledReason.Rumor : DeploymentPersonDisabledReason.None));
        }
        return new(people.AsReadOnly(), 1, null);
    }

    private static int CompareTransportPersons(SourceWorldContext current, DeploymentPersonCandidate a, DeploymentPersonCandidate b)
    {
        //69DF30/6BE460 compares presence, not the magnitude of either effect.
        foreach (int effect in new[] { 94, 32 })
        {
            bool left = TK(TacticRemakeCurrentTraits.CommanderHasEffect(current, a.PhysicalSlot, effect), "sort-traits");
            bool right = TK(TacticRemakeCurrentTraits.CommanderHasEffect(current, b.PhysicalSlot, effect), "sort-traits");
            if (left != right) return left ? -1 : 1;
        }
        return a.OfficerId.CompareTo(b.OfficerId);
    }

    private static TransportQuantityView ReadTransportQuantities(GameState state, SourceWorldContext current,
        BaseReference source, int id, int troops, int gold, int food)
    {
        var missing = new List<string>();
        int? maxTroops = Limit(BaseResourceKind.Troops, TransportSetupCurrentInputs.TroopsBaseKey, TransportSetupCurrentInputs.TroopsMultiplierKey);
        int? maxGold = Limit(BaseResourceKind.Gold, TransportSetupCurrentInputs.GoldBaseKey, TransportSetupCurrentInputs.GoldMultiplierKey);
        int? maxFood = Limit(BaseResourceKind.Supplies, TransportSetupCurrentInputs.SuppliesBaseKey, TransportSetupCurrentInputs.SuppliesMultiplierKey);
        //780A70 only takes min(old,max). Nonnegative input is owned by the quantity editor.
        int? selectedTroops = maxTroops.HasValue ? Math.Min(troops, maxTroops.Value) : null;
        int? selectedGold = maxGold.HasValue ? Math.Min(gold, maxGold.Value) : null;
        int? selectedFood = maxFood.HasValue ? Math.Min(food, maxFood.Value) : null;
        bool? canDecide = null, personValid = null;
        try
        {
            int p = TK(current.ReadPersonLookupReference(id)?.PhysicalSlot, "selected-person");
            bool valid = TK(current.ReadPersonNativeValidity(p), "person-validity");
            personValid = valid;
            canDecide = !valid ? false : selectedTroops.HasValue && selectedGold.HasValue && selectedFood.HasValue
                ? selectedTroops != 0 || selectedGold != 0 || selectedFood != 0 : null;
        }
        catch (InvalidDataException error) { missing.Add(error.Message); }
        return new(maxTroops, maxGold, maxFood, selectedTroops, selectedGold, selectedFood,
            Remaining(BaseResourceKind.Troops, selectedTroops), Remaining(BaseResourceKind.Gold, selectedGold),
            Remaining(BaseResourceKind.Supplies, selectedFood), canDecide, missing.AsReadOnly())
            { IsPersonValid = personValid, LowFoodWarningUnresolvedInput = "transport.setup.low-food-current-22FE10-expense" };

        int? Limit(BaseResourceKind kind, string baseKey, string multiplierKey)
        {
            try
            {
                int stock = TK(current.ReadBaseResource(source, kind), "quantity-stock");
                int p = TK(current.ReadPersonLookupReference(id)?.PhysicalSlot, "quantity-person");
                if (!TK(current.ReadPersonNativeValidity(p), "quantity-person-validity")) return stock;
                var force = TransportPersonForce(current, id);
                int physical = TK(force.PhysicalSlot, "quantity-force");
                if (!state.Forces.TryGetValue(physical, out var forceOwner)) throw TM("quantity-force-owner");
                int policy = TK(BattleRemakeCurrentForcePolicies.ReadForceEffect(state, forceOwner, 12), "quantity-policy12");
                int trait = TK(TacticRemakeCurrentTraits.CommanderEffectMaximum(current, p, 32), "quantity-person-effect32");
                int baseValue = Parameter(baseKey), multiplier = Parameter(multiplierKey);
                int policyPart = unchecked(Parameter(TransportSetupCurrentInputs.PolicyCoefficientKey) * policy * baseValue * multiplier) / 100;
                int traitPart = unchecked(Parameter(TransportSetupCurrentInputs.TraitCoefficientKey) * trait * baseValue * multiplier) / 100;
                return Math.Min(stock, unchecked(baseValue + policyPart + traitPart));
            }
            catch (InvalidDataException error) { missing.Add(error.Message); return null; }
        }
        int Parameter(string key) => TK(TransportSetupCurrentInputs.Read(state, key), key);
        int? Remaining(BaseResourceKind kind, int? selected)
        {
            if (!selected.HasValue) return null;
            try { return unchecked(TK(current.ReadBaseResource(source, kind), "quantity-stock") - selected.Value); }
            catch (InvalidDataException error) { missing.Add(error.Message); return null; }
        }
    }

    private static BaseReference TransportSource(GameState state, SourceWorldContext current, int id)
    {
        if (!state.Cities.ContainsKey(id) || id is < 1 or > 51) throw TM("source-city");
        var source = current.ReadCityTableReference(id) ?? throw TM("source-city-pointer");
        if (!TK(current.ReadBaseNativeValidity(source), "source-city-validity")) throw TM("source-city-invalid");
        return source;
    }
    private static int TransportSourceHex(SourceWorldContext current, BaseReference source)
    {
        int link = TK(current.ReadBaseObjectLink(source), "source-object-link");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw TM("source-object");
        return TK(current.ReadObjectHexIndex(obj), "source-hex");
    }
    private static int TransportBooks(GameState state, SourceWorldContext current, BaseReference source)
    {
        if (current.ReadRegistryDescriptor(RegistryKind.District) is null) throw TM("primary-district-registry");
        int primary = TK(OwnedArmyLifecycleAuxiliaries.CurrentPlayerPrimaryDistrict(state, current), "player-primary-district");
        return state.Districts.GetValueOrDefault(primary)?.OrdersRemaining ?? throw TM("command-books");
    }
    private static ForceReference TransportPersonForce(SourceWorldContext current, int id)
    {
        int p = TK(current.ReadPersonLookupReference(id)?.PhysicalSlot, "person-pointer");
        return current.ReadPersonForce(p) ?? throw TM("person-force");
    }
    private static T TK<T>(T? value, string field) where T : struct => value ?? throw TM(field);
    private static InvalidDataException TM(string field) => new("transport.setup." + field);
}
