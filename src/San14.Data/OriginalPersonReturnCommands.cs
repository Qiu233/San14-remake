using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonReturnCommandPacket(int PersonPhysical, ushort PersonId,
    OriginalForceReference Force, OriginalBaseReference Home, OriginalHexReference Start,
    OriginalHexReference End, ushort Destination2A, byte DestinationDistrict28, bool Player50,
    NativeMapSeasonDate CurrentDate);
public sealed record OriginalPersonReturnCommandPreparation(OriginalPersonReturnCommandPacket? Packet, string? UnresolvedOwner);
public sealed record OriginalPersonReturnCommandResult(GameState? State, int? NativeReturn,
    int? CommandId, int? ReceiverIdentity, int? TravelDays, string? UnresolvedOwner);

/// <summary>Ordinary same-force remote2EA9A0→1F24F0→1F68B0. This creates
/// real owned command IDs, associated pointer nodes and separate value lists.
/// The independent client constructs no original presentation receiver.</summary>
public static partial class OriginalPersonReturnCommands
{
    public const string Revision = "san14:ce-owned-person-return-commands-20261007";

    public static GameState InstallFresh(GameState state, OriginalEventInitializationContext current)
    {
        if (state.NativePersonReturnCommands is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmyLifecycle?.Auxiliaries?.AssociatedCommand18Descriptor is null ||
            current.ReadOwnedCurrentListPool() is not { } pool)
            throw Missing("fresh_command_manager_allocation");
        var second = current.CreateNearbyTemporary(); //1D7DD0 manager+28 ptr_list ctor.
        ushort?[] free = Enumerable.Range(0, 1852).Select(x => (ushort?)unchecked((ushort)(x - 1))).ToArray();
        free[0] = 1852; //Ctor overwrites the first row after its−1..1850 loop.
        var owner = new NativePersonCommandManagerState(Revision, 1851, free, 0, [], [], new(second.Identity),
            OriginalPersonCommandValuePool.Fresh(0x400, 0x8000), OriginalPersonCommandValuePool.Fresh(0x400, 0x1400), []);
        var result = state with { NativePersonReturnCommands = owner,
            NativeArmyLifecycle = state.NativeArmyLifecycle with { Pool = pool.ExportOwnedState() } };
        NativePersonCommandManagerStateValidation.Validate(result); BindCurrent(result, current); return result;
    }

    public static void BindCurrent(GameState state, OriginalEventInitializationContext current)
    {
        current.SetOwnedPersonCommandManager(state.NativePersonReturnCommands);
        if (state.NativePersonReturnCommands is not { } owner) return;
        var descriptor = state.NativeArmyLifecycle?.Auxiliaries?.AssociatedCommand18Descriptor
            ?? throw Missing("current_associated_Command18_descriptor");
        current.SetCurrentAssociatedContainer(new(0)); current.SetAssociatedChainDescriptor(0, new(descriptor.Identity));
        var members = new OriginalPersonCommandValuePool(owner.MembersPool);
        foreach (var entry in owner.Commands)
        {
            current.SetLinkedEntityRawState58(entry.Key, ReadInt(entry.Value, 0x58));
            var values = members.Count(entry.Value.Members20) is null ? null : members.Values(entry.Value.Members20)
                .Select(p => p?.Kind == "MemberId32" && p.Identity is int id ? (uint?)id : null).ToArray();
            current.SetLinkedEntityMemberIds(entry.Key, values);
            current.SetAssociatedPayloadSlot7Target(entry.Key, 0x092670); //Actual CE Return vslot7: no-op.
        }
        current.AssociatedPayloadSlot7Handlers.TryAdd(0x092670, _ => { });
        current.BindOwnedPersonCommandTree(owner.IdToReceiver);
    }

