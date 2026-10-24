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
        if (order is SetCommanderSelectionOrder commanders)
            return CommanderSelectionOrders.Apply(state, force, commanders, context, policy);
        if (order is SetOfficerOfficeOrder or SetOfficerOfficesOrder)
            return OfficerOfficeOrders.Apply(state, force, order, context, policy);
        if (order is not GrantItemOrder and not RevokeItemOrder)
            return next.Apply(state, force, order, context);
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Personnel administration requires direct control of the force.", "personnel.scope");
        PersonnelChange change;
        ItemState? item = null;
        int officerId;
        {
            int itemId = order is GrantItemOrder grant ? grant.ItemId : ((RevokeItemOrder)order).ItemId;
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
            if (order is GrantItemOrder give)
            {
                officerId = give.RecipientOfficerId;
                if (holderId != rulerId)
                    return CommandResult.Reject("Revoke the subordinate's item before granting it again.", "personnel.item_revoke_first");
                change = new(PersonnelActionKind.GrantItem, officerId, itemId, holderId, officerId);
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
        if (target.Presence != OfficerPresence.Present || target.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, target) ||
            PlayableDiplomacy.IsOfficerOnMission(state, target.Id) ||
            state.PreparedOfficerMoves?.ContainsKey(target.Id) == true ||
            state.PreparedTalentOrders?.ContainsKey(target.Id) == true ||
            state.PreparedDeployments.Values.Any(plan => plan.OfficerId == target.Id))
            return CommandResult.Reject("Officer is currently unavailable.", "personnel.officer_busy");
        PersonnelEvaluation evaluation = policy.Evaluate(state, force, change, context)
            ?? throw new InvalidDataException("Personnel policy returned no evaluation.");
        if (evaluation.Allowed is null)
            return CommandResult.RuleUnavailable(evaluation.MissingRuleKey ?? "personnel.policy",
                evaluation.Reason ?? "Personnel rule is unresolved.");
        if (evaluation.Allowed != true)
            return CommandResult.Reject(evaluation.Reason ?? "Personnel policy denied this change.", "personnel.ineligible");
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
        if (quote.LoyaltyDelta != 0 && target.Loyalty is null)
            return CommandResult.RuleUnavailable("personnel.loyalty", "Current loyalty is unresolved; effects cannot consume it as zero.");
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
        return CommandResult.Accept(state with
        {
            Items = items,
            Officers = new(state.Officers) { [target.Id] = changed },
            Cities = cities, Districts = districts,
            PersonnelReports = (state.PersonnelReports ?? []).Append(report).ToArray()
        });
    }

    private static bool Owned(GameState state, int forceId, int officerId) =>
        state.Officers.TryGetValue(officerId, out OfficerState? officer) && officer.ForceId == forceId;
}
