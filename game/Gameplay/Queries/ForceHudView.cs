using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

internal sealed record ForceHudView(int ForceId, int PeriodNumber, string Date,
    string RulerName, string Orders, string DoctrineName, string Gold, string Supplies,
    string Troops, string Cities, string Officers, string Districts,
    bool CanAdvance, ForceSummary Figures)
{
    public string Year { get; init; } = "";
    public string Month { get; init; } = "";
    public string Period { get; init; } = "";
    public int? OrderCount { get; init; }
    public string OrderCountText { get; init; } = "";
    public string DoctrineLevelText { get; init; } = "";
    public ForceDoctrineProgress? DoctrineProgress { get; init; }
    public int? ExperienceTotal { get; init; }
    public ForceExperienceHeaderChange? PendingExperience { get; init; }
    public MapHeaderIdentity Identity { get; init; } = null!;
    public MapHeaderCommandBooks CommandBooks { get; init; } = null!;
    public string UnknownText { get; init; } = "";

    public static ForceHudView Create(GameState world, int forceId, MapHeaderProjection header, PreviewText text)
    {
        // The resident header reads World's current third date. PeriodDate
        // remains the strategy period's authority and cannot fill a missing
        // independent calendar or substitute for a day within that period.
        NativeMapSeasonDate? date = world.NativeMapSeason?.ThirdDate;
        string unknown = text.Get("detail_unknown");
        var identity = header.Identity;
        string rulerName = identity.RulerOfficerId is int rulerId ? world.Officers.GetValueOrDefault(rulerId)?.Name ?? text.Get("detail_unknown") : text.Get("detail_unknown");
        ForceSummary figures = ForceSummary.Capture(world, forceId);
        int? orders = header.CommandBooks.Count;
        string count = orders is int known ? text.Format("map_hud_order_count", known) : text.Get("detail_unknown");
        ForceDoctrineView? doctrine = identity.Update == MapHeaderUpdate.Updated
            ? ForceDoctrineView.Create(world, identity.ForceId!.Value) : null;
        return new(forceId, world.PeriodNumber,
            date is null ? unknown : text.Format("play_map_date", date.Year, date.Month, date.Day),
            rulerName, text.Format("play_map_orders", orders is int number ? number : text.Get("detail_unknown")), doctrine?.Name ?? text.Get("detail_unknown"),
            text.Format("play_map_force_gold", figures.Gold), text.Format("play_map_force_supplies", figures.Supplies),
            text.Format("play_map_force_troops", figures.Troops), text.Format("play_map_force_cities", figures.Cities),
            text.Format("play_map_force_officers", figures.Officers), text.Format("play_map_force_districts", figures.Districts),
            world.Phase == TurnPhase.Strategy && PlayableOutcome.Evaluate(world, forceId) == PlayableResult.Ongoing,
            figures)
        {
            Identity = identity,
            CommandBooks = header.CommandBooks,
            DoctrineProgress = doctrine?.Progress,
            ExperienceTotal = OwnedForce164Inputs.ReadCurrent(world, forceId),
            PendingExperience = ForceExperienceHeaderChange.Create(world, forceId),
            UnknownText = text.Get("detail_unknown"),
            Year = date is null ? unknown : text.Format("map_hud_year", date.Year),
            Month = date is null ? unknown : text.Format("map_hud_month", date.Month),
            Period = date is null ? unknown : text.Get(date.Day < 11 ? "map_hud_early_period" :
                date.Day < 21 ? "map_hud_middle_period" : "map_hud_late_period"),
            OrderCount = orders,
            OrderCountText = count,
            DoctrineLevelText = text.Format("map_hud_doctrine_level",
                doctrine?.Level is int level ? level : text.Get("detail_unknown"))
        };
    }
}
