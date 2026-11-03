using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static GameState InstallPersonnelHirePersonInputsCurrent(GameState state, int physical,
        int identity, int year, PersonnelHireTemplate template)
    {
        var simulation = state.NativeArmySimulation ?? throw HireMissing("copy_parameter_owner");
        var before = simulation.Parameters.Int32Values;
        if (before.GetValueOrDefault(OwnedPersonRelationInputs.VersionKey) != 1 ||
            before.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1 ||
            before.GetValueOrDefault(RulerCaptureReleaseInputs.VersionKey) != 1 ||
            before.GetValueOrDefault(PersonAssignmentPrecallInputs.VersionKey) != 1)
            throw HireMissing("copy_existing_input_owners");
        var values = new Dictionary<string, int>(before);
        //2216E0→2277B0 explicitly zeroes B2/B4/B6/BE (TC220000.cpp7621),
        //and226480 clears the relation lists.334EF0 does not replace them
        //from a same-ID historical Person.
        //32F720 explicitly replaces bloodline B2 with the allocated identity.
        foreach (int field in new[] { 0xB4, 0xB6, 0xBA, 0xBC, 0xBE })
            values[OwnedPersonRelationInputs.PersonFieldKey(physical, field)] = 0;
        values[OwnedPersonRelationInputs.PersonFieldKey(physical, 0xB2)] = identity;
        for (int i = 0; i < 8; i++)
        {
            values[OwnedPersonRelationInputs.PersonFieldKey(physical, 0xC0 + i * 2)] = 0;
            values[OwnedPersonRelationInputs.PersonFieldKey(physical, 0xD0 + i * 2)] = 0;
        }
        foreach (int offset in new[] { 0x16E, 0x176, 0x178, 0x17A })
            values[RulerCaptureReleaseInputs.PersonWordKey(physical, offset)] = 0;
        values[OwnedArmyGovernorComparatorInputs.PersonFlags160Key(physical)] = 0;
        values[OwnedArmyGovernorComparatorInputs.PersonWord166Key(physical)] = year;
        int loyalty = template.Person.Loyalty ?? throw HireMissing("copy_template_loyalty");
        values[OwnedArmyGovernorComparatorInputs.PersonRawE8Key(physical)] = loyalty;
        values[PersonAssignmentPrecallInputs.PersonAreaLengthKey(physical)] = 0;
        values[PersonAssignmentPrecallInputs.PersonAreaCount1E8Key(physical)] = 0;
        values[PersonAssignmentPrecallInputs.PersonResetWord1FFKey(physical)] = 0;
        values[PersonAssignmentPrecallInputs.PersonResetByte1FEKey(physical)] = 0;
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    //226480 on an independent offer. Static generated appearance, abilities,
    //names and doctrine survive; no main Person or alias is reset here.
    internal static PersonnelHireTemplate ResetPersonnelHireTemplateSnapshot(PersonnelHireTemplate template)
    {
        var person = template.Person;
        bool maximum = HireNeed(OfficerBehaviorRules.PreservesMaximumLoyalty(person.Behavior), "reset_template_maximum_loyalty");
        var reset = person with
        {
            ForceId = null, DistrictId = null, LocationId = null, Deployed = false,
            ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(0), Appointment = OfficerAppointment.Synchronize(0),
            Loyalty = maximum ? 255 : 0,
            LocationState = new(OfficerPosition.Unplaced, OfficerDistrictMembership.Unaffiliated),
            Behavior = OfficerBehaviorSourceAdapter.Decode(0, 0),
            CurrentTraitIds = new int[9], CurrentFormationMask = 0, CurrentTacticSlots = new byte[10],
            TraitActivation = new([], new int[9]), MeritState = new(0, 0, 0, 0, 0, 0),
            Growth = new(new ulong?[] { 0, 0, 0, 0, 0 }, person.Growth?.Notifications),
            Resentment = new(0, 0), Health = OfficerHealth.Healthy,
            DuelCondition = new(0, person.DuelCondition?.Readiness), AppearanceNotificationPersonId = 0
        };
        return template with { Person = reset };
    }

    internal sealed partial class PersonAppearanceSource
    {
        //29E080, kept separate from2EE160's extra ruler/status entrance gates.
        internal GameState ApplyHireJoinCurrent(int person, BaseReference home, ForceReference force)
        {
            var previousRefresh = current.TransferServiceRefresher;
            current.TransferServiceRefresher = (first, second, third) =>
                latest = package.RefreshPersonnelMilestoneCurrent(latest, current, first, second, third);
            try
            {
                SetStatus(person, 4);
                var district = current.ReadBaseDistrictReference(home) ?? throw Missing("hire_destination_District");
                byte districtId = checked((byte)Known(district.PhysicalSlot, "hire_District_pointer"));
                District(person, districtId);
                ushort location = Known(current.ReadBaseObjectLink(home), "hire_destination4E");
                current.WritePersonFirstLocation(person, location);
                current.WritePersonSecondLocation(person, location);
                var owner = Owner(person);
                Change(person, p => p with { ForceId = Known(current.ReadForcePhysicalSlot(force), "hire_Force_pointer"),
                    DistrictId = districtId, LocationId = location, Presence = OfficerPresence.Present,
                    LocationState = current.ReadOfficerLocation(owner.Id) ?? throw Missing("hire_current_position") });
                int year = Math.Clamp((int)(latest.NativeMapSeason?.ThirdDate.Year ?? throw Missing("hire_year")), 0, 350);
                SetParameter(OwnedArmyGovernorComparatorInputs.PersonWord166Key(person), year);
                byte loyalty = CalculateLoyalty(person, force);
                if (Known(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "hire_maximum_loyalty")) loyalty = 255;
                current.SetMoraleWavePersonPostE7(person, 73);
                SetLoyalty(person, loyalty);
                Change(person, p => p with { Appointment = OfficerAppointment.Synchronize(73) });
                ushort flags = Known(current.ReadArmyReturnPersonFlags196(person), "hire_flags15E");
                current.WriteArmyReturnPersonFlags196(person, (ushort)(flags | 0x80));
                Change(person, p => p with { Behavior = current.ReadPersonBehavior(person) });
                int lowerOption = Known(BattleRemakeCurrentSettings.ReadSelectorE58(latest), "hire_credit_selector");
                lowerOption = Known(BattleRemakeCurrentSettings.ReadInt32(latest, lowerOption == 0 ? 0x4C4 : 0x128), "hire_credit_lower_option");
                int initial = Credit(2), lower = Credit(lowerOption == 1 ? 2 : 1), upper = Credit(6);
                int merit = initial < lower ? lower : Math.Min(initial, upper);
                Change(person, p => p with { MeritState = new(unchecked((short)merit), 0, 2,
                    p.MeritState?.FirstSelectionCount, p.MeritState?.SecondSelectionCount, 0) });
                return latest;
            }
            finally { current.TransferServiceRefresher = previousRefresh; }
        }
    }
}

