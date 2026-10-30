using San14.Core;

namespace San14.Data;

public sealed record PersonReturnCommandPacket(int PersonPhysical, ushort PersonId,
    ForceReference Force, BaseReference Home, HexReference Start,
    HexReference End, ushort Destination, byte DistrictId, bool IsPlayer,
    NativeMapSeasonDate CurrentDate);
public sealed record PersonReturnCommandPreparation(PersonReturnCommandPacket? Packet, string? UnresolvedOwner);
public sealed record PersonReturnCommandResult(GameState? State, int? NativeReturn,
    int? CommandId, int? ReceiverIdentity, int? TravelDays, string? UnresolvedOwner);

/// <summary>Schedules personnel journeys using the simulation's typed task state.
/// The current-world adapter only provides locations and association indexing.</summary>
public static partial class PersonReturnCommands
{
    public static GameState InstallFresh(GameState state, SourceWorldContext current)
    {
        var result = state with { PersonnelCommands = PersonnelCommandTasks.Create() };
        BindCurrent(result, current);
        return result;
    }

    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        current.SetOwnedPersonCommandManager(state.PersonnelCommands);
        if (state.PersonnelCommands is not { } owner) return;
        var descriptor = current.OwnedIndexes?.AssociatedTaskHandle
            ?? throw Missing("personnel association index");
        current.SetCurrentAssociatedContainer(new(0));
        current.SetAssociatedChainDescriptor(0, new(descriptor.Identity));
        foreach (var task in owner.Returns.Values)
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot ?? -1);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)id).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        foreach (var task in owner.Disappearances?.Values.AsEnumerable() ?? [])
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)id).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        foreach (var task in owner.Talents?.Values.AsEnumerable() ?? [])
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)(current.ReadPersonIdentity(
                current.ResolveOfficerSource(id) ?? throw Missing("talent member source")) ?? throw Missing("talent member identity"))).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        foreach (var task in owner.Diplomacies?.Values.AsEnumerable() ?? [])
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)id).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        foreach (var task in owner.Strategies?.Values.AsEnumerable() ?? [])
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)Get(
                current.ReadPersonIdentity(current.ResolveOfficerSource(id) ?? throw Missing("strategic member source")),
                "strategic member identity")).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        foreach (var task in owner.Independences?.Values.AsEnumerable() ?? [])
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)Get(
                current.ReadPersonIdentity(current.ResolveOfficerSource(id) ?? throw Missing("independence member source")),
                "independence member identity")).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        foreach (var task in owner.Caravans?.Values.AsEnumerable() ?? [])
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)id).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        foreach (var task in owner.Intelligences?.Values.AsEnumerable() ?? [])
        {
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot);
            current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)id).ToArray());
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
        }
        current.BindOwnedPersonCommandTree(owner.DispatchOrder().ToDictionary(task => task.Slot, task => task.Id));
    }

    public static PersonReturnCommandPreparation Prepare(GameState state,
        SourceWorldContext current, int personPhysical, NativeMapSeasonDate currentDate)
    {
        try
        {
            ushort id = Get(current.ReadPersonIdentity(personPhysical), "Person10");
            var force = current.ReadPersonForce(personPhysical) ?? throw Missing("Person_Force");
            if (!Get(current.ReadForceNativeValidity(force), "Person_Force_validity")) throw Missing("branch.265250_lost_Force");
            ushort first = Get(current.ReadPersonFirstLocation(personPhysical), "PersonE2");
            var home = BaseLookup.Find(first, current);
            if (!Get(current.ReadBaseNativeValidity(home), "Home_validity") || current.ReadBaseForceSlot(home) != force.PhysicalSlot)
                throw Missing("branch.265250_lost_or_changed_Home");
            var start = ResolvePersonPosition(state, current, personPhysical, currentDate);
            int endHex = current.ReadBaseCurrentHexIndex(home);
            var end = current.ReadHexTableReference(endHex < 48400 ? endHex : 0) ?? throw Missing("Home_Hex_pointer");
            if (start.PhysicalSlot == end.PhysicalSlot) throw Missing("branch.same_Home_leaf");
            if (state.PersonnelCommands is null) throw Missing("current_Command_manager_owner");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("current_Player_Force");
            bool isPlayer = player.PhysicalSlot is int playerId && playerId != 0 && playerId == force.PhysicalSlot;
            byte district = unchecked((byte)Get(current.ReadBaseDistrictSlot(home), "Home_virtual78_District"));
            return new(new(personPhysical, id, force, home, start, end, first, district, isPlayer, currentDate), null);
        }
        catch (InvalidDataException missing) { return new(null, missing.Message); }
    }

    public static PersonReturnCommandResult Apply(GameState state, SourceWorldContext current,
        PersonReturnCommandPacket packet)
    {
        try
        {
            var before = CurrentCommandOwner(state, current);
            int id = before.NextTaskId;
            int? slot = before.AvailableDispatchSlots.Length == 0 ? null : before.AvailableDispatchSlots[0];
            var destination = ToDestination(packet.Home);
            var task = new PersonnelReturnTask(id, slot, [], [], destination, packet.DistrictId, 48400,
                false, false, false, false, slot.HasValue, new(0, 1, 1, 0), new(0, 1, 1, 0));
            var owner = before with { NextTaskId = id + 1,
                AvailableDispatchSlots = slot.HasValue ? before.AvailableDispatchSlots[1..] : [],
                Returns = new(before.Returns) { [id] = task } };
            if (slot is null)
            {
                current.WritePersonSecondLocation(packet.PersonPhysical,
                    Math.Min(Get(current.ReadBaseObjectLink(packet.Home), "return home location"), (ushort)1061));
                current.SetOwnedPersonCommandManager(owner);
                return new(state with { PersonnelCommands = owner }, 1, null, id, null, null);
            }
            var associated = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("personnel association index");
            if (!current.AppendPersonReturnAssociated(new(associated.Identity), id))
                throw Missing("personnel association registration");
            //1DE290 registers the new receiver while its member chain is
            //still empty. An existing association remains earlier in that
            //ordered chain;1F68B0 does not remove or replace its members.
            //Publish this actual manager before the per-member22D6C0 query,
            //including any old journey deadline that query initializes.
            current.SetOwnedPersonCommandManager(owner);
            current.SetLinkedEntityRawState58(id, slot.Value);
            current.SetLinkedEntityMemberIds(id, []);
            current.SetAssociatedPayloadPostLoadCallback(id, PostLoadCallback.NoOp);
            int startHex = Math.Clamp(Get(packet.Start.PhysicalSlot, "return starting hex"), 0, 48399);
            ushort flags = Get(current.ReadArmyReturnPersonFlags196(packet.PersonPhysical), "person return flags");
            current.WriteArmyReturnPersonFlags196(packet.PersonPhysical, OfficerBehaviorSourceAdapter.MarkReturn(flags));
            var memberPosition = ResolvePersonPosition(state, current, packet.PersonPhysical, packet.CurrentDate);
            owner = CurrentCommandOwner(state, current);
            task = owner.Returns.GetValueOrDefault(id) ?? throw Missing("registered return receiver");
            ushort area = Get(current.ReadHexAreaSlot(memberPosition), "return starting area");
            current.WritePersonSecondLocation(packet.PersonPhysical, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
            //1F68B0 appends the member and Home destination before231670.
            //Keep the earlier command intact;229ED0 continues to select the
            //first associated receiver containing this real Person ID.
            task = task with { MemberIds = [packet.PersonId], RouteDestinations = [destination], StartHex = startHex,
                StartedOn = packet.CurrentDate };
            owner = owner with { Returns = new(owner.Returns) { [id] = task } };
            current.SetOwnedPersonCommandManager(owner);
            current.SetLinkedEntityMemberIds(id, [(uint?)packet.PersonId]);
            var path = current.CreateNearbyTemporary();
            PersonReturnPathResult route;
            try { route = PersonReturnPathSource.Fill(state, current, packet.Start, packet.End, packet.Force, path); }
            finally { current.ClearNearbyTemporary(path, true); }
            int days = Get(route.Days, route.UnresolvedOwner ?? "personnel travel duration");
            task = task with { ArriveOn = DateFromOrdinal(unchecked(DateOrdinal(packet.CurrentDate) + days)),
                IsPlayer = packet.IsPlayer };
            owner = owner with { Returns = new(owner.Returns) { [id] = task } };
            var updated = state with { PersonnelCommands = owner };
            BindCurrent(updated, current);
            return new(updated, 1, slot, id, days, null);
        }
        catch (InvalidDataException missing) { return new(null, null, null, null, null, missing.Message); }
    }

    public static GameState PublishCurrent(GameState state, SourceWorldContext current)
        => PublishCurrent(state, current, false);

    internal static GameState PublishBorrowedCurrent(GameState state, SourceWorldContext current)
        => PublishCurrent(state, current, true);

    private static GameState PublishCurrent(GameState state, SourceWorldContext current, bool borrowedCurrent)
    {
        var owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("personnel journey state");
        var people = new Dictionary<int, OfficerState>(state.Officers);
        foreach (var person in people.Values.ToArray())
        {
            int physical = current.ResolveOfficerSource(person.Id) ?? throw Missing("person source identity");
            var location = person.LocationState ?? throw Missing("person location");
            people[person.Id] = person with
            {
                Behavior = current.ReadPersonBehavior(physical)
            };
        }
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("association registry");
        return OfficerLocationSourceAdapter.PublishPositions((state with { Officers = people,
                PersonnelCommands = owner }).WithArmyLifecycle(state.ReadArmyLifecycle()! with
            { Indexes = borrowedCurrent ? pool.ProjectOwnedRoles() : pool.ExportOwnedState() }), current);
    }

    public static HexReference ResolvePersonPosition(GameState state, SourceWorldContext current,
        int personPhysical, NativeMapSeasonDate date)
        => ResolvePersonPositionCore(state, current, personPhysical, date, null);

    // Ordinary E4 and disappearance positions do not consume a world date.
    // The borrowed caller must supply it only for an actual journey query.
    internal static HexReference ResolvePersonPosition(GameState state, SourceWorldContext current,
        int personPhysical, Func<NativeMapSeasonDate> readDate)
        => ResolvePersonPositionCore(state, current, personPhysical, null, readDate);

    private static HexReference ResolvePersonPositionCore(GameState state, SourceWorldContext current,
        int personPhysical, NativeMapSeasonDate? date, Func<NativeMapSeasonDate>? readDate)
    {
        var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw Missing("Person_associated_lookup");
        if (linked.Identity is int identity && current.ReadLinkedEntityNativeValidity(linked) == true)
        {
            var owner = CurrentCommandOwner(state, current);
            if (owner.Disappearances?.GetValueOrDefault(identity) is { } task)
                return ReadDisappearancePosition(current, task);
            if (owner.Talents?.GetValueOrDefault(identity) is { } talent)
                return TalentTravelSource.ResolvePosition(state, current, talent, date ?? readDate!());
            if (owner.Diplomacies?.GetValueOrDefault(identity) is { } diplomacy)
                return TalentTravelSource.ResolvePosition(state, current, diplomacy.StartHexId, diplomacy.EndHexId,
                    diplomacy.SourceDistrictId, diplomacy.StartDate, diplomacy.DueDate, date ?? readDate!());
            if (owner.Strategies?.GetValueOrDefault(identity) is { } strategic)
                return TalentTravelSource.ResolvePosition(state, current, strategic.StartHexId, strategic.EndHexId,
                    strategic.SourceDistrictId, strategic.StartDate, strategic.DueDate, date ?? readDate!());
            if (owner.Independences?.GetValueOrDefault(identity) is { } independence)
                return TalentTravelSource.ResolvePosition(state, current, independence.StartHexId, independence.EndHexId,
                    independence.SourceDistrictId, independence.StartDate, independence.DueDate, date ?? readDate!());
            if (owner.Caravans?.GetValueOrDefault(identity) is { } caravan)
                return current.ReadHexTableReference(caravan.EndHexId) ?? throw Missing("caravan_country_port_Hex");
            if (owner.Intelligences?.GetValueOrDefault(identity) is { } intelligence)
                return TalentTravelSource.ResolvePosition(state, current, intelligence.StartHexId, intelligence.EndHexId,
                    intelligence.SourceDistrictId, intelligence.StartDate, intelligence.DueDate, date ?? readDate!());
            return ResolveJourneyPosition(state, current, identity, date ?? readDate!());
        }
        ushort second = Get(current.ReadPersonSecondLocation(personPhysical), "PersonE4");
        int hex;
        if (second is >= 62 and <= 561)
        {
            var army = current.ReadArmyUnitTableReference(second - 61) ?? throw Missing("Person_Army_pointer");
            if (!Get(current.ReadArmyUnitNativeValidity(army), "Person_Army_validity")) throw Missing("invalid_Person_Army");
            hex = Get(current.ReadArmyUnitRawHex2A(army), "Person_Army_Hex2A");
        }
        else if (second is >= 562 and <= 1061) hex = Get(current.ReadAreaHexIndex(new(second - 561)), "Person_Area_Hex3A");
        else if (second is >= 1 and <= 61) hex = current.ReadBaseCurrentHexIndex(BaseLookup.Find(second, current));
        //231D90 decodes known E4 outside City/Gate/Area/Army as tagFF.
        //22D6C0 returns a null pointer for that known category, not Hex0.
        //The Get above still rejects unknown E4; associated tasks stay first.
        else return new HexReference(null);
        return current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("Person_current_Hex_pointer");
    }

    internal static PersonnelReturnDestination ToDestination(BaseReference location) => new(
        location.Kind == BaseKind.City ? PersonnelReturnBaseKind.City : PersonnelReturnBaseKind.Gate,
        Get(location.PhysicalSlot, "return destination") + (location.Kind == BaseKind.Gate ? 51 : 0));
    internal static BaseReference ToBase(PersonnelReturnDestination location) => new(
        location.Kind == PersonnelReturnBaseKind.City ? BaseKind.City : BaseKind.Gate,
        location.BaseId - (location.Kind == PersonnelReturnBaseKind.Gate ? 51 : 0));
    private static int DateOrdinal(NativeMapSeasonDate date) => unchecked(date.Day - 31 + (date.Month + date.Year * 12) * 30);
    internal static int CalendarOrdinal(NativeMapSeasonDate date) => DateOrdinal(date);
    internal static NativeMapSeasonDate DateFromOrdinal(int ordinal)
    { short year = unchecked((short)(ordinal / 360)); int remaining = Math.Max(0, unchecked(ordinal - year * 360));
        return new(year, unchecked((sbyte)(remaining / 30 + 1)), unchecked((sbyte)(remaining % 30 + 1)), 0); }
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static T Get<T>(T? value, string name) where T : class => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("Personnel return requires " + name + ".");
}

