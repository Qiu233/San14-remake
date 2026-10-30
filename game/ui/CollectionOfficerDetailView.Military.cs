using Godot;
using San14.Data;

internal partial class CollectionOfficerDetailView
{
    private void AddMilitary(Control body, UiSourceLayout page, CollectionOfficerDetailPage publication)
    {
        foreach (var tactic in publication.Tactics)
        {
            var rectangle = page.Rectangle(tactic.NodeIndex);
            var marker = new Control { Name = "CollectionOfficerTactic" + tactic.Slot,
                Position = rectangle.Position, Size = rectangle.Size, MouseFilter = MouseFilterEnum.Stop };
            body.AddChild(marker);
            var source = new UiSourceLayout(repository, 0xD493EA98).ResizedSubtree(0, rectangle.Size, true);
            source.Art(marker, 2, source.Rectangle(2), selector.SourceAssets, selector.SourceShader,
                commonSpriteOverride: "000_cmn_mrk_base_yellow");
            source.Text(marker, 3, default, tactic.Name, selector.SourceFont, MapTextPresets.Create()[0]);
            marker.SetMeta("tactic_id", tactic.TacticId);
            //The inherited controller mounts11_00_01_offset_05 independently
            //of the person body. Node99 remains hidden by the gallery source.
            marker.MouseEntered += () =>
            {
                if (!ReadAdmission()) return;
                tooltip?.DisplayText(null);
                tacticBalloon?.Display(tactic.Balloon ??
                    throw new InvalidDataException("collection.officer.tactic_balloon_publication"));
            };
            marker.MouseExited += () => tacticBalloon?.Display(null);
        }
        var names = ArmyMapUiSources.Create();
        foreach (var formation in publication.Formations)
        {
            var rectangle = page.Rectangle(formation.NodeIndex);
            var marker = new Control { Name = "CollectionOfficerFormation" + formation.FormationId,
                Position = rectangle.Position, Size = rectangle.Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(marker);
            var source = new UiSourceLayout(repository, 0x270E3D1B);
            source.Art(marker, 2, source.Rectangle(2), selector.SourceAssets, selector.SourceShader,
                commonSpriteOverride: formation.Available ? "000_cmn_mrk_base_yellow" : "000_cmn_mrk_base_disable");
            int preset = formation.Available ? 0 : 6;
            var colours = MapTextPresets.Create()[preset];
            source.Text(marker, 3, default, formation.Name, selector.SourceFont, colours);
            int icon = formation.FormationId switch { 2 => 0x1A8, 3 => 0x1A9, 4 => 0x1AA,
                5 => 0x1AC, 6 => 0x1AD, 7 => 0x1AB, 8 => 0x1AE, 9 => 0x1AF, 10 => 0x1A3, 14 => 0x3EF, _ => 0x1A2 };
            source.Art(marker, 4, source.Rectangle(4), selector.SourceAssets, selector.SourceShader,
                new MapCornerColors(colours.LT, colours.RT, colours.LB, colours.RB),
                commonSpriteOverride: names.IconName(new(icon, 0)), expansionCommonSprite: icon == 0x3EF);
            marker.SetMeta("formation_id", formation.FormationId);
        }
    }
}
