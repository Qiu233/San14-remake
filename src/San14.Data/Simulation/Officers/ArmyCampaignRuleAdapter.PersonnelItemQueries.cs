using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed record PersonnelItemCommandRow(int ItemId, string Name, string KindText,
    string AbilityText, string EffectText, int LoyaltyValue, string HolderName, string HolderStatusText)
{
    public IReadOnlyDictionary<int, PersonnelGroupPersonCell> SortCells { get; init; } =
        new Dictionary<int, PersonnelGroupPersonCell>();
}
public sealed record PersonnelItemCommandBudget(int DistrictId, int CommandBooksCost,
    int DistrictBooksBefore, long ReservedCommandBooks, long AvailableCommandBooks, int DistrictBooksAfter);
public sealed record PersonnelItemCommandPreview(int? ItemId, int? TargetOfficerId, int AbilityKind,
    PersonnelItemOfficerPreview? PreviousHolder, PersonnelItemOfficerPreview? NextHolder);
public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Each nullable selection is a genuine absent UI selection. The
    /// old-holder panel can preview removal before a recipient is selected;
    /// a recipient chosen first retains its actual card and zero item margin.</summary>
    public AreaAdministrationQuery<PersonnelItemCommandPreview> QueryPersonnelItemCommandPreview(GameState state,
        int forceId, int? itemId, int? targetId)
    {
        try
        {
            var force = state.Forces.GetValueOrDefault(forceId) ?? throw ItemCommandMissing("preview_force");
            return AreaAdministrationQuery<PersonnelItemCommandPreview>.Known(
                new OwnedPersonnelCommandRules(this).ReadItemCommandPreview(state, force, itemId, targetId));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<PersonnelItemCommandPreview>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<PersonnelItemCommandBudget> QueryPersonnelItemCommandBudget(GameState state, int forceId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int district = ForcePrimaryDistrictSource.Resolve(current, new(forceId)).PhysicalSlot
                ?? throw ItemCommandMissing("budget_district");
            int books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw ItemCommandMissing("budget_books");
            int cost = PersonnelCommandParameters.Read(state, PersonnelCommandParameters.ItemCommandBooks)
                ?? throw ItemCommandMissing("budget_cost");
            if (cost < 0) throw ItemCommandMissing("budget_cost_range");
            long reserved = CommandBookAvailability.Reserved(state, district);
            //6F6210 displays the raw subtraction; actual charging clamps0..200.
            return AreaAdministrationQuery<PersonnelItemCommandBudget>.Known(
                new(district, cost, books, reserved, books - reserved, unchecked(books - cost)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<PersonnelItemCommandBudget>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<IReadOnlyList<int>> QueryPersonnelItemCommandCandidates(GameState state, int forceId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var rows = new List<(int Item, int Status)>();
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw ItemCommandMissing("item_registry"))
            {
                var reference = entry ?? throw ItemCommandMissing("item_registry_payload");
                int id = reference.PhysicalSlot ?? throw ItemCommandMissing("item_registry_receiver");
                if (!(current.ReadRegistryObjectNativeValidity(reference) ?? throw ItemCommandMissing("item_validity"))) continue;
                var item = state.Items?.GetValueOrDefault(id) ?? throw ItemCommandMissing("item_owner");
                byte flags = item.Lifecycle?.RetainedFlags ?? throw ItemCommandMissing("item_flags");
                if ((flags & 1) == 0 || item.Holder?.OfficerId is not int holder ||
                    !OwnedPersonnelCommandRules.AdmitTransferOwner(state, current, forceId, holder)) continue;
                int person = current.ResolveOfficerSource(holder) ?? throw ItemCommandMissing("holder_pointer");
                int status = current.ReadPersonStatus(person) ?? throw ItemCommandMissing("holder_status");
                rows.Add((id, status));
            }
            //69D430 indexes first;69F6D0 then stably orders by holder status.
            return AreaAdministrationQuery<IReadOnlyList<int>>.Known(rows.OrderBy(row => row.Item)
                .OrderBy(row => row.Status).Select(row => row.Item).ToArray());
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<int>>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<IReadOnlyList<int>> QueryPersonnelItemTargetCandidates(GameState state, int forceId, int? itemId)
    {
        try
        {
            int? holder = itemId is int selected
                ? state.Items?.GetValueOrDefault(selected)?.Holder?.OfficerId ?? throw ItemCommandMissing("selected_holder")
                : null;
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var result = new List<int>();
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw ItemCommandMissing("target_registry"))
            {
                int physical = entry ?? throw ItemCommandMissing("target_registry_payload");
                int id = current.ReadPersonIdentity(physical) ?? throw ItemCommandMissing("target_identity");
                if (id != holder && OwnedPersonnelCommandRules.AdmitTransferRecipient(current, forceId, id, false)) result.Add(id);
            }
            return AreaAdministrationQuery<IReadOnlyList<int>>.Known(result.AsReadOnly());
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<int>>.Unknown(error.Message); }
    }

    /// <summary>Uses the complete current Person information cells and their
    /// comparers with the item caller's actual eleven-tab resource metadata.</summary>
    public AreaAdministrationQuery<OfficerInformationPageSet> QueryPersonnelItemTargetPages(GameState state,
        int forceId, IReadOnlyList<int> officerIds, IReadOnlyList<OfficerInformationGroup> groups)
    {
        try
        {
            var source = new OfficerInformationCurrent(this, state, forceId);
            // This caller exposes the existing personal-disposition columns.
            // Bind only its selected Person owners, using the same aftermath
            // source as normal simulation rather than guessing missing values.
            foreach (int id in officerIds)
            {
                int physical = source.current.ResolveOfficerSource(id) ?? throw ItemCommandMissing("target_aftermath_pointer");
                source.current.SetOfficerAftermath(physical, _personDestructionInputs.ReadCurrent(state, physical));
            }
            return AreaAdministrationQuery<OfficerInformationPageSet>.Known(
                source.Pages(officerIds, "",
                    System.Collections.Immutable.ImmutableArray.CreateRange(groups)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OfficerInformationPageSet>.Unknown(error.Message); }
    }

    /// <summary>The ordinary item selector's seven source columns, over the
    /// caller's already admitted items. One current identity view serves all
    /// rows; this list does not create portraits or change the command world.</summary>
    public AreaAdministrationQuery<IReadOnlyList<PersonnelItemCommandRow>> QueryPersonnelItemCommandRows(
        GameState state, IReadOnlyList<int> itemIds)
    {
        try
        {
            var current = CaptureDetailForceInputs(state) ?? throw ItemCommandMissing("current");
            var result = new List<PersonnelItemCommandRow>(itemIds.Count);
            foreach (int id in itemIds)
            {
                var item = state.Items?.GetValueOrDefault(id) ?? throw ItemCommandMissing("item");
                var definition = state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)
                    ?? throw ItemCommandMissing("definition");
                var appearance = definition.Appearance ?? throw ItemCommandMissing("appearance");
                int value = Math.Min(definition.LoyaltyValue ?? throw ItemCommandMissing("loyalty_value"), 50);
                int raw = (item.Holder ?? throw ItemCommandMissing("holder")).OfficerId ?? 0;
                var reference = (raw <= 6000 ? current.ReadPersonLookupReference(raw)
                    : current.ReadTransferPersonTableReference(0)) ?? throw ItemCommandMissing("holder_lookup");
                int physical = reference.PhysicalSlot ?? throw ItemCommandMissing("holder_receiver");
                bool valid = current.ReadPersonNativeValidity(physical) ?? throw ItemCommandMissing("holder_validity");
                string name = "", statusText = "";
                string? holderKey = null;
                int? statusNumber = null;
                if (valid)
                {
                    int identity = current.ReadPersonIdentity(physical) ?? throw ItemCommandMissing("holder_identity");
                    name = state.Officers.GetValueOrDefault(identity)?.Name ?? throw ItemCommandMissing("holder_name");
                    holderKey = state.Officers.GetValueOrDefault(identity)?.CurrentNameSortKey;
                    byte status = current.ReadPersonStatus(physical) ?? throw ItemCommandMissing("holder_status");
                    statusNumber = status;
                    //30C1B0: only status0..9 index the Data0 caption table.
                    statusText = status < 10 ? ReadHistoryDataText(0, 0xF9 + status) : "";
                }
                result.Add(new(id, definition.Name,
                    appearance.Kind is >= 1 and <= 26 ? ReadHistoryDataText(0, 0x81E + appearance.Kind) : "",
                    appearance.AbilityKind == 0 ? "" : ReadHistoryDataText(0, 0x183 + appearance.AbilityKind - 1) +
                        "+" + Math.Min(appearance.AbilityValue, 10).ToString(CultureInfo.InvariantCulture),
                    appearance.EffectKind is 1 or 2 ? ReadHistoryDataText(0, 0x839 + appearance.EffectKind) : "",
                    value, name, statusText)
                {
                    SortCells = new Dictionary<int, PersonnelGroupPersonCell>
                    {
                        [0] = new(0, null, null, null, definition.NameSortKey, false, null)
                            { SortUnresolvedOwner = definition.NameSortKey is null ? "personnel.item_name_sort_key" : null },
                        [1] = new(1, null, appearance.Kind, appearance.Kind, null, false, null),
                        [8] = new(8, null, null, appearance.AbilityKind == 0 ? int.MaxValue :
                            (5 - appearance.AbilityKind) * 1000 + Math.Min(appearance.AbilityValue, 10), null, appearance.AbilityKind == 0, null),
                        [3] = new(3, null, null, appearance.EffectKind == 0 ? int.MaxValue : appearance.EffectKind, null, appearance.EffectKind == 0, null),
                        [4] = new(4, null, value, value, null, false, null),
                        [6] = new(6, null, null, null, holderKey, !valid, null)
                            { SortUnresolvedOwner = valid && holderKey is null ? "personnel.holder_name_sort_key" : null },
                        [9] = new(9, null, statusNumber, statusNumber ?? int.MaxValue, null, !valid, null)
                    }
                });
            }
            return AreaAdministrationQuery<IReadOnlyList<PersonnelItemCommandRow>>.Known(result.AsReadOnly());
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<PersonnelItemCommandRow>>.Unknown(error.Message); }
    }

    /// <summary>The selected recipient uses the existing full person card.
    /// The transfer's after-values are supplied separately by its evaluation.</summary>
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryPersonnelItemTransferOfficerCard(
        GameState state, int? officerId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int physical = officerId is int id ? current.ResolveOfficerSource(id) ?? throw ItemCommandMissing("recipient") : 0;
            bool valid = officerId.HasValue && (current.ReadPersonNativeValidity(physical) ?? throw ItemCommandMissing("recipient_validity"));
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(
                cards.ReadCity(state.PeriodNumber, physical, valid, 0));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(error.Message); }
    }

    private static InvalidDataException ItemCommandMissing(string owner) => new("personnel.item_display." + owner);
}
