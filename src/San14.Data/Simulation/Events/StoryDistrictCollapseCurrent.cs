using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class EventPersonMovementCurrent
    {
        internal GameState CollapseStoryDistricts(ForceReference force)
        {
            if (!Need(current.ReadForceNativeValidity(force), "collapse_Force_validity")) return latest;
            var primary = ForcePrimaryDistrictSource.Resolve(current, force);
            if (!Need(current.ReadDistrictNativeValidity(primary), "collapse_primary_District_validity")) return latest;
            //22BEC0 counts the actual filtered D0 chain, without a separate
            //District validity gate. Reacquire it after every real merge.
            if (ForceDistricts().Count() <= 1) return latest;
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldArea = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            storyImmediateRetirement = true;
            try
            {
                BindInputs();
                var operation = new ArmyReturnDistrictOperationQueries(
                    new PersonReturnGovernorQueries(latest, rules._governorInputs, current),
                    () => latest, value => latest = value, ReceiveAssignment, current, [], rules, Date);
                do
                {
                    using var districts = ForceDistricts().GetEnumerator();
                    if (!districts.MoveNext()) throw Missing("collapse_District_iterator_head");
                    var selected = districts.Current;
                    if (selected == primary)
                    {
                        if (!districts.MoveNext()) throw Missing("collapse_District_iterator_next");
                        selected = districts.Current;
                    }
                    //Full293490(mode0): former leader, serving/captive people,
                    //real command cancellation, City/Gate governors, stock24,
                    //caution groups, route policies, D0 unlink and reusable reset.
                    PersonReturnDistrictReorganizationSource.MergeCurrent(current, selected, primary, operation);
                } while (ForceDistricts().Count() > 1);

                latest = PersonReturnDistrictTransferSource.PublishAreaAssignments(latest, current,
                    operation.ChangedAreas, operation.ChangedGates);
                latest = PersonReturnDistrictReorganizationSource.PublishCurrent(latest, current, operation);
                latest = PersonAssignmentPrecallInputs.PublishCurrent(latest, current);
                latest = BaseCaptureCurrentInputs.PublishCurrent(latest, current);
                Publish();
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw Missing("collapse_effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("collapse_effect_calendar"), effects.ToArray()) };
                return latest;
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldArea;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }

            IEnumerable<DistrictReference> ForceDistricts()
            {
                int key = Need(current.ReadForcePhysicalSlot(force), "collapse_Force_identity");
                foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("collapse_District_D0"))
                {
                    var district = entry ?? throw Missing("collapse_District_payload");
                    int physical = Need(district.PhysicalSlot, "collapse_District_receiver");
                    if (key is >= 1 and <= 51 && Need(current.ReadDistrictForceId(physical), "collapse_District10") == key)
                        yield return district;
                }
            }
        }
    }
}
