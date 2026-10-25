using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //271C20's employed prefix. Every callback observes the latest state and
    //shares this operation's current, registered lists and temporary pool.
    private GameState ApplyRecruitServingPrefixCurrent(GameState state, SourceWorldContext current,
        int person, DistrictReference destinationDistrict, BaseReference destinationHome,
        NativeMapSeasonDate date, Func<int> readDay450, List<CampaignEffectRequest> sink,
        bool absorption = false)
    {
        T Need<T>(T? value, string field) where T : struct => value ?? throw RecruitMissing(field);
        var working = state;
        var pool = current.ReadOwnedCurrentListPool() ?? throw RecruitMissing("join_prefix_pool");
        var originalForce = current.ReadPersonForce(person) ?? throw RecruitMissing("join_prefix_Force");
        var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
        var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
        void BindPolicy() => current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
        current.AssignmentAdvisorResetCallbacks = force =>
        {
            var reset = PersonAdvisorResetSource.ApplyWithinPersonCapture(working, current, pool, force);
            working = reset.State ?? throw RecruitMissing(reset.UnresolvedOwner ?? "join_advisor_reset");
            BindPolicy(); return true;
        };
        current.AssignmentAreaOfficerRemoval = (area, oldId) =>
        {
            var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, oldId);
            if (removed.NativeReturn is null) throw RecruitMissing(removed.UnresolvedOwner ?? "join_area_removal");
            if (removed.NativeReturn != 0 && removed.PersonPhysical is int touched)
                working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, touched, [], publishVector: true);
            return removed.NativeReturn != 0;
        };
        try
        {
            var jobs = PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(current, person,
                destinationDistrict, destinationHome);
            if (jobs.NativeReturn is null) throw RecruitMissing(jobs.UnresolvedOwner ?? "join_29C430");
            working = ApplyPersonAssociationRemovalReports(working, current, person, jobs, readDay450, sink);
            working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, person, [jobs]);
        }
        finally
        {
            current.AssignmentAdvisorResetCallbacks = oldAdvisor;
            current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
            BindPolicy();
        }
        var departments = PersonCaptureForceDepartments.ApplyCurrent(working, current, originalForce, person);
        working = departments.State ?? throw RecruitMissing(departments.UnresolvedOwner ?? "join_2E8160");
        BindPolicy();
        if (TalentForceIsPlayerCurrent(working, current, originalForce))
        {
            var removed = CommanderSelectionSource.RemoveCurrentOfficer(working, current, person);
            working = removed.State ?? throw RecruitMissing(removed.UnresolvedOwner ?? "join_29CCB0");
        }

        //22A200(Person,1) checks the commander identity; a captive's holder
        //Army and another Army occupying the same location are not its Army.
        ushort location = Need(current.ReadPersonSecondLocation(person), "join_Army_E4");
        var army = current.ReadArmyUnitTableReference(location is >= 62 and <= 561 ? location - 61 : 0)
            ?? throw RecruitMissing("join_Army_lookup");
        if (Need(current.ReadArmyUnitCommanderId(army), "join_Army12") != Need(current.ReadPersonIdentity(person), "join_Person10"))
            army = current.ReadArmyUnitTableReference(0) ?? throw RecruitMissing("join_Army0");
        //22AC00 precedes the Army validity branch even when the Army is valid.
        var linked = current.ReadPersonLinkedEntity(person) ?? throw RecruitMissing("join_associated22AC00");
        if (!Need(current.ReadArmyUnitNativeValidity(army), "join_Army_validity"))
        {
            if (Need(current.ReadLinkedEntityNativeValidity(linked), "join_associated_validity"))
            {
                int kind = absorption ? Need(new PersonReturnGovernorQueries(working, _governorInputs, current)
                    .ReadCurrentAssociatedVirtual98(linked), "absorption_join_associated_kind") : 0;
                if (!absorption || kind is 1 or 15)
                    working = CompleteRecruitAssociatedCommandCurrent(working, current, linked, date, sink);
                else
                {
                    var detached = PersonReturnCommands.DetachPersonFromFirstAssociated(working, current, person);
                    working = working with { PersonnelCommands = detached.Owner
                        ?? throw RecruitMissing(detached.UnresolvedOwner ?? "absorption_join_member_detachment") };
                    ushort home = Need(current.ReadPersonFirstLocation(person), "absorption_join_old_home");
                    if (home is >= 1 and <= 61) current.WritePersonSecondLocation(person, home);
                }
            }
            return working;
        }
        int physical = Need(army.PhysicalSlot, "join_Army_pointer");
        var temporary = current.CreateNearbyTemporary();
        try
        {
            OwnedArmyDestructionStage.CollectCaptivesCurrent(current, physical, temporary);
            var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw RecruitMissing("join_captive_head");
            while (node.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(node) ?? throw RecruitMissing("join_captive_payload");
                if (entry.Kind != RegistryKind.Person || entry.PhysicalSlot is not int captive)
                    throw RecruitMissing("join_captive_Person");
                working = ApplyCaptiveReleaseCurrent(working, current, captive, date, readDay450, sink);
                node = current.ReadRegistryNodeNext(node) ?? throw RecruitMissing("join_captive_next");
            }
            //The temporary stays alive across2725E0, exactly as in271C20.
            var image = NotificationArmy(working, physical);
            var operation = new FacilityOperation(() => date,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw RecruitMissing("join_current_calendar"),
                _ => readDay450());
            var beforeOverride = image.OverrideTarget;
            working = MaintainOrdinaryArmyCommand(working, image, operation, null, current);
            var maintained = NotificationArmy(working, physical);
            if (!ReferenceEquals(beforeOverride, maintained.OverrideTarget))
                current.SetArmyCommandOverride(physical, ArmyOverrideTargetSourceAdapter.Read(working, maintained));
            return working;
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
    }

    private GameState CompleteRecruitAssociatedCommandCurrent(GameState state, SourceWorldContext current,
        PersonLinkedEntityReference linked, NativeMapSeasonDate date, List<CampaignEffectRequest> sink)
        => PersonReturnCommands.CancelAssociatedCommandCurrent(state, current,
            TalentNeed(linked.Identity, "join_associated_command_identity"), date);
}
