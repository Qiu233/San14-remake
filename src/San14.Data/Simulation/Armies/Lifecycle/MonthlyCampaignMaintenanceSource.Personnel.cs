using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        //27B0F0's selected Person consumer. The enclosing selection already
        //checked native validity and owns the two live Person temporaries.
        private void ConsumeSpecialPersonnel(int person, ForceReference destination)
            => ConsumeSpecialPersonnelCurrent(person, destination, storyStateAdvantage: false);

        private void ConsumeSpecialPersonnelCurrent(int person, ForceReference destination, bool storyStateAdvantage)
        {
            var reports = new List<CampaignEffectRequest>();
            if (!SpecialRegistered(person))
            {
                var home = SpecialRulerHome(destination);
                SpecialAppointNew(person, CurrentGateMapQueries.FindCity(home, current), reports);
            }
            else
            {
                //27BA73..27BAB9: fifth1 skips a registered Person only when
                //231D90's decoded index equals raw HomeE2 and that actual
                //registered Base's virtual78 District equals PersonE0.
                if (storyStateAdvantage && IsStoryStateAdvantagePersonnelSettled(person)) return;
                //The source retains22EA10 before calling22E930. The shared
                //271C20 entry has these exact param4=0,param5=0,param7=1 values;
                //it includes actual captive release, associated cancellation,
                //2725E0 command maintenance and District reorganization.
                var home = SpecialRulerHome(destination);
                var district = ForcePrimaryDistrictSource.Resolve(current, destination);
                var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("special_World34_date");
                state = package.ApplyRecruitJoinCurrent(state, current, person, district,
                    home, date, SpecialDay450, reports).State;
                Observe("monthly.special-personnel.transferred." + person);
            }
            if (reports.Count != 0 && storyStateAdvantage && borrowedEffects is not null)
                borrowedEffects.AddRange(reports);
            else if (reports.Count != 0)
            {
                var requests = state.CampaignEffectRequests ?? throw Missing("special_report_owner");
                long revision = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("special_report_revision");
                state = state with { CampaignEffectRequests = requests.Append(
                    CampaignEffectSource.MonthlyMaintenance, revision, reports) };
            }
            //The new state is visible to the enclosing relation/Person loop;
            //no pool capture, export or whole-world role projection occurs.
            current.BaseForcePolicyEffectReader = (force, effect) =>
                ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true).Value;
        }

        private bool SpecialRegistered(int person)
        {
            var reference = new RegistryObjectReference(RegistryKind.Person, person);
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw Missing("special_Person100_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("special_Person100_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("special_Person100_payload");
                if (payload == reference) return true;
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("special_Person100_next");
            }
            return false;
        }

        private TransferPersonReference SpecialLookup(int raw) =>
            (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("special_Person_lookup");

        private BaseReference SpecialRulerHome(ForceReference force)
        {
            ushort raw = Need(current.ReadForceRecord(Need(force.PhysicalSlot, "special_ruler_Force"))?.RawRuler10, "special_ruler10");
            int ruler = Need(SpecialLookup(raw).PhysicalSlot, "special_ruler_pointer");
            return BaseLookup.Find(Need(current.ReadPersonFirstLocation(ruler), "special_ruler_E2"), current);
        }

        private int SpecialDay450() => (package.ReadArmyDestructionSampler(state, current)
            ?? throw Missing("special_World450_current")).Day450;

        private void SpecialAppointNew(int person, BaseReference city, List<CampaignEffectRequest> reports)
        {
            //29E080 derives its Force from the actual mapped City, rather
            //than borrowing the retained destination argument of27B0F0.
            int rawForce = Need(current.ReadBaseForceSlot(city), "special_City_Force22CAD0");
            var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw Missing("special_City_Force_pointer");
            var appearance = new PersonAppearanceSource(package, state, current, pool);
            state = appearance.ApplyMonthlySpecialJoinCurrent(person, city, force, SpecialDay450, reports);
            if (!SpecialRegistered(person))
                _ = ObjectRegistration.Register(new(RegistryKind.Person, person), current);
            if (Need(current.ReadBaseNativeValidity(city), "special_governor_City_validity"))
            {
                //22ACF0 still resolves the former governor before282C10.
                _ = SpecialLookup(Need(current.ReadArmyReturnBaseGovernor2E(city), "special_governor2E"));
                var update = package.ApplyPersonFreedomGovernorCurrent(state, current, city);
                state = ArmyReturnCommanderHomeUpdater.PublishPersonCurrent(state, current,
                    new(person, city, false, [update], null));
            }
            var aftermath = current.ReadOfficerAftermath(person) ?? throw Missing("special_resentment_owner");
            int target = aftermath.Resentment?.TargetOfficerId ?? aftermath.SourceKnowledge?.UnmappedResentmentTarget
                ?? throw Missing("special_resentment162");
            ushort ruler = Need(current.ReadMoraleWaveRuler22EAC0(force), "special_resentment_Force_ruler");
            if (target == ruler)
            {
                current.SetOfficerAftermath(person, aftermath with { Resentment = new(0, 0),
                    SourceKnowledge = aftermath.SourceKnowledge is { } residue
                        ? residue with { UnmappedResentmentTarget = null } : null });
                state = current.PublishOfficerAftermath(state, Need(current.ReadPersonIdentity(person), "special_Person10"));
            }
            Observe("monthly.special-personnel.new." + person);
        }
    }

    internal sealed partial class PersonAppearanceSource
    {
        //The existing29E080 creator remains shared with Hire. Only this
        //operation supplies the already-owned association report consumer;
        //annual appearance/Hire retain their original default callback.
        private Action<PersonAssignmentPrecallResult>? _monthlyAssignmentReports;

        internal GameState ApplyMonthlySpecialJoinCurrent(int person, BaseReference city,
            ForceReference force, Func<int> readDay450, List<CampaignEffectRequest> reports)
        {
            var previous = _monthlyAssignmentReports;
            _monthlyAssignmentReports = result => latest = package.ApplyPersonAssociationRemovalReports(
                latest, current, person, result, readDay450, reports);
            try
            {
                latest = ApplyHireJoinCurrent(person, city, force);
                //29E080's final DWORD1C0 store clears both selection counts.
                //Do not preserve selections from an unregistered template.
                Change(person, owner =>
                {
                    var merit = owner.MeritState ?? throw Missing("monthly_join_credit_owner");
                    return owner with { MeritState = new(merit.Merit, merit.PendingMerit,
                        merit.RankStageId, 0, 0, merit.ResetCode) };
                });
                return latest;
            }
            finally { _monthlyAssignmentReports = previous; }
        }
    }
}
