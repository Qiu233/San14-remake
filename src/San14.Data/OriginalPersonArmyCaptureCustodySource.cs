using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonArmyCaptureStatusReceipt(int PersonPhysical, ushort PersonId,
    byte StatusBeforeWrite, byte? DistrictForLeaderRepair)
{
    public bool RequiresDistrictLeaderRepair => StatusBeforeWrite == 2;
}
public sealed record OriginalPersonArmyCaptureStatusResult(GameState? State,
    OriginalPersonArmyCaptureStatusReceipt? Receipt, string? UnresolvedOwner);
public sealed record OriginalPersonArmyCaptureCustodyPreparation(
    OriginalPersonArmyCaptureCustodyPacket? Packet, string? UnresolvedOwner);
public sealed record OriginalPersonArmyCaptureCustodyReceipt(int PersonPhysical, ushort PersonId,
    int WinnerArmyPhysical, OriginalBaseReference WinnerHome, ushort FirstLocationAfter,
    ushort SecondLocationAfter, bool SecondLocationCopiedFromHome, bool WritesOldForce19D,
    byte? OldForce19DAfter);
public sealed class OriginalPersonArmyCaptureCustodyPacket
{
    public OriginalPersonArmyCaptureCustodyReceipt Receipt { get; }
    internal OriginalEventInitializationContext Owner { get; }
    internal OriginalPersonArmyCaptureCustodyPacket(OriginalEventInitializationContext owner,
        OriginalPersonArmyCaptureCustodyReceipt receipt) => (Owner, Receipt) = (owner, receipt);
}

