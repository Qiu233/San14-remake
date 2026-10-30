using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //69F020/6B0CA0 use the same three keys, with no invented sort key.
    internal IReadOnlyList<int> SortExchangeTargetsCurrent(GameState state, SourceWorldContext current,
        int own, IReadOnlyList<int> candidates)
        => candidates.Select(id => new
        {
            Id = id,
            Adjacent = (_captiveFreedomHomes ??= new(_scenario)).AreForceCitiesAdjacent(current, own, id),
            Cities = DNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current, ExchangeForce(current, id)), "exchange_target_Cities"),
            Relation = ReadDiplomacyRelationCurrent(state, current, own, id).Relation
        }).OrderByDescending(row => row.Adjacent).ThenBy(row => row.Cities).ThenByDescending(row => row.Relation)
            .ThenBy(row => row.Id).Select(row => row.Id).ToArray();

    internal IReadOnlyList<DiplomacyPreparationGiftChoice> ReadExchangeRequestedItemChoicesCurrent(
        GameState state, SourceWorldContext current, int own, int other)
    {
        var rows = new List<DiplomacyPreparationGiftChoice>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw DiplomacyNumericMissing("exchange_selector_Item_registry"))
        {
            int item = DNeed(entry?.PhysicalSlot, "exchange_selector_Item_pointer");
            if (!ExchangeRequestedOfferQualified(state, current, own, other, new(0, 0, item), false, true)) continue;
            var owner = state.Items?.GetValueOrDefault(item) ?? throw DiplomacyNumericMissing("exchange_selector_Item_owner");
            rows.Add(new(item, state.ItemDefinitions.GetValueOrDefault(owner.DefinitionId)?.Name
                ?? throw DiplomacyNumericMissing("exchange_selector_Item_name")));
        }
        return rows.AsReadOnly();
    }

    internal IReadOnlyList<DiplomacyPreparationGiftChoice> ReadExchangeRequestedAreaChoicesCurrent(
        GameState state, SourceWorldContext current, int own, int other)
    {
        var rows = new List<DiplomacyPreparationGiftChoice>();
        foreach (var area in current.ReadRegisteredAreaReferences() ?? throw DiplomacyNumericMissing("exchange_selector_Area_registry"))
        {
            int id = DNeed(area?.PhysicalSlot, "exchange_selector_Area_pointer");
            if (ReadDiplomacyAreaOfferEligibilityCurrent(state, current, id, other, own))
                rows.Add(new(id, _scenario.Snapshot.Areas[id].Name));
        }
        return rows.AsReadOnly();
    }

    internal int ReadExchangeRequestedResourceMaximumCurrent(GameState state, SourceWorldContext current,
        int other, bool supplies)
        => ReadDiplomacyBaseResourceCurrent(current, new DiplomacyCommandSource(state, current).Capital(other),
            supplies ? BaseResourceKind.Supplies : BaseResourceKind.Gold);

    //6E23E0: a friendly alliance or the actual two-member coalition flag.
    internal bool IsExchangeTargetCurrent(GameState state, SourceWorldContext current, int own, int other)
    {
        if (own == other || other is >= 46 and <= 50) return false;
        var qualification = new DiplomacyCommandSource(state, current);
        if (ReadDiplomacyRelationCurrent(state, current, own, other).Level <= 3) return false;
        if (qualification.TreatyMonths(own, other) == 0 &&
            !((ExchangeFlags12(state, own) & 2) != 0 && (ExchangeFlags12(state, other) & 2) != 0)) return false;
        //710860 ignores retiring/invalid envoys and tests the retained task.
        foreach (var task in state.PersonnelCommands?.Diplomacies?.Values ?? throw DiplomacyNumericMissing("exchange_task_owner"))
            if (task.ForceId == own && task.TargetForceId == other && !task.IsRetiring &&
                (task.NativeKind is >= 4 and <= 9 or 19) && task.MemberIds.Length != 0 &&
                DiplomacyContinuationAllowedCurrent(state, current, task).Allowed) return false;
        return true;
    }

    internal void RequireExchangeSubmissionCurrent(GameState state, SourceWorldContext current,
        PrepareDiplomaticOrder order)
    {
        if (!IsExchangeTargetCurrent(state, current, order.ForceId, order.TargetForceId))
            throw DiplomacyCommandSource.Invalid("exchange_target");
        var given = new DiplomacyOffer(order.GiftGold, order.GiftSupplies, order.GiftItemId, order.GiftAreaId);
        RequireExchangeSingleCondition(given);
        RequireExchangeSingleCondition(order.RequestedOffer);
        if (given.Gold != 0 && order.RequestedOffer.Gold != 0 ||
            given.Supplies != 0 && order.RequestedOffer.Supplies != 0)
            throw DiplomacyCommandSource.Invalid("exchange_same_currency");
        if (!ExchangeRequestedOfferQualified(state, current, order.ForceId, order.TargetForceId,
            order.RequestedOffer, true, true)) throw DiplomacyCommandSource.Invalid("exchange_requested_condition");
    }

    private static void RequireExchangeSingleCondition(DiplomacyOffer offer)
    {
        if (offer.Gold < 0 || offer.Supplies < 0 ||
            (offer.Gold > 0 ? 1 : 0) + (offer.Supplies > 0 ? 1 : 0) +
            (offer.ItemId is > 0 ? 1 : 0) + (offer.AreaId is > 0 ? 1 : 0) != 1)
            throw DiplomacyCommandSource.Invalid("exchange_single_condition");
        if (offer.ItemId is < 1 or > 201 || offer.AreaId is < 1 or > 500)
            throw DiplomacyCommandSource.Invalid("exchange_condition_identity");
    }

    //1E81E0 checks resources only at the arrival/update boundary (param2).
    //An invalid retained Item/Area is skipped, exactly as in the source.
    internal bool IsExchangeRequestedOfferStillQualifiedCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, bool checkResources)
        => ExchangeRequestedOfferQualified(state, current, task.ForceId, task.TargetForceId,
            task.RequestedOffer, checkResources, false);

    private bool ExchangeRequestedOfferQualified(GameState state, SourceWorldContext current,
        int own, int other, DiplomacyOffer offer, bool checkResources, bool selection)
    {
        var qualification = new DiplomacyCommandSource(state, current);
        var target = ExchangeForce(current, other);
        if (checkResources && (offer.Gold != 0 || offer.Supplies != 0))
        {
            if (!DNeed(current.ReadForceNativeValidity(target), "exchange_resource_Force_validity")) return false;
            var capital = qualification.Capital(other);
            if (!DNeed(current.ReadBaseNativeValidity(capital), "exchange_resource_capital_validity")) return false;
            if (offer.Gold != 0 && offer.Gold > ReadDiplomacyBaseResourceCurrent(current, capital, BaseResourceKind.Gold)) return false;
            if (offer.Supplies != 0 && offer.Supplies > ReadDiplomacyBaseResourceCurrent(current, capital, BaseResourceKind.Supplies)) return false;
        }
        if (offer.ItemId is int itemId)
        {
            var item = current.ReadRegistryTableReference(RegistryKind.Item, itemId)
                ?? throw DiplomacyNumericMissing("exchange_requested_Item_pointer");
            if (!DNeed(current.ReadRegistryObjectNativeValidity(item), "exchange_requested_Item_validity")) return !selection;
            var owner = state.Items?.GetValueOrDefault(itemId) ?? throw DiplomacyNumericMissing("exchange_requested_Item_owner");
            int ruler = DNeed(current.ReadPersonIdentity(qualification.Ruler(other)), "exchange_requested_ruler10");
            return DNeed(current.ReadCaptureItemOwner4A(itemId), "exchange_requested_Item4A") == ruler &&
                (DNeed(owner.Lifecycle?.RetainedFlags, "exchange_requested_Item4F") & 1) != 0 &&
                DNeed(current.ReadRegistryItemRawField50(item), "exchange_requested_Item50") == 1;
        }
        if (offer.AreaId is int areaId)
        {
            var area = current.ReadAreaTableReference(areaId) ?? throw DiplomacyNumericMissing("exchange_requested_Area_pointer");
            if (!DNeed(current.ReadAreaNativeValidity(area), "exchange_requested_Area_validity")) return !selection;
            if (selection) return ReadDiplomacyAreaOfferEligibilityCurrent(state, current, areaId, other, own);
            if (DNeed(current.ReadAreaCurrentForceSlot(area), "exchange_requested_Area_Force") != other) return false;
            byte mask = _scenario.Snapshot.Areas[areaId].RegionalNeighborMask;
            for (int edge = 0; edge < 8; edge++)
            {
                ushort raw = DNeed(current.ReadAreaNeighborSlot(area, edge), "exchange_requested_neighbor56");
                var neighbor = current.ReadAreaTableReference(raw < 501 ? raw : 0)
                    ?? throw DiplomacyNumericMissing("exchange_requested_neighbor_pointer");
                if (DNeed(current.ReadAreaNativeValidity(neighbor), "exchange_requested_neighbor_validity") &&
                    DNeed(current.ReadAreaCurrentForceSlot(neighbor), "exchange_requested_neighbor_Force") == own &&
                    (mask & (1 << edge)) != 0) return true;
            }
            return false;
        }
        return true;
    }

    internal DiplomacyTrialResult ResolveExchangeTrialCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer finalSenderOffer)
    {
        //8077D0 always performs3249B0 before the player sender's Cmd5C
        //override. Advice calls the raw query directly and never reads5C.
        var trial = new DiplomacyQuery(this, state, current, task).ExchangeTrial(finalSenderOffer, task.RequestedOffer);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw DiplomacyNumericMissing("exchange_trial_player");
        if (DNeed(player.PhysicalSlot, "exchange_trial_player60") != task.ForceId) return trial;
        int retained = DNeed(task.AdvisorOutcomeAtDispatch, "exchange_dispatch_advisor_outcome5C");
        return retained == -1 ? trial : trial with { Accepted = retained != 0 };
    }

    //812BB0 phase5 ->812EB0. The recipient has changed the sender's
    //payment; the requested condition and the dispatch prediction stay fixed.
    internal DiplomacyTrialResult ResolveExchangeCounterOfferCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer proposal)
        => new DiplomacyQuery(this, state, current, task).ExchangeCounterOffer(task.InitialOffer, proposal);

    //79BA10 ->2F39D0(kind19). Event-owned formatting may advance App32;
    //the sampler and equipment prediction themselves do not consume it.
    internal DiplomacyAdvisorResult ReadExchangeAdvisorCurrent(GameState state, SourceWorldContext current,
        int own, int executor, int other, DiplomacyOffer given, DiplomacyOffer requested)
        => ReadExchangeAdvisorCoreCurrent(state, current, own, executor, other, given, requested, out _);

    //1F5570's dispatch call evaluates fresh advice once. A fully accurate
    //advisor retains that same actual trial in Cmd5C; all other cases use -1.
    internal (GameState State, int Outcome) ReadExchangeAdvisorOutcomeAtDispatchCurrent(
        GameState state, SourceWorldContext current, PrepareDiplomaticOrder order)
    {
        var result = ReadExchangeAdvisorCoreCurrent(state, current, order.ForceId, order.ExecutorOfficerId,
            order.TargetForceId, new(order.GiftGold, order.GiftSupplies, order.GiftItemId, order.GiftAreaId),
            order.RequestedOffer, out int outcome);
        return (result.State, outcome);
    }

    private DiplomacyAdvisorResult ReadExchangeAdvisorCoreCurrent(GameState state, SourceWorldContext current,
        int own, int executor, int other, DiplomacyOffer given, DiplomacyOffer requested, out int outcomeAtDispatch)
    {
        outcomeAtDispatch = -1;
        var force = ExchangeForce(current, own);
        TransferPersonReference Advisor()
        {
            ushort identity = DNeed(current.ReadAssignmentForceAdvisor52(own), "exchange_advisor52");
            return (identity <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))
                ?? throw DiplomacyNumericMissing("exchange_advisor_pointer");
        }
        bool Valid(TransferPersonReference person) => person.PhysicalSlot is int physical &&
            DNeed(current.ReadPersonNativeValidity(physical), "exchange_advisor_validity");
        var advisor = Advisor();
        if (!Valid(advisor)) return new(state, null, null);
        int accuracy = 0;
        if (DNeed(current.ReadForceNativeValidity(force), "exchange_advisor_Force_validity"))
        {
            var fresh = Advisor();
            if (Valid(fresh))
            {
                int identity = DNeed(current.ReadPersonIdentity(DNeed(fresh.PhysicalSlot, "exchange_fresh_advisor")), "exchange_fresh_advisor10");
                var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
                int first = DNeed(abilities.ResolveDimension(state, identity, 2,
                    (forceId, effect) => _baseCapacityInputs.ReadPanelModifier(state, forceId, effect)), "exchange_advisor_INT1");
                int second = DNeed(abilities.ResolveDimension(state, identity, 2,
                    (forceId, effect) => _baseCapacityInputs.ReadPanelModifier(state, forceId, effect)), "exchange_advisor_INT2");
                accuracy = Math.Clamp(unchecked(60 + Math.Max(0, first - 70) + Math.Max(0, second - 90)), 0, 100);
            }
        }
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw DiplomacyNumericMissing("exchange_advisor_sampler");
        bool accurate = TacticEffectSourceSamplerMath.Sample(100, 10, executor, other, 0, 0, sampler) < accuracy;
        var q = new DiplomacyQuery(this, state, current, executor, other, 19, 0, null);
        bool accepted = q.ExchangeTrial(given, requested).Accepted;
        if (accuracy >= 100) outcomeAtDispatch = accepted ? 1 : 0;
        bool positive = accepted == accurate;
        int advisorId = DNeed(current.ReadPersonIdentity(DNeed(advisor.PhysicalSlot, "exchange_advisor_physical")), "exchange_advisor_ID");
        var operands = new HistoryMessageOperands(Person0: advisorId, Person1: q.Id(q.Ruler(q.Own)),
            Person2: executor, Person3: q.Id(q.Ruler(q.Other)), Area0: positive ? null : given.AreaId ?? 0);
        var formatted = FormatDiplomacyReportCurrent(state, current, positive ? 0x3346 : 0x3356, operands);
        return new(formatted.State, formatted.Text, string.IsNullOrEmpty(formatted.Text) ? null : advisorId)
        { PositivePrediction = positive };
    }

    private sealed partial class DiplomacyQuery
    {
        internal DiplomacyTrialResult ExchangeCounterOffer(DiplomacyOffer initial, DiplomacyOffer proposal)
        {
            if (NativeKind != 19) throw DiplomacyNumericMissing("exchange_counter_kind");
            DiplomacyTrialResult End(bool accepted, string branch, int probability = 0, int? sample = null)
                => new(accepted, probability, sample, "exchange-counter-" + branch);
            var capital = new DiplomacyCommandSource(State, Current).Capital(OwnOrdinal);
            int gold = ReadDiplomacyBaseResourceCurrent(Current, capital, BaseResourceKind.Gold);
            if (initial.Gold != 0) gold = unchecked(gold + initial.Gold);
            int food = ReadDiplomacyBaseResourceCurrent(Current, capital, BaseResourceKind.Supplies);
            if (initial.Supplies != 0) food = unchecked(food + initial.Supplies);
            int originalValue = OfferValue(initial), proposedValue = OfferValue(proposal);
            int relation = Relation(OwnOrdinal, OtherOrdinal);
            int troops = DNeed(Current.ReadBaseResource(capital, BaseResourceKind.Troops), "exchange_counter_capital3C");
            int ruler = Ruler(Other), politics = 0;
            if (Valid(ruler))
            {
                int ability = DNeed(new TacticRemakeCurrentAbilities(Current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)).ResolveDimension(State, Id(ruler), 3,
                        (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect)), "exchange_counter_ruler_POL");
                int trait = DNeed(TacticRemakeCurrentTraits.CommanderEffectSum(Current, ruler, 0x40), "exchange_counter_ruler_trait64");
                politics = unchecked(ability + unchecked(trait * ability) / 100);
            }
            if (originalValue < proposedValue)
            {
                if (proposal.Gold != 0 && unchecked(gold - proposal.Gold) <= 2000 ||
                    proposal.Supplies != 0 && unchecked(food - proposal.Supplies) <= unchecked(troops + 20000))
                    return End(false, "reserve");
                if (proposedValue > unchecked(unchecked(relation / 5 + 100) * originalValue) / 100)
                    return End(false, "increase-limit");
            }
            else if (proposal.Gold != 0 && unchecked(gold - proposal.Gold) < 0 ||
                proposal.Supplies != 0 && unchecked(food - proposal.Supplies) < 0)
                return End(false, "insufficient-stock");
            if (proposedValue < originalValue) return End(true, "reduced-payment");
            if (proposal.ItemId is int itemId)
            {
                var item = Current.ReadRegistryTableReference(RegistryKind.Item, itemId)
                    ?? throw DiplomacyNumericMissing("exchange_counter_Item_pointer");
                if (DNeed(Current.ReadRegistryObjectNativeValidity(item), "exchange_counter_Item_validity"))
                {
                    var owner = State.Items?.GetValueOrDefault(itemId) ?? throw DiplomacyNumericMissing("exchange_counter_Item_owner");
                    var appearance = State.ItemDefinitions.GetValueOrDefault(owner.DefinitionId)?.Appearance
                        ?? throw DiplomacyNumericMissing("exchange_counter_Item45_47");
                    if (appearance.EffectKind == 0 && appearance.AbilityKind == 0) return End(true, "plain-item");
                }
            }
            int probability = Math.Clamp(unchecked(politics / 10 - DDiv(unchecked(proposedValue * 100), originalValue,
                "exchange_counter_original_value") + 100 + relation / 6), 0, 100);
            var sampler = package.ReadArmyDestructionSampler(State, Current)
                ?? throw DiplomacyNumericMissing("exchange_counter_sampler");
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1, originalValue, proposedValue,
                OwnOrdinal, OtherOrdinal, sampler);
            return End(sample < probability, "trial", probability, sample);
        }

        //3249B0(kind19), including its distinct sampler seed. A boolean
        //prediction is deliberately never substituted for322B80's zero.
        internal DiplomacyTrialResult ExchangeTrial(DiplomacyOffer given, DiplomacyOffer requested)
        {
            if (NativeKind != 19) throw DiplomacyNumericMissing("exchange_trial_kind");
            DiplomacyTrialResult End(bool accepted, string branch, int? sample = null)
                => new(accepted, 0, sample, "exchange-" + branch);
            int offeredValue = Math.Max(1, OfferValue(given)), requestedValue = OfferValue(requested);
            var capital = new DiplomacyCommandSource(State, Current).Capital(OtherOrdinal);
            int gold = ReadDiplomacyBaseResourceCurrent(Current, capital, BaseResourceKind.Gold);
            int food = ReadDiplomacyBaseResourceCurrent(Current, capital, BaseResourceKind.Supplies);
            int troops = DNeed(Current.ReadBaseResource(capital, BaseResourceKind.Troops), "exchange_capital3C");
            int politics = Politics(false), relation = Relation(OwnOrdinal, OtherOrdinal);
            if (requested.Gold > 0)
            {
                if (unchecked(gold - requested.Gold) <= 2000) return End(false, "gold-reserve");
            }
            else if (requested.Supplies > 0)
            {
                if (unchecked(food - requested.Supplies) <= unchecked(troops + 20000)) return End(false, "food-reserve");
            }
            else if (requested.ItemId is >= 1 and <= 201 &&
                !package.ExchangeItemDoesNotLowerRulerAbilities(State, Current, Ruler(Other), given.ItemId, requested.ItemId.Value))
                return End(false, "ruler-equipment-loss");
            int threshold = unchecked(relation / 6 + 100 + (politics / 10 - unchecked(requestedValue * 100) / offeredValue));
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(State, Current)
                ?? throw DiplomacyNumericMissing("exchange_player");
            int playerId = DNeed(player.PhysicalSlot, "exchange_player60");
            if (playerId != OwnOrdinal && playerId != OtherOrdinal) threshold = unchecked(threshold + 20);
            var sampler = package.ReadArmyDestructionSampler(State, Current)
                ?? throw DiplomacyNumericMissing("exchange_sampler");
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1, ExecutorId, Id(Ruler(Own)), Id(Ruler(Other)), 0, sampler);
            return End(sample < threshold, "trial", sample);
        }
    }

    //The original temporarily changes Item4A. Each new holder iterator
    //re-evaluates that raw field against registry110. Mode2 omits health/rank/office but preserves
    //the real secondary factor, doctrine panel, caps and seal override.
    private bool ExchangeItemDoesNotLowerRulerAbilities(GameState state, SourceWorldContext current,
        int rulerPhysical, int? givenItem, int requestedItem)
    {
        int ruler = DNeed(current.ReadPersonIdentity(rulerPhysical), "exchange_ruler10");
        var person = state.Officers.GetValueOrDefault(ruler) ?? throw DiplomacyNumericMissing("exchange_ruler_owner");
        var basic = person.BaseAbilities ?? throw DiplomacyNumericMissing("exchange_ruler_base");
        var caps = person.AbilityState ?? throw DiplomacyNumericMissing("exchange_ruler_caps");
        var inventory = state.Items ?? throw DiplomacyNumericMissing("exchange_inventory");
        var membership = new List<(ItemState Item, bool Before, bool After)>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw DiplomacyNumericMissing("exchange_Item_registry"))
        {
            int item = DNeed(entry?.PhysicalSlot, "exchange_Item_pointer");
            bool before = DNeed(current.ReadCaptureItemOwner4A(item), "exchange_Item4A") == ruler;
            bool after = givenItem == item || item != requestedItem && before;
            if (before || after) membership.Add((inventory.GetValueOrDefault(item)
                ?? throw DiplomacyNumericMissing("exchange_Item_owner"), before, after));
        }
        int secondary = ExchangeAbilityParameter(state, SimulationParameterKeys.Tactics.AbilitySecondaryBaseline);
        byte status = DNeed(current.ReadPersonStatus(rulerPhysical), "exchange_ruler_E6");
        if (status != 1 && OfficerBehaviorRules.UsesOrdinaryPersonRules(person.Behavior) == true &&
            OfficerMeritRules.IsStageOne(person.MeritState) == true)
        {
            var options = state.NewGameOptions ?? throw DiplomacyNumericMissing("exchange_ability_options");
            if (options.CapturedOfficersEnabled)
                secondary = ExchangeAbilityParameter(state, SimulationParameterKeys.Tactics.AbilitySecondaryAdjustment);
        }
        int doctrine = DNeed(OfficerDoctrineRules.ReadDoctrineId(person.DoctrineId, caps), "exchange_ruler_doctrine");
        int force = DNeed((current.ReadPersonForce(rulerPhysical) ?? throw DiplomacyNumericMissing("exchange_ruler_Force")).PhysicalSlot, "exchange_ruler_Force60");
        int[] bases = [basic.Leadership, basic.Strength, basic.Intelligence, basic.Politics, basic.Charm];
        for (int dimension = 0; dimension < 5; dimension++)
        {
            int panel = 0;
            if (doctrine != 0)
            {
                var policies = force == 0 ? state.UnassignedForcePolicyEffects : state.Forces.GetValueOrDefault(force)?.PolicyEffects;
                int effect = dimension + doctrine * 5 - 3;
                if (policies?.IsEnabled(effect) is not bool enabled) throw DiplomacyNumericMissing("exchange_ruler_doctrine_activation");
                if (enabled) panel = DNeed(_baseCapacityInputs.ReadPanelModifier(state, force, effect), "exchange_ruler_doctrine_panel");
            }
            int beforeBonus = 0, afterBonus = 0;
            bool beforeSeal = false, afterSeal = false;
            foreach (var row in membership)
            {
                var item = row.Item;
                if (item.Id == 123) { beforeSeal |= row.Before; afterSeal |= row.After; }
                var bonus = state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.AbilityBonus
                    ?? throw DiplomacyNumericMissing("exchange_Item_ability");
                int[] values = [bonus.Leadership, bonus.Strength, bonus.Intelligence, bonus.Politics, bonus.Charm];
                if (row.Before) beforeBonus = Math.Max(beforeBonus, Math.Min(values[dimension], 10));
                if (row.After) afterBonus = Math.Max(afterBonus, Math.Min(values[dimension], 10));
            }
            int cap = DNeed(dimension == 4 ? caps.CharmLimit : caps.OrdinaryLimit, "exchange_ruler_cap");
            int core = unchecked(bases[dimension] * secondary) / 100 + panel;
            int before = dimension == 4 && beforeSeal ? Math.Max(1, Math.Min(100, cap)) : Math.Max(1, Math.Min(unchecked(core + beforeBonus), cap));
            int after = dimension == 4 && afterSeal ? Math.Max(1, Math.Min(100, cap)) : Math.Max(1, Math.Min(unchecked(core + afterBonus), cap));
            if (after < before) return false;
        }
        return true;
    }

    internal GameState ApplyExchangeRelationCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, bool accepted, List<CampaignEffectRequest> effects)
    {
        var q = new DiplomacyQuery(this, state, current, task);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DiplomacyNumericMissing("exchange_relation_player");
        bool otherPlayer = player.PhysicalSlot == q.OtherOrdinal;
        if (accepted)
        {
            if (otherPlayer) return state;
            int before = q.Relation(q.OwnOrdinal, q.OtherOrdinal);
            return ApplyDiplomacyRelationCurrent(state, current, q.OwnOrdinal, q.OtherOrdinal,
                unchecked(Math.Max(50, unchecked(before - 15)) - before), effects);
        }
        state = ApplyDiplomacyRelationCurrent(state, current, q.OwnOrdinal, q.OtherOrdinal, -5, effects);
        return otherPlayer ? DApplyFailureCooldownExchange(state, q.OwnOrdinal) : state;
    }

    private static GameState DApplyFailureCooldownExchange(GameState state, int force)
    {
        var date = state.NativeMapSeason?.ThirdDate ?? throw DiplomacyNumericMissing("exchange_cooldown_date");
        int ordinal = unchecked(PersonReturnCommands.CalendarOrdinal(date) + 60);
        short year = unchecked((short)(ordinal / 360));
        int remaining = Math.Max(0, unchecked(ordinal - year * 360));
        var next = new NativeMapSeasonDate(year, unchecked((sbyte)(remaining / 30 + 1)), unchecked((sbyte)(remaining % 30 + 1)), 0);
        var native = state.NativeArmySimulation ?? throw DiplomacyNumericMissing("exchange_cooldown_native");
        var owner = native.Forces.GetValueOrDefault(force) ?? throw DiplomacyNumericMissing("exchange_cooldown_Force");
        return state with { NativeArmySimulation = native with { Forces = new(native.Forces) { [force] = owner with { DiplomacyCooldownDate = next } } } };
    }

    //81E4C0: call after the ordinary sender resources/item refund and before
    //the sender Area transfer, preserving the source's noncommutative order.
    internal GameState ApplyExchangeRequestedOfferSettlementCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, bool accepted, List<CampaignEffectRequest> effects, NativeMapSeasonDate? operationDate = null)
    {
        if (!accepted) return state;
        var q = new DiplomacyQuery(this, state, current, task);
        var own = DRulerHome(q, q.Own, out int ownRuler);
        var other = DRulerHome(q, q.Other);
        var offer = task.RequestedOffer;
        state = DAddDiplomacyResource(state, current, own, BaseResourceKind.Gold, offer.Gold);
        state = DAddDiplomacyResource(state, current, other, BaseResourceKind.Gold, unchecked(-offer.Gold));
        state = DAddDiplomacyResource(state, current, own, BaseResourceKind.Supplies, offer.Supplies);
        state = DAddDiplomacyResource(state, current, other, BaseResourceKind.Supplies, unchecked(-offer.Supplies));
        if (offer.ItemId is >= 1 and <= 201)
        {
            var item = current.ReadRegistryTableReference(RegistryKind.Item, offer.ItemId.Value)
                ?? throw DiplomacyNumericMissing("exchange_settlement_Item");
            if (DNeed(current.ReadRegistryObjectNativeValidity(item), "exchange_settlement_Item_validity"))
                state = DWriteDiplomaticItem(state, current, offer.ItemId.Value, ownRuler, true);
        }
        return state;
    }

    //The requested Area is the final transfer, after sender Area.
    internal GameState ApplyExchangeRequestedAreaSettlementCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, bool accepted, List<CampaignEffectRequest> effects, NativeMapSeasonDate? operationDate = null)
    {
        if (!accepted || task.RequestedOffer.AreaId is not (>= 1 and <= 500)) return state;
        var area = current.ReadAreaTableReference(task.RequestedOffer.AreaId.Value)
            ?? throw DiplomacyNumericMissing("exchange_settlement_Area");
        if (!DNeed(current.ReadAreaNativeValidity(area), "exchange_settlement_Area_validity")) return state;
        return ApplyDiplomacyAreaTransferCurrent(state, current, area, ExchangeForce(current, task.ForceId), effects, operationDate);
    }

    private static ForceReference ExchangeForce(SourceWorldContext current, int physical)
        => current.ReadForceTableReference(physical is >= 0 and < 52 ? physical : 0)
            ?? throw DiplomacyNumericMissing("exchange_Force_pointer");
    private static byte ExchangeFlags12(GameState state, int force)
    {
        if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
            return DNeed(reset[0x12], "exchange_reset12");
        return DNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags12, "exchange_Force12");
    }
    private static int ExchangeAbilityParameter(GameState state, string key)
        => state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true
            ? value : throw DiplomacyNumericMissing("exchange_ability_parameter_" + key);
}
