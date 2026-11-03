using Godot;
using San14.Data;

/// <summary>The dedicated ordinary formation balloon. It borrows the setup
/// page's application assets and font; only its local controls are retired.</summary>
internal partial class DeploymentFormationBalloonView : Control
{
    private UiSourceText name = null!, category = null!, range = null!, rangeCaption = null!;
    private Control landBackground = null!, waterBackground = null!, description = null!;
    private UiSourceLayout source = null!, parameter = null!;
    private MapFont font = null!;
    private PreviewText text = null!;
    private DeploymentFormationBalloon? current;
    private string? missing;
    private int? currentFormationId;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font, PreviewText text)
    {
        this.font = font; this.text = text;
        Name = "DeploymentFormationBalloon"; ZIndex = 20; MouseFilter = MouseFilterEnum.Ignore;
        source = assets.SourceLayout(0x291C8AEE); Size = source.Rectangle(0).Size;
        source.Blur(this, 1, source.Rectangle(1));
        // Nodes4 and7 are replaced child mounts. Their authored descendants
        // are placeholders and must not be drawn behind the real children.
        foreach (int node in new[] { 2, 8, 9 }) source.Art(this, node, source.Rectangle(node), assets, shader);
        name = source.Text(this, 3, default, "", font);
        var marker = assets.SourceLayout("cmn_mrker_s");
        var markerRoot = new Control { Name = "FormationCategory", Position = source.Rectangle(4).Position,
            Size = marker.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore }; AddChild(markerRoot);
        landBackground = marker.Art(markerRoot, 1, marker.Rectangle(1), assets, shader,
            commonSpriteOverride: "000_cmn_mrk_base_yellow");
        waterBackground = marker.Art(markerRoot, 1, marker.Rectangle(1), assets, shader,
            commonSpriteOverride: "000_cmn_mrk_base_cyan");
        category = marker.Text(markerRoot, 2, default, "", font);
        parameter = assets.SourceLayout(0xF725023A);
        var parameterRoot = new Control { Name = "FormationRange", Position = source.Rectangle(7).Position,
            Size = parameter.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore }; AddChild(parameterRoot);
        foreach (int node in new[] { 1, 2 }) parameter.Art(parameterRoot, node, parameter.Rectangle(node), assets, shader);
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data)
            .Messages.Sections[1].Records;
        rangeCaption = parameter.Text(parameterRoot, 3, default, messages[0x36A].Text!, font, MapTextPresets.Create()[13]);
        range = parameter.Text(parameterRoot, 4, default, "", font);
        description = new Control { Name = "FormationDescription", Position = source.Rectangle(10).Position,
            Size = source.Rectangle(10).Size, MouseFilter = MouseFilterEnum.Ignore }; AddChild(description);
        SetMeta("source_layout", source.Source.Source.ResourceUri); Hide();
    }

    internal static MapFontProfile RangeProfile(UiSourceLayout source, bool highlighted)
    {
        var presets = MapTextPresets.Create();
        var profile = MapFontProfile.FromNode(source.Nodes[4], source.Source.Source.ResourceUri)
            .WithPreset(presets[27]) with { WidthFitMode = 3 };
        if (!highlighted) return profile;
        // Source colour7 argument67 selects only preset66's four body
        // corners. Keep preset27's stroke, shadow and position unchanged.
        var body = presets[66];
        return profile with { ColourLT = body.LT.CpuPacked, ColourRT = body.RT.CpuPacked,
            ColourLB = body.LB.CpuPacked, ColourRB = body.RB.CpuPacked };
    }

    internal void Display(DeploymentLandFormationChoice choice,
        AreaAdministrationQuery<DeploymentFormationBalloon>? query, Vector2 pointer)
    {
        var value = query?.Value;
        string? issue = query?.UnresolvedInput ?? (value is null ? "deployment.formation_balloon.publication" : null);
        if (currentFormationId != choice.FormationId || !ReferenceEquals(current, value) || missing != issue)
        {
            current = value; missing = issue; currentFormationId = choice.FormationId;
            name.Text = value?.Name ?? choice.Name;
            category.Text = value?.CategoryCaption ?? "";
            if (value is not null) rangeCaption.Text = value.RangeCaption;
            landBackground.Visible = value is { RawKind: not 2 };
            waterBackground.Visible = value is { RawKind: 2 };
            range.Configure(font, RangeProfile(parameter, value?.HighlightRange == true));
            range.Text = value?.RangeValue ?? "";
            foreach (var child in description.GetChildren()) { description.RemoveChild(child); child.QueueFree(); }
            // The source loader explicitly clears node10's fitting/fixed-line
            // flag and character mode. Retain its literal wrapped paragraph,
            // rather than assigning the formatted-message3/2 profile.
            var profile = MapFontProfile.FromNode(source.Nodes[10], source.Source.Source.ResourceUri);
            profile = profile with { WidthFitMode = 0, RawCharacterMode = 0, RawFlags = profile.RawFlags & ~4u };
            string wrapped = DeploymentNarrationPlainParagraph.WrapContainedKnownColourParagraph(
                value?.Description ?? text.Get("detail_unknown"), line => font.Measure(line, profile).Width,
                (int)description.Size.X, allowPresetColours: true);
            var lines = DeploymentNarrationParagraphPlan.Runs(wrapped, allowPresetColours: true);
            float advance = font.Source.Record('\u3042', profile.Face).Height * profile.ScaleY + profile.VerticalSpace;
            var presets = MapTextPresets.Create();
            for (int index = 0; index < lines.Count; index++)
            {
                var line = new UiSourceText { Name = "FormationDescriptionLine" + index,
                    Position = new(0, index * advance), Size = new(description.Size.X, advance) };
                description.AddChild(line); line.Configure(font, profile);
                line.SetRuns(lines[index].Runs.Select(run => new UiSourceTextRun(run.Text,
                    run.SourcePresetIndex is int preset ? presets[preset].LT.CpuPacked : run.Colour switch
                    {
                        NarrationBodyColour.TextPreset38Body => presets[38].LT.CpuPacked,
                        NarrationBodyColour.TextPreset39Body => presets[39].LT.CpuPacked,
                        NarrationBodyColour.TextPreset40Body => presets[40].LT.CpuPacked,
                        _ => (uint?)null
                    })).ToArray());
            }
            SetMeta("formation_id", choice.FormationId);
            if (issue is not null) SetMeta("unresolved_owner", issue); else RemoveMeta("unresolved_owner");
        }
        Position = new(Math.Clamp(pointer.X + 20, 0, 1920 - Size.X), Math.Clamp(pointer.Y + 20, 0, 1080 - Size.Y));
        Show();
    }
}
