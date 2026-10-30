using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelGroupMovementExecution
{
    internal PersonnelGroupMovementRules CapturePersonnelGroupMovementRules(GameState state,
        SourceWorldContext current, int forceId) => new(state, current, forceId);

    CommandResult IPersonnelGroupMovementExecution.Move(GameState state, ForceState force,
        PreparePersonnelGroupMoveOrder order, CommandContext context)
    {
        try
        {
            if (state.Phase != TurnPhase.Strategy || context.DelegatedDistrictId is not null || force.Id != order.ForceId)
                return CommandResult.Reject("Personnel group movement requires direct strategy control.", "personnel.groups.move_scope");
            RequirePersonnelGroupApplicationCurrent();
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            PersonReturnCommands.BindCurrent(state, current);
            var rules = CapturePersonnelGroupMovementRules(state, current, force.Id);
            if (order.Groups is not { Count: > 0 and <= 10 } ||
                order.Groups.Select(group => group.GroupIndex).Distinct().Count() != order.Groups.Count)
                return CommandResult.Reject("Invalid personnel group selection.", "personnel.groups.move_selection");
            var selectedGroups = order.Groups.OrderBy(group => group.GroupIndex).ToArray();
            foreach (var group in selectedGroups)
            {
                if (group.GroupIndex is < 0 or > 9 || !rules.IsGroupSelectable(group.GroupIndex))
                    return CommandResult.Reject("The personnel group is no longer available.", "personnel.groups.move_group");
                if (!rules.DestinationLocationIds.Contains(group.DestinationLocationId))
                    return CommandResult.Reject("Destination is not controlled by this force.", "personnel.groups.move_destination");
                //715420 retains native-valid saved members across allegiance
                //changes.71BE00 does not impose a per-member Force gate; its
                //actual15E/232370/Home tests govern each individual movement.
            }
            if (rules.AvailableBooks - CommandBookAvailability.Reserved(state, rules.DistrictId) < rules.BookCost)
                return CommandResult.Reject("District lacks uncommitted command books.", "personnel.groups.move_books");

            var working = state;
            var moved = new List<int>();
            var leaders = new List<int>();
            var destinationChanges = new Dictionary<int, int?>();
            int? reportDestination = null;
            bool alreadyAtDestination = false;
            //The selected source rows retain group-index order;71BE00 then
            //preserves each row's saved member order.
            //Each actual moving Person gets its own1F1360 singleton command.
            //The current flags written by one group govern later duplicates.
            foreach (var group in selectedGroups)
            {
                int movedBefore = moved.Count;
                var members = rules.ReadGroupMembers(group.GroupIndex);
                if (members.Count > 0 && rules.CanMoveOfficer(members[0]) &&
                    rules.Home(members[0]) != group.DestinationLocationId) leaders.Add(members[0]);
                foreach (int id in members)
                {
                    if (!rules.CanMoveOfficer(id)) continue;
                    if (rules.Home(id) == group.DestinationLocationId)
                    { alreadyAtDestination = true; continue; }
                    var result = PersonReturnCommands.StartReassignment(working, current, [id], group.DestinationLocationId);
                    working = result.State ?? throw PersonnelGroupMovementRules.Missing(
                        result.UnresolvedOwner ?? "movement1F1360");
                    moved.Add(id);
                    reportDestination ??= group.DestinationLocationId;
                }
                destinationChanges[group.GroupIndex] = moved.Count > movedBefore ? group.DestinationLocationId : null;
            }

            var effects = new List<CampaignEffectRequest>();
            if (alreadyAtDestination)
            {
                var notice = FormatPersonnelAdministrationCurrent(working, current, 0x5C15, new());
                working = notice.State;
                PersonnelAdministrationAnnouncement(effects, notice.Text);
            }
            if (moved.Count > 0)
            {
                int speaker = SelectPersonnelGroupMoveRepresentative(working, current, leaders.Count > 0 ? leaders : moved);
                int ruler = PersonnelAdministrationRuler(state, force.Id);
                var line = FormatPersonnelAdministrationCurrent(working, current, 0x579C,
                    new(Person0: speaker, Person1: ruler, Base0: checked((ushort)reportDestination!.Value), Integers: [moved.Count]));
                working = line.State;
                effects.Add(PersonnelAdministrationOfficerLine(current, state, speaker, ruler, line));
            }

            //737E20 debits once, after the entire batch and its source speech.
            int after = Math.Clamp(unchecked(rules.AvailableBooks - rules.BookCost), 0, 200);
            current.SetDistrictRawByte(rules.DistrictId, 0x14, (byte)after);
            var district = working.Districts[rules.DistrictId];
            var saved = working.NativeForceUnregisterCurrent ?? throw PersonnelGroupMovementRules.Missing("payment_saved_District");
            var window = saved.DistrictWindows10.GetValueOrDefault(rules.DistrictId)?.ToArray()
                ?? throw PersonnelGroupMovementRules.Missing("payment_District_window");
            window[4] = (byte)after;
            working = working with
            {
                Districts = new(working.Districts) { [rules.DistrictId] = district with { OrdersRemaining = after } },
                NativeForceUnregisterCurrent = saved with
                { DistrictWindows10 = new(saved.DistrictWindows10) { [rules.DistrictId] = window } }
            };
            var charged = current.ReadDistrictTableReference(rules.DistrictId)
                ?? throw PersonnelGroupMovementRules.Missing("payment_District_pointer");
            if (HasCommandAccountingUser(working) &&
                BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, charged, PersonnelGroupMovementRules.Missing))
                working = ApplyCommandBookUsageCurrent(working, current, rules.BookCost);
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = working with
            {
                CampaignEffectRequests = (working.CampaignEffectRequests ?? throw PersonnelGroupMovementRules.Missing("pending_effects"))
                    .Append(CampaignEffectSource.PersonnelAdministrationCommands,
                        working.NativeMapSeason?.AdvancedCalendarSteps ?? throw PersonnelGroupMovementRules.Missing("calendar"), effects)
            };
            var accepted = working.WithArmyLifecycle((working.ReadArmyLifecycle()
                ?? throw PersonnelGroupMovementRules.Missing("Army_lifecycle")) with { Indexes = world.Pool.ExportOwnedState() });
            //The application's destination cache is not a world/save owner.
            //Publish only after every fallible world operation has completed.
            PublishPersonnelGroupMoveDestinationsCurrent(destinationChanges);
            return CommandResult.Accept(accepted);
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable("personnel.groups.move_rules", error.Message); }
    }

    private int SelectPersonnelGroupMoveRepresentative(GameState state, SourceWorldContext current, IReadOnlyList<int> ids)
    {
        //778D70 ->693B10: status ascending, effective five-ability sum
        //descending, identity ascending. The first-group leaders have priority
        //as a candidate set; only an empty set falls back to all movers.
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var scores = new Dictionary<int, int>();
        int Score(int id)
        {
            if (scores.TryGetValue(id, out int cached)) return cached;
            int sum = 0;
            for (int dimension = 0; dimension < 5; dimension++)
                sum = unchecked(sum + (abilities.ResolveDimension(state, id, dimension,
                    (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
                    ?? throw PersonnelGroupMovementRules.Missing("speaker_effective_ability")));
            scores.Add(id, sum); return sum;
        }
        byte Status(int id) => current.ReadPersonStatus(current.ResolveOfficerSource(id)
            ?? throw PersonnelGroupMovementRules.Missing("speaker_pointer"))
            ?? throw PersonnelGroupMovementRules.Missing("speaker_status");
        return ids.OrderBy(Status).ThenByDescending(Score).ThenBy(id => id).First();
    }
}

/// <summary>One captured working current for the group page and its batch
/// command. Saved membership is distinct from current availability.</summary>
internal sealed class PersonnelGroupMovementRules
{
    internal GameState SourceState { get; }
    internal SourceWorldContext Current { get; }
    internal int ForceId { get; }
    internal int DistrictId { get; }
    internal int BookCost => CityOfficerMovementSource.ReadCost(SourceState);
    internal int AvailableBooks => SourceState.Districts.GetValueOrDefault(DistrictId)?.OrdersRemaining
        ?? throw Missing("District14");
    internal IReadOnlyList<int> DestinationLocationIds { get; }
    private readonly PersonnelGroupsState groups;

    internal PersonnelGroupMovementRules(GameState state, SourceWorldContext current, int forceId)
    {
        SourceState = state; Current = current; ForceId = forceId;
        groups = state.PersonnelGroups ?? throw Missing("saved_groups");
        if (groups.Groups.IsDefault || groups.Groups.Length != 10) throw Missing("ten_groups");
        var force = current.ReadForceTableReference(forceId) ?? throw Missing("Force_pointer");
        if (!K(current.ReadForceNativeValidity(force), "Force_validity")) throw Missing("invalid_Force");
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        DistrictId = K(primary.PhysicalSlot, "primary_District");
        var destinations = new List<int>();
        //737E20 uses World80 registration order and22CAD0 pointer equality.
        foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("Base_registry"))
        {
            var at = entry ?? throw Missing("registered_Base_pointer");
            int raw = K(current.ReadBaseForceSlot(at), "Base_Force");
            var owner = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("Base_Force_pointer");
            if (owner == force) destinations.Add(K(current.ReadBaseObjectLink(at), "Base4E"));
        }
        DestinationLocationIds = destinations.AsReadOnly();
    }

    internal IReadOnlyList<int> ReadGroupMembers(int index)
    {
        if (index is < 0 or > 9 || groups.Groups[index].IsDefault || groups.Groups[index].Length > 50)
            throw Missing("group_index_or_members");
        //715420 resolves all50 slots but appends only native-valid Persons.
        return groups.Groups[index].Where(id => K(Current.ReadPersonNativeValidity(Person(id)), "member_validity")).ToArray();
    }

    internal bool IsGroupSelectable(int index)
    {
        foreach (int id in ReadGroupMembers(index))
        {
            int person = Person(id);
            int home = Home(id);
            var at = BaseLookup.Find(home, Current);
            int location = K(Current.ReadBaseObjectLink(at), "selectable_Base4E");
            if (home == location && K(Current.ReadPersonSecondLocation(person), "selectable_PersonE4") == location &&
                (K(Current.ReadArmyReturnPersonFlags196(person), "selectable_Person15E") & 1) == 0) return true;
        }
        return false;
    }

    internal int PersonForce(int id) => K((Current.ReadPersonForce(Person(id)) ?? throw Missing("Person_Force")).PhysicalSlot,
        "Person_Force_identity");
    internal int Home(int id) => K(Current.ReadPersonFirstLocation(Person(id)), "PersonE2");
    internal bool CanMoveOfficer(int id)
    {
        int person = Person(id);
        if ((K(Current.ReadArmyReturnPersonFlags196(person), "Person15E") & 1) != 0) return false;
        byte status = K(Current.ReadPersonStatus(person), "PersonE6");
        //231D90 decodes E4 directly, including62..561 ->Army(tag27).
        //It does not replace that descriptor with the Army's current Hex.
        if (status is >= 1 and <= 4 &&
            PersonPositionRepair.DecodeFirstLocation(K(Current.ReadPersonFirstLocation(person), "PersonE2")) !=
            PersonPositionRepair.DecodeSecondLocation(K(Current.ReadPersonSecondLocation(person), "PersonE4"))) return false;
        var linked = Current.ReadPersonLinkedEntity(person) ?? throw Missing("associated22AC00");
        if (!K(Current.ReadLinkedEntityNativeValidity(linked), "associated_validity")) return true;
        var owner = Current.ReadOwnedPersonCommandManager() ?? throw Missing("task_manager");
        int taskId = K(linked.Identity, "associated_identity");
        if (owner.Disappearances?.ContainsKey(taskId) == true) return false; //232370 v98==16 only.
        if (owner.Returns.ContainsKey(taskId) || owner.Talents?.ContainsKey(taskId) == true ||
            owner.Diplomacies?.ContainsKey(taskId) == true || owner.Strategies?.ContainsKey(taskId) == true ||
            owner.Independences?.ContainsKey(taskId) == true || owner.Caravans?.ContainsKey(taskId) == true ||
            owner.Intelligences?.ContainsKey(taskId) == true) return true;
        throw Missing("associated_virtual98");
    }

    internal int? ReadTravelDays(int officerId, int destination)
    {
        var result = PersonReturnPathSource.QueryReassignment(SourceState, Current, Home(officerId), destination);
        return result.Days ?? throw Missing(result.UnresolvedOwner ?? "travel2318F0");
    }
    private int Person(int id) => K((id is >= 0 and <= 6000 ? Current.ReadPersonLookupReference(id)
        : Current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_pointer");
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    internal static InvalidDataException Missing(string owner) => new("personnel.groups.move." + owner);
}
