using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private NewGameForceOfficersContent BuildNewGameForceOfficers(CampaignComposition composition, GameState world, int forceId)
    {
        var source = composition.PrepareArmyQueries(world).CaptureNewGameForceOfficers(world, forceId);
        if (source.ForceValid != true) throw new InvalidDataException("The selected force's current officer list is unavailable.");
        return new(source, world.Forces[forceId].Name, row =>
        {
            if (row.DetailOfficerId is not int id || !world.Officers.ContainsKey(id))
                throw new InvalidDataException("The officer's current detail identity is unavailable.");
            var detail = CampaignDetailViews.Officer(world, composition.Snapshot, id, _ui);
            return detail with { Content = detail.Content with { PortraitAppearanceCode = row.PortraitAppearance } };
        });
    }
}
