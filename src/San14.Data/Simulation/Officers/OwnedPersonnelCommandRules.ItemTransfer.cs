using San14.Core;

namespace San14.Data;

internal sealed partial class OwnedPersonnelCommandRules
{
    internal PersonnelItemCommandPreview ReadItemCommandPreview(GameState state, ForceState force,
        int? itemId, int? targetId)
    {
        var current = army.CaptureOwnedArmyReturnWorld(state).Context;
        var inputs = new CurrentItems(army, current);
        var abilities = army.CreateArmyDestructionAbilities(current);
        ItemState? item = itemId is int selected ? state.Items?.GetValueOrDefault(selected)
            ?? throw TransferMissing("preview_item") : null;
        int? previous = item is null ? null : item.Holder?.OfficerId ?? throw TransferMissing("preview_holder");
        int kind = item is null ? 0 : state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.Appearance?.AbilityKind
            ?? throw TransferMissing("preview_ability_kind");
        int itemValue = item is null ? 0 : state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.LoyaltyValue
            ?? throw TransferMissing("preview_item_value");
        var after = item is null ? state : state with
            { Items = new(state.Items!) { [item.Id] = item with { Holder = new(targetId) } } };
        PersonnelItemOfficerPreview? old = previous is int oldId ? Person(oldId, false) : null;
        PersonnelItemOfficerPreview? next = targetId is int nextId ? Person(nextId, true) : null;
        return new(itemId, targetId, kind, old, next);

        PersonnelItemOfficerPreview Person(int id, bool grant)
        {
            var person = state.Officers.GetValueOrDefault(id) ?? throw TransferMissing("preview_person");
            var input = inputs.Resolve(state, force, new(grant ? PersonnelActionKind.GrantItem : PersonnelActionKind.RevokeItem,
                id, itemId)) ?? throw TransferMissing("preview_person_guards");
            var guards = input.LoyaltyGuards ?? throw TransferMissing("preview_loyalty_guards");
            bool change = PersonnelLoyaltyMath.ShouldChange(guards) ?? throw TransferMissing("preview_loyalty_gate");
            int? loyalty = person.Loyalty;
            if (change && item is not null)
            {
                if (person.Loyalty is not int before) throw TransferMissing("preview_person_loyalty");
                int penalty = grant ? 0 : input.ConfiscationPenalty ?? throw TransferMissing("preview_penalty");
                loyalty = PersonnelLoyaltyMath.TryApply(checked((byte)before), checked((byte)itemValue), grant, penalty, guards);
                if (loyalty is null or < 0 or > 100) throw TransferMissing("preview_loyalty_domain");
            }
            var beforeAbility = abilities.Resolve(state, id) ?? throw TransferMissing("preview_before_abilities");
            var afterAbility = abilities.Resolve(after, id) ?? throw TransferMissing("preview_after_abilities");
            if (!ItemAbilityBonuses.TryCalculate(state, id, out var beforeItems) ||
                !ItemAbilityBonuses.TryCalculate(after, id, out var afterItems)) throw TransferMissing("preview_item_margins");
            return PersonPreview(person, loyalty, beforeAbility, afterAbility,
                DisplayAbility(beforeAbility, beforeItems!, afterItems!, kind), input, grant, itemValue);
        }
    }

