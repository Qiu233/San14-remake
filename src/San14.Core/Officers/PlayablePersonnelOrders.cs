namespace San14.Core;

/// <summary>Known item-holder and office changes through the shared command boundary.</summary>
public sealed class PlayablePersonnelOrders(IOrderRules next, IPersonnelCommandRules policy) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is HireOfficersOrder or LearnOfficerTacticOrder or MediationOrder)
            return PersonnelDevelopmentOrders.Apply(state, force, order, context, policy);
        if (order is RewardOfficersOrder or GrantOfficerMeritOrder or PromoteOfficerCreditOrder or BanishOfficersOrder)
            return PersonnelAdministrationOrders.Apply(state, force, order, context, policy);
        if (order is ToggleCommanderAutomaticTacticsOrder automatic)
            return policy is ICommanderAutomaticTacticsCommandRules automaticRules
                ? CommanderAutomaticTacticsCommandApplication.ApplyValidated(state, force, automatic, context, automaticRules)
                : CommandResult.RuleUnavailable("commander-automatic.rules", "Current commander automatic tactics are unavailable.");
        if (order is SetCommanderSelectionOrder commanders)
            return CommanderSelectionOrders.Apply(state, force, commanders, context, policy);
        if (order is SetOfficerOfficeOrder or SetOfficerOfficesOrder)
            return OfficerOfficeOrders.Apply(state, force, order, context, policy);
        if (order is not GrantItemOrder and not RevokeItemOrder and not PreparePersonnelItemTransferOrder)
            return next.Apply(state, force, order, context);
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Personnel administration requires direct control of the force.", "personnel.scope");
        PersonnelChange change;
        ItemState? item = null;
        int officerId;
        {
            int itemId = order switch { GrantItemOrder grant => grant.ItemId, RevokeItemOrder revoke => revoke.ItemId,
                PreparePersonnelItemTransferOrder prepare => prepare.ItemId, _ => throw new InvalidDataException("Unexpected item order.") };
            if (state.Items is null)
                return CommandResult.RuleUnavailable("personnel.inventory", "Current item inventory is unresolved.");
            if (!state.Items.TryGetValue(itemId, out item) || !state.ItemDefinitions.ContainsKey(item.DefinitionId))
                return CommandResult.Reject("Item is missing.", "personnel.item_missing");
            if (item.IsPresent is null || item.Holder is null)
                return CommandResult.RuleUnavailable("personnel.item_state", "Item presence or holder is unresolved.");
            if (item.IsPresent != true)
                return CommandResult.Reject("Item is not present.", "personnel.item_absent");
            if (force.RulerId is not int rulerId || !Owned(state, force.Id, rulerId))
                return CommandResult.RuleUnavailable("personnel.ruler", "Current ruler identity is unresolved.");
            if (item.Holder.OfficerId is not int holderId)
                return CommandResult.RuleUnavailable("personnel.item_force", "An unheld item does not establish a force treasury owner.");
            if (!Owned(state, force.Id, holderId))
                return CommandResult.Reject("Item holder belongs to another force.", "personnel.item_control");
            if (order is GrantItemOrder or PreparePersonnelItemTransferOrder)
            {
                int recipient = order is GrantItemOrder give ? give.RecipientOfficerId : ((PreparePersonnelItemTransferOrder)order).RecipientOfficerId;
                officerId = recipient;
                if (holderId == officerId)
                    return CommandResult.Reject("The recipient already holds this item.", "personnel.item_same_holder");
                bool revokePreparation = order is PreparePersonnelItemTransferOrder && recipient == rulerId;
                change = revokePreparation ? new(PersonnelActionKind.RevokeItem, holderId, itemId, holderId, rulerId)
                    : new(PersonnelActionKind.GrantItem, officerId, itemId, holderId, officerId);
                if (revokePreparation) officerId = holderId;
            }
            else
            {
                RevokeItemOrder revoke = (RevokeItemOrder)order;
                officerId = revoke.OwnerOfficerId;
                if (holderId != officerId)
                    return CommandResult.Reject("Item holder changed since selection.", "personnel.item_holder_changed");
                change = new(PersonnelActionKind.RevokeItem, officerId, itemId, holderId, rulerId);
            }
        }
        if (!Owned(state, force.Id, officerId))
            return CommandResult.Reject("Target officer does not belong to this force.", "personnel.officer_control");
        OfficerState target = state.Officers[officerId];
        PersonnelEvaluation evaluation = policy.Evaluate(state, force, change, context)
            ?? throw new InvalidDataException("Personnel policy returned no evaluation.");
        if (evaluation.Allowed is null)
            return CommandResult.RuleUnavailable(evaluation.MissingRuleKey ?? "personnel.policy",
                evaluation.Reason ?? "Personnel rule is unresolved.");
        if (evaluation.Allowed != true)
            return CommandResult.Reject(evaluation.Reason ?? "Personnel policy denied this change.", "personnel.ineligible");
        if (evaluation.ItemTransferPreview is null && (target.Presence != OfficerPresence.Present || target.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, target) ||
            PlayableDiplomacy.IsOfficerOnMission(state, target.Id) ||
            state.PreparedOfficerMoves?.ContainsKey(target.Id) == true ||
            state.PreparedTalentOrders?.ContainsKey(target.Id) == true ||
            state.PreparedDeployments.Values.Any(plan => plan.OfficerId == target.Id)))
            return CommandResult.Reject("Officer is currently unavailable.", "personnel.officer_busy");
        if (evaluation.Quote is not { } quote)
            throw new InvalidDataException("Allowed personnel policy omitted its accounting.");
        DistrictState? district = quote.DistrictId is int districtId &&
            state.Districts.TryGetValue(districtId, out DistrictState? knownDistrict) ? knownDistrict : null;
        CityState? city = quote.GoldCityId is int cityId &&
            state.Cities.TryGetValue(cityId, out CityState? knownCity) ? knownCity : null;
        if (quote.CommandBooks < 0 || quote.Gold < 0 || quote.LoyaltyDelta is < -100 or > 100 ||
            string.IsNullOrWhiteSpace(quote.Revision) ||
            (quote.CommandBooks > 0 && district is null) || (quote.Gold > 0 && city is null) ||
            (quote.DistrictId is not null && (district is null || district.ForceId != force.Id)) ||
            (quote.GoldCityId is not null && (city is null || city.ForceId != force.Id)))
            throw new InvalidDataException("Personnel quote contains invalid accounting or control references.");
        bool unchanged = change.PreviousHolderId == change.NextHolderId;
        if (unchanged) return CommandResult.Accept(state);
        if (quote.CommandBooks != 0 && district!.OrdersRemaining is null)
            return CommandResult.RuleUnavailable("rules.command_budget", "Personnel command books are unresolved.");
        if (district?.OrdersRemaining is int books && books - CommandBookAvailability.Reserved(state, district.Id) < quote.CommandBooks)
            return CommandResult.Reject("District lacks uncommitted command books.", "personnel.insufficient_orders");
        if (city is not null && city.Gold < quote.Gold)
            return CommandResult.Reject("City lacks personnel funds.", "personnel.insufficient_gold");
        if (quote.StoredLoyaltyBefore.HasValue != quote.StoredLoyaltyAfter.HasValue)
            throw new InvalidDataException("Personnel stored loyalty quote has incomplete byte facts.");
        if (quote.StoredLoyaltyAfter.HasValue &&
            (evaluation.ItemTransferPreview is null || policy is not IPersonnelItemTransferCommandRules))
            return CommandResult.RuleUnavailable("personnel.stored_loyalty_writer", "Stored item loyalty requires its source-owned transfer writer.");
        if (quote.LoyaltyDelta != 0 && target.Loyalty is null)
            return CommandResult.RuleUnavailable("personnel.loyalty", "Current loyalty is unresolved; effects cannot consume it as zero.");
        if (order is PreparePersonnelItemTransferOrder)
            return evaluation.ItemTransferPreview is { } preparing && policy is IPersonnelItemTransferCommandRules preparingRules
                ? preparingRules.PrepareItemTransfer(state, force, change, preparing, context)
                : CommandResult.RuleUnavailable("personnel.item_narration", "The current item announcement is unavailable.");
        OfficerState changed = target;
        int actualLoyalty = quote.LoyaltyDelta == 0 ? 0 :
            Math.Clamp(checked(target.Loyalty!.Value + quote.LoyaltyDelta), 0, 100) - target.Loyalty.Value;
        if (quote.LoyaltyDelta != 0) changed = changed with { Loyalty = target.Loyalty!.Value + actualLoyalty };
        Dictionary<int, ItemState>? items = state.Items;
        if (item is not null)
        {
            var transferred = item with { Holder = new ItemHolder(change.NextHolderId) };
            if (quote.ItemFlagsAfter is byte flags)
                transferred = transferred with { Lifecycle = (item.Lifecycle ??
                    throw new InvalidDataException("Personnel item flag writer has no lifecycle owner.")) with { RetainedFlags = flags } };
            items = new(state.Items!) { [item.Id] = transferred };
        }
        var report = new PersonnelReportEntry(change.Kind, state.PeriodNumber, force.Id, target.Id,
            change.ItemId, change.PreviousHolderId, change.NextHolderId, change.PreviousOfficeId,
            change.NextOfficeId, quote.CommandBooks, quote.Gold, actualLoyalty, quote.Revision)
        { DistrictId = quote.DistrictId, GoldCityId = quote.GoldCityId };
        var cities = new Dictionary<int, CityState>(state.Cities);
        var districts = new Dictionary<int, DistrictState>(state.Districts);
        if (city is not null) cities[city.Id] = city with { Gold = city.Gold - quote.Gold };
        if (district is not null) districts[district.Id] = district with
        {
            OrdersRemaining = district.OrdersRemaining is int currentBooks ? currentBooks - quote.CommandBooks : null
        };
        var officers = new Dictionary<int, OfficerState>(state.Officers) { [target.Id] = changed };
        if (evaluation.ItemTransferPreview is { } transfer)
        {
            if (transfer.ItemId != change.ItemId || transfer.PreviousHolder.OfficerId != change.PreviousHolderId ||
                transfer.NextHolder.OfficerId != change.NextHolderId ||
                transfer.PreviousHolder.OfficerId == transfer.NextHolder.OfficerId ||
                transfer.DistrictId != quote.DistrictId || transfer.CommandBooksCost != quote.CommandBooks ||
                transfer.DistrictBooksBefore != district?.OrdersRemaining ||
                transfer.DistrictBooksAfter != Math.Clamp(unchecked(transfer.DistrictBooksBefore - quote.CommandBooks), 0, 200))
                throw new InvalidDataException("Personnel transfer preview does not match the evaluated command.");
            var quotedLoyalty = change.Kind == PersonnelActionKind.GrantItem ? transfer.NextHolder : transfer.PreviousHolder;
            if (quote.StoredLoyaltyBefore != quotedLoyalty.StoredLoyaltyBefore ||
                quote.StoredLoyaltyAfter != quotedLoyalty.StoredLoyaltyAfter)
                throw new InvalidDataException("Personnel stored loyalty preview does not match its quote.");
            // Both source loyalty writes happen before the one holder change.
            // Effective abilities remain derived from the resulting inventory.
            foreach (var effect in new[] { transfer.PreviousHolder, transfer.NextHolder })
            {
                if (!state.Officers.TryGetValue(effect.OfficerId, out var current) ||
                    current.Loyalty != effect.LoyaltyBefore || effect.LoyaltyAfter is < 0 or > 100)
                    throw new InvalidDataException("Personnel transfer loyalty result has no matching current owner.");
                if (effect.StoredLoyaltyBefore is byte storedBefore && effect.StoredLoyaltyAfter is byte storedAfter)
                {
                    int? projectedBefore = storedBefore <= 100 ? storedBefore : null;
                    int? projectedAfter = storedAfter <= 100 ? storedAfter : null;
                    if (effect.LoyaltyBefore != projectedBefore || effect.LoyaltyAfter != projectedAfter)
                        throw new InvalidDataException("Personnel stored loyalty does not match its ordinary projection.");
                }
                else if (effect.StoredLoyaltyBefore.HasValue || effect.StoredLoyaltyAfter.HasValue ||
                    (effect.LoyaltyBefore is null) != (effect.LoyaltyAfter is null))
                    throw new InvalidDataException("Personnel transfer loyalty result has incomplete source facts.");
                officers[effect.OfficerId] = current with { Loyalty = effect.LoyaltyAfter };
            }
        }
        if (evaluation.ItemTransferPreview is { } accounted)
            districts[accounted.DistrictId] = districts[accounted.DistrictId] with
                { OrdersRemaining = accounted.DistrictBooksAfter };
        GameState result = state with
        {
            Items = items,
            Officers = officers,
            Cities = cities, Districts = districts,
            PersonnelReports = (state.PersonnelReports ?? []).Append(report).ToArray()
        };
        return evaluation.ItemTransferPreview is { } completed && policy is IPersonnelItemTransferCommandRules sourceRules
            ? sourceRules.CompleteItemTransfer(result, force, change, completed, context)
            : CommandResult.Accept(result);
    }

    private static bool Owned(GameState state, int forceId, int officerId) =>
        state.Officers.TryGetValue(officerId, out OfficerState? officer) && officer.ForceId == forceId;
}
