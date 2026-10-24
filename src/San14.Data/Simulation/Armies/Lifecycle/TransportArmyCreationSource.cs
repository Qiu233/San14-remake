using San14.Core;

namespace San14.Data;

public sealed record TransportArmyCreatedFields(int NativeSlot, byte Status10, ushort Commander12,
    ushort Credit14, ushort Troops16, ushort Wounded18, byte Morale1A, byte Formation1B, byte Formation1C,
    byte Flags26, ushort? HomeSequence28, ushort CurrentHex2A, ushort[] Waypoints2C,
    byte Action37, byte TargetKind38, ushort TargetIdentity3A, byte Category40, byte Choice41,
    ushort InitialNextHex48, byte Field56, int Gold5C, int Supplies60, ushort SavedHex152,
    ushort CommanderSecondE4, ArmyOverrideTargetState OverrideTarget)
{
    /// <summary>2D8FE0 retains the free receiver's saved current13C.
    /// Fresh/reset installs0; an old receiver without the field stays null.</summary>
    public byte? MapUiByte13C { get; init; }
}
public sealed record TransportArmyCreationResourceReceipt(int SourceCityPhysical,
    int TroopsBefore, int TroopsAfter, int GoldBefore, int GoldAfter, int SuppliesBefore, int SuppliesAfter);
public sealed record TransportArmyCreationPrefixReceipt(TransportArmyPacket Packet,
    TransportArmyCreatedFields Fields, TransportArmyCreationResourceReceipt Resources,
    bool GovernorUpdateReached, IReadOnlyList<string> FieldOwnersMissing);