    public static OriginalPersonReturnCommandPreparation Prepare(GameState state,
        OriginalEventInitializationContext current, int personPhysical, NativeMapSeasonDate currentDate)
    {
        try
        {
            ushort id = Get(current.ReadPersonIdentity(personPhysical), "Person10");
            var force = current.ReadPersonForce(personPhysical) ?? throw Missing("Person_Force");
            if (!Get(current.ReadForceNativeValidity(force), "Person_Force_validity")) throw Missing("branch.265250_lost_Force");
            ushort first = Get(current.ReadPersonFirstLocation(personPhysical), "PersonE2");
            var home = OriginalBaseLookup.Find(first, current);
            if (!Get(current.ReadBaseNativeValidity(home), "Home_validity") || current.ReadBaseForceSlot(home) != force.PhysicalSlot)
                throw Missing("branch.265250_lost_or_changed_Home");
            var start = ReadPersonCurrentHex(state, current, personPhysical, currentDate);
            int endHex = current.ReadBaseCurrentHexIndex(home);
            var end = current.ReadHexTableReference(endHex < 48400 ? endHex : 0) ?? throw Missing("Home_Hex_pointer");
            if (start.PhysicalSlot == end.PhysicalSlot) throw Missing("branch.same_Home_leaf");
            if (state.NativePersonReturnCommands is not { Revision: Revision }) throw Missing("current_Command_manager_owner");
            var player = OriginalOwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("current_Player_Force");
            bool isPlayer = player.PhysicalSlot is int playerId && playerId != 0 && playerId == force.PhysicalSlot;
            byte district = unchecked((byte)Get(current.ReadBaseDistrictSlot(home), "Home_virtual78_District"));
            return new(new(personPhysical, id, force, home, start, end, first, district, isPlayer, currentDate), null);
        }
        catch (InvalidDataException missing) { return new(null, missing.Message); }
    }