public sealed partial class SourceWorldContext
{
    //The selected31C520 copy is bound into the existing main-table receiver.
    //No world capture, pool import or unrelated Person binding occurs here.
    internal void BindPersonnelHirePersonCurrent(int physical, OfficerState person, int previousForce)
    {
        ushort flags = OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior)
            ?? throw new InvalidDataException("personnel.hire.copy_behavior");
        byte marker = OfficerBehaviorSourceAdapter.EncodeMarker(person.Behavior)
            ?? throw new InvalidDataException("personnel.hire.copy_marker");
        byte status = OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus)
            ?? throw new InvalidDataException("personnel.hire.copy_status");
        SetPersonTablePointer(person.Id, physical);
        SetPersonRecord(new(physical, checked((ushort)person.Id), flags, marker, status, 0));
        SetPersonPortraitAppearance(physical, person.PortraitAppearanceIndex);
        _officerLocations[person.Id] = person.LocationState;
        SetPersonFirstLocation(physical, checked((ushort)(person.LocationId ?? 0)));
        SetPersonDistrictSlot(physical, checked((byte)(person.DistrictId ?? 0)));
        WritePersonResetWord1FF(physical, 0);
        WritePersonResetByte1FE(physical, 0);
        SetAssignmentPersonAreaVector(physical, new([], 0));
        SetOfficerHealth(person.Id, person.Health);
        SetOfficerAftermath(physical, new(person.Resentment, person.AllegianceDisposition,
            new(person.Id, null, 0, previousForce)));
        SetPersonDestructionLoyaltyE8(physical, person.Loyalty is int loyalty ? checked((byte)loyalty) : null);
        SetMoraleWavePersonPostE7(physical, OfficerAppointmentSourceAdapter.ReadOfficeId(person.Appointment));
        OfficerTraitActivationSourceAdapter.Write(this, physical, person.TraitActivation);
        SetPersonFormationMask164(physical, person.CurrentFormationMask);
        for (int i = 0; i < 9; i++) SetPersonTraitSlot150(physical, i,
            person.CurrentTraitIds is { Length: 9 } traits ? checked((ushort)traits[i]) : null);
        for (int i = 0; i < 10; i++) SetPersonTacticSlot168(physical, i,
            person.CurrentTacticSlots is { Length: 10 } tactics ? tactics[i] : null);
    }
}
