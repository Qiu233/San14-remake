using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>Explicit player selections for the first ordinary City-to-City
/// transport leaf, with no user waypoints. ReturnToSource selects the later
/// Person-return bit; Choice41=1 is the source fresh choice or identical
/// explicit option selection.</summary>
public sealed record TransportArmyCityRequest(int OfficerId, int SourceCityId,
    int DestinationCityId, int Troops, int Gold, int Supplies, bool ReturnToSource = false);

public sealed class TransportArmyPacket
{
    private readonly int[] _words;
    internal TransportArmyPacket(TransportArmyCityRequest request, int commanderPhysical,
        int forcePhysical, int sourcePhysical, int targetPhysical, int naval)
    {
        Request = request; CommanderPhysical = commanderPhysical; ForcePhysical = forcePhysical;
        SourceCityPhysical = sourcePhysical; DestinationCityPhysical = targetPhysical;
        _words = [request.OfficerId, request.Troops, naval, request.Gold, request.Supplies,
            0xBD10, 0xBD10, 0xBD10, 0xBD10, 0xBD10, 2, 5, targetPhysical, request.ReturnToSource ? 1 : 0, 1];
        Dwords = Array.AsReadOnly(_words);
    }
    internal TransportArmyPacket(DispatchTransportArmyOrder order, int commanderPhysical,
        int forcePhysical, int sourcePhysical, byte action, byte kind, int targetPhysical)
    {
        CommandOrder = order; CommanderPhysical = commanderPhysical; ForcePhysical = forcePhysical;
        SourceCityPhysical = sourcePhysical; DestinationCityPhysical = kind == 5 ? targetPhysical : 0;
        _words = [order.OfficerId, order.Troops, order.NavalFormationId, order.Gold, order.Supplies,
            48400, 48400, 48400, 48400, 48400, action, kind, targetPhysical,
            order.ReturnOfficerToSource ? 1 : 0, order.SourceBehaviorChoice];
        for (int index = 0; index < order.WaypointHexIds.Count; index++) _words[5 + index] = order.WaypointHexIds[index];
        Dwords = Array.AsReadOnly(_words);
    }
    internal DispatchTransportArmyOrder? CommandOrder { get; }
    /// <summary>Present only for the compatibility City-only producer. The
    /// full producer carries its own explicit command, without a fake City.</summary>
    public TransportArmyCityRequest? Request { get; }
    public int CommanderPhysical { get; }
    public int ForcePhysical { get; }
    public int SourceCityPhysical { get; }
    public int DestinationCityPhysical { get; }
    public IReadOnlyList<int> Dwords { get; }
    public byte[] Encode()
    {
        var bytes = new byte[60];
        for (int i = 0; i < _words.Length; i++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), _words[i]);
        return bytes;
    }
}
public sealed record TransportArmyPacketResult(TransportArmyPacket? Packet, string? UnresolvedOwner);

/// <summary>The real TransportState732250/780A70 and target7EB310/7E8D80
/// packet projection. This does not allocate an Army or execute2009B0.</summary>
public static class TransportArmyPacketSource
{
    /// <summary>Explicit Make selections and the real mode2 target. Kind/ID
    /// encodings stay in this boundary; no UI default or route vector is used.</summary>
    internal static TransportArmyPacketResult TryComposeCommand(GameState state,
        SourceWorldContext current, DispatchTransportArmyOrder order)
    {
        if (order.Troops is < 0 or > ushort.MaxValue || order.Gold is < 0 or > 100000 ||
            order.Supplies is < 0 or > 5000000 || order.Troops == 0 && order.Gold == 0 && order.Supplies == 0 ||
            order.NavalFormationId is < 0 or > 20 || order.SourceBehaviorChoice is < 0 or > 2 ||
            order.WaypointHexIds is null || order.WaypointHexIds.Count > 5 ||
            order.WaypointHexIds.Any(hex => hex is < 0 or >= 48400))
            return Missing("branch.transport_explicit_selections");
        var person = current.ReadPersonLookupReference(order.OfficerId);
        if (person?.PhysicalSlot is not int physical || current.ReadPersonNativeValidity(physical) != true ||
            current.ReadPersonIdentity(physical) != order.OfficerId)
            return Missing("current.transport_Person");
        if (current.ReadPersonStatus(physical) != 4) return Missing("branch.transport_ordinary_status4");
        if (current.ReadPersonFirstLocation(physical) is not ushort home) return Missing("current.transport_HomeE2");
        var force = current.ReadPersonForce(physical);
        if (force?.PhysicalSlot is not int owner || owner is < 1 or > 45 || current.ReadForceNativeValidity(force) != true)
            return Missing("current.transport_ordinary_Force");
        try
        {
            var source = BaseLookup.Find(home, current);
            if (source.Kind != BaseKind.City || source.PhysicalSlot != order.SourceCityId ||
                current.ReadBaseNativeValidity(source) != true || current.ReadBaseForceSlot(source) != owner)
                return Missing("current.transport_source_City");
            if (order.Action is { ActionKey: "move", Target: { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 } hexTarget })
            {
                var hex = current.ReadHexTableReference(hexTarget.Id);
                if (hex?.PhysicalSlot is not int hexPhysical) return Missing("current.transport_target_Hex");
                return new(new(order, physical, owner, source.PhysicalSlot!.Value, 1, 9, hexPhysical), null);
            }
            if (order.Action is not { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location })
                return Missing("branch.transport_mode2_action");
            int selected = order.Action.Target.Id;
            BaseReference? target = selected is >= 1 and <= 51 ? current.ReadCityTableReference(selected) :
                selected is >= 52 and <= 61 ? current.ReadGateTableReference(selected - 51) : null;
            if (target?.PhysicalSlot is not int targetPhysical || current.ReadBaseNativeValidity(target) != true)
                return Missing("current.transport_target_Base");
            if (current.ReadBaseForceSlot(target) is not int targetForceSlot ||
                current.ReadForceTableReference(targetForceSlot) is not { } targetForce ||
                current.ReadForceNativeValidity(targetForce) != true || targetForce != force)
                return Missing("current.transport_target_friendly_Force");
            if (target.Kind == BaseKind.City && targetPhysical == source.PhysicalSlot)
                return Missing("branch.transport_distinct_actual_Cities");
            return new(new(order, physical, owner, source.PhysicalSlot!.Value,
                target.Kind == BaseKind.City ? (byte)2 : (byte)3,
                target.Kind == BaseKind.City ? (byte)5 : (byte)6, targetPhysical), null);
        }
        catch (InvalidDataException error) { return Missing(error.Message); }
    }

