using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //8908E0 publishes these seven semantic rows. Research admission here is
    //not the later research-panel budget/capacity predicate.
    private MapMainCommandEntryPresentation ReadAppointmentCommandEntry(GameState state,
        SourceWorldContext current, MapMenuCommand command, bool? restricted, Action bindCurrent)
    {
        try
        {
            if (command == MapMenuCommand.Policy) return new(command, true, 0x2ACE);
            if (command is MapMenuCommand.Research or MapMenuCommand.EditGroup)
            {
                if (restricted is null) throw MainCommandMissing("appointment_application_selection");
                return new(command, !restricted.Value, restricted.Value ? 0x11171 :
                    command == MapMenuCommand.Research ? 0x5C7E : 0x5C0B);
            }
            if (command == MapMenuCommand.MoveGroup)
            {
                if (restricted is null) throw MainCommandMissing("appointment_application_selection");
                if (restricted.Value) return new(command, false, 0x11171);
                bindCurrent();
                var player = current.CurrentPlayerForce ?? throw MainCommandMissing("appointment_player");
                return ReadMoveGroupCommandEntry(state, current, command,
                    ForcePrimaryDistrictSource.Resolve(current, player));
            }
            if (command is not (MapMenuCommand.Office or MapMenuCommand.CreateDistrict or MapMenuCommand.EditDistrict))
                throw new ArgumentOutOfRangeException(nameof(command));
            var force = current.CurrentPlayerForce ?? throw MainCommandMissing("appointment_player");
            bool valid = MainNeed(current.ReadForceNativeValidity(force), "appointment_force_validity");
            if (command == MapMenuCommand.Office)
            {
                if (!valid) return new(command, false, 0x2AD0);
                bindCurrent();
                bool any = ReadOfficeEntryCandidates(current, force);
                return new(command, any, any ? 0x2ACF : 0x2AD0);
            }
            //70C280/70B9C0 explicitly write zero before their validity gate;
            //this is a written value, unlike Patrol's no-write help branch.
            if (!valid) return new(command, false, 0);
            if (restricted is null) throw MainCommandMissing("appointment_application_selection");
            if (restricted.Value) return new(command, false, 0x11171);
            bindCurrent();
            int forceId = MainNeed(force.PhysicalSlot, "appointment_force_identity");
            if (command == MapMenuCommand.EditDistrict)
            {
                bool any = ReadAppointmentDistrictCount(current, forceId) > 1;
                return new(command, any, any ? 0x3141 : 0x590B);
            }
            if (!ReadAppointmentHasFreeDistrict(current)) return new(command, false, 0x5909);
            if (ReadAppointmentCityCount(current, forceId) < 2) return new(command, false, 0x5908);
            if (ReadAppointmentDistrictCount(current, forceId) >= 8) return new(command, false, 0x5909);
            bool leader = ReadAppointmentHasDistrictLeader(state, current, forceId);
            return new(command, leader, leader ? 0x3140 : 0x590A);
        }
        catch (InvalidDataException error) { return new(command, null, null, error.Message); }
    }

    //1EDC30 fills an actual temporary Person list. Its successful head, not
    //page rows, names, skill sorting or merely a qualified candidate, enables
    //the Office entry. Allocation capacity remains part of this predicate.
    private bool ReadOfficeEntryCandidates(SourceWorldContext current, ForceReference force)
    {
        int forceId = MainNeed(force.PhysicalSlot, "appointment_office_force");
        var list = FinalizationTemporary(current);
        try
        {
            current.ClearTemporaryHexList(list);
            if (forceId is >= 1 and <= 51)
                foreach (int person in NormalTurnServingPeople(current, forceId, 0, true))
                {
                    if (PersonCurrentBusySource.Read(current, person, MainCommandMissing)) continue;
                    byte status = MainNeed(current.ReadPersonStatus(person), "appointment_office_status");
                    if (status is 1 or 6 || DuelAftermathSpecialPerson(current, person) ||
                        (MainNeed(current.ReadArmyReturnPersonFlags196(person), "appointment_office_flags") & 0x4000) != 0) continue;
                    //The earlier status1/special/bit4000 rejections make the
                    //last1EDC30 OR reduce to its actual authored byte1BC gate.
                    if ((MainNeed(current.ReadPersonTraitMask1BC(person), "appointment_office_trait") & 255) == 1) continue;
                    FinalizationAppend(current, list, new(RegistryKind.Person, person));
                }
            return (current.ReadHexListHead(list) ?? throw MainCommandMissing("appointment_office_head")).Identity is not null;
        }
        finally { FinalizationRelease(current, list); }
    }

    private static bool ReadAppointmentHasFreeDistrict(SourceWorldContext current)
    {
        //6BDC70(1) checks all fixed slots1..51, preserving actual aliases.
        //The later creation writer has its own stronger slot restrictions.
        for (int slot = 1; slot < 52; slot++)
        {
            var district = current.ReadDistrictTableReference(slot) ?? throw MainCommandMissing("appointment_free_district_table");
            if (!MainNeed(current.ReadDistrictNativeValidity(district), "appointment_free_district_validity")) return true;
        }
        return false;
    }

    private static int ReadAppointmentCityCount(SourceWorldContext current, int forceId)
    {
        if (forceId is < 1 or > 51) return 0;
        int count = 0;
        //22AB60/2243A0/3347B0 walks the fixed52 City pointers, not the
        //registered Base list, and does not add a City validity predicate.
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw MainCommandMissing("appointment_city_table");
            if (MainNeed(current.ReadBaseForceSlot(city), "appointment_city_force") == forceId) count++;
        }
        return count;
    }

    private static int ReadAppointmentDistrictCount(SourceWorldContext current, int forceId)
    {
        if (forceId is < 1 or > 51) return 0;
        int count = 0;
        foreach (var district in current.ReadArmyReturnRegisteredDistricts() ?? throw MainCommandMissing("appointment_district_registry"))
        {
            int at = MainNeed(district?.PhysicalSlot, "appointment_district_receiver");
            if (MainNeed(current.ReadDistrictForceId(at), "appointment_district_force") == forceId) count++;
        }
        return count;
    }

    private bool ReadAppointmentHasDistrictLeader(GameState state, SourceWorldContext current, int forceId)
    {
        int rawRuler = MainNeed(current.ReadForceRecord(forceId)?.RawRuler10, "appointment_ruler_id");
        var ruler = rawRuler <= 6000 ? current.ReadPersonLookupReference(rawRuler) : current.ReadTransferPersonTableReference(0);
        int rulerPhysical = MainNeed(ruler?.PhysicalSlot, "appointment_ruler_pointer");
        foreach (int person in NormalTurnServingPeople(current, forceId, 0, true))
        {
            var linked = current.ReadPersonLinkedEntity(person) ?? throw MainCommandMissing("appointment_leader_association");
            if (linked.Identity is not null) continue; //22AC00 pointer, not a broad busy predicate.
            if (MainNeed(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, person), "appointment_leader_army")) continue;
            //6BDB80 compares22A6C0's decoded first/home descriptors only
            //when both native Persons are valid; it does not compare Hexes.
            if (MainNeed(current.ReadPersonNativeValidity(rulerPhysical), "appointment_ruler_validity") &&
                MainNeed(current.ReadPersonNativeValidity(person), "appointment_leader_validity") &&
                PersonPositionRepair.DecodeFirstLocation(MainNeed(current.ReadPersonFirstLocation(rulerPhysical), "appointment_ruler_home")) ==
                PersonPositionRepair.DecodeFirstLocation(MainNeed(current.ReadPersonFirstLocation(person), "appointment_leader_home"))) continue;
            if (DuelAftermathSpecialPerson(current, person) ||
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "appointment_leader_flags") & 0x4000) != 0) continue;
            return true;
        }
        return false;
    }

    private MapMainCommandEntryPresentation ReadMoveGroupCommandEntry(GameState state,
        SourceWorldContext current, MapMenuCommand command, DistrictReference district)
    {
        int districtId = MainNeed(district.PhysicalSlot, "appointment_move_district");
        if (MainNeed(current.ReadDistrictRawByte(districtId, 0x14), "appointment_move_books") == 0)
            return new(command, false, 0x57E1);
        int rawForce = MainNeed(current.ReadDistrictForceId(districtId), "appointment_move_raw_force");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw MainCommandMissing("appointment_move_force");
        int forceId = MainNeed(force.PhysicalSlot, "appointment_move_force_identity");
        int bases = 0;
        if (forceId is >= 1 and <= 51)
            foreach (var entry in current.ReadRegisteredBases() ?? throw MainCommandMissing("appointment_move_base_registry"))
            {
                var at = entry ?? throw MainCommandMissing("appointment_move_base_receiver");
                if (MainNeed(current.ReadBaseForceSlot(at), "appointment_move_base_force") != forceId) continue;
                if (MainNeed(current.ReadBaseNativeValidity(at), "appointment_move_base_validity") && ++bases > 1) break;
            }
        if (bases < 2) return new(command, false, 0x5C0A);
        var board = state.PersonnelGroups ?? throw MainCommandMissing("appointment_group_board");
        if (board.Groups.IsDefault || board.Groups.Length != 10) throw MainCommandMissing("appointment_ten_groups");
        bool any = false, movable = false;
        foreach (var group in board.Groups)
        {
            if (group.IsDefault || group.Length > 50) throw MainCommandMissing("appointment_group_members");
            for (int slot = 0; slot < 50; slot++)
            {
                int id = slot < group.Length ? group[slot] : 0;
                var pointer = id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
                int person = MainNeed(pointer?.PhysicalSlot, "appointment_group_person_pointer");
                if (!MainNeed(current.ReadPersonNativeValidity(person), "appointment_group_person_validity")) continue;
                any = true;
                int home = MainNeed(current.ReadPersonFirstLocation(person), "appointment_group_home");
                var at = BaseLookup.Find(home, current);
                int link = MainNeed(current.ReadBaseObjectLink(at), "appointment_group_home_link");
                if (home == link && MainNeed(current.ReadPersonSecondLocation(person), "appointment_group_position") == link &&
                    (MainNeed(current.ReadArmyReturnPersonFlags196(person), "appointment_group_flags") & 1) == 0)
                { movable = true; break; }
            }
        }
        return new(command, movable, movable ? 0x5C0C : any ? 0x57E2 : 0x5C10);
    }
}