    public CommandResult PrepareItemTransfer(GameState state, ForceState force, PersonnelChange change,
        PersonnelItemTransferPreview transfer, CommandContext context)
    {
        try { return army.PreparePersonnelItemTransferCurrent(state, change); }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "The current item narration owner is unresolved."); }
    }

    public CommandResult CompleteItemTransfer(GameState state, ForceState force, PersonnelChange change,
        PersonnelItemTransferPreview transfer, CommandContext context)
    {
        try { return CommandResult.Accept(army.CompletePersonnelItemTransferCurrent(state, transfer)); }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "The current item accounting owner is unresolved."); }
    }

    // 1F03F0 removes the old holder's loyalty contribution, applies the new
    // holder's contribution, changes custody, then charges the one command.
    private PersonnelEvaluation EvaluateItemTransfer(GameState state, ForceState force,
        PersonnelChange change, CommandContext actor, byte flags)
    {
        try
        {
            if (change.ItemId is not int itemId || change.PreviousHolderId is not int previous ||
                change.NextHolderId is not int next || previous == next ||
                state.Items?.GetValueOrDefault(itemId) is not { } item ||
                item.Holder?.OfficerId != previous ||
                !state.Officers.TryGetValue(previous, out var oldHolder) ||
                !state.Officers.TryGetValue(next, out var newHolder) ||
                oldHolder.ForceId != force.Id || newHolder.ForceId != force.Id)
                return new(false, Reason: "The item transfer's current holders do not match the command.");
            var current = army.CaptureOwnedArmyReturnWorld(state).Context;
            if (!AdmitTransferOwner(state, current, force.Id, previous) ||
                !AdmitTransferRecipient(current, force.Id, next))
                return new(false, Reason: "The item holder or recipient is unavailable for the current transfer.");
            var inputs = new CurrentItems(army, current);
            var rules = new PersonnelCommandRules(inputs);
            var remove = rules.Evaluate(state, force,
                new(PersonnelActionKind.RevokeItem, previous, itemId, previous, next), actor);
            if (remove is not { Allowed: true, Quote: { } oldQuote }) return remove;
            var grant = rules.Evaluate(state, force,
                new(PersonnelActionKind.GrantItem, next, itemId, previous, next), actor);
            if (grant is not { Allowed: true, Quote: { } newQuote }) return grant;
            if (oldQuote.DistrictId != newQuote.DistrictId || oldQuote.CommandBooks != newQuote.CommandBooks ||
                oldQuote.Gold != 0 || newQuote.Gold != 0 || oldQuote.Revision != newQuote.Revision)
                throw TransferMissing("accounting_owner");
            int districtId = newQuote.DistrictId ?? throw TransferMissing("headquarters_district");
            int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining
                ?? throw TransferMissing("district_command_books");
            long reserved = CommandBookAvailability.Reserved(state, districtId);
            long available = books - reserved;
            if (available < newQuote.CommandBooks)
                return new(false, newQuote, Reason: "District lacks uncommitted command books.");

            int? oldAfter = LoyaltyAfter(oldHolder, oldQuote.LoyaltyDelta);
            int? newAfter = LoyaltyAfter(newHolder, newQuote.LoyaltyDelta);
            var after = state with
            {
                Items = new(state.Items!) { [itemId] = item with
                {
                    Holder = new(next),
                    Lifecycle = (item.Lifecycle ?? throw TransferMissing("item_lifecycle")) with
                        { RetainedFlags = (byte)(flags & 0xF7) }
                } },
                Officers = new(state.Officers)
                {
                    [previous] = oldHolder with { Loyalty = oldAfter },
                    [next] = newHolder with { Loyalty = newAfter }
                }
            };
            // This resolver reads the supplied current inventory and all normal
            // health/appointment/doctrine/cap inputs. The live identity owner is
            // shared; no persistent ability cache or simplified item sum is made.
            var abilities = army.CreateArmyDestructionAbilities(current);
            var oldBeforeAbility = abilities.Resolve(state, previous) ?? throw TransferMissing("old_holder_abilities");
            var oldAfterAbility = abilities.Resolve(after, previous) ?? throw TransferMissing("old_holder_after_abilities");
            var newBeforeAbility = abilities.Resolve(state, next) ?? throw TransferMissing("new_holder_abilities");
            var newAfterAbility = abilities.Resolve(after, next) ?? throw TransferMissing("new_holder_after_abilities");
            int kind = state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.Appearance?.AbilityKind
                ?? throw TransferMissing("item_ability_kind");
            if (!ItemAbilityBonuses.TryCalculate(state, previous, out var oldItemBefore) ||
                !ItemAbilityBonuses.TryCalculate(after, previous, out var oldItemAfter) ||
                !ItemAbilityBonuses.TryCalculate(state, next, out var newItemBefore) ||
                !ItemAbilityBonuses.TryCalculate(after, next, out var newItemAfter))
                throw TransferMissing("item_bonus_margins");
            var oldInput = inputs.Resolve(state, force, new(PersonnelActionKind.RevokeItem, previous, itemId))
                ?? throw TransferMissing("old_display_guards");
            var newInput = inputs.Resolve(state, force, new(PersonnelActionKind.GrantItem, next, itemId))
                ?? throw TransferMissing("new_display_guards");
            var oldPreview = PersonPreview(oldHolder, oldAfter, oldBeforeAbility, oldAfterAbility,
                DisplayAbility(oldBeforeAbility, oldItemBefore!, oldItemAfter!, kind), oldInput, false,
                state.ItemDefinitions[item.DefinitionId].LoyaltyValue ?? throw TransferMissing("item_loyalty_value"));
            var newPreview = PersonPreview(newHolder, newAfter, newBeforeAbility, newAfterAbility,
                DisplayAbility(newBeforeAbility, newItemBefore!, newItemAfter!, kind), newInput, true,
                state.ItemDefinitions[item.DefinitionId].LoyaltyValue ?? throw TransferMissing("item_loyalty_value"));
            var preview = new PersonnelItemTransferPreview(itemId, oldPreview, newPreview,
                districtId, newQuote.CommandBooks, books, reserved, available,
                Math.Clamp(unchecked(books - newQuote.CommandBooks), 0, 200)) { AbilityKind = kind };
            var targetQuote = change.Kind == PersonnelActionKind.GrantItem ? newQuote : oldQuote;
            return new(true, targetQuote with { ItemFlagsAfter = (byte)(flags & 0xF7) })
                { ItemTransferPreview = preview };
        }
        catch (InvalidDataException error)
        {
            return new(null, MissingRuleKey: error.Message,
                Reason: "The current item transfer requires its owned loyalty, ability and accounting inputs.");
        }
    }

    private static PersonnelItemOfficerPreview PersonPreview(OfficerState person, int? after,
        OfficerAbilities beforeAbility, OfficerAbilities afterAbility, OfficerAbilities displayAbility,
        PersonnelCommandContext input, bool grant, int itemValue)
    {
        var guards = input.LoyaltyGuards ?? throw TransferMissing("display_guards");
        bool isRuler = guards.IsRuler ?? throw TransferMissing("display_ruler");
        bool change = PersonnelLoyaltyMath.ShouldChange(guards) ?? throw TransferMissing("display_loyalty_gate");
        int? before = isRuler ? null : person.Loyalty is int loyalty ? Math.Min(loyalty, 100) : null;
        int? displayAfter = change ? after is int value ? Math.Min(value, 100) : null : null;
        int? magnitude = change ? grant ? Math.Min(itemValue, 50)
            : before is int a && displayAfter is int b && b < 100 ? Math.Max(a - b, 0) : null : null;
        return new(person.Id, person.Loyalty, after, beforeAbility, afterAbility)
        {
            IsRuler = isRuler, LoyaltyChangeVisible = change,
            DisplayLoyaltyBefore = before, DisplayLoyaltyAfter = displayAfter,
            DisplayLoyaltyChangeMagnitude = magnitude, DisplayAbilitiesAfter = displayAbility
        };
    }

    // The original UI changes the one item dimension after obtaining the full
    // current ability. It does not apply the final ability cap a second time.
    private static OfficerAbilities DisplayAbility(OfficerAbilities before, OfficerAbilities itemBefore,
        OfficerAbilities itemAfter, int kind)
    {
        int At(OfficerAbilities a) => kind switch
        { 1 => a.Leadership, 2 => a.Strength, 3 => a.Intelligence, 4 => a.Politics, 5 => a.Charm, _ => 0 };
        int delta = At(itemAfter) - At(itemBefore);
        return kind switch
        {
            0 => before,
            1 => before with { Leadership = unchecked(before.Leadership + delta) },
            2 => before with { Strength = unchecked(before.Strength + delta) },
            3 => before with { Intelligence = unchecked(before.Intelligence + delta) },
            4 => before with { Politics = unchecked(before.Politics + delta) },
            5 => before with { Charm = unchecked(before.Charm + delta) },
            _ => throw TransferMissing("item_ability_kind_range")
        };
    }

    internal static bool AdmitTransferOwner(GameState state, SourceWorldContext current, int force, int id)
    {
        int person = current.ResolveOfficerSource(id) ?? throw TransferMissing("holder_pointer");
        if (!Need(current.ReadPersonNativeValidity(person), "holder_validity") ||
            Need(current.ReadPersonOldForceSlot(person), "holder_force") != force) return false;
        var associated = current.ReadPersonLinkedEntity(person) ?? throw TransferMissing("holder_associated");
        if (Need(current.ReadLinkedEntityNativeValidity(associated), "holder_associated_validity") ||
            Need(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, person), "holder_army_validity")) return false;
        byte status = Need(current.ReadPersonStatus(person), "holder_status");
        return status is >= 1 and <= 5 or > 9;
    }

    internal static bool AdmitTransferRecipient(SourceWorldContext current, int force, int id, bool requireRegistration = true)
    {
        int person = current.ResolveOfficerSource(id) ?? throw TransferMissing("recipient_pointer");
        if ((requireRegistration && !(current.ReadRegisteredPersonPhysicalSlots() ?? throw TransferMissing("Person_registry")).Contains(person)) ||
            !Need(current.ReadPersonNativeValidity(person), "recipient_validity") ||
            Need(current.ReadPersonOldForceSlot(person), "recipient_force") != force ||
            Need(current.ReadPersonStatus(person), "recipient_status") is < 1 or > 4) return false;
        ushort flags = Need(current.ReadArmyReturnPersonFlags196(person), "recipient_flags");
        if (Need(OfficerHealthSourceAdapter.ReadByte(current, person), "recipient_health") >= 2 ||
            (flags & 1) != 0) return false;
        bool band = false;
        for (int i = 0; i < 5; i++) if (id >= 4111 + i * 20 && id <= 4120 + i * 20) { band = true; break; }
        if (band && Need(current.ReadPersonFirstLocation(person), "recipient_home") is >= 1 and <= 61)
        {
            var affiliation = current.ReadPersonForce(person) ?? throw TransferMissing("recipient_affiliation");
            int actualForce = Need(current.ReadForcePhysicalSlot(affiliation), "recipient_actual_force");
            if (actualForce is < 46 or > 50) return false;
        }
        if ((flags & 0x4000) != 0) return false;
        if (PersonPositionRepair.DecodeFirstLocation(Need(current.ReadPersonFirstLocation(person), "recipient_home")) !=
            PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(person), "recipient_position"))) return false;
        var linked = current.ReadPersonLinkedEntity(person) ?? throw TransferMissing("recipient_associated");
        if (!Need(current.ReadLinkedEntityNativeValidity(linked), "recipient_associated_validity")) return true;
        var owner = current.ReadOwnedPersonCommandManager() ?? throw TransferMissing("recipient_task_manager");
        int task = Need(linked.Identity, "recipient_associated_identity");
        if (owner.Disappearances?.ContainsKey(task) == true) return false;
        if (owner.Returns.ContainsKey(task) || owner.Talents?.ContainsKey(task) == true ||
            owner.Diplomacies?.ContainsKey(task) == true || owner.Strategies?.ContainsKey(task) == true ||
            owner.Independences?.ContainsKey(task) == true || owner.Caravans?.ContainsKey(task) == true ||
            owner.Intelligences?.ContainsKey(task) == true) return true;
        throw TransferMissing("recipient_associated_virtual98");
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw TransferMissing(field);
    private static int? LoyaltyAfter(OfficerState holder, int delta) => delta == 0 ? holder.Loyalty
        : holder.Loyalty is int loyalty ? checked(loyalty + delta)
        : throw TransferMissing("holder_loyalty");
    private static InvalidDataException TransferMissing(string owner) => new("personnel.item_transfer." + owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal CommandResult PreparePersonnelItemTransferCurrent(GameState state, PersonnelChange change)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        int previous = change.PreviousHolderId ?? throw Missing("previous_holder");
        int next = change.NextHolderId ?? throw Missing("next_holder");
        int target = current.ResolveOfficerSource(next) ?? throw Missing("target");
        bool rulerTarget = (current.ReadPersonStatus(target) ?? throw Missing("target_status")) == 1;
        int speaker = rulerTarget ? previous : next;
        int person = current.ResolveOfficerSource(speaker) ?? throw Missing("speaker");
        var force = current.ReadPersonForce(person) ?? throw Missing("speaker_force");
        int physical = force.PhysicalSlot ?? throw Missing("speaker_force_receiver");
        int ruler = current.ReadForceRecord(physical)?.RawRuler10 ?? throw Missing("speaker_ruler");
        var text = FormatReportMessage(state, current, rulerTarget ? 0x5876 : 0x586A,
            new(Person0: speaker, Person1: ruler), ReportMessageOrigin.PersonnelItemTransfer, null,
            _personnelNarrationVoice ?? throw Missing("voice_settings"));
        var effects = new List<CampaignEffectRequest>();
        PersonnelAdministrationSound(effects, rulerTarget ? 6 : 4);
        effects.Add(PersonnelAdministrationOfficerLine(current, state, speaker, ruler, text));
        return CommandResult.Accept(text.State) with { PersonnelItemTransferAnnouncement = new(physical,
            change.ItemId ?? throw Missing("item"), previous, next, effects.AsReadOnly()) };

        static InvalidDataException Missing(string field) => new("personnel.item_narration." + field);
    }

    internal GameState CompletePersonnelItemTransferCurrent(GameState state, PersonnelItemTransferPreview transfer)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        var saved = state.NativeForceUnregisterCurrent ?? throw Missing("district_window_owner");
        byte?[] window = saved.DistrictWindows10.GetValueOrDefault(transfer.DistrictId)?.ToArray()
            ?? throw Missing("district_window");
        if (window.Length < 5) throw Missing("district_window_length");
        window[4] = checked((byte)transfer.DistrictBooksAfter);
        current.SetDistrictRawByte(transfer.DistrictId, 0x14, window[4]);
        state = state with { NativeForceUnregisterCurrent = saved with
            { DistrictWindows10 = new(saved.DistrictWindows10) { [transfer.DistrictId] = window } } };
        // The original optional application branch cannot add statistics when
        // its independently saved user is -1. No application presence is guessed.
        int selected = state.CampaignEventWorld?.CurrentUserIndex ?? throw Missing("selected_user");
        if (selected == -1 || !HasCommandAccountingUser(state)) return state;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_pointer");
        if (!(current.ReadForceNativeValidity(player) ?? throw Missing("player_validity"))) return state;
        int playerForce = current.ReadForcePhysicalSlot(player) ?? throw Missing("player_identity");
        int districtForce = current.ReadDistrictForceId(transfer.DistrictId) ?? throw Missing("district_force");
        return districtForce == playerForce ? ApplyCommandBookUsageCurrent(state, current, transfer.CommandBooksCost) : state;

        static InvalidDataException Missing(string field) => new("personnel.item_accounting." + field);
    }
}
