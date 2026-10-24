using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICityMerchantExecution
{
    public AreaAdministrationQuery<CityMerchantDraft> OpenCityMerchantDraft(GameState state, int cityId,
        NarrationVoiceRequests? voiceRequests = null)
    {
        try { return AreaAdministrationQuery<CityMerchantDraft>.Known(new(CreateCityMerchantSource(state, cityId, voiceRequests))); }
        catch (InvalidDataException missing) { return AreaAdministrationQuery<CityMerchantDraft>.Unknown(missing.Message); }
    }

    private CityMerchantSource CreateCityMerchantSource(GameState state, int cityId,
        NarrationVoiceRequests? voiceRequests = null)
    {
        CityDomesticParameterCatalog.RequireScenario(_scenario, Seed.Language);
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        if (!_baseCapacityInputs.BindCurrent(state, current)) throw CityMerchantSource.Missing("capacity_owner");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        return new(state, current, cityId, (at, id, dimension) => abilities.ResolveDimension(at, id, dimension),
            OfficerCard, DefaultHint, PrepareNarration);

        AreaAdministrationQuery<AreaAdministrationOfficerCard> OfficerCard(int id)
        {
            try
            {
                int physical = id == 0 ? 0 : current.ResolveOfficerSource(id)
                    ?? throw CityMerchantSource.Missing("card_Person_pointer");
                bool valid = id != 0 && CityMerchantSource.K(current.ReadPersonNativeValidity(physical), "card_Person_validity");
                var card = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                    Seed.Language, _baseCapacityInputs, _repository).ReadTransport(0, physical, valid);
                if (valid) card = card with { Traits = AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>
                    .Unknown("city-merchant.trait_context") };
                return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(card);
            }
            catch (InvalidDataException missing) { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(missing.Message); }
        }
        AreaAdministrationQuery<string> DefaultHint()
        {
            try { return AreaAdministrationQuery<string>.Known(FormatHistoryReport(state, current, 0x5A76,
                new(Base0: checked((ushort)cityId))).Text); }
            catch (InvalidDataException missing) { return AreaAdministrationQuery<string>.Unknown(missing.Message); }
        }
        AreaAdministrationQuery<CityDomesticNarration> PrepareNarration(IReadOnlyList<int> selected,
            CityMerchantMode mode, int amount, string locale, Func<int, int> random)
        {
            try
            {
                if (selected.Count == 0 || amount <= 0 || locale != Seed.Language) throw CityMerchantSource.Missing("narration_input");
                int ruler = ReadCityMerchantRuler(current, cityId);
                int speaker = selected[0];
                var people = new DeploymentNarrationCurrentPeople(state, current, abilities, speaker, ruler,
                    panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect),
                    cityArgument: checked((ushort)cityId), appearanceRepository: _repository,
                    appearanceScenario: _scenario, appearanceLanguage: Seed.Language, voiceRequests: voiceRequests);
                var arguments = new TransportNarrationCurrentContext(state, current, people, null, [amount]);
                int firstVoice = voiceRequests?.Pending.Count ?? 0;
                var text = new NarrationResourceMessages(_repository.LoadMessages(locale, MessageKind.Game).Messages)
                    .ResolveCity(arguments, random, mode == CityMerchantMode.Buy ? 0x5A64 : 0x5A6C,
                        executeVoiceRequest: voiceRequests is not null);
                var officer = state.Officers.GetValueOrDefault(speaker) ?? throw CityMerchantSource.Missing("narration_Person");
                ushort appearance = officer.PortraitAppearanceIndex ?? throw CityMerchantSource.Missing("narration_portrait");
                return AreaAdministrationQuery<CityDomesticNarration>.Known(new(speaker, ruler, checked((ushort)cityId),
                    officer.Name, appearance, ReadNarrationPortrait(state, current, _definitions, speaker, appearance),
                    text.RootMessageId, text.MessageId, text.Choices, text.Text, text.Pages)
                    { Voices = voiceRequests is null ? [] : Array.AsReadOnly(voiceRequests.Pending.Skip(firstVoice).ToArray()) });
            }
            catch (InvalidDataException missing) { return AreaAdministrationQuery<CityDomesticNarration>.Unknown(missing.Message); }
        }
    }

    private static int ReadCityMerchantRuler(SourceWorldContext current, int cityId)
    {
        var city = current.ReadCityTableReference(cityId) ?? throw CityMerchantSource.Missing("ruler_City");
        var district = current.ReadCityDistrictReference(city) ?? throw CityMerchantSource.Missing("ruler_District");
        if (!CityMerchantSource.K(current.ReadDistrictNativeValidity(district), "ruler_District_validity")) throw CityMerchantSource.Missing("ruler_invalid_District");
        byte rawForce = CityMerchantSource.K(current.ReadDistrictForceSlot(district), "ruler_District10");
        var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw CityMerchantSource.Missing("ruler_Force");
        if (!CityMerchantSource.K(current.ReadForceNativeValidity(force), "ruler_Force_validity")) throw CityMerchantSource.Missing("ruler_invalid_Force");
        ushort raw = current.ReadForceRecord(CityMerchantSource.K(force.PhysicalSlot, "ruler_Force_pointer"))?.RawRuler10
            ?? throw CityMerchantSource.Missing("ruler_Force10");
        var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw CityMerchantSource.Missing("ruler_Person");
        int physical = CityMerchantSource.K(person.PhysicalSlot, "ruler_Person_pointer");
        if (!CityMerchantSource.K(current.ReadPersonNativeValidity(physical), "ruler_Person_validity")) throw CityMerchantSource.Missing("ruler_invalid_Person");
        return CityMerchantSource.K(current.ReadPersonIdentity(physical), "ruler_identity");
    }

    CommandResult ICityMerchantExecution.Trade(GameState state, ForceState force, CityMerchantOrder order, CommandContext context)
    {
        try
        {
            var source = CreateCityMerchantSource(state, order.CityId);
            _ = ReadCityMerchantRuler(source.Current, order.CityId);
            if (!source.CanOpen()) return CommandResult.Reject("Merchant is not available in the current city.", "merchant.admission");
            if (order.OfficerIds.Count > CityMerchantSource.MaximumOfficers || order.OfficerIds.Any(id => !source.IsCandidate(id)))
                return CommandResult.Reject("Officer is not an available merchant executor.", "merchant.officer");
            var quote = source.Page(order.OfficerIds, order.Mode, order.Amount);
            if (quote.UnresolvedInput is not null) throw CityMerchantSource.Missing(quote.UnresolvedInput);
            if (quote.CanConfirm != true)
                return CommandResult.Reject("Merchant trade is not affordable or has already been used.", "merchant.unavailable");
            var current = source.Current;
            var working = state;
            int weight = 0;
            foreach (int id in order.OfficerIds) weight = unchecked(weight + source.Weight(working, source.Candidate(id)));
            if (weight <= 0) return CommandResult.Reject("Merchant executor capacity is empty.", "merchant.capacity");
            foreach (int id in order.OfficerIds)
            {
                var candidate = source.Candidate(id);
                if (!CityMerchantSource.K(current.ReadPersonNativeValidity(candidate.PhysicalSlot), "formal_Person_validity")) continue;
                ushort before = CityMerchantSource.K(current.ReadArmyReturnPersonFlags196(candidate.PhysicalSlot), "formal_Person_flags15E");
                ushort after = (ushort)(before | 1);
                current.WriteArmyReturnPersonFlags196(candidate.PhysicalSlot, after);
                var person = working.Officers[id];
                working = working with { Officers = new(working.Officers) { [id] = person with
                    { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(person.Behavior, after) } } };
                int requested = MerchantCvtt(((float)source.Weight(working, candidate) / (float)weight) *
                    (float)unchecked(order.Amount * CityMerchantSource.MeritRate * 10) / 1000f);
                var merit = _pendingMerit.TryApplyWithinArmyOperation(working, current, id, requested);
                if (merit.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
                    throw CityMerchantSource.Missing("formal_pending_merit1695C0");
                working = merit.State;
            }

            //1F10A0 posts Gold first; the City setter reads its real capacity.
            var city = working.Cities[order.CityId];
            int gold = CityMerchantSource.K(current.ReadBaseResource(source.City, BaseResourceKind.Gold), "formal_Gold");
            int price = order.Mode == CityMerchantMode.Buy
                ? city.MerchantFoodPerTenGold ?? throw CityMerchantSource.Missing("formal_C8")
                : unchecked((city.MerchantFoodPerTenGold ?? throw CityMerchantSource.Missing("formal_C8")) * CityMerchantSource.SellPricePercent) / 100;
            if (price <= 0) throw CityMerchantSource.Missing("formal_price_divisor");
            int goldRequest = order.Mode == CityMerchantMode.Sell ? unchecked(gold + unchecked(order.Amount * 10) / price)
                : unchecked(gold - unchecked(price + unchecked((order.Amount - 1) * 10)) / price);
            int goldAfter = Math.Clamp(goldRequest, 0, BasePostLoad.ReadGoldCapacity(source.City, current));
            current.SetBaseResource(source.City, BaseResourceKind.Gold, goldAfter);
            working = working with { Cities = new(working.Cities) { [city.Id] = city with { Gold = goldAfter } } };
            int food = CityMerchantSource.K(current.ReadBaseResource(source.City, BaseResourceKind.Supplies), "formal_Food");
            int foodRequest = unchecked(food + (order.Mode == CityMerchantMode.Buy ? order.Amount : -order.Amount));
            int foodAfter = Math.Clamp(foodRequest, 0, BasePostLoad.ReadSuppliesCapacity(source.City, current));
            current.SetBaseResource(source.City, BaseResourceKind.Supplies, foodAfter);
            working = working with { Cities = new(working.Cities) { [city.Id] = working.Cities[city.Id] with { Supplies = foodAfter } } };
            int district = CityMerchantSource.K(source.District.PhysicalSlot, "formal_District");
            int books = working.Districts[district].OrdersRemaining ?? throw CityMerchantSource.Missing("formal_Books");
            working = working with { Districts = new(working.Districts) { [district] = working.Districts[district] with
                { OrdersRemaining = Math.Clamp(unchecked(books - CityMerchantSource.CommandCost), 0, 200) } } };
            if (working.CampaignEventWorld?.CurrentUserIndex != -1)
                throw CityMerchantSource.Missing("formal_command_service31FD60");
            var capture = working.NativeBaseCaptureCurrent ?? throw CityMerchantSource.Missing("formal_C9_owner");
            byte flags = capture.Cities.GetValueOrDefault(city.Id)?.C9 ?? throw CityMerchantSource.Missing("formal_C9");
            working = working with { NativeBaseCaptureCurrent = capture with { Cities = new(capture.Cities)
                { [city.Id] = capture.Cities[city.Id] with { C9 = (byte)(flags | 4) } } } };
            current.SetCaptureCityByteC9(city.Id, (byte)(flags | 4));
            working = CompleteCityMerchantFoodWarning(working, source);
            working = AppendCityMerchantHistory(working, current, source, order);
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException missing) { return CommandResult.RuleUnavailable("rules.city_merchant", missing.Message); }
    }

    private GameState CompleteCityMerchantFoodWarning(GameState state, CityMerchantSource source)
    {
        var current = source.Current;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw CityMerchantSource.Missing("warning_player");
        int playerId = CityMerchantSource.K(current.ReadForcePhysicalSlot(player), "warning_player_identity");
        if (playerId == 0) return state;
        int force = CityMerchantSource.K(current.ReadBaseForceSlot(source.City), "warning_city_force");
        var ownerForce = current.ReadForceTableReference(force <= 51 ? force : 0)
            ?? throw CityMerchantSource.Missing("warning_Force_pointer");
        int ownerIdentity = CityMerchantSource.K(current.ReadForcePhysicalSlot(ownerForce), "warning_Force_identity");
        if (playerId != ownerIdentity) return state; //2338E0 compares resolved identities.
        if (!CityMerchantSource.K(current.ReadForceNativeValidity(player), "warning_player_validity")) return state;
        bool warning = false;
        current.CurrentPlayerForce = player;
        //233A00 compares the City's virtualB0 Force pointer with the current
        //player, independently of the District number or its leader.
        if (ownerForce == player)
        {
            var expense = CityCaptureFoodWarningSource.TryReadFoodExpense(state, current, source.City);
            int cost = expense.Value ?? throw CityMerchantSource.Missing(expense.UnresolvedOwner ?? "warning_expense22FE10");
            int food = CityMerchantSource.K(current.ReadBaseResource(source.City, BaseResourceKind.Supplies), "warning_food");
            if (unchecked(food - unchecked(cost * 3)) < 1)
            {
                var income = CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(state, current, source.City,
                    abilities: CreateArmyDestructionAbilities(current), sceneries: _landmarkDefinitions);
                int next = income.Value ?? throw CityMerchantSource.Missing(income.UnresolvedOwner ?? "warning_income22D9E0");
                warning = unchecked(food + next - unchecked(cost * 3)) < 1;
            }
        }
        var owner = state.NativeBaseCaptureCurrent ?? throw CityMerchantSource.Missing("warning_C9_owner");
        var city = owner.Cities[source.CityId];
        byte before = city.C9 ?? throw CityMerchantSource.Missing("warning_C9");
        current.SetCaptureCityByteC9(source.CityId, warning ? (byte)(before | 2) : (byte)(before & 0xFD));
        return state with { NativeBaseCaptureCurrent = owner with { Cities = new(owner.Cities)
            { [source.CityId] = city with { C9 = warning ? (byte)(before | 2) : (byte)(before & 0xFD) } } } };
    }

    private GameState AppendCityMerchantHistory(GameState state, SourceWorldContext current,
        CityMerchantSource source, CityMerchantOrder order)
    {
        int first = source.Candidate(order.OfficerIds[0]).PhysicalSlot;
        ushort person = CityMerchantSource.K(current.ReadPersonIdentity(first), "history_Person");
        var message = FormatHistoryReport(state, current, order.Mode == CityMerchantMode.Buy ? 0x5A74 : 0x5A75,
            new(Person0: person, Base0: checked((ushort)source.CityId), Integers: [order.Amount]));
        state = message.State;
        int district = CityMerchantSource.K(current.ReadPersonDistrictSlot(first), "history_Person_District");
        int day = Context(current).ReadCurrentSamplerWorld(state)?.Day450 ?? throw CityMerchantSource.Missing("history_day450");
        int hex = current.ReadBaseCurrentHexIndex(source.City);
        var result = new CampaignHistoryWriter(_repository, _scenario, Seed.Language).Write(state, current,
            new ReportCommand(0, district, hex, day, message.Text, CampaignHistoryWriter.Supplemental(state), person,
                MarkBit0: 1, MarkBit3: 1));
        if (!result.NeedsNotification || result.Written is not { } notice) return result.State;
        var pending = result.State.CampaignEffectRequests ?? throw CityMerchantSource.Missing("history_notification_owner");
        return result.State with { CampaignEffectRequests = pending.Append(CampaignEffectSource.CityMerchant,
            state.NativeClock?.UpdateOrdinal ?? throw CityMerchantSource.Missing("history_revision"),
            [new(null, null) { HistoryNotification = notice }]) };
    }

    private static int MerchantCvtt(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
}
