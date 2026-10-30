using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private CommandResult ApplyCurrentWorldItemEditCurrent(GameState state, ForceState actorForce,
        CurrentWorldEditOrder order, CommandContext context)
    {
        if (order is not EditCurrentWorldItemOrder and not EditCurrentWorldItemsOrder)
            return CommandResult.Reject("Unsupported current-world item edit.", "current-world.edit.item.order");
        try
        {
            RequireCurrentWorldItemEditProfile(state);
            var current = CaptureCurrentWorldItemEdit(state);
            var rows = ReadCurrentWorldItemEditRows(state, current);
            CurrentWorldItemEditDraft? single = order is EditCurrentWorldItemOrder one ? one.Draft : null;
            if (order is EditCurrentWorldItemOrder && single is null)
                return CommandResult.Reject("The item draft is absent.", "current-world.edit.item.draft");
            IReadOnlyList<int> ids = single is not null ? [single.ItemId]
                : ((EditCurrentWorldItemsOrder)order).ItemIds.IsDefault
                    ? [] : ((EditCurrentWorldItemsOrder)order).ItemIds;
            var selected = SelectCurrentWorldItemEditRows(rows, ids);
            int? holder = single?.HolderOfficerId ?? (order is EditCurrentWorldItemsOrder batch ? batch.HolderOfficerId : null);
            bool prohibit = single?.TransferProhibited ?? ((EditCurrentWorldItemsOrder)order).TransferProhibited;
            bool confirmed = order is EditCurrentWorldItemOrder edit ? edit.ConfirmedRemoval
                : ((EditCurrentWorldItemsOrder)order).ConfirmedRemoval;
            if (holder is < 1 or > 6000)
                return CommandResult.Reject("The item holder is outside the original Person range.", "current-world.edit.item.holder");
            //The actual refresh disables the prohibition and clears it when
            //the selected Person is invalid. This is independent of flags0/2.
            bool holderValid = false;
            if (holder is int chosen)
            {
                var pointer = current.ReadPersonLookupReference(chosen) ?? throw CurrentWorldItemEditMissing("chosen_holder_pointer");
                holderValid = CurrentWorldItemEditPersonValid(current, pointer, "chosen_holder_validity");
                bool retained = selected.All(row => row.HolderOfficerId == chosen);
                if (!retained && !ReadCurrentWorldItemEditOfficers(state, current).Any(person => person.Id == chosen))
                    return CommandResult.Reject("The selected officer is no longer an item-custody candidate.", "current-world.edit.item.holder_candidate");
            }
            prohibit &= holderValid;
            if (single is not null)
            {
                var before = selected[0];
                var values = single.Values;
                if (values is null) return CommandResult.Reject("The item values are absent.", "current-world.edit.item.values");
                try { values.Validate(); }
                catch (InvalidDataException)
                { return CommandResult.Reject("The item values exceed their original fields.", "current-world.edit.item.field_range"); }
                if (!ItemEditorCatalog.SelectableKinds.Contains(values.Kind) || values.ImageIndex != before.Values.ImageIndex ||
                    values.Worth != before.Values.Worth && values.Worth > 30)
                    return CommandResult.Reject("The edit exceeds this item's original controls.", "current-world.edit.item.field_range");
                if (!before.CanEditCustody && (holder != before.HolderOfficerId ||
                    prohibit != (before.HolderIsValid && before.TransferProhibited)))
                    return CommandResult.Reject("This item does not permit custody editing.", "current-world.edit.item.custody_disabled");
                if (holder is null && before.HolderOfficerId is >= 1 and <= 6000 && !confirmed)
                    return CommandResult.Reject("Removing this item requires the original confirmation.", "current-world.edit.item.removal_confirmation");
            }
            else if (holder is null && selected.Any(row => row.Lifecycle.Status!.Code != 2) && !confirmed)
                return CommandResult.Reject("Removing these items requires the original confirmation.", "current-world.edit.item.removal_confirmation");

            var items = state.Items ?? throw CurrentWorldItemEditMissing("items");
            var changes = new List<(ItemState Before, ItemState After, ItemDefinition? Definition)>();
            SharedSourceRandom32? random = null;
            HashSet<int>? validAreas = null;
            foreach (var row in selected)
            {
                var before = items[row.ItemId];
                var life = before.Lifecycle!;
                byte flags = (byte)((life.RetainedFlags!.Value & 0xF7) | (prohibit ? 8 : 0));
                var after = before with { Lifecycle = life with { RetainedFlags = flags } };
                if (holder is int person)
                    after = after with { Holder = new(person), Lifecycle = after.Lifecycle! with { Status = ItemStatusKnowledge.Exact(1) } };
                else if (life.Status!.Code != 2)
                {
                    if (validAreas is null)
                    {
                        validAreas = [];
                        for (int slot = 0; slot < 501; slot++)
                        {
                            var area = current.ReadAreaTableReference(slot) ?? throw CurrentWorldItemEditMissing("random_Area_pointer");
                            if (CurrentWorldItemEditNeed(current.ReadAreaNativeValidity(area), "random_Area_validity")) validAreas.Add(slot);
                        }
                        if (validAreas.Count == 0) throw CurrentWorldItemEditMissing("random_Area_candidates");
                        random = new(state.CampaignAppRandom32?.State ?? throw CurrentWorldItemEditMissing("App32_cursor"));
                    }
                    int location;
                    do
                    {
                        location = random!.Next(501);
                    } while (!validAreas.Contains(location));
                    after = after with { Holder = new(null), Lifecycle = after.Lifecycle! with
                        { Status = ItemStatusKnowledge.Exact(2), RetainedLocationCode = (ushort)location } };
                }
                //An already hidden item retains its exact independent holder
                //and location when None is selected;324580 is not re-entered.
                ItemDefinition? changedDefinition = null;
                if (single is not null && single.Values != row.Values)
                {
                    var values = single.Values;
                    var old = state.ItemDefinitions[before.DefinitionId];
                    var appearance = old.Appearance! with { Kind = values.Kind, AbilityKind = values.AbilityKind,
                        AbilityValue = values.AbilityValue, EffectKind = values.EffectKind, EffectValue = values.EffectValue };
                    var bonus = new OfficerAbilities(values.AbilityKind == 1 ? values.AbilityValue : 0,
                        values.AbilityKind == 2 ? values.AbilityValue : 0, values.AbilityKind == 3 ? values.AbilityValue : 0,
                        values.AbilityKind == 4 ? values.AbilityValue : 0, values.AbilityKind == 5 ? values.AbilityValue : 0);
                    changedDefinition = old with { Appearance = appearance, AbilityBonus = bonus, LoyaltyValue = values.Worth };
                }
                if (after != before || changedDefinition is not null) changes.Add((before, after, changedDefinition));
            }
            if (changes.Count == 0) return CommandResult.Accept(state);

            //All selected rows, source operands and random decisions are known
            //before posting any current writer. The capture belongs to this
            //operation; rejecting it never publishes its registry mutations.
            var changedItems = new Dictionary<int, ItemState>(items);
            Dictionary<int, ItemDefinition>? definitions = null;
            Dictionary<string, int>? parameters = null;
            bool registryChanged = false;
            foreach (var change in changes)
            {
                int id = change.Before.Id;
                changedItems[id] = change.After;
                if (change.Definition is { } definition)
                {
                    (definitions ??= new(state.ItemDefinitions))[definition.Id] = definition;
                    var old = state.ItemDefinitions[definition.Id].Appearance!;
                    var updated = definition.Appearance!;
                    if (old.EffectKind != updated.EffectKind || old.EffectValue != updated.EffectValue)
                    {
                        var bank = state.NativeArmySimulation!.Parameters;
                        if (bank.Float32Values.ContainsKey(PersonArmyCaptureInputs.VersionKey) ||
                            bank.Int32Values.GetValueOrDefault(PersonArmyCaptureInputs.VersionKey) != 1)
                            throw CurrentWorldItemEditMissing("Item_effect_current_owner");
                        Store(PersonArmyCaptureInputs.ItemSubtype47Key(id), updated.EffectKind);
                        Store(PersonArmyCaptureInputs.ItemLifespanExtensionKey(id), updated.EffectValue);
                        current.SetCaptureItemSubtype47(id, (byte)updated.EffectKind);
                        current.SetItemLifespanExtension(id, (byte)updated.EffectValue);
                    }
                }
                var lifecycle = change.After.Lifecycle!;
                var previousLifecycle = change.Before.Lifecycle!;
                if (change.After.Holder != change.Before.Holder || lifecycle.Status != previousLifecycle.Status)
                {
                    var pointer = current.ReadRegistryTableReference(RegistryKind.Item, id) ?? throw CurrentWorldItemEditMissing("write_Item_pointer");
                    current.SetCaptureItemOwner4A(id, (ushort)(change.After.Holder!.OfficerId ?? 0));
                    current.SetRegistryItemStatus(id, lifecycle.Status);
                    if (lifecycle.Status!.Code == 1 && previousLifecycle.Status!.Code != 1)
                    { _ = ObjectRegistration.Register(pointer, current); registryChanged = true; }
                    else if (lifecycle.Status.Code == 2 && previousLifecycle.Status!.Code != 2)
                    { _ = current.RemoveAllRegisteredItemReferences(pointer); registryChanged = true; }
                }
            }
            var next = state with { Items = changedItems, ItemDefinitions = definitions ?? state.ItemDefinitions };
            if (random is not null) next = next with { CampaignAppRandom32 = new(random.ReadCurrentState()) };
            if (parameters is not null)
            {
                var native = next.NativeArmySimulation!;
                next = next with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = parameters } } };
            }
            if (registryChanged)
            {
                var pool = current.ReadOwnedCurrentListPool() ?? throw CurrentWorldItemEditMissing("registry_pool");
                var lifecycle = next.ReadArmyLifecycle() ?? throw CurrentWorldItemEditMissing("registry_lifecycle");
                next = next.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
            }
            return CommandResult.Accept(MarkCurrentWorldEdited(next, CurrentWorldEditKind.Item));

            void Store(string key, int value)
            {
                var bank = state.NativeArmySimulation!.Parameters;
                if (bank.Float32Values.ContainsKey(key)) throw CurrentWorldItemEditMissing("integer_parameter_" + key);
                (parameters ??= new(bank.Int32Values, StringComparer.Ordinal))[key] = value;
            }
        }
        catch (InvalidDataException error)
        {
            return error.Message.StartsWith("current-world.edit.item.selection", StringComparison.Ordinal)
                ? CommandResult.Reject("The item selection is no longer editable.", "current-world.edit.item.selection")
                : CommandResult.RuleUnavailable("current-world.edit.item.input", error.Message);
        }
    }
}
