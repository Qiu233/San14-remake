using Godot;
using San14.Data;

// Content is resolved by the page owner from the selected scenario record.
internal sealed record ScenarioSelectionButtonContent(string Year, string Month, string Title,
    LoadedSprite? EditionBadge, bool ScenarioTypeFlag, bool? CompletedMarkVisible);

/// <summary>The original scenario row, borrowing its page's artwork/font.
/// Preview focus and release activation are separate owner requests. The
/// current preview does not latch the ordinary push artwork.</summary>
internal partial class ScenarioSelectionButton : UiSourceButton
{
    private Action? previewChanged, activated;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private UiSourceText year = null!, month = null!, title = null!, unknownCompletion = null!;
    private Control editionBadge = null!, firstScenarioMarker = null!, secondScenarioMarker = null!, completedMark = null!;
    private UiSourceSprite? editionImage;

    // The direct source loader leaves the optional hit tester unbound, so
    // the complete root participates, including the completion mark at left.
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted &&
        point.X >= 0 && point.Y >= 0 && point.X <= Size.X && point.Y <= Size.Y;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, LoadedSprite firstKindArtwork, LoadedSprite secondKindArtwork,
        LoadedSprite completedArtwork, ScenarioSelectionButtonContent? content,
        Action previewChanged, Action activated)
    {
        this.previewChanged = previewChanged;
        this.activated = activated;
        this.assets = assets;
        this.shader = shader;
        var source = new UiSourceLayout(repository, 0xF2C9CA9A);
        // CScenarioSelectButton loads directly through 851330. The source
        // owner mounts this natural size, without the FT recursive resize.
        Size = source.Rectangle(0).Size;
        ActionMode = ActionModeEnum.Release;
        // The extra focus/push panes6/9/14 have separate animation tracks.
        // Their animation is deferred, as on the existing source buttons.
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        year = source.Text(this, 16, default, content?.Year ?? "", font); year.Name = "ScenarioYear";
        month = source.Text(this, 17, default, content?.Month ?? "", font); month.Name = "ScenarioMonth";
        title = source.Text(this, 18, default, content?.Title ?? "", font); title.Name = "ScenarioTitle";
        unknownCompletion = source.Text(this, 18, default, "?", font, rectangle: source.Rectangle(22));
        unknownCompletion.Name = "ScenarioCompletionUnknown";
        var tutorialCaption = source.Text(this, 23, default, "", font);
        tutorialCaption.Name = "UnusedTutorialCaption";
        UiSourceText[] captions = [year, month, title, tutorialCaption, unknownCompletion];
        int[] captionNodes = [16, 17, 18, 23, 18];
        var profiles = captionNodes.Select(node => MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)).ToArray();
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        {
            for (int i = 0; i < captions.Length; i++)
            {
                captions[i].Configure(font, profiles[i].WithPreset(presets[3 + state]));
                captions[i].QueueRedraw();
            }
        });
        Rect2 badgeRectangle = source.Rectangle(19);
        editionBadge = new Control { Name = "EditionBadge", Position = badgeRectangle.Position,
            Size = badgeRectangle.Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(editionBadge);
        editionBadge.SetMeta("source_node_index", 19);
        firstScenarioMarker = BoundArt(20, firstKindArtwork, "FirstScenarioKind");
        secondScenarioMarker = BoundArt(21, secondKindArtwork, "SecondScenarioKind");
        completedMark = BoundArt(22, completedArtwork, "CompletedScenario");
        ApplyContent(content);
        // Locked scenarios still update the preview when hovered.
        MouseEntered += () => { if (SourceInputAdmitted) this.previewChanged?.Invoke(); };
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.activated?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("completed_sprite", completedArtwork.Definition.Name);

        UiSourceSprite BoundArt(int index, LoadedSprite artwork, string name)
        {
            Rect2 rectangle = source.Rectangle(index);
            var image = new UiSourceSprite { Name = name, Position = rectangle.Position, Size = rectangle.Size };
            AddChild(image);
            image.Configure(shader, assets.SrgbSprite(artwork.Definition.Name, artwork), UiSourceSprite.Blend.Ordinary);
            image.SetMeta("source_node_index", index);
            image.SetMeta("source_layout", source.Source.Source.ResourceUri);
            image.SetMeta("caption_sprite", artwork.Definition.Name);
            return image;
        }
    }

    internal void ApplyContent(ScenarioSelectionButtonContent? content, bool? enabled = true)
    {
        // A missing row hides the persistent control; it is not a blank option.
        if (content is null) { Hide(); return; }
        Show();
        year.Text = content.Year;
        month.Text = content.Month;
        title.Text = enabled is null ? content.Title + " ?" : content.Title;
        if (content.EditionBadge is { } badge)
        {
            var texture = assets.SrgbSprite(badge.Definition.Name, badge);
            if (editionImage is null)
            {
                editionImage = new UiSourceSprite { Size = editionBadge.Size };
                editionBadge.AddChild(editionImage);
                editionImage.Configure(shader, texture, UiSourceSprite.Blend.Ordinary);
            }
            else editionImage.SetSource(texture);
            editionBadge.SetMeta("caption_sprite", badge.Definition.Name);
        }
        editionBadge.Visible = content.EditionBadge is not null;
        firstScenarioMarker.Visible = content.ScenarioTypeFlag;
        secondScenarioMarker.Visible = !content.ScenarioTypeFlag;
        completedMark.Visible = content.CompletedMarkVisible == true;
        unknownCompletion.Visible = content.CompletedMarkVisible is null;
        SetMeta("completion_known", content.CompletedMarkVisible.HasValue);
        SetMeta("availability_known", enabled.HasValue);
    }

    public override void _ExitTree() { previewChanged = null; activated = null; InputAdmission = null; }
}