    public static TransportArmyPacketResult TryComposeCityEnter(GameState state,
        SourceWorldContext current, TransportArmyCityRequest request)
        => request.ReturnToSource ? Missing("branch.transport_enter_selection_is_return") : TryComposeCity(state, current, request);

    /// <summary>Both actual bit20 selections retain the same Action2/City
    /// destination packet. The bit selects the later Person return branch;
    /// it does not order the transport Army itself to travel back.</summary>
    public static TransportArmyPacketResult TryComposeCity(GameState state,
        SourceWorldContext current, TransportArmyCityRequest request)
    {
        if (request.Troops is < 0 or > ushort.MaxValue || request.Gold is < 0 or > 100000 ||
            request.Supplies is < 0 or > 5000000 || request.Troops == 0 && request.Gold == 0 && request.Supplies == 0)
            return Missing("branch.transport_quantity_selection");
        var person = current.ReadPersonLookupReference(request.OfficerId);
        if (person?.PhysicalSlot is not int physical || current.ReadPersonNativeValidity(physical) != true ||
            current.ReadPersonIdentity(physical) != request.OfficerId)
            return Missing("current.transport_Person");
        if (current.ReadPersonStatus(physical) != 4) return Missing("branch.transport_ordinary_status4");
        if (current.ReadPersonFirstLocation(physical) is not ushort home) return Missing("current.transport_HomeE2");
        var force = current.ReadPersonForce(physical);
        if (force?.PhysicalSlot is not int owner || owner is < 1 or > 45 || current.ReadForceNativeValidity(force) != true)
            return Missing("current.transport_ordinary_Force");
        try
        {
            var source = BaseLookup.Find(home, current);
            var target = BaseLookup.Find(request.DestinationCityId, current);
            if (source.Kind != BaseKind.City || source.PhysicalSlot is not >= 1 or > 51 ||
                target.Kind != BaseKind.City || target.PhysicalSlot is not >= 1 or > 51 ||
                source.PhysicalSlot != request.SourceCityId || target.PhysicalSlot == source.PhysicalSlot)
                return Missing("branch.transport_distinct_actual_Cities");
            if (current.ReadBaseNativeValidity(source) != true || current.ReadBaseNativeValidity(target) != true ||
                current.ReadBaseForceSlot(source) != owner || current.ReadBaseForceSlot(target) != owner)
                return Missing("current.transport_friendly_Cities");
            int? naval = ReadDefaultNaval(state, current, force);
            if (naval is null) return Missing("current.transport_Force15E");
            return new(new(request, physical, owner, source.PhysicalSlot.Value, target.PhysicalSlot.Value, naval.Value), null);
        }
        catch (InvalidDataException error) { return Missing(error.Message); }
    }

    /// <summary>TC2B5B40/SC2B59C0 uses Force15E, not Person naval aptitude.
    /// The field is a saved current scalar; no original resource is reloaded.</summary>
    public static int? ReadDefaultNaval(GameState state, SourceWorldContext current,
        ForceReference force)
    {
        bool? valid = current.ReadForceNativeValidity(force);
        if (valid is null) return null;
        if (!valid.Value) return 20;
        if (force.PhysicalSlot is not int physical || state.NativeArmySimulation?.Parameters is not { } parameters ||
            !parameters.Int32Values.TryGetValue(BattleRemakeCurrentForcePolicies.ForceFieldKey(physical, 0x15E), out int value) ||
            value is < 0 or > 255) return null;
        return value switch { 12 => 17, 13 => 18, _ => 20 };
    }
    private static TransportArmyPacketResult Missing(string reason) => new(null, reason);
}