public sealed partial class SourceWorldContext
{
    private PersonnelCommandTasks? _ownedPersonCommandManager;
    public void SetOwnedPersonCommandManager(PersonnelCommandTasks? manager) => _ownedPersonCommandManager = manager;
    public PersonnelCommandTasks? ReadOwnedPersonCommandManager() => _ownedPersonCommandManager;
    internal bool AppendPersonReturnTemporary(HexListHandle descriptor, int physical)
    {
        ConditionalRegistryEnter();
        try { var node = AllocateHexListNode(descriptor); if (node.Identity is not int id) return false;
            WriteCurrentListPayload(id, new TransferPersonReference(physical)); return true; }
        finally { ConditionalRegistryExit(); }
    }
    internal bool AppendPersonReturnAssociated(HexListHandle descriptor, int receiver)
    {
        ConditionalRegistryEnter();
        try { var node = AllocateRegistryNode(new(descriptor.Identity)); if (node?.Identity.HasValue != true) return false;
            SetAssociatedChainPayload(node, new(receiver)); return true; }
        finally { ConditionalRegistryExit(); }
    }
    internal void BindOwnedPersonCommandTree(Dictionary<int, int> map)
    {
        //The native tree's observable in-order identity mapping is saved;
        //this owned current tree supplies the same ordered payload traversal.
        SetAssociatedTreeHead(0, new(0)); SetAssociatedTreeNil19(0, 1);
        var sorted = map.OrderBy(x => x.Key).ToArray();
        int Build(int begin, int end, int parent)
        {
            if (begin == end) return 0;
            int middle = (begin + end) / 2, node = middle + 1;
            SetAssociatedTreeNil19(node, 0); SetAssociatedTreePayload28(node, new(sorted[middle].Value));
            SetAssociatedTreeLink(node, 8, new(parent));
            SetAssociatedTreeLink(node, 0, new(Build(begin, middle, node)));
            SetAssociatedTreeLink(node, 0x10, new(Build(middle + 1, end, node))); return node;
        }
        int root = Build(0, sorted.Length, 0);
        SetAssociatedTreeLink(0, 8, new(root)); SetAssociatedTreeLink(0, 0, new(sorted.Length == 0 ? 0 : 1));
        SetAssociatedTreeLink(0, 0x10, new(sorted.Length));
    }
}
