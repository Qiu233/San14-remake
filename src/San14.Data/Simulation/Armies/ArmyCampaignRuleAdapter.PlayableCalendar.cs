using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Runs the existing ordinary Return manager at actual8/22 dates
    /// before the calendar writer, and selected playable facilities/Structure
    /// attacks at their reached cuts. No stage13 moment or historical phase
    /// capture is manufactured. Already-consumed cycles are not replayed.</summary>
    public GameState AdvancePlayableCalendar(GameState state)
    {
        if (state.NativePlayableTerminal is not null) return state;
        bool returnUnresolved = false;
        Func<GameState, NativeMapSeasonDate, GameState> returnTick = (current, date) =>
        {
            if (returnUnresolved || date.Hour is not (8 or 22) || current.PersonnelCommands is not { } commands ||
                commands.Returns.Count == 0 && commands.Disappearances is { Count: 0 }) return current;
            var returned = TryAdvancePersonReturnCommands(current, date);
            if (returned.State is not { } continued)
            {
                //Keep the unresolved command and its busy Person. Its next
                //period retry must not become a fabricated arrival or refund.
                returnUnresolved = true;
                return OwnedCurrentPlayerForce.ReadRawForce3A(current) is byte player &&
                    current.Forces.ContainsKey(player)
                    ? current with { PeriodReports = [.. current.PeriodReports,
                        new("person_return_unresolved", player) { RuleId = returned.UnresolvedOwner }] }
                    : current;
            }
            var reports = returned.Receipts.Where(row => row.Arrival is not null).Select(row =>
            {
                int person = row.Arrival!.PersonPhysical;
                int id = _definitions.ReadOfficerSourceIdentity(person)
                    ?? throw new InvalidDataException("Returning Person source identity is unknown.");
                if (_definitions.ResolveOfficerSource(id) != person)
                    throw new InvalidDataException("Returning Person source identity is inconsistent.");
                var officer = continued.Officers[id];
                return new PeriodReportEntry("officer_return", officer.ForceId ?? 0,
                    officer.LocationId, officer.Id) { RuleId = "ce-202360-Return8_22-32B190" };
            });
            return continued with { PeriodReports = [.. continued.PeriodReports, .. reports] };
        };
        long revision = state.NativeMapSeason?.AdvancedCalendarSteps ?? 0;
        long elapsedDays = state.NativeMapSeason?.AdvancedDays ?? 0;
        return NormalArmyCombatCalendarRuntime.AdvanceCampaign(state, beforeCalendarWriter: (current, date) =>
        {
            if (date.Hour == 0 && current.Units.Count != 0)
            {
                var appeared = CompletePlayableArmyAppearance(current, date);
                current = appeared.State ?? throw new InvalidDataException(
                    "Playable Army appearance is unresolved: " + appeared.UnresolvedOwner);
            }
            current = returnTick(current, date);
            if (current.Units.Values.Any(unit => unit.Mission?.Kind is
                UnitMissionKind.BuildStructure or UnitMissionKind.RepairStructure or UnitMissionKind.DemolishStructure) ||
                date.Hour == 10 && current.Units.Values.Any(unit => unit.MilitaryPlan is
                    { Completed: false, Action: { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Structure } }))
            {
                long dayAtWriter = elapsedDays;
                var operation = new FacilityOperation(() => date, revision,
                    reached => unchecked((reached.NativeArmySimulation?.World.SamplerEpoch
                        ?? throw FacilityConstructionDefinitions.Missing("playable_epoch")).Day450 + (int)dayAtWriter));
                current = CompletePlayableFacilityWork(current, operation, date.Hour == 10);
            }
            var step = MapSeasonRuntime.StepCalendarWriter(date);
            if (step.DayAdvanced) elapsedDays++;
            revision++;
            return current;
        });
    }
}
