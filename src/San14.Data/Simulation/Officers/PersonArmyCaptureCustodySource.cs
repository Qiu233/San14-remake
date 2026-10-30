using San14.Core;

namespace San14.Data;

public sealed record PersonArmyCaptureStatusReceipt(int PersonPhysical, ushort PersonId,
    byte StatusBeforeWrite, byte? DistrictForLeaderRepair)
{
    public bool RequiresDistrictLeaderRepair => StatusBeforeWrite == 2;
}
public sealed record PersonArmyCaptureStatusResult(GameState? State,
    PersonArmyCaptureStatusReceipt? Receipt, string? UnresolvedOwner);
public sealed record PersonArmyCaptureCustodyPreparation(
    PersonArmyCaptureCustodyPacket? Packet, string? UnresolvedOwner);
public sealed record PersonArmyCaptureCustodyReceipt(int PersonPhysical, ushort PersonId,
    int WinnerArmyPhysical, BaseReference WinnerHome, ushort FirstLocationAfter,
    ushort SecondLocationAfter, bool SecondLocationCopiedFromHome, bool WritesOldForce19D,
    byte? OldForce19DAfter);
public sealed class PersonArmyCaptureCustodyPacket
{
    public PersonArmyCaptureCustodyReceipt Receipt { get; }
    internal SourceWorldContext Owner { get; }
    internal PersonArmyCaptureCustodyPacket(SourceWorldContext owner,
        PersonArmyCaptureCustodyReceipt receipt) => (Owner, Receipt) = (owner, receipt);
}

