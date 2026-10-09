using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

internal sealed record ForceHudView(int ForceId, int PeriodNumber, string Date,
    string ForceName, string Orders, string DoctrineName, string Gold, string Supplies,
    string Troops, string Cities, string Officers, string Districts,
    bool CanAdvance, int? PortraitAppearanceCode, ForceSummary Figures)
{
    public static ForceHudView Create(GameState world, int forceId, ScenarioCityPreview? snapshot, PreviewText text)
    {
        ForceState force = world.Forces[forceId];
        ForceSummary figures = ForceSummary.Capture(world, forceId);
        int orders = world.Districts.Values.Where(district => district.ForceId == forceId &&
            district.Number == 1).Sum(district => district.OrdersRemaining ?? 0);
        string doctrineName = force.DoctrineId is int doctrineId && world.Doctrines.TryGetValue(doctrineId,
            out DoctrineDefinition? doctrine) ? doctrine.Name : "";
        int? portrait = snapshot?.Characters.FirstOrDefault(person =>
            person.CharacterId != 0 && person.CharacterId == force.RulerId)?.AppearanceCode;
        return new(forceId, world.PeriodNumber,
            text.Format("play_map_date", world.Date.Year, world.Date.Month, world.Date.Day),
            force.Name, text.Format("play_map_orders", orders), doctrineName,
            text.Format("play_map_force_gold", figures.Gold), text.Format("play_map_force_supplies", figures.Supplies),
            text.Format("play_map_force_troops", figures.Troops), text.Format("play_map_force_cities", figures.Cities),
            text.Format("play_map_force_officers", figures.Officers), text.Format("play_map_force_districts", figures.Districts),
            world.Phase == TurnPhase.Strategy && PlayableOutcome.Evaluate(world, forceId) == PlayableResult.Ongoing,
            portrait, figures);
    }
}
