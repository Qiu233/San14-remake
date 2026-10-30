using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonAppearanceSource
    {
        //240DB0/2783B0 and the home updater have already applied their actual
        //association gates and writes. Consume only the reports they produced,
        //at this exact point, before further membership/position changes.
        private void ConsumeOwnedAssignmentReports(int person, PersonAssignmentPrecallResult result)
        {
            if (result.Reports.Count == 0) return;
            var reports = new List<CampaignEffectRequest>();
            latest = package.ApplyPersonAssociationRemovalReports(latest, current, person, result,
                () => (package.ReadArmyDestructionSampler(latest, current)
                    ?? throw Missing("assignment_report_World450")).Day450, reports);
            if (reports.Count == 0) return;
            //Use the existing association-report publication used by ordinary
            //return arrival. It carries history notifications, not a synthetic
            //appearance event or a second transfer. History and App32 changes
            //above are adopted even when no notification was admitted.
            latest = latest with
            {
                CampaignEffectRequests = (latest.CampaignEffectRequests
                    ?? throw Missing("assignment_report_effect_owner")).Append(
                        CampaignEffectSource.ForceAssociationRemoval,
                        latest.NativeMapSeason?.AdvancedCalendarSteps
                            ?? throw Missing("assignment_report_calendar"),
                        reports.ToArray())
            };
        }
    }
}
