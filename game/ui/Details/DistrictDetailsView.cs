using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary District page consumes one retained current publication.
/// Its renderer never recalculates forecasts or selects an officer.</summary>
public partial class DistrictDetailsView : DetailsView
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Control? currentBody;
    private UiSourceDetailSelector selector = null!;

    internal int CurrentDistrictId { get; private set; }
    internal InformationDetailSelectionReturn ReadSelectionReturn() => selector.ReadReturn(CurrentDistrictId);

    internal void Configure(GameResourceRepository repository, DistrictInformationPage page,
        PreviewText text, Action close, Action<InformationListKind>? related = null, InformationDetailSelectionContext? selection = null, Action<int>? select = null)
    {
        this.repository = repository; this.text = text;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        ConfigureCommonBackdrop(repository);
        var header = new UiSourceHeader { Name = "DistrictDetailsTitle" };
        ConfigureSourcePage("DistrictDetails", header, close);
        canvas = new() { Name = "DistrictSourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(canvas, 3, shell.Rectangle(3, new(191, 147)), assets, shader);
        canvas.AddChild(header); header.Configure(repository, assets, shader, font, Message(DistrictInformationSources.TitleId));
        var tab = new UiSourceTabButton { Name = "BasicTab", Position = shell.Rectangle(4, new(191, 147)).Position,
            Size = new(Message(DistrictInformationSources.BasicTabCaptionId).Length * 28 + 36, 64), Selected = true };
        canvas.AddChild(tab); tab.Configure(repository, assets, shader, font, Message(DistrictInformationSources.BasicTabCaptionId));
        selector = new(); canvas.AddChild(selector);
        selector.Configure(repository, assets, shader, font, text,
            selection ?? new([new(page.DistrictId, page.Name, null)], () => 0),
            InformationListKind.Districts, page.DistrictId, () => IsVisibleInTree() && !IsQueuedForDeletion(), select);

        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CloseDistrictDetails", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = () => IsVisibleInTree() && !IsQueuedForDeletion() };
        canvas.AddChild(back); back.ConfigureScenarioReturn(repository, assets, shader, text.SourceLocale, RequestClose);
        Apply(page);
        RelatedButtons(related);
        Resized += Fit; Fit();
    }

    internal void Apply(DistrictInformationPage page)
    {
        int bodyIndex = currentBody?.GetIndex() ?? -1;
        if (currentBody is { } previous) { canvas.RemoveChild(previous); previous.QueueFree(); currentBody = null; }
        BuildBody(page);
        if (bodyIndex >= 0) canvas.MoveChild(currentBody!, bodyIndex);
        selector.Select(page.DistrictId);
        SetMeta("district_id", page.DistrictId); SetMeta("source_unknown", page.UnresolvedOwner ?? "");
        CurrentDistrictId = page.DistrictId;
    }

    private void BuildBody(DistrictInformationPage page)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, page.LayoutName));
        var body = new Control { Name = "CurrentDistrictDetails", Position = new(191, 197), Size = source.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(body); currentBody = body;
        var presets = MapTextPresets.Create();
        var sprites = new LocalizedUiSpriteSource(repository);
        if (!Hidden(2) && page.StandingPortrait is { } standing)
        {
            var portrait = new UiSourceStandingPortrait { Name = "DistrictLeaderStanding", Position = source.Rectangle(2).Position };
            body.AddChild(portrait); portrait.Configure(repository, assets, shader, standing.LoadSprites(sprites.LoadTexturePackSprite));
            portrait.Scale = source.Rectangle(2).Size / portrait.Size;
        }
        foreach (var node in source.Nodes)
        {
            if (Hidden(node.Index) || Replaced(node.Index)) continue;
            if (node.Type is 1 or 3)
                source.Art(body, node.Index, source.Rectangle(node.Index), assets, shader,
                    page.TextColors.TryGetValue(node.Index, out var colours) ? colours : null);
            else if (node.Type == 6) source.Blur(body, node.Index, source.Rectangle(node.Index));
        }
        var displayText = new Dictionary<int, string>(page.Text);
        foreach (var (node, _) in page.UnresolvedFields)
            if ((uint)node < (uint)source.Nodes.Count && source.Nodes[node].Type == 2)
                displayText.TryAdd(node, text.Get("detail_unknown"));
        foreach (var (node, value) in displayText)
        {
            if (Hidden(node) || Replaced(node)) continue;
            MapTextPreset? preset = page.TextStyles.TryGetValue(node, out int style) ? presets[style] : null;
            var label = source.Text(body, node, default, value, font, preset);
            var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri);
            if (preset is not null) profile = profile.WithPreset(preset);
            if (page.TextColors.TryGetValue(node, out var colours))
                profile = profile with { ColourLT = colours.LT.CpuPacked, ColourRT = colours.RT.CpuPacked,
                    ColourLB = colours.LB.CpuPacked, ColourRB = colours.RB.CpuPacked };
            label.Configure(font, profile); label.SetKnownColourLine(value);
            if (page.UnresolvedFields.TryGetValue(node, out var missing)) label.SetMeta("unresolved_owner", missing);
        }
        if (!Hidden(4)) Badge();
        if (!Hidden(93))
        {
            var miniMap = new UiSourceMiniMap { Name = "DistrictMiniMap", Position = source.Rectangle(93).Position };
            body.AddChild(miniMap); miniMap.Configure(repository, assets, shader, page.MiniMap);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        bool Hidden(int index)
        {
            for (int? cursor = index; cursor is int current; cursor = source.Nodes[current].ParentIndex)
                if (page.HiddenNodes.Contains(current)) return true;
            return false;
        }
        bool Replaced(int index)
        {
            for (int? cursor = index; cursor is int current; cursor = source.Nodes[current].ParentIndex)
                if (current is 2 or 4 or 93) return true;
            return false;
        }
        void Badge()
        {
            var badge = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, DistrictInformationSources.NameplateLayoutName));
            var mount = new Control { Name = "DistrictNameplate", Position = source.Rectangle(4).Position,
                Size = badge.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(mount);
            foreach (var node in badge.Nodes)
                if (node.Type is 1 or 3 && !FlagSubtree(node.Index))
                    badge.Art(mount, node.Index, badge.Rectangle(node.Index), assets, shader);
            var name = badge.Text(mount, 7, default, page.Name, font);
            if (page.NameIconSpriteId is int spriteId)
            {
                string spriteName = ArmyMapUiSources.Create().IconName(new(spriteId, 0));
                var icon = new UiSourceSprite { Name = "DistrictNameInlineIcon" };
                mount.AddChild(icon); icon.Configure(shader, assets.SrgbSprite(spriteName), UiSourceSprite.Blend.Ordinary);
                icon.SetMeta("source_sprite_id", spriteId); icon.SetMeta("source_sprite", spriteName);
                UiSourceInlineIconLayout.Apply(name, icon, new Rect2(name.Position, name.Size), font,
                    MapFontProfile.FromNode(badge.Nodes[7], badge.Source.Source.ResourceUri),
                    assets.Sprite(spriteName).Texture.Region.Size);
            }
            if (page.ForceFlag is { } appearance)
            {
                var flag = new UiSourceForceFlag { Name = "DistrictForceFlag", Position = badge.Rectangle(4).Position };
                mount.AddChild(flag); flag.Configure(repository, assets, shader, font, appearance);
            }
            if (page.UnresolvedFields.TryGetValue(4, out var missing)) mount.SetMeta("unresolved_owner", missing);
            bool FlagSubtree(int index)
            {
                for (int? cursor = index; cursor is int current; cursor = badge.Nodes[current].ParentIndex)
                    if (current == 4) return true;
                return false;
            }
        }
    }

    private void RelatedButtons(Action<InformationListKind>? related)
    {
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 position = footer.Rectangle(21, new(0, 904)).Position + new Vector2(45, -103);
        (InformationListKind Kind, int Caption, string Name)[] actions =
        [
            (InformationListKind.Cities, 0x207, "DistrictCities"),
            (InformationListKind.Gates, 0x41D, "DistrictGates"),
            (InformationListKind.Officers, 0x211, "DistrictOfficers"),
            (InformationListKind.Armies, 0x224, "DistrictArmies")
        ];
        foreach (var action in actions)
        {
            string caption = Message(action.Caption);
            var button = new UiSourceCommandButton { Name = action.Name, Position = position,
                Size = new(Math.Max(96, caption.Length * 28 + 20), 48), Disabled = related is null,
                InputAdmission = () => IsVisibleInTree() && !IsQueuedForDeletion() };
            canvas.AddChild(button);
            button.Configure(repository, assets, shader, font, caption, () => related?.Invoke(action.Kind));
            position.X += button.Size.X;
        }
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing district detail Data1/{id:X}.");
    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        font?.Dispose(); shader?.Dispose(); assets?.Dispose(); base._ExitTree();
    }
}