/// <summary>2E0FC0 ordinary Person scalar/custody phases after the reached
/// task, appointment, Force and original-District callbacks. BeginContext's
/// status2 result requires the caller's real post-status2E9140(0,1) before
/// PrepareWinnerArmy. These phases alone are not the complete capture call.</summary>
public static class OriginalPersonArmyCaptureCustodySource
{
    /// <summary>2E15B9..2E15DD: Office73, cached current status2, status6 and
    /// byte168=0. Only the caller's operation-local context is changed; the
    /// returned state is an intermediate publication image for that operation.</summary>
    public static OriginalPersonArmyCaptureStatusResult BeginContext(GameState state,
        OriginalEventInitializationContext current, int personPhysical)
    {
        try
        {
            OfficerState person = Person(state, current, personPhysical);
            if (!Known(current.ReadPersonNativeValidity(personPhysical), "current.Person_native_validity"))
                throw Missing("branch.invalid_Person");
            ushort flags = Known(current.ReadArmyReturnPersonFlags196(personPhysical), "current.Person_flags15E");
            if ((flags & 0x4000) != 0) throw Missing("branch.appendix_265250_or_status5_prefix");
            byte status = Known(current.ReadPersonStatus(personPhysical), "current.Person_E6_after_prefix_callbacks");
            if (status == 1) throw Missing("branch.ruler_2F86F0_or_single_city_at_home_prefix");
            if (status is not (2 or 3 or 4)) throw Missing("branch.capture_prefix_requires_current_status2_or3_or4");
            var auxiliaries = current.ReadPersonDestructionRuntimeFields(personPhysical)
                ?? throw Missing("current.Person_destruction_auxiliary_owner");
            if (!state.OfficerOffices.ContainsKey(73)) throw Missing("current.Office73_projection");
            byte? district = status == 2
                ? Known(current.ReadPersonDistrictSlot(personPhysical), "current.Person_E0_for_post_status_District") : null;
            auxiliaries = auxiliaries with { Byte1A0 = 0 };
            current.WritePersonStatus(personPhysical, 6);
            current.SetPersonDestructionRuntimeFields(personPhysical, auxiliaries);
            var updated = person with { OfficeId = 73,
                NativeAbilityRuntime = person.NativeAbilityRuntime! with { Office11F = 73, Status11E = 6 },
                NativeDestructionRuntime = auxiliaries };
            return new(state with { Officers = new(state.Officers) { [person.Id] = updated } },
                new(personPhysical, (ushort)person.Id, status, district), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    /// <summary>2E161F..2E16F3's valid winner-Army branch. originalForce is
    /// the receiver saved at capture entry, before callbacks can alter District.
    /// Read fresh winner home/commander after any post-status District repair.</summary>
    public static OriginalPersonArmyCaptureCustodyPreparation PrepareWinnerArmy(GameState state,
        OriginalEventInitializationContext current, int personPhysical,
        OriginalArmyUnitReference winnerArmy, OriginalForceReference originalForce)
    {
        try
        {
            OfficerState person = Person(state, current, personPhysical);
            if (!Known(current.ReadArmyUnitNativeValidity(winnerArmy), "current.winner_Army_native_validity"))
                throw Missing("branch.capture_nonvalid_winner_Army_Base_or_Person");
            int winnerPhysical = Known(winnerArmy.PhysicalSlot, "current.winner_Army_physical22DB70");
            OriginalBaseReference home = current.FindArmyUnitCurrentBase(winnerArmy); //22A730, including actual Base0 fallback.
            ushort first = Math.Min(Known(current.ReadBaseObjectLink(home), "current.winner_Home4E"), (ushort)61);
            ushort second = OriginalPersonArmyLocationSource.EncodeArmySecondLocation(winnerPhysical); //241F20(kind27).
            ushort commanderId = Known(current.ReadArmyUnitCommanderId(winnerArmy), "current.winner_Army_commander12");
            var commander = commanderId < 6001 ? current.ReadPersonLookupReference(commanderId)
                : current.ReadTransferPersonTableReference(0);
            if (commander is null) throw Missing("current.winner_commander_ID_pointer");
            bool validCommander = commander.PhysicalSlot is int commanderPhysical &&
                Known(current.ReadPersonNativeValidity(commanderPhysical), "current.winner_commander_native_validity");
            //The source writes this Person's E2/E4 before querying the winner
            //commander again. Preserve that order even for an aliased receiver.
            ushort? commanderSecond = commander.PhysicalSlot == personPhysical ? second :
                validCommander ? current.ReadPersonSecondLocation(commander.PhysicalSlot!.Value) : null;
            bool copyHome = validCommander && OriginalPersonPositionRepair.DecodeSecondLocation(
                Known(commanderSecond, "current.winner_commander_E4")).Kind != 27;
            if (copyHome) second = Math.Min(first, (ushort)1061);
            bool writesOldForce = OriginalPersonPositionRepair.DecodeSecondLocation(second).Kind == 27 ||
                CountOwnedCities(current, originalForce) > 1;
            byte? oldForce = writesOldForce ? (byte)Math.Clamp(
                Known(current.ReadForcePhysicalSlot(originalForce), "current.saved_original_Force_ordinal60"), 0, 51) : null;
            return new(new(current, new(personPhysical, (ushort)person.Id, winnerPhysical, home,
                first, second, copyHome, writesOldForce, oldForce)), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public static void ApplyContext(OriginalPersonArmyCaptureCustodyPacket packet)
    {
        var r = packet.Receipt; var current = packet.Owner;
        current.WritePersonFirstLocation(r.PersonPhysical, r.FirstLocationAfter);
        current.WritePersonSecondLocation(r.PersonPhysical,
            OriginalPersonArmyLocationSource.EncodeArmySecondLocation(r.WinnerArmyPhysical));
        if (r.SecondLocationCopiedFromHome)
            current.WritePersonSecondLocation(r.PersonPhysical, r.SecondLocationAfter);
        if (r.WritesOldForce19D)
        {
            var old = current.ReadPersonDestructionRuntimeFields(r.PersonPhysical)
                ?? throw Missing("current.Person_destruction_auxiliary_owner");
            current.SetPersonDestructionRuntimeFields(r.PersonPhysical, old with { Byte19D = r.OldForce19DAfter });
        }
    }

    /// <summary>Writes existing saved owners only. Force and District continue
    /// to identify original service; custody derives from second location and
    /// its current Army. Enclosing destruction publishes registry/Army cleanup.</summary>
    public static GameState PublishCurrent(GameState state, OriginalPersonArmyCaptureCustodyPacket packet)
    {
        var r = packet.Receipt; var current = packet.Owner;
        OfficerState person = Person(state, current, r.PersonPhysical);
        var location = person.NativeLocationRuntime ?? throw Missing("current.Person_location_owner");
        var auxiliary = current.ReadPersonDestructionRuntimeFields(r.PersonPhysical)
            ?? throw Missing("current.Person_destruction_auxiliary_owner");
        return state with { Officers = new(state.Officers) { [person.Id] = person with
        {
            LocationId = Known(current.ReadPersonFirstLocation(r.PersonPhysical), "current.Person_first_E2"),
            OfficeId = 73,
            NativeAbilityRuntime = person.NativeAbilityRuntime! with
            { Office11F = 73, Status11E = Known(current.ReadPersonStatus(r.PersonPhysical), "current.Person_status_E6") },
            NativeLocationRuntime = location with { Second11C = current.ReadPersonSecondLocation(r.PersonPhysical) },
            NativeDestructionRuntime = auxiliary
        } } };
    }

    private static int CountOwnedCities(OriginalEventInitializationContext current, OriginalForceReference force)
    {
        int physical = Known(current.ReadForcePhysicalSlot(force), "current.original_Force_ordinal_for22AB60");
        if (physical is < 1 or > 51) return 0;
        int count = 0;
        //2243A0/3347B0: all52 current City pointers, virtualB0, no native-validity predicate.
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw Missing("current.Force_City_pointer_table");
            if (Known(current.ReadBaseForceSlot(city), "current.City_Force_virtualB0") == physical) count++;
        }
        return count;
    }

    private static OfficerState Person(GameState state, OriginalEventInitializationContext current, int physical)
    {
        if (physical is < 1 or > 1850 || current.ReadPersonIdentity(physical) is not ushort id ||
            !state.Officers.TryGetValue(id, out var person) || person.NativeAbilityRuntime?.PhysicalSlot != physical)
            throw Missing("current.Person_saved_physical_identity");
        return person;
    }
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}
