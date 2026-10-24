using System.Collections.Frozen;
using San14.Core;

namespace San14.Client.Gameplay;

internal sealed record CampaignCommandRoster(IReadOnlyList<NamedCommandChoice> Bases,
    int DefaultBaseId, IReadOnlySet<int> CityIds, IReadOnlyList<NamedCommandChoice> Units,
    string PendingMoves, string PendingTalent, string Status);

/// <summary>One current command-panel publication. Controls bind values and
/// selections; no world, entity or mutable collection is retained in the view.</summary>
internal sealed record CampaignCommandView(bool CanEditNewGameOptions, bool CanStart,
    bool CanIssueOrders, bool IsReporting, CampaignCommandRoster? Roster)
{
    public static CampaignCommandView Capture(GameState? world, int? playerForceId,
        int? selectedForceId, PreviewText text)
    {
        bool initializing = world is { Phase: TurnPhase.Initializing };
        if (world is not { RulesetId: PlayableSkeleton.RulesetId } || playerForceId is not int player ||
            !world.Forces.ContainsKey(player))
            return new(initializing, initializing && selectedForceId is not null, false, false, null);

        PlayableResult outcome = PlayableOutcome.Evaluate(world, player);
        bool strategy = world.Phase == TurnPhase.Strategy && outcome == PlayableResult.Ongoing;
        bool reporting = world.Phase == TurnPhase.Reporting;
        NamedCommandChoice[] bases = world.Cities.Values.Where(city => city.ForceId == player)
            .OrderBy(city => city.Id).Select(city => new NamedCommandChoice(city.Id, city.Name))
            .Concat(world.Gates.Values.Where(gate => gate.ForceId == player).OrderBy(gate => gate.Id)
                .Select(gate => new NamedCommandChoice(gate.Id, gate.Name))).ToArray();
        int defaultBase = world.Forces[player].RulerId is int rulerId &&
            world.Officers.TryGetValue(rulerId, out OfficerState? ruler) ? ruler.LocationId ?? -1 : -1;
        NamedCommandChoice[] units = world.Units.Values.Where(unit => unit.ForceId == player)
            .OrderBy(unit => unit.Id).Select(unit => new NamedCommandChoice(unit.Id,
                text.Format("play_unit_item", world.Officers[unit.OfficerId].Name, unit.Troops))).ToArray();
        string date = text.DateLine(world.Date.Year, world.Date.Month, world.Date.Day);
        string orders = world.Districts.Values.Where(district => district.ForceId == player && district.Number == 1)
            .Sum(district => district.OrdersRemaining ?? 0).ToString();
        string status = text.Format(reporting ? "play_reporting" : "play_active",
            world.Forces[player].Name, date, world.PeriodNumber) + "  " + text.Format("play_orders", orders);
        if (outcome == PlayableResult.Unified) status += "  " + text.Get("play_unified");
        if (outcome == PlayableResult.Defeated) status += "  " + text.Get("play_defeated");
        var roster = new CampaignCommandRoster(Array.AsReadOnly(bases), defaultBase,
            world.Cities.Keys.ToFrozenSet(), Array.AsReadOnly(units),
            text.Format("play_move_pending", world.PreparedOfficerMoves?.Count ?? 0),
            text.Format("play_talent_pending", (world.PreparedTalentOrders?.Count ?? 0) +
                (world.PersonnelCommands?.Talents?.Values.Count(task => task.ForceId == player && !task.IsRetiring) ?? 0)), status);
        return new(initializing, initializing && selectedForceId is not null, strategy, reporting, roster);
    }
}