public sealed record TransportArmyCreationPrefixResult(TransportArmyCreationPrefixReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>Actual2D8FE0 fields and source City debits, before registration/
/// Governor/CTroops tails. This only prepares values. It neither changes the
/// caller's current world nor publishes a partial Army as a complete creation.</summary>
public sealed class TransportArmyCreationSource
{
    private readonly ArmyFormationWriterSource _formations;
    private TransportArmyCreationSource(ArmyFormationWriterSource formations) => _formations = formations;
    public static TransportArmyCreationSource FromLoadedSource(ArmyFormationWriterSource formations)
        => new(formations);
    public static TransportArmyCreationSource LoadCurrent(GameResourceRepository repository, string language)
        => new(ArmyFormationWriter.ReadSource(repository.LoadSkillDefinitions($"0010_{language}/fixdataexce.s14"), language));

    public TransportArmyCreationPrefixResult Prepare(GameState state, SourceWorldContext current,
        TransportArmyPacket packet, Func<int, ushort?>? readHomeSequence28 = null)
    {
        try
        {
            if (state.NativeArmySimulation?.Parameters is not { } parameters ||
                parameters.Language != _formations.CodeSource.Language || parameters.RuleVersion != _formations.CodeSource.RuleVersion ||
                state.ContentHashes.GetValueOrDefault(_formations.Source.ResourceUri) != _formations.Source.Sha256)
                return Missing("current.transport_formation_source");
            if (state.ReadArmyLifecycle() is not { Slots.Count: 501 } arena ||
                arena.Revision != OwnedArmyLifecycleCurrent.Revision)
                return Missing("current.transport_owned_reset_arena");
            var recomposed = packet.CommandOrder is { } command
                ? TransportArmyPacketSource.TryComposeCommand(state, current, command)
                : packet.Request is { } cityRequest
                    ? TransportArmyPacketSource.TryComposeCity(state, current, cityRequest)
                    : new TransportArmyPacketResult(null, "current.transport_packet_request");
            if (recomposed.Packet is not { } checkedPacket) return Missing(recomposed.UnresolvedOwner!);
            if (!packet.Dwords.SequenceEqual(checkedPacket.Dwords)) return Missing("current.transport_packet_selection_changed");

            int person = packet.CommanderPhysical;
            ushort commanderHome = Need(current.ReadPersonFirstLocation(person), "CommanderE2");
            ushort second = Need(current.ReadPersonSecondLocation(person), "PersonE4");
            var decoded = PersonPositionRepair.DecodeSecondLocation(second);
            if (decoded.Kind == 27 && decoded.Index is >= 1 and <= 500)
            {
                var existing = arena.Slots[decoded.Index];
                if (existing.Commander12 is null) return Missing("current.transport_existing_Army12");
                if (existing.Commander12 == packet.Dwords[0] && existing.Status10 is null)
                    return Missing("current.transport_existing_Army10");
                if (existing.Commander12 == packet.Dwords[0] && existing.Status10 != 0)
                    return Missing("branch.transport_existing_CommanderArmy");
            }
            var associated = current.ReadPersonLinkedEntity(person)
                ?? throw Unknown("Person22AC00");
            if (Need(current.ReadLinkedEntityNativeValidity(associated), "Person22AC00_validity"))
                return Missing("branch.transport_existing_associated_command");
            int nativeSlot = OwnedArmyLifecycleCurrent.FirstCreationSlot(state) ?? 0;
            if (nativeSlot == 0) return Missing("current.transport_first_invalid_501_receiver");
            var receiver = arena.ReceiverSlots()[nativeSlot];
            if (receiver.OverrideTarget is not { Kind: not null, Destination: not null } inherited)
                return Missing("current.transport_reset_override3C3E");

            var home = new BaseReference(BaseKind.City, packet.SourceCityPhysical);
            var force = current.ReadPersonForce(person) ?? throw Unknown("Person_Force");
            int beforeTroops = Need(current.ReadBaseResource(home, BaseResourceKind.Troops), "Base3C");
            int ready = Bound(unchecked(beforeTroops - packet.Dwords[1]), BasePostLoad.ReadResourceCapacities(home, current).Troops);
            ready = Bound(ready, BasePostLoad.ReadResourceCapacities(home, current).Troops);
            byte morale = ArmyMoraleSource.SetByte2419A0(Need(current.ReadBaseMorale44(home), "Base44"),
                BasePostLoad.ReadMoraleUpper(force, current));
            int beforeGold = Need(current.ReadBaseResource(home, BaseResourceKind.Gold), "Base_virtual90");
            int gold = Bound(unchecked(beforeGold - packet.Dwords[3]), BasePostLoad.ReadResourceCapacities(home, current).Gold);
            gold = Bound(gold, BasePostLoad.ReadResourceCapacities(home, current).Gold);
            int beforeSupplies = Need(current.ReadBaseResource(home, BaseResourceKind.Supplies), "Base_virtualA0");
            int supplies = Bound(unchecked(beforeSupplies - packet.Dwords[4]), BasePostLoad.ReadResourceCapacities(home, current).Supplies);
            supplies = Bound(supplies, BasePostLoad.ReadResourceCapacities(home, current).Supplies);

            //The source lookup scans every valid physical header, including
            //pending/in-the-field Armies. It does not apply2C96E0 here.
            int sequence = 0; bool sequenceKnown = true;
            foreach (var other in arena.Slots)
            {
                if (other.PhysicalSlot == 0 || other.Commander12 == 0) continue;
                ushort commander = Need(other.Commander12, $"Army[{other.PhysicalSlot}]12");
                if (Need(other.Status10, $"Army[{other.PhysicalSlot}]10") == 0) continue;
                var otherPerson = current.ReadPersonLookupReference(commander) ?? throw Unknown("sequence_Commander_pointer");
                //22A7A0 returns known0 for an invalid Person. This is not a
                //missing Home and does not reach that Army's word28.
                if (otherPerson.PhysicalSlot is not int otherPhysical) continue;
                if (!Need(current.ReadPersonNativeValidity(otherPhysical), "sequence_Commander_validity")) continue;
                ushort otherHome = Need(current.ReadPersonFirstLocation(otherPhysical), "sequence_CommanderE2");
                if (otherHome != commanderHome) continue;
                ushort? previous = readHomeSequence28 is null
                    ? TransportArmyHomeSequenceSource.ReadCurrentWord28(state, other.PhysicalSlot)
                    : readHomeSequence28(other.PhysicalSlot);
                if (previous is null) sequenceKnown = false;
                else if (sequence <= previous.Value) sequence = previous.Value + 1;
            }
            ushort? sequence28 = sequenceKnown ? (ushort)Math.Clamp(sequence, 0, 500) : null;
            ushort? currentHex = ArmyConstruction.ResolveCurrentHex2A(current, packet.Dwords[0]);
            if (currentHex is null) return Missing("current.transport_HomeObject_Hex2A");
            int selectedNaval = packet.Dwords[2];
            int naval = (uint)selectedNaval <= 20 && _formations.NativeFields7E[selectedNaval] == 2
                ? selectedNaval : Need(TransportArmyPacketSource.ReadDefaultNaval(state, current, force), "naval_Force15E");
            byte land = _formations.NativeFields7E[19] != 2 ? (byte)19 : (byte)0;
            ushort governor = Need(current.ReadArmyReturnBaseGovernor2E(home), "Base2E");
            //These free receivers are produced only by the owned source
            //constructor/reset/26 lifecycle. No additional reset is executed.
            byte inheritedFlags26 = 0;
            byte flags = (byte)(inheritedFlags26 & 0xD3);
            if (packet.Dwords[13] == 1) flags |= 0x20;
            var fields = new TransportArmyCreatedFields(nativeSlot, 2, (ushort)packet.Dwords[0], 0,
                (ushort)Math.Clamp(packet.Dwords[1], 0, 65535), 0, morale, land, (byte)naval, flags, sequence28,
                currentHex.Value, packet.Dwords.Skip(5).Take(5).Select(x => (ushort)(x == 0xBD10 ? x : Math.Clamp(x, 0, 48399))).ToArray(),
                (byte)Math.Clamp(packet.Dwords[10], 0, 17), unchecked((byte)packet.Dwords[11]), unchecked((ushort)packet.Dwords[12]),
                0, (byte)Math.Clamp(packet.Dwords[14], 0, 2), 0xBD10, 0,
                Math.Clamp(packet.Dwords[3], 0, 100000), Math.Clamp(packet.Dwords[4], 0, 5000000), currentHex.Value,
                PersonArmyLocationSource.EncodeArmySecondLocation(nativeSlot), inherited with { })
                { MapUiByte13C = receiver.MapUiByte13C };
            return new(new(packet, fields, new(packet.SourceCityPhysical, beforeTroops, ready, beforeGold, gold, beforeSupplies, supplies),
                governor == packet.Dwords[0], sequenceKnown ? Array.Empty<string>() : new[] { "current.sameHome_Army_word28" }), null);
        }
        catch (MissingOwner error) { return Missing(error.Message); }
        catch (InvalidDataException error) { return Missing(error.Message); }
    }
    private static int Bound(int value, int cap) => value < 0 ? 0 : Math.Min(value, cap);
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Unknown(field);
    private static MissingOwner Unknown(string field) => new("current.transport_" + field);
    private static TransportArmyCreationPrefixResult Missing(string field) => new(null, field);
    private sealed class MissingOwner(string message) : Exception(message);
}
