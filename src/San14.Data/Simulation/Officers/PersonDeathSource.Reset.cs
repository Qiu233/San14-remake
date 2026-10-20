using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonDeathSource
    {
        private void ResetPerson(int person)
        {
            SetStatus(person, 0);
            current.SetMoraleWavePersonPostE7(person, 0);
            Change(person, p => p with { Appointment = OfficerAppointment.Synchronize(0) });
            int before = associations.Count;
            PersonDistrictTransfer.Apply(person, 0, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations, RequireReports));
            latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, person, associations.Skip(before).ToArray());
            Change(person, p => p with { DistrictId = null, ForceId = null,
                LocationState = (p.LocationState ?? new(null, null)) with { Membership = OfficerDistrictMembership.Unaffiliated } });
            ushort flags = Known(current.ReadOwnedReturnPersonFlags(person), "reset_old_flags15E");
            byte loyalty = (flags & 0x2000) != 0 ? (byte)255 : (byte)0;
            current.SetPersonDestructionLoyaltyE8(person, 0);
            current.SetPersonDestructionLoyaltyE8(person, loyalty);
            latest = ArmyDestructionLoyalty.PublishCurrentRawE8(latest, current, person, loyalty);
            current.WritePersonFirstLocation(person, 0);
            latest = OwnedArmyLifecycleDetachedInputs.PublishCurrentPersonFirstLocation(latest, current, person);
            current.WritePersonSecondLocation(person, 0);
            Change(person, p => p with { LocationId = null,
                LocationState = (p.LocationState ?? new(null, null)) with { Position = OfficerPosition.Unplaced } });
            var residue = current.ReadOfficerLocationResidue(Identity(person));
            latest = latest with { ResourceState = CampaignSourceKnowledge.From(latest.ResourceState)
                .With(new(Identity(person), residue?.LocationCode, residue?.DistrictCode)) };
            ResetRelations(person);
            // The second clear of these same five source XP words creates no
            // second owner; the unrelated global growth notice tree survives.
            Change(person, p => p with { Growth = new(new ulong?[] { 0, 0, 0, 0, 0 }, p.Growth?.Notifications) });
            for (int index = 0; index < 9; index++)
            {
                ushort previous = Known(current.ReadPersonTraitSlot150(person, index), "reset_trait_slot");
                current.WritePersonTraitSlot150(person, index, 0);
                if (previous != 0)
                {
                    ushort mask = Known(current.ReadPersonTraitMask1BC(person), "reset_trait_mask");
                    current.WritePersonTraitMask1BC(person, (ushort)(mask & ~(1 << index)));
                    current.WritePersonTraitExtra1BE(person, index, 0);
                }
            }
            current.WritePersonFormationMask164(person, 0);
            for (int index = 0; index < 10; index++) current.WritePersonTacticSlot168(person, index, 0);
            var behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(Owner(person).Behavior, 0);
            current.SetPersonDeathBehavior(person, behavior);
            Change(person, p => p with { Behavior = behavior });
            int id = Identity(person);
            current.SetOfficerHealth(id, OfficerHealth.Healthy);
            latest = current.ResetPersonDeathAftermath(latest, person);
            Change(person, p => p with { Health = OfficerHealth.Healthy,
                DuelCondition = new(0, p.DuelCondition?.Readiness) });
            SetSavedGovernorReset(person);
            latest = RulerCaptureReleaseInputs.ResetCurrentDeathWords(latest, person);
            latest = BattleRemakeCurrentForcePolicies.ResetCurrentDeathPersonPolicy(latest, id, person);
            current.SetPersonPostLoadByte(person, 0x1BA, 0);
            current.WritePersonTraitMask1BC(person, 0);
            for (int index = 0; index < 9; index++) current.WritePersonTraitExtra1BE(person, index, 0);
            behavior = Owner(person).Behavior!.WithSpecialValidityMarker(0);
            current.SetPersonDeathBehavior(person, behavior);
            Change(person, p => p with { CurrentTraitIds = new int[9], CurrentFormationMask = 0,
                CurrentTacticSlots = new byte[10], TraitActivation = new([], new int[9]),
                MeritState = new(0, 0, 0, 0, 0, 0), Behavior = behavior,
                AppearanceNotificationPersonId = 0 });
            latest = PersonAssignmentPrecallInputs.ResetCurrentDeathAssignment(latest, current, person);
            // Unowned representation-only source fields are intentionally not
            // revived as a raw reset snapshot. Resource provenance is retained;
            // future business readers must acquire their own current owner.
        }

        private void SetSavedGovernorReset(int physical)
        {
            var native = latest.NativeArmySimulation ?? throw Missing("reset_governor_owner");
            if (native.Parameters.Int32Values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1)
                throw Missing("reset_governor_version");
            var values = new Dictionary<string, int>(native.Parameters.Int32Values)
            { [OwnedArmyGovernorComparatorInputs.PersonFlags160Key(physical)] = 0,
              [OwnedArmyGovernorComparatorInputs.PersonWord166Key(physical)] = 0 };
            latest = latest with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
        }
    }
}

public sealed partial class SourceWorldContext
{
    internal void SetPersonDeathBehavior(int physical, OfficerBehavior behavior)
    {
        Check(physical, 1851, "Person death receiver");
        var old = _persons.GetValueOrDefault(physical) ?? new PersonFields(null, null, null);
        _persons[physical] = old with { Behavior = behavior };
    }
}
