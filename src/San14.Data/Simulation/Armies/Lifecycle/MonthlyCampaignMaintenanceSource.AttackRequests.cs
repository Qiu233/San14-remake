using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        //Monthly Turn action6, Force C0 order. Requester validity precedes
        //the term read: neither Enabled nor a missing requester means false.
        private void ProcessRequestTerms()
        {
            foreach (int physical in Registry(RegistryKind.Force))
            {
                var force = Force(physical);
                if (!Valid(Requester(force))) continue;
                int before = Need(Objective(force).RemainingMonths, "request_term");
                if (before == 0) continue;
                int difference = unchecked(before - 1);
                WriteObjective(force, Objective(force) with
                    { RemainingMonths = Math.Clamp(difference, 0, 60) });
                Observe($"monthly.request.term.{physical}");
                if (difference == 0) ResolveRequest(force, true);
            }
        }

        //2A2620(Force,null,0) reaches2E98E0(kind2,notify1) only at term0.
        //The public diplomacy leaf is the same result contract: progress
        //brackets, both advantage8 queries, source reports, relation changes,
        //and finally clearing159..163. It borrows this operation's current.
        private void ResolveRequest(ForceReference force, bool notify)
        {
            if (Need(Objective(force).RemainingMonths, "request_outcome_term") != 0) return;
            //Both actual monthly callers pass true. Do not silently publish
            //the notifying result for a future distinct native caller.
            if (!notify) throw Missing("request_nonnotifying_resolution");
            var effects = new List<CampaignEffectRequest>();
            state = package.CancelDiplomacyAttackRequestCurrent(state, current, Physical(force), effects);
            if (effects.Count != 0)
            {
                var pending = state.CampaignEffectRequests ?? throw Missing("request_pending_owner");
                long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("request_source_revision");
                state = state with { CampaignEffectRequests = pending.Append(
                    CampaignEffectSource.MonthlyMaintenance, revision, effects) };
                Observe("monthly.request.history-written");
            }
            Observe("monthly.request.completed");
        }
    }
}
