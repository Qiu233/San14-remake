using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //259360 cause1 passes (victim,killer,1,0,1,null,1,1,0) to295EE0.
    //The enclosing destruction owns the Army; this call owns only the actual
    //Person transition and its source-time reports, items and relationships.
    internal (GameState State, int? NativeReturn) ApplyDuelDeathCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int person,
        TransferPersonReference killer) => new PersonDeathSource(this, state, current, pool)
            .ApplyCombatDeath(person, killer, 1, false);

    internal sealed partial class PersonDeathSource
    {
        private int DeathCombatParameter(string name, int initial)
        {
            var parameters = latest.NativeArmySimulation?.Parameters ?? throw Missing("combat_parameters");
            string key = "simulation.person-death." + name;
            if (parameters.Float32Values.ContainsKey(key)) throw Missing("combat_parameter_type_" + name);
            //Immutable CE mechanism constants, extracted offline. An existing
            //saved mechanism override remains authoritative when present.
            return parameters.Int32Values.GetValueOrDefault(key, initial);
        }

        private void ApplyKillerForceRelations(int person, byte status, int office,
            ForceReference oldForce, bool willUnregister)
        {
            if (!Valid(killer) || willUnregister) return;
            var other = Force(Physical(killer, "killer_relation_receiver"));
            _ = Known(current.ReadForcePhysicalSlot(other), "killer_relation_Force_class");
            int loss;
            int notify;
            if (status == 1)
            { loss = DeathCombatParameter("ruler-killed-relation-loss", 80); notify = 1; }
            else if (Known(OwnedArmyMoraleWaveInputs.ReadPost37(latest, office), "killed_relation_office37") <
                Parameter("office-report-rank-threshold"))
            { loss = DeathCombatParameter("notable-killed-relation-loss", 20); notify = 1; }
            else
            { loss = DeathCombatParameter("general-killed-relation-loss", 5); notify = 0; }
            latest = new MonthlyCampaignMaintenanceSource(package, latest, current, pool)
                .ApplyRelation(oldForce, other, unchecked(-loss), notify);
            BindLatestPolicyReader();
            Observe("death.killer-force-relations");
        }

        private void MergeDeceasedLeaderDistrict(int person, ForceReference force)
        {
            byte raw = Known(current.ReadPersonDistrictSlot(person), "leader_DistrictE0");
            var district = current.ReadDistrictTableReference(raw < 52 ? raw : 0)
                ?? throw Missing("leader_District_lookup");
            int physical = Known(district.PhysicalSlot, "leader_District_pointer");
            int count = 0;
            //230350(District,0,0) is234850's current registration iterator,
            //with only the actual E0/status1..4 filter; it does not test Home.
            foreach (int? row in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("leader_Person_registry"))
            {
                int other = Known(row, "leader_Person_pointer");
                if (Known(current.ReadPersonDistrictSlot(other), "leader_member_E0") == physical &&
                    Status(other) is >= 1 and <= 4) count = unchecked(count + 1);
            }
            if (count != 1) return;
            var destination = ForcePrimaryDistrictSource.Resolve(current, force);
            var operation = new ArmyReturnDistrictOperationQueries(
                new PersonReturnGovernorQueries(latest, package._governorInputs, current),
                () => latest, value => latest = value, Assignment, current, [], package,
                latest.NativeMapSeason?.ThirdDate);
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldRemoval = current.AssignmentAreaOfficerRemoval;
            current.AssignmentAdvisorResetCallbacks = owner =>
            {
                var reset = PersonAdvisorResetSource.ApplyWithinPersonDeath(latest, current, pool, owner);
                latest = reset.State ?? throw Missing(reset.UnresolvedOwner ?? "leader_advisor_reset");
                BindLatestPolicyReader();
                return true;
            };
            current.AssignmentAreaOfficerRemoval = (area, old) =>
            {
                var result = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, old);
                if (result.NativeReturn is null) throw Missing(result.UnresolvedOwner ?? "leader_area_removal");
                if (result.NativeReturn != 0 && result.PersonPhysical is int touched)
                    latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, touched, [], publishVector: true);
                return result.NativeReturn != 0;
            };
            try
            {
                PersonReturnDistrictReorganizationSource.MergeCurrent(current, district, destination, operation);
                latest = OwnedArmyReturnCurrentFields.PublishCurrent(latest, current);
                latest = OwnedArmyLifecycleDetachedInputs.PublishCurrent(latest, current);
                latest = PersonAssignmentPrecallInputs.PublishCurrent(latest, current);
                latest = BaseCaptureCurrentInputs.PublishCurrent(latest, current);
                latest = PersonReturnDistrictTransferSource.PublishAreaAssignments(latest, current,
                    operation.ChangedAreas, operation.ChangedGates);
                latest = PersonReturnDistrictReorganizationSource.PublishCurrent(latest, current, operation);
                Observe("death.leader-District-merged");
            }
            finally
            { current.AssignmentAdvisorResetCallbacks = oldAdvisor; current.AssignmentAreaOfficerRemoval = oldRemoval; }

            void Assignment(int touched, PersonAssignmentPrecallResult result)
            {
                if (result.NativeReturn is null) throw Missing(result.UnresolvedOwner ?? "leader_assignment");
                if (result.Reports.Count == 0) return;
                var effects = new List<CampaignEffectRequest>();
                latest = package.ApplyPersonAssociationRemovalReports(latest, current, touched, result,
                    () => (package.ReadArmyDestructionSampler(latest, current) ?? throw Missing("leader_report_clock")).Day450,
                    effects);
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw Missing("leader_report_effects")).Append(CampaignEffectSource.ArmyBattleDefeat,
                            latest.NativeClock?.UpdateOrdinal ?? throw Missing("leader_report_revision"), effects) };
            }
        }
    }
}