    /// <summary>Mutates only the enclosing operation-local current context.
    /// Unknown requires that world to be discarded; the saved input is intact.
    /// PublishCurrent or the enclosing259360 publication installs the image.</summary>
    public static OriginalPersonReturnCommandResult Apply(GameState state, OriginalEventInitializationContext current,
        OriginalPersonReturnCommandPacket packet)
    {
        OriginalHexListHandle? people = null;
        try
        {
            var before = state.NativePersonReturnCommands ?? throw Missing("current_Command_manager_owner");
            var memberPool = new OriginalPersonCommandValuePool(before.MembersPool);
            var targetPool = new OriginalPersonCommandValuePool(before.TargetsPool);
            var commands = before.Commands.ToDictionary(x => x.Key, x => x.Value with { RawFields = x.Value.RawFields.ToArray() });
            var map = new Dictionary<int, int>(before.IdToReceiver); var free = before.FreeNext30.ToArray();
            var unionValues = new Dictionary<int, NativeCommandUnionValue>(before.TargetNodeValues);
            ushort head = Get(before.FreeHeadEA8, "Command_manager_free_headEA8");
            var primary = current.ReadOwnedCurrentListPool() ?? throw Missing("current_primary_pool");
            people = current.CreateNearbyTemporary(); current.ClearNearbyTemporary(people, false);
            if (!current.AppendPersonReturnTemporary(people, packet.PersonPhysical)) throw Missing("branch.Person_temporary_allocation_failure");

            int receiver = before.NextReceiverIdentity;
            var command = ConstructReturn(memberPool, targetPool); commands.Add(receiver, command);
            int? commandId = null;
            if (head < 1852)
            {
                head = Get(free[head], "Command_manager_free_next30");
                commandId = before.FreeHeadEA8!.Value;
                map.Add(commandId.Value, receiver);
                var associated = state.NativeArmyLifecycle!.Auxiliaries!.AssociatedCommand18Descriptor!;
                current.AppendPersonReturnAssociated(new(associated.Identity), receiver);
                WriteInt(command, 0x58, commandId.Value); //241640 after real manager registration.
            }
            if (commandId is null)
            {
                //1F24F0 genuinely exhausted its ID pool. The newly constructed
                //unregistered receiver/value descriptors still exist in source.
                current.WritePersonSecondLocation(packet.PersonPhysical,
                    Math.Min(Get(current.ReadBaseObjectLink(packet.Home), "Home4E"), (ushort)1061));
                var failedOwner = Image(); current.SetOwnedPersonCommandManager(failedOwner);
                return new(state with { NativePersonReturnCommands = failedOwner }, 1, null, receiver, null, null);
            }
            if (command.Members20.Identity is null || command.Targets38.Identity is null)
                throw Missing("branch.Command_value_descriptor_allocation_failure");
            WriteWord(command, 0x2A, packet.Destination2A); command.RawFields[0x28] = packet.DestinationDistrict28;
            WriteWord(command, 0x4C, (ushort)Math.Clamp(Get(packet.Start.PhysicalSlot, "start_Hex_identity"), 0, 48399));
            ushort flags = Get(current.ReadArmyReturnPersonFlags196(packet.PersonPhysical), "Person_flags15E");
            current.WriteArmyReturnPersonFlags196(packet.PersonPhysical, (ushort)(flags | 1));
            ushort area = Get(current.ReadHexAreaSlot(packet.Start), "Person_current_Hex12");
            current.WritePersonSecondLocation(packet.PersonPhysical, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
            _ = memberPool.Append(command.Members20, new("MemberId32", packet.PersonId));

            //235000 appends one actual CUnionData target and writes start date
            //when the fresh target list count is0. Its tag isCity3 orGate4.
            WriteDate(command, 0x60, packet.CurrentDate);
            int? targetNode = targetPool.Append(command.Targets38, new("Union24", 0));
            if (targetNode is int node)
            {
                targetPool.SetPayload(node, new("Union24", node));
                int kind = packet.Home.Kind == OriginalBaseKind.City ? 3 : 4;
                unionValues[node] = new(kind, packet.Home.PhysicalSlot, kind, packet.Home.PhysicalSlot);
            }
            var path = current.CreateNearbyTemporary();
            OriginalPersonReturnPathResult route;
            try { route = OriginalPersonReturnPathSource.Fill(state, current, packet.Start, packet.End, packet.Force, path); }
            finally { current.ClearNearbyTemporary(path, true); }
            int days = Get(route.Days, route.UnresolvedOwner ?? "231670_travel_days");
            WriteDate(command, 0x66, DateFromOrdinal(unchecked(DateOrdinal(packet.CurrentDate) + days)));
            WriteInt(command, 0x40, 0); WriteInt(command, 0x50, packet.Player50 ? 1 : 0);
            var after = Image();
            var updated = state with { NativePersonReturnCommands = after };
            BindCurrent(updated, current);
            return new(updated, 1, commandId, receiver, days, null);

            NativePersonCommandManagerState Image() => before with { FreeHeadEA8 = head, FreeNext30 = free,
                NextReceiverIdentity = receiver + 1, IdToReceiver = map, Commands = commands,
                MembersPool = memberPool.Export(), TargetsPool = targetPool.Export(), TargetNodeValues = unionValues };
        }
        catch (InvalidDataException missing) { return new(null, null, null, null, null, missing.Message); }
        finally { if (people is not null) current.ClearNearbyTemporary(people, true); }
    }

    public static GameState PublishCurrent(GameState state, OriginalEventInitializationContext current)
    {
        var owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("current_Command_manager_owner");
        var people = new Dictionary<int, OfficerState>(state.Officers);
        foreach (var person in people.Values.ToArray())
        {
            var ability = person.NativeAbilityRuntime ?? throw Missing("current_Person_ability_owner");
            var location = person.NativeLocationRuntime ?? throw Missing("current_Person_location_owner");
            people[person.Id] = person with
            {
                NativeAbilityRuntime = ability with { Flags196 = current.ReadArmyReturnPersonFlags196(ability.PhysicalSlot) },
                NativeLocationRuntime = location with { Second11C = current.ReadPersonSecondLocation(ability.PhysicalSlot) }
            };
        }
        var result = state with { Officers = people, NativePersonReturnCommands = owner,
            NativeArmyLifecycle = state.NativeArmyLifecycle! with
            { Pool = (current.ReadOwnedCurrentListPool() ?? throw Missing("current_primary_pool")).ExportOwnedState() } };
        NativePersonCommandManagerStateValidation.Validate(result); NativeArmyLifecycleStateValidation.Validate(result); return result;
    }

    public static OriginalHexReference ReadPersonCurrentHex(GameState state, OriginalEventInitializationContext current,
        int personPhysical, NativeMapSeasonDate date)
    {
        var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw Missing("Person_associated_lookup");
        if (linked.Identity is int identity && current.ReadLinkedEntityNativeValidity(linked) == true)
            return ReadCommandCurrentHex(state, current, identity, date);
        ushort second = Get(current.ReadPersonSecondLocation(personPhysical), "PersonE4");
        int hex;
        if (second is >= 62 and <= 561)
        {
            var army = current.ReadArmyUnitTableReference(second - 61) ?? throw Missing("Person_Army_pointer");
            if (!Get(current.ReadArmyUnitNativeValidity(army), "Person_Army_validity")) throw Missing("invalid_Person_Army");
            hex = Get(current.ReadArmyUnitRawHex2A(army), "Person_Army_Hex2A");
        }
        else if (second is >= 562 and <= 1061) hex = Get(current.ReadAreaHexIndex(new(second - 561)), "Person_Area_Hex3A");
        else if (second is >= 1 and <= 61) hex = current.ReadBaseCurrentHexIndex(OriginalBaseLookup.Find(second, current));
        else throw Missing("Person_current_position_kind");
        return current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("Person_current_Hex_pointer");
    }

    private static NativePersonCommandState ConstructReturn(OriginalPersonCommandValuePool members, OriginalPersonCommandValuePool targets)
    {
        var command = new NativePersonCommandState("return", new byte?[128], members.AllocateDescriptor(), targets.AllocateDescriptor());
        WriteInt(command, 8, 0); command.RawFields[0x28] = 0; WriteWord(command, 0x2A, 0);
        for (int offset = 0x40; offset < 0x4C; offset++) command.RawFields[offset] = 0;
        for (int offset = 0x50; offset < 0x60; offset++) command.RawFields[offset] = 0;
        WriteInt(command, 0x58, -1); WriteWord(command, 0x4C, 48400);
        WriteDate(command, 0x60, new(0, 1, 1, 0)); WriteDate(command, 0x66, new(0, 1, 1, 0));
        //220D30's two31B250 ctors write padding once. Later31CDE0/33B9E0
        //copy only the five date fields and preserve those bytes.
        command.RawFields[0x65] = 0; command.RawFields[0x6B] = 0;
        for (int offset = 0x70; offset < 0x7C; offset++) command.RawFields[offset] = 0;
        members.Clear(command.Members20); return command;
    }
    internal static int? ReadInt(NativePersonCommandState c, int offset)
    { int value = 0; for (int i = 0; i < 4; i++) { if (c.RawFields[offset + i] is not byte b) return null; value |= b << (8 * i); } return value; }
    internal static ushort? ReadWord(NativePersonCommandState c, int offset) => c.RawFields[offset] is byte a && c.RawFields[offset + 1] is byte b ? (ushort)(a | b << 8) : null;
    private static void WriteInt(NativePersonCommandState c, int offset, int value)
    { for (int i = 0; i < 4; i++) c.RawFields[offset + i] = (byte)(value >> (8 * i)); }
    private static void WriteWord(NativePersonCommandState c, int offset, ushort value)
    { c.RawFields[offset] = (byte)value; c.RawFields[offset + 1] = (byte)(value >> 8); }
    private static void WriteDate(NativePersonCommandState c, int offset, NativeMapSeasonDate date)
    { WriteWord(c, offset, unchecked((ushort)date.Year)); c.RawFields[offset + 2] = unchecked((byte)date.Month);
        c.RawFields[offset + 3] = unchecked((byte)date.Day); c.RawFields[offset + 4] = unchecked((byte)date.Hour); }
    private static int DateOrdinal(NativeMapSeasonDate date) => unchecked(date.Day - 31 + (date.Month + date.Year * 12) * 30);
    private static NativeMapSeasonDate DateFromOrdinal(int ordinal)
    { short year = unchecked((short)(ordinal / 360)); int remaining = Math.Max(0, unchecked(ordinal - year * 360));
        return new(year, unchecked((sbyte)(remaining / 30 + 1)), unchecked((sbyte)(remaining % 30 + 1)), 0); }
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static T Get<T>(T? value, string name) where T : class => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("Current person-return source owner is unknown: " + name);
}

public sealed partial class OriginalEventInitializationContext
{
    private NativePersonCommandManagerState? _ownedPersonCommandManager;
    public void SetOwnedPersonCommandManager(NativePersonCommandManagerState? manager) => _ownedPersonCommandManager = manager;
    public NativePersonCommandManagerState? ReadOwnedPersonCommandManager() => _ownedPersonCommandManager;
    internal bool AppendPersonReturnTemporary(OriginalHexListHandle descriptor, int physical)
    {
        ConditionalRegistryEnter();
        try { var node = AllocateHexListNode(descriptor); if (node.Identity is not int id) return false;
            _nativeListPayloads[id] = new OriginalTransferPersonReference(physical); return true; }
        finally { ConditionalRegistryExit(); }
    }
    internal bool AppendPersonReturnAssociated(OriginalHexListHandle descriptor, int receiver)
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