/// <summary>2E0FC0 ordinary Person scalar/custody phases after the reached
/// task, appointment, Force and original-District callbacks. BeginContext's
/// status2 result requires the caller's real post-status2E9140(0,1) before
/// PrepareWinnerArmy. These phases alone are not the complete capture call.</summary>
public static class PersonArmyCaptureCustodySource
{
    /// <summary>2E15B9..2E15DD: Office73, cached current status2, status6 and
    /// byte168=0. Only the caller's operation-local context is changed; the
    /// returned state is an intermediate publication image for that operation.</summary>
    public static PersonArmyCaptureStatusResult BeginContext(GameState state,
        SourceWorldContext current, int personPhysical, bool allowLastCityRuler = false)
    {
        try
        {
            OfficerState person = Person(state, current, personPhysical);
            if (!Known(current.ReadPersonNativeValidity(personPhysical), "current.Person_native_validity"))
                throw Missing("branch.invalid_Person");
            bool ordinaryPerson = Known(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(personPhysical)), "current.Person_flags15E");
            if (!ordinaryPerson) throw Missing("branch.appendix_265250_or_status5_prefix");
            byte status = Known(current.ReadPersonStatus(personPhysical), "current.Person_E6_after_prefix_callbacks");
            if (status == 1 && !allowLastCityRuler) throw Missing("branch.ruler_2F86F0_or_single_city_at_home_prefix");
            if (status is not (1 or 2 or 3 or 4)) throw Missing("branch.capture_prefix_requires_current_status2_or3_or4");
            return BeginAfterCaptureCallbacksCurrent(state, current, personPhysical);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    /// <summary>Reached2E15B9..2E15DD stores after actual capture callbacks.
    /// The caller owns its source admission; ordinary Army and Story Force
    /// capture share these stores without inheriting each other's outer gate.</summary>
    internal static PersonArmyCaptureStatusResult BeginAfterCaptureCallbacksCurrent(GameState state,
        SourceWorldContext current, int personPhysical)
    {
        try
        {
            OfficerState person = Person(state, current, personPhysical);
            byte status = Known(current.ReadPersonStatus(personPhysical), "current.Person_E6_after_prefix_callbacks");
            var auxiliaries = current.ReadOfficerAftermath(personPhysical)
                ?? throw Missing("current.Person_destruction_auxiliary_owner");
            if (!state.OfficerOffices.ContainsKey(73)) throw Missing("current.Office73_projection");
            current.SetMoraleWavePersonPostE7(personPhysical, 73);
            byte? district = status == 2
                ? Known(current.ReadPersonDistrictSlot(personPhysical), "current.Person_E0_for_post_status_District") : null;
            current.ClearCaptureResetKnowledge(personPhysical);
            current.WritePersonStatus(personPhysical, 6);
            var updated = person with { Appointment = OfficerAppointment.Synchronize(73),
                ServiceStatus = OfficerServiceStatus.Captive };
            return new(current.PublishOfficerAftermath(state with { Officers = new(state.Officers) { [person.Id] = updated } }, person.Id),
                new(personPhysical, (ushort)person.Id, status, district), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    /// <summary>2E161F..2E16F3's valid winner-Army branch. originalForce is
    /// the receiver saved at capture entry, before callbacks can alter District.
    /// Read fresh winner home/commander after any post-status District repair.</summary>
    public static PersonArmyCaptureCustodyPreparation PrepareWinnerArmy(GameState state,
        SourceWorldContext current, int personPhysical,
        ArmyUnitReference winnerArmy, ForceReference originalForce)
    {
        try
        {
            OfficerState person = Person(state, current, personPhysical);
            if (!Known(current.ReadArmyUnitNativeValidity(winnerArmy), "current.winner_Army_native_validity"))
                throw Missing("branch.capture_nonvalid_winner_Army_Base_or_Person");
            int winnerPhysical = Known(winnerArmy.PhysicalSlot, "current.winner_Army_physical22DB70");
            BaseReference home = current.FindArmyUnitCurrentBase(winnerArmy); //22A730, including actual Base0 fallback.
            ushort first = Math.Min(Known(current.ReadBaseObjectLink(home), "current.winner_Home4E"), (ushort)61);
            ushort second = PersonArmyLocationSource.EncodeArmySecondLocation(winnerPhysical); //241F20(kind27).
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
            bool copyHome = validCommander && PersonPositionRepair.DecodeSecondLocation(
                Known(commanderSecond, "current.winner_commander_E4")).Kind != 27;
            if (copyHome) second = Math.Min(first, (ushort)1061);
            bool writesOldForce = PersonPositionRepair.DecodeSecondLocation(second).Kind == 27 ||
                CountOwnedCities(current, originalForce) > 1;
            byte? oldForce = writesOldForce ? (byte)Math.Clamp(
                Known(current.ReadForcePhysicalSlot(originalForce), "current.saved_original_Force_ordinal60"), 0, 51) : null;
            return new(new(current, new(personPhysical, (ushort)person.Id, winnerPhysical, home,
                first, second, copyHome, writesOldForce, oldForce)), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public static void ApplyContext(PersonArmyCaptureCustodyPacket packet)
    {
        var r = packet.Receipt; var current = packet.Owner;
        current.WritePersonFirstLocation(r.PersonPhysical, r.FirstLocationAfter);
        current.WritePersonSecondLocation(r.PersonPhysical,
            PersonArmyLocationSource.EncodeArmySecondLocation(r.WinnerArmyPhysical));
        if (r.SecondLocationCopiedFromHome)
            current.WritePersonSecondLocation(r.PersonPhysical, r.SecondLocationAfter);
        if (r.WritesOldForce19D)
        {
            current.WriteFormerForceKnowledge(r.PersonPhysical, Known(r.OldForce19DAfter, "current.saved_original_force"));
        }
    }

    /// <summary>Writes existing saved owners only. Force and District continue
    /// to identify original service; custody derives from second location and
    /// its current Army. Enclosing destruction publishes registry/Army cleanup.</summary>
    public static GameState PublishCurrent(GameState state, PersonArmyCaptureCustodyPacket packet)
    {
        var r = packet.Receipt; var current = packet.Owner;
        OfficerState person = Person(state, current, r.PersonPhysical);
        var location = person.LocationState ?? throw Missing("current.Person_location_owner");
        var result = OfficerLocationSourceAdapter.PublishPosition(state with { Officers = new(state.Officers) { [person.Id] = person with
        {
            LocationId = Known(current.ReadPersonFirstLocation(r.PersonPhysical), "current.Person_first_E2"),
            Appointment = OfficerAppointment.Synchronize(73),
            ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(Known(current.ReadPersonStatus(r.PersonPhysical), "current.Person_status_E6"))
        } } }, person.Id, current);
        return current.PublishOfficerAftermath(result, person.Id);
    }

    private static int CountOwnedCities(SourceWorldContext current, ForceReference force)
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

    private static OfficerState Person(GameState state, SourceWorldContext current, int physical)
    {
        if (physical is < 1 or > 1850 || current.ReadPersonIdentity(physical) is not ushort id ||
            !state.Officers.TryGetValue(id, out var person) || current.ResolveOfficerSource(id) != physical)
            throw Missing("current.Person_saved_physical_identity");
        return person;
    }
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}
