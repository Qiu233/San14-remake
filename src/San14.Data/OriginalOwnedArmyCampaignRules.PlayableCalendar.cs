using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>Runs the existing ordinary Return manager at actual8/22 dates
    /// before the calendar writer. No Army combat stage13 is reconstructed from
    /// the period-end snapshot. Already-consumed calendar cycles are not replayed.</summary>
    public GameState AdvancePlayableCalendar(GameState state)
    {
        if (state.NativePlayableTerminal is not null) return state;
        bool returnUnresolved = false;
        return OriginalNormalArmyCombatCalendarRuntime.AdvanceCampaign(state, beforeCalendarWriter: (current, date) =>
        {
            if (returnUnresolved || date.Hour is not (8 or 22) ||
                current.NativePersonReturnCommands is not { Commands.Count: > 0 }) return current;
            var returned = TryAdvancePersonReturnCommands(current, date);
            if (returned.State is not { } continued)
            {
                //Keep the unresolved command and its busy Person. Its next
                //period retry must not become a fabricated arrival or refund.
                returnUnresolved = true;
                return OriginalOwnedCurrentPlayerForce.ReadRawForce3A(current) is byte player &&
                    current.Forces.ContainsKey(player)
                    ? current with { PeriodReports = [.. current.PeriodReports,
                        new("person_return_unresolved", player) { RuleId = returned.UnresolvedOwner }] }
                    : current;
            }
            var reports = returned.Receipts.Where(row => row.Arrival is not null).Select(row =>
            {
                int person = row.Arrival!.PersonPhysical;
                var officer = continued.Officers.Values.Single(officer => officer.NativeAbilityRuntime?.PhysicalSlot == person);
                return new PeriodReportEntry("officer_return", officer.ForceId ?? 0,
                    officer.LocationId, officer.Id) { RuleId = "ce-202360-Return8_22-32B190" };
            });
            return continued with { PeriodReports = [.. continued.PeriodReports, .. reports] };
        });
    }
}
