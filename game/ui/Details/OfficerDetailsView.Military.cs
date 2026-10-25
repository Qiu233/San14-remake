using Godot;
using San14.Data;

public partial class OfficerDetailsView
{
    private void AddMilitary(Control body, UiSourceLayout page)
    {
        // Each current tactic retains its own already-expanded effect lines.
        // Hover only changes this local display, without a query or RNG call.
        var effects = new Control { Name = "OfficerTacticEffects", Position = page.Rectangle(99).Position,
            Size = page.Rectangle(99).Size, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(effects);
        var effectLines = Enumerable.Range(0, 2).Select(line => page.Text(effects, 99, default, "", CurrentFont,
            rectangle: new Rect2(new Vector2(0, line * effects.Size.Y / 2), new Vector2(effects.Size.X, effects.Size.Y / 2)))).ToArray();
        foreach (var tactic in publication.Military.Tactics)
        {
            if (tactic.Slot is < 0 or >= 10 || tactic.Name is not { } name || tactic.TacticId is not int identity) continue;
            var rectangle = page.Rectangle(72 + tactic.Slot);
            var marker = new Control { Name = "OfficerTactic" + tactic.Slot, Position = rectangle.Position,
                Size = rectangle.Size, MouseFilter = MouseFilterEnum.Stop };
            body.AddChild(marker);
            var source = new UiSourceLayout(currentRepository, 0xD493EA98).ResizedSubtree(0, rectangle.Size, true);
            source.Art(marker, 2, source.Rectangle(2), CurrentAssets, CurrentShader, commonSpriteOverride: "000_cmn_mrk_base_yellow");
            source.Text(marker, 3, default, name, CurrentFont, MapTextPresets.Create()[0]);
            marker.SetMeta("tactic_id", identity);
            marker.MouseEntered += () =>
            {
                if (!DetailInputAdmitted()) return;
                IReadOnlyList<string> lines = tactic.UnresolvedOwner is null ? tactic.Effects : [currentText.Get("detail_unknown")];
                if (lines.Count > effectLines.Length) throw new InvalidDataException("Officer tactic has more than two source effect lines.");
                for (int line = 0; line < effectLines.Length; line++)
                    effectLines[line].SetKnownColourLine(line < lines.Count ? lines[line] : "");
            };
            marker.MouseExited += () =>
            { foreach (var line in effectLines) line.Text = ""; };
        }
        var names = ArmyMapUiSources.Create();
        for (int slot = 0; slot < publication.Military.Formations.Length && slot < 10; slot++)
        {
            var formation = publication.Military.Formations[slot];
            var rectangle = page.Rectangle(85 + slot);
            var marker = new Control { Name = "OfficerFormation" + formation.FormationId,
                Position = rectangle.Position, Size = rectangle.Size, MouseFilter = MouseFilterEnum.Stop };
            body.AddChild(marker);
            var source = new UiSourceLayout(currentRepository, 0x270E3D1B);
            if (formation.Available.Available)
                source.Art(marker, 2, source.Rectangle(2), CurrentAssets, CurrentShader, commonSpriteOverride:
                    formation.Available.Value == true ? "000_cmn_mrk_base_yellow" : "000_cmn_mrk_base_disable");
            int preset = formation.Available is { Available: true, Value: false } ? 6 : 0;
            source.Text(marker, 3, default, formation.Name, CurrentFont, MapTextPresets.Create()[preset]);
            int icon = formation.FormationId switch { 2 => 0x1A8, 3 => 0x1A9, 4 => 0x1AA,
                5 => 0x1AC, 6 => 0x1AD, 7 => 0x1AB, 8 => 0x1AE, 9 => 0x1AF, 10 => 0x1A3, 14 => 0x3EF, _ => 0x1A2 };
            var colours = MapTextPresets.Create()[preset];
            source.Art(marker, 4, source.Rectangle(4), CurrentAssets, CurrentShader,
                new MapCornerColors(colours.LT, colours.RT, colours.LB, colours.RB),
                commonSpriteOverride: names.IconName(new(icon, 0)), expansionCommonSprite: icon == 0x3EF);
            marker.SetMeta("formation_id", formation.FormationId);
            if (formation.Available.UnresolvedInput is { } missing)
            { marker.SetMeta("availability_unresolved", missing); AttachTooltip(marker, currentText.Get("detail_unknown")); }
        }
    }

    private void AddRelations(Control body, UiSourceLayout page)
    {
        var relations = publication.Relations;
        Add(24, relations.Father); Add(31, relations.Mother); Add(39, relations.Spouse);
        int[] nodes = [48, 54, 61];
        for (int index = 0; index < nodes.Length && index < relations.SwornBrothers.Length; index++) Add(nodes[index], relations.SwornBrothers[index]);
        void Add(int node, OfficerDetailRelatedPerson person)
        {
            if (person.Portrait is not { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: { } pack, SpriteName: { } name }) return;
            var rectangle = page.Rectangle(node);
            var portrait = new UiSourcePortrait { Name = "RelatedOfficer" + node, Position = rectangle.Position, Size = rectangle.Size };
            body.AddChild(portrait); body.MoveChild(portrait, 1);
            portrait.Configure(currentRepository, CurrentAssets, CurrentShader, DetailSprite(pack, name));
        }
    }
}
