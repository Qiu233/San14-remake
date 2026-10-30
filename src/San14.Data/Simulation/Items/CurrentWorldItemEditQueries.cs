using San14.Core;

namespace San14.Data;

public sealed record CurrentWorldItemEditRow(int ItemId, string Name, ItemEditorValues Values,
    int? HolderOfficerId, string HolderName, ItemLifecycleKnowledge Lifecycle,
    bool TransferProhibited, bool CanEditCustody, bool IsUnavailable)
{
    public bool HolderIsValid { get; init; }
    public CurrentWorldItemEditDraft CreateDraft() => new(ItemId, Values, HolderOfficerId,
        HolderIsValid && TransferProhibited);
}

public sealed record CurrentWorldItemBatchEditDraft(int? HolderOfficerId,
    bool TransferProhibited, bool RequiresRemovalConfirmation);

public sealed record CurrentWorldItemEditHolder(int? HolderOfficerId, string Name, bool IsValid,
    OfficerPortraitRoute Portrait, DetailForceAppearance? ForceFlag, string ForceName);

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<IReadOnlyList<CurrentWorldItemEditRow>> QueryCurrentWorldItemEditRows(GameState state)
    {
        try
        {
            RequireCurrentWorldItemEditProfile(state);
            return AreaAdministrationQuery<IReadOnlyList<CurrentWorldItemEditRow>>.Known(
                ReadCurrentWorldItemEditRows(state, CaptureCurrentWorldItemEdit(state)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<CurrentWorldItemEditRow>>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<IReadOnlyList<ItemEditorCustodyOfficerChoice>> QueryCurrentWorldItemEditOfficers(GameState state)
    {
        try
        {
            RequireCurrentWorldItemEditProfile(state);
            return AreaAdministrationQuery<IReadOnlyList<ItemEditorCustodyOfficerChoice>>.Known(
                ReadCurrentWorldItemEditOfficers(state, CaptureCurrentWorldItemEdit(state)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<ItemEditorCustodyOfficerChoice>>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<CurrentWorldItemBatchEditDraft> QueryCurrentWorldItemBatchEditDraft(
        GameState state, IReadOnlyList<int> itemIds)
    {
        try
        {
            var query = QueryCurrentWorldItemEditRows(state);
            var rows = query.Value ?? throw CurrentWorldItemEditMissing(query.UnresolvedInput ?? "batch_rows");
            var selected = SelectCurrentWorldItemEditRows(rows, itemIds);
            int? first = selected[0].HolderOfficerId;
            int? holder = selected.All(row => row.HolderOfficerId == first) ? first : null;
            return AreaAdministrationQuery<CurrentWorldItemBatchEditDraft>.Known(new(holder,
                holder.HasValue && selected[0].HolderIsValid && selected.All(row => row.TransferProhibited),
                selected.Any(row => row.Lifecycle.Status!.Code != 2)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldItemBatchEditDraft>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<CurrentWorldItemEditHolder> QueryCurrentWorldItemEditHolder(GameState state, int? holderId)
    {
        try
        {
            RequireCurrentWorldItemEditProfile(state);
            int id = holderId ?? 0;
            if (id is < 0 or > 6000) throw CurrentWorldItemEditMissing("holder_range");
            var current = CaptureCurrentWorldItemEdit(state);
            var person = current.ReadPersonLookupReference(id) ?? throw CurrentWorldItemEditMissing("holder_pointer");
            bool valid = CurrentWorldItemEditPersonValid(current, person, "holder_validity");
            int identity = valid ? CurrentWorldItemEditNeed(current.ReadPersonIdentity(person.PhysicalSlot!.Value), "holder_identity") : 0;
            ushort appearance = valid ? CurrentWorldItemEditNeed(current.ReadPersonPortraitAppearance(person.PhysicalSlot!.Value), "holder_portrait")
                : (ushort)ItemInformationSources.InvalidHolderAppearanceId;
            var portrait = ReadOfficerPortraitAppearance(state, current, _definitions, identity, appearance, ArmyMapUiSources.Create());
            if (portrait.Kind == OfficerPortraitRouteKind.Unresolved)
                throw CurrentWorldItemEditMissing(portrait.UnresolvedInput ?? "holder_portrait_route");
            string name = valid ? state.Officers.GetValueOrDefault(identity)?.Name
                ?? throw CurrentWorldItemEditMissing("holder_name") : "";
            //641630 passes Force0 for an invalid Person. The existing flag
            //reader retains the original empty-Force palette and glyph.
            var force = valid ? current.ReadPersonForce(person.PhysicalSlot!.Value) : current.ReadForceTableReference(0);
            if (force is null) throw CurrentWorldItemEditMissing("holder_Force_pointer");
            bool forceValid = CurrentWorldItemEditNeed(current.ReadForceNativeValidity(force), "holder_Force_validity");
            _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
            var flag = ReadDetailForceAppearance(state, current, _mapInformationNames, force, forceValid, MapSpritePalettes.Create())
                ?? throw CurrentWorldItemEditMissing("holder_Force_flag");
            return AreaAdministrationQuery<CurrentWorldItemEditHolder>.Known(new(holderId, name, valid, portrait, flag,
                flag.DisplayName ?? throw CurrentWorldItemEditMissing("holder_Force_name")));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldItemEditHolder>.Unknown(error.Message); }
    }

    private void RequireCurrentWorldItemEditProfile(GameState state)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw CurrentWorldItemEditMissing("parameters");
        if (Seed.Language is not ("TC" or "SC") || bank.Language != Seed.Language ||
            bank.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
            throw CurrentWorldItemEditMissing("rule_owner");
    }

    //One established complete current scope for every item query and submit.
    //It retains this operation's pool and binds saved Person/Force vacancies,
    //Item receivers and the actual associated tasks before any consumer runs.
    private SourceWorldContext CaptureCurrentWorldItemEdit(GameState state) =>
        CaptureOwnedArmyReturnWorld(state).Context;

    private static bool CurrentWorldItemEditPersonValid(SourceWorldContext current,
        TransferPersonReference person, string field) => CurrentWorldItemEditNeed(
            current.ReadRegistryObjectNativeValidity(new(RegistryKind.Person, person.PhysicalSlot)), field);

    private static IReadOnlyList<ItemEditorCustodyOfficerChoice> ReadCurrentWorldItemEditOfficers(GameState state,
        SourceWorldContext current)
    {
        var result = new List<ItemEditorCustodyOfficerChoice>();
        foreach (int id in Enumerable.Range(1, 1000).Concat(Enumerable.Range(2001, 88))
            .Concat(Enumerable.Range(5101, 100)).Concat(Enumerable.Range(3001, 150)))
        {
            var person = current.ReadPersonLookupReference(id) ?? throw CurrentWorldItemEditMissing("candidate_Person_pointer");
            //The native predicate accepts a known null pointer as invalid.
            //An absent lookup object still means unknown and must reject.
            if (!CurrentWorldItemEditPersonValid(current, person, "candidate_Person_validity")) continue;
            int physical = CurrentWorldItemEditNeed(person.PhysicalSlot, "candidate_Person_receiver");
            if (CurrentWorldItemEditNeed(current.ReadPersonStatus(physical), "candidate_Person_status") is < 1 or > 4) continue;
            string name = state.Officers.GetValueOrDefault(id)?.Name ?? throw CurrentWorldItemEditMissing("candidate_Person_name");
            result.Add(new(id, name));
        }
        return result.AsReadOnly();
    }

    private IReadOnlyList<CurrentWorldItemEditRow> ReadCurrentWorldItemEditRows(GameState state, SourceWorldContext current)
    {
        var items = state.Items ?? throw CurrentWorldItemEditMissing("items");
        var blocked = ReadCurrentWorldItemEditUnavailable(state, current);
        var result = new List<CurrentWorldItemEditRow>();
        for (int id = 1; id <= 201; id++)
        {
            var item = items.GetValueOrDefault(id) ?? throw CurrentWorldItemEditMissing("Item_owner");
            var definition = state.ItemDefinitions.GetValueOrDefault(item.DefinitionId) ?? throw CurrentWorldItemEditMissing("Item_definition");
            var appearance = definition.Appearance ?? throw CurrentWorldItemEditMissing("Item_appearance");
            int kind = appearance.Kind;
            if (kind == 0 || kind is 8 or 9 || kind <= 19 && ((0xB3000 >> kind) & 1) != 0 || id is >= 125 and <= 127) continue;
            if (item.Id != id || item.DefinitionId != id || definition.Id != id) throw CurrentWorldItemEditMissing("Item_identity");
            var lifecycle = item.Lifecycle ?? throw CurrentWorldItemEditMissing("Item_lifecycle");
            byte flags = CurrentWorldItemEditNeed(lifecycle.RetainedFlags, "Item_flags");
            _ = CurrentWorldItemEditNeed(lifecycle.RetainedLocationCode, "Item_location");
            if (lifecycle.Status?.Kind != ItemStatusKnowledgeKind.ExactSourceState) throw CurrentWorldItemEditMissing("Item_status");
            var holder = item.Holder ?? throw CurrentWorldItemEditMissing("Item_holder");
            int raw = holder.OfficerId ?? 0;
            if (raw is < 0 or > 6000) throw CurrentWorldItemEditMissing("Item_holder_range");
            var person = current.ReadPersonLookupReference(raw) ?? throw CurrentWorldItemEditMissing("Item_holder_pointer");
            bool holderValid = CurrentWorldItemEditPersonValid(current, person, "Item_holder_validity");
            string holderName = holderValid ? state.Officers.GetValueOrDefault(raw)?.Name
                ?? throw CurrentWorldItemEditMissing("Item_holder_name") : "";
            int worth = CurrentWorldItemEditNeed(definition.LoyaltyValue, "Item_worth");
            if (worth is < 0 or > 255) throw CurrentWorldItemEditMissing("Item_worth_range");
            var values = new ItemEditorValues(kind, appearance.AbilityKind, Math.Min(appearance.AbilityValue, 10),
                appearance.EffectKind, Math.Min(appearance.EffectValue, 10), Math.Min(worth, 50), appearance.ImageIndex);
            values.Validate();
            result.Add(new(id, definition.Name, values, holder.OfficerId, holderName, lifecycle,
                (flags & 8) != 0, (flags & 1) != 0, lifecycle.Status.Code == 4 || blocked.Contains(id))
                { HolderIsValid = holderValid });
        }
        return result.AsReadOnly();
    }

    private HashSet<int> ReadCurrentWorldItemEditUnavailable(GameState state, SourceWorldContext current)
    {
        var result = new HashSet<int>();
        var manager = current.ReadOwnedPersonCommandManager() ?? throw CurrentWorldItemEditMissing("command_manager");
        var queries = new PersonReturnGovernorQueries(state, _governorInputs, current);
        foreach (var linked in current.ReadCurrentPersonLinkedEntities() ?? throw CurrentWorldItemEditMissing("associated_registry"))
        {
            if (linked?.Identity is not int id) throw CurrentWorldItemEditMissing("associated_pointer");
            int kind = CurrentWorldItemEditNeed(queries.ReadCurrentAssociatedVirtual98(linked), "associated_kind");
            if (kind is >= 4 and <= 9 || kind == 19)
            {
                //The actual 1EDDC0 getter reads command word76, represented
                //by the existing current diplomacy escrow rather than a hint.
                var diplomacy = manager.Diplomacies?.GetValueOrDefault(id) ?? throw CurrentWorldItemEditMissing("associated_Item76");
                if (diplomacy.InitialOffer.ItemId is int item) AddIfNativeItem(item);
            }
            else if (kind is 17 or 18)
            {
                var caravan = manager.Caravans?.GetValueOrDefault(id) ?? throw CurrentWorldItemEditMissing("associated_Item72");
                AddIfNativeItem(caravan.OfferedItemId);
            }
        }
        return result;

        void AddIfNativeItem(int itemId)
        {
            //4C7C30 calls the actual Item native-valid predicate before
            //adding a task's referenced object to the exclusion list.
            int slot = itemId is >= 0 and <= 201 ? itemId : 0;
            var pointer = current.ReadRegistryTableReference(RegistryKind.Item, slot)
                ?? throw CurrentWorldItemEditMissing("associated_Item_pointer");
            if (CurrentWorldItemEditNeed(current.ReadRegistryObjectNativeValidity(pointer), "associated_Item_validity"))
                result.Add(slot);
        }
    }

    private static IReadOnlyList<CurrentWorldItemEditRow> SelectCurrentWorldItemEditRows(
        IReadOnlyList<CurrentWorldItemEditRow> rows, IReadOnlyList<int> itemIds)
    {
        if (itemIds is null || itemIds.Count == 0 || itemIds.Count > 201 || itemIds.Distinct().Count() != itemIds.Count)
            throw CurrentWorldItemEditMissing("selection");
        var result = new List<CurrentWorldItemEditRow>(itemIds.Count);
        foreach (int id in itemIds)
        {
            var row = rows.FirstOrDefault(candidate => candidate.ItemId == id) ?? throw CurrentWorldItemEditMissing("selection_Item");
            if (row.IsUnavailable) throw CurrentWorldItemEditMissing("selection_unavailable");
            result.Add(row);
        }
        return result.AsReadOnly();
    }

    private static T CurrentWorldItemEditNeed<T>(T? value, string field) where T : struct =>
        value ?? throw CurrentWorldItemEditMissing(field);
    private static InvalidDataException CurrentWorldItemEditMissing(string field) => new("current-world.edit.item." + field);
}
