using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonDeathSource
    {
        //28DFB0(old,new,out,0). The caller retains the real Person list for
        //the later independence decisions and source-order reports.
        private void ApplyRulerSuccessionTransfer(int oldRuler, int newRuler, HexListHandle retainedPeople,
            bool refreshForce = false)
        {
            if (!Known(current.ReadPersonNativeValidity(oldRuler), "succession_old_validity") ||
                !Known(current.ReadPersonNativeValidity(newRuler), "succession_new_validity")) return;
            var force = Force(oldRuler);
            int forceId = Known(current.ReadForcePhysicalSlot(force), "succession_Force60");
            ushort rulerId = (ushort)Math.Min((int)Identity(newRuler), 6000);
            current.SetForceRecord(new(forceId, rulerId));
            SuccessionForceField(forceId, 0x10, rulerId, 2);
            var owner = latest.Forces.GetValueOrDefault(forceId) ?? throw Missing("succession_Force_owner");
            int doctrine = SuccessionDoctrine(newRuler);
            latest = latest with { Forces = new(latest.Forces) { [forceId] = owner with
                { RulerId = rulerId, DoctrineId = doctrine,
                  Name = Known(current.ReadForceKingdomNameIndex(forceId), "succession_Force48") == 0 ? Owner(newRuler).Name : owner.Name } } };
            SetStatus(newRuler, 1);
            current.SetMoraleWavePersonPostE7(newRuler, 73);
            Change(newRuler, p => p with { Appointment = OfficerAppointment.Synchronize(73) });
            SuccessionLoyalty(newRuler, force, penalizeAmbition: false);
            SuccessionMerit(newRuler, 0);
            SuccessionMerit(oldRuler, 2);
            if (Known(current.ReadAssignmentForceAdvisor52(forceId), "succession_Force52") == Identity(newRuler))
            {
                current.SetAssignmentForceAdvisor52(forceId, 0);
                SuccessionForceField(forceId, 0x52, 0, 2);
            }
            var effects = new List<CampaignEffectRequest>();
            var date = latest.NativeMapSeason?.ThirdDate ?? throw Missing("succession_ThirdDate");
            var operation = package.CreateCaptureDistrictOperationQueries(current, () => latest, value => latest = value, date, effects);
            byte newDistrict = Known(current.ReadPersonDistrictSlot(newRuler), "succession_new_E0");
            byte oldDistrict = Known(current.ReadPersonDistrictSlot(oldRuler), "succession_old_E0");
            if (newDistrict != oldDistrict)
                PersonReturnDistrictReorganizationSource.MergeCurrent(current,
                    District(newDistrict), District(oldDistrict), operation);
            SetStatus(oldRuler, 4);
            byte restoreDistrict = Known(current.ReadPersonDistrictSlot(oldRuler), "succession_retained_old_E0");
            Transfer(oldRuler, 0);
            var primary = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current,
                District(Known(current.ReadPersonDistrictSlot(newRuler), "succession_reorganize_new_E0")), operation);
            if (primary.UnresolvedOwner is not null) throw Missing(primary.UnresolvedOwner);
            Transfer(oldRuler, restoreDistrict);
            latest = PersonReturnDistrictReorganizationSource.PublishCurrent(latest, current, operation);
            latest = PersonReturnDistrictTransferSource.PublishAreaAssignments(latest, current, operation.ChangedAreas, operation.ChangedGates);
            latest = PersonAssignmentPrecallInputs.PublishCurrent(latest, current);
            //Publish the actual migration/governor writes before any later
            //policy/loyalty reader; keep every outer temporary alive.
            latest = OwnedArmyReturnCurrentFields.PublishCurrent(latest, current);
            latest = OwnedArmyLifecycleDetachedInputs.PublishCurrent(latest, current);
            Enter();
            try { current.ClearTemporaryHexList(retainedPeople); }
            finally { Exit(); }
            //2349D0 mode4 uses232F10(mode4)=true, including captives and
            //other statuses. There is no extra native-validity predicate.
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("succession_Person100"))
            {
                int person = Known(entry, "succession_Person_payload");
                if (forceId is < 1 or > 51 || Known(current.ReadPersonOldForceSlot(person), "succession_Person_Force") != forceId) continue;
                SuccessionLoyalty(person, force, penalizeAmbition: true);
                Append(retainedPeople, new(RegistryKind.Person, person));
            }
            AppendRulerSuccessionEffects(effects);
            //28DFB0's ordinary Story caller passes1. Death passes0 and
            //retains its later independent-successor work before this tail.
            if (refreshForce) ApplyRulerSuccessionForceTail(force, newRuler, oldRuler);
            BindLatestPolicyReader();
            return;

            DistrictReference District(byte raw) => current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
                ?? throw Missing("succession_District_pointer");
            void Transfer(int person, int district)
            {
                var results = new List<PersonAssignmentPrecallResult>();
                var previousRefresh = current.TransferServiceRefresher;
                current.TransferServiceRefresher = operation.RefreshPersonnelMilestoneCurrent;
                try
                {
                    PersonDistrictTransfer.Apply(person, district, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current,
                        results, result => operation.Assignment(person, result)));
                }
                finally { current.TransferServiceRefresher = previousRefresh; }
                latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, person, results);
            }
        }

        //26D140(Force,new,old,1): ordered governance reset, derived refreshes,
        //then a doctrine change refunds OLD-ruler costs and installs the new root.
        private void ApplyRulerSuccessionForceTail(ForceReference force, int newRuler, int oldRuler)
        {
            if (!Known(current.ReadForceNativeValidity(force), "succession_tail_Force_validity") ||
                !Known(current.ReadPersonNativeValidity(newRuler), "succession_tail_new_validity") ||
                !Known(current.ReadPersonNativeValidity(oldRuler), "succession_tail_old_validity")) return;
            int forceId = Known(current.ReadForcePhysicalSlot(force), "succession_tail_Force60");
            //Matching TC file-backed immutable1A49030=10.
            SuccessionForceField(forceId, 0x16A, 10, 2);
            current.SetAssignmentForceAdvisor52(forceId, 0);
            SuccessionForceField(forceId, 0x52, 0, 2);
            SuccessionForceField(forceId, 0x16C, 0, 2);
            Refresh(true, true, true); //2E9020(Force,1)
            for (int group = 0; group < 5; group++)
            {
                for (int ordinal = 0; ordinal < 7; ordinal++)
                {
                    SuccessionForceField(forceId, 0x54 + group * 30 + ordinal * 2, 0, 2);
                    SuccessionForceField(forceId, 0x62 + group * 30 + ordinal * 2, 0, 2);
                }
                if (group == 1) Refresh(true, false, false);
                else if (group == 4) Refresh(false, true, false);
                else if (group == 3) Refresh(false, false, true);
            }
            Refresh(true, true, true); //27BE40,27AA40,2FF040 again.
            if (IsPlayer(force))
            {
                var message = package.FormatNormalTurnReport(latest, current, 0x5800, new());
                latest = message.State;
                AppendRulerSuccessionEffects([new(null, null) { TalentCommandPresentation =
                    new(TalentCommandPresentationOperation.Announcement, null, null, null, null, message.Text) }]);
            }
            int newDoctrine = SuccessionDoctrine(newRuler), oldDoctrine = SuccessionDoctrine(oldRuler);
            if (newDoctrine != oldDoctrine)
            {
                for (int group = 0; group < 5; group++) SuccessionForceField(forceId, 0x70 + group * 30, 0, 1);
                var forceOwner = latest.Forces.GetValueOrDefault(forceId) ?? throw Missing("succession_research_Force");
                var reserves = latest.ForceCreditReserves ?? throw Missing("succession_research_reserves");
                if (!reserves.PointsByForce.TryGetValue(forceId, out var points)) throw Missing("succession_research_points");
                var baseline = new DoctrineResearchWorkspace(points,
                    forceOwner.PolicyEffects?.ActiveEffectIds ?? throw Missing("succession_research_active"),
                    forceOwner.PolicyEffects?.SuppressedEffectIds ?? throw Missing("succession_research_pending"),
                    forceOwner.DoctrineResearch?.AvailablePanelIds ?? throw Missing("succession_research_available"));
                var working = baseline.Copy();
                var panels = package.ReadDoctrineResearchDefinitions(current);
                int discount = package.ResearchParameter(latest, "matching-doctrine-discount", 1);
                for (int id = 1; id < 80; id++)
                {
                    if (!working.Researched.Contains(id)) continue;
                    var panel = panels.GetValueOrDefault(id) ?? throw Missing("succession_refund_panel");
                    var cost = DoctrineResearchSource.Costs(panel, oldDoctrine, discount);
                    Refund(panel.FirstDoctrineId, cost.First); Refund(panel.SecondDoctrineId, cost.Second);
                }
                working.Researched.Clear(); working.Pending.Clear(); working.Available.Clear(); working.Available.Add(1);
                var root = panels.GetValueOrDefault(1) ?? throw Missing("succession_research_root");
                var layouts = panels.Values.OrderBy(row => row.Id).GroupBy(row => row.LayoutSlot)
                    .ToDictionary(group => group.Key, group => group.Last().Id);
                DoctrineResearchSource.Apply(working, baseline, root, newDoctrine, discount, layouts);
                latest = PublishDoctrineResearchWorkspace(latest, forceId, working);
                latest = package.ClampResourcesAfterDoctrineResearch(latest, current, baseline, working, forceId);
                void Refund(int doctrine, int amount)
                {
                    if (doctrine == 0) return;
                    if (doctrine is < 1 or > 6) throw Missing("succession_refund_doctrine");
                    working.Points[doctrine - 1] = (byte)Math.Min(99, working.Points[doctrine - 1] + amount);
                }
            }
            BindLatestPolicyReader();
            return;

            void Refresh(bool morale, bool policy, bool cities)
            {
                BindLatestPolicyReader();
                var result = PersonAdvisorResetSource.RefreshGovernanceCurrent(latest, current, pool, force, morale, policy, cities);
                latest = result.State ?? throw Missing(result.UnresolvedOwner ?? "succession_governance_refresh");
                BindLatestPolicyReader();
            }
        }

        private int SuccessionDoctrine(int person) => Known(OfficerDoctrineRules.ReadDoctrineId(
            Owner(person).DoctrineId, Owner(person).AbilityState), "succession_Person14C");

        private void SuccessionMerit(int person, int stage)
        {
            var ranks = package._scenario.Snapshot.CreditRankParameters?.Ranks ?? throw Missing("succession_credit_ranks");
            int selector = Known(BattleRemakeCurrentSettings.ReadSelectorE58(latest), "succession_credit_selector");
            int option = Known(BattleRemakeCurrentSettings.ReadInt32(latest, selector == 0 ? 0x4C4 : 0x128), "succession_credit_option");
            int lower = ranks[option == 1 ? 2 : 1].SignedThreshold, upper = ranks[6].SignedThreshold;
            int value = ranks[stage].SignedThreshold;
            value = value < lower ? lower : Math.Min(value, upper);
            Change(person, p => p with { MeritState = new(unchecked((short)value), 0, stage, 0, 0, 0) });
        }

        private void SuccessionLoyalty(int person, ForceReference force, bool penalizeAmbition)
        {
            byte result = 250;
            if (Status(person) != 1)
            {
                ushort rulerRaw = Known(current.ReadForceRecord(Known(force.PhysicalSlot, "succession_loyalty_Force"))?.RawRuler10,
                    "succession_loyalty_ruler10");
                int ruler = Known((rulerRaw <= 6000 ? current.ReadPersonLookupReference(rulerRaw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
                    "succession_loyalty_ruler");
                if (StrategicIndependenceSwornCurrent(latest, current, person, ruler) ||
                    StrategicIndependenceSpouseCurrent(latest, current, person, ruler)) result = 150;
                else if (OfficeRelationList(latest, current, person, ruler, 0xC0) ||
                    (Known(current.ReadOwnedReturnPersonFlags(person), "succession_loyalty_flags14") & 0x4000) != 0) result = 120;
                else
                {
                    int difference = Math.Abs(Known(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, person), "succession_affinity") -
                        Known(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, ruler), "succession_ruler_affinity"));
                    int affinity = Math.Min(50, Math.Min(75, Math.Min(difference, 150 - difference)));
                    var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                        SimulationRuleIdentity.ForLocale(package.Seed.Language));
                    int charm = Known(abilities.ResolveDimension(latest, Identity(ruler), 4,
                        (owner, effect) => package._baseCapacityInputs.ReadPanelModifier(latest, owner, effect)), "succession_ruler_charm");
                    int bonus = Relation(person, 0xB2) == Relation(ruler, 0xB2) ? 5 : 0;
                    if (SuccessionDoctrine(person) != 0 && SuccessionDoctrine(person) == SuccessionDoctrine(ruler)) bonus += 3;
                    int signed = unchecked((short)Known(RulerCaptureReleaseInputs.ReadCurrentWord(latest, person, 0x17A), "succession_Person17A"));
                    int rulerSigned = unchecked((short)Known(RulerCaptureReleaseInputs.ReadCurrentWord(latest, ruler, 0x17A), "succession_ruler17A"));
                    if (signed - rulerSigned <= TalentRecruitParameters.Read(latest, "initial-loyalty-signed-threshold")) bonus += 3;
                    int ambitionPenalty = 0;
                    if (penalizeAmbition)
                    {
                        int ambition = unchecked((short)Known(RulerCaptureReleaseInputs.ReadCurrentWord(latest, person, 0x17C), "succession_Person17C"));
                        if (ambition > Known(current.ReadAllegianceDisposition(person), "succession_Person16C"))
                            ambitionPenalty = ambition / 2 + 1; //TC immutable1A4AC8C=2.
                    }
                    result = (byte)Math.Clamp(unchecked(bonus + charm / 14 - affinity / 2 - ambitionPenalty + 100 +
                        (OfficeRelationList(latest, current, person, ruler, 0xD0) ? -15 : 0)), 0, 100);
                }
            }
            if ((Known(current.ReadOwnedReturnPersonFlags(person), "succession_loyalty_flags13") & 0x2000) != 0) result = 255;
            current.SetPersonDestructionLoyaltyE8(person, result);
            latest = ArmyDestructionLoyalty.PublishCurrentRawE8(latest, current, person, result);
        }

        private void SuccessionForceField(int force, int offset, int value, int width)
        {
            var native = latest.NativeArmySimulation ?? throw Missing("succession_force_parameters");
            latest = latest with { NativeArmySimulation = native with { Parameters = native.Parameters with
                { Int32Values = new(native.Parameters.Int32Values) { [BattleRemakeCurrentForcePolicies.ForceFieldKey(force, offset)] = value } } } };
            //A reused Force's saved reset image remains an existing owner.
            if (latest.NativeForceUnregisterCurrent is { } reset && reset.ForceResetImages.TryGetValue(force, out var old))
            {
                var image = old.ToArray();
                image[offset] = (byte)value;
                if (width == 2) image[offset + 1] = (byte)(value >> 8);
                latest = latest with { NativeForceUnregisterCurrent = reset with
                    { ForceResetImages = new(reset.ForceResetImages) { [force] = image } } };
            }
        }
    }
}
