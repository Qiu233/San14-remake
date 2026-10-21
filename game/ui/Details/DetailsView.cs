using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>
/// Shared rendering for inspected original detail layouts. Profiles supply screen
/// composition and bindings; simulation state and binary resource readers stay separate.
/// Fonts, runtime styling and the common shell composition remain under comparison.
/// </summary>
public abstract partial class DetailsView : Control
{
    private Control _canvas = null!;
    private UiAssets? _assets;
    private TextureButton? _returnButton;
    private Action? _close;
    private MapFont? _sourceFont;
    private Shader? _sourceShader;
    private readonly List<string> _unbound = [];
    private UiAssets Assets => _assets ??
        throw new InvalidOperationException("Detail assets have not been initialized.");

    private protected void ConfigureLayout(GameResourceRepository repository, string profilePath,
        string viewName, string titleKey, PreviewText text, IReadOnlyDictionary<string, string> values,
        int? portraitAppearanceCode, string selectionCaption, Action close, bool mapBackdrop = false,
        IReadOnlyDictionary<string, int>? valueTextPresets = null, string? selectionIcon = null,
        DetailForceAppearance? forceAppearance = null, IReadOnlyList<DetailForceAppearance?>? neighborFlags = null,
        DetailMiniMapValues? miniMap = null)
    {
        Name = viewName;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 3;
        using var profileFile = Godot.FileAccess.Open(profilePath, Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidDataException($"Detail binding profile is missing: {profilePath}.");
        using JsonDocument profile = JsonDocument.Parse(profileFile.GetAsText());
        JsonElement settings = profile.RootElement;
        uint layoutId = uint.Parse(settings.GetProperty("layoutId").GetString()!, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
        LoadedLayout source = repository.LoadLayout("ScreenLayout", layoutId);
        bool sourceTypography = settings.TryGetProperty("sourceTypography", out var typography) && typography.GetBoolean();
        var textPresets = MapTextPresets.Create();
        UiSourceLayout? sourceLayout = sourceTypography ? new(source) : null;
        IReadOnlyList<LcskNodeRecord> nodes = sourceLayout?.Nodes ?? LcskNodeReader.Read(source.Layout);
        _assets = new UiAssets(repository);
        if (sourceTypography)
        {
            _sourceFont = new(repository, text.SourceLocale);
            _sourceShader = UiSourceSprite.CreateShader();
        }
        _canvas = new Control { Size = new Vector2(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        _canvas.Theme = new Theme
        {
            DefaultFont = new SystemFont { FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"] }
        };
        AddChild(_canvas);
        if (mapBackdrop)
        {
            _canvas.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
            _canvas.AddChild(new MapDialogBackdrop { Size = new(1920, 1080) });
        }
        else AddColor(new Rect2(0, 0, 1920, 1080), new Color("#50432f"));
        // City/area use their proven common list frame. Other profiles retain
        // their existing composition until their own callers are established.
        JsonElement frameSettings = settings.GetProperty("frame");
        uint frameId = uint.Parse(frameSettings.GetProperty("layoutId").GetString()!,
            NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        LoadedLayout frame = repository.LoadLayout("ScreenLayout", frameId);
        LcskNodeRecord frameNode = LcskNodeReader.Read(frame.Layout)
            .Single(node => node.Name == frameSettings.GetProperty("node").GetString());
        var frameRectangle = new Rect2(
            frameSettings.GetProperty("origin")[0].GetSingle(),
            frameSettings.GetProperty("origin")[1].GetSingle(), frameNode.Value44, frameNode.Value46);
        if (sourceTypography)
        {
            new UiSourceLayout(frame).Art(_canvas, frameNode.Index, frameRectangle, Assets, _sourceShader!);
            var title = new UiSourceLayout(repository, 0xDC8AC8AA);
            title.Art(_canvas, 1, title.Rectangle(1), Assets, _sourceShader!);
            title.Text(_canvas, 2, default, text.Get(titleKey), _sourceFont!, textPresets[56]);
        }
        else
        {
            DrawSprite(frame.Layout, frameNode, frameRectangle, Vector2.One);
            AddPicture(Assets.Sprite("000_cmn_base_title").Texture, new Rect2(0, 0, 540, 88));
            AddLabel(text.Get(titleKey), new Rect2(120, 25, 330, 50), 32,
                HorizontalAlignment.Left, new Color("#eee3a6"));
        }

        HashSet<string> hiddenNames = settings.GetProperty("hiddenNodes").EnumerateArray()
            .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        if (sourceTypography) hiddenNames.Add(nodes[4].Name); // replaced by the caller's actual nameplate sublayout
        foreach (JsonElement missing in settings.GetProperty("unsupportedNodes").EnumerateArray())
        {
            string name = missing.GetString()!;
            hiddenNames.Add(name);
            bool handledFlag = sourceTypography && (name == "cmn_flg_force" ||
                Enumerable.Range(0, 8).Any(i => name == $"cmn_flg_force_{i:0000}"));
            bool handledMiniMap = sourceTypography && name == settings.GetProperty("miniMapNode").GetString();
            if (!handledFlag && !handledMiniMap) _unbound.Add(name);
        }
        string portraitNode = settings.GetProperty("portraitNode").GetString()!;
        JsonElement bindings = settings.GetProperty("texts");
        var sourceTextPresets = new Dictionary<int, MapTextPreset>();
        if (settings.TryGetProperty("sourceTextPresets", out JsonElement presetBindings))
        {
            foreach (JsonProperty presetBinding in presetBindings.EnumerateObject())
            {
                int presetIndex = int.Parse(presetBinding.Name, CultureInfo.InvariantCulture);
                foreach (JsonElement entry in presetBinding.Value.EnumerateArray())
                {
                    int index = entry.GetInt32();
                    if (index < 0 || index >= nodes.Count || nodes[index].Type != 2)
                        throw new InvalidDataException($"Detail text preset has no source text node {index}.");
                    sourceTextPresets.Add(index, textPresets[presetIndex]);
                }
            }
        }
        JsonElement progressBars = settings.TryGetProperty("progressBars", out JsonElement configuredBars)
            ? configuredBars : default;
        var knownNames = nodes.Select(node => node.Name).ToHashSet(StringComparer.Ordinal);
        var partialTextStyles = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        string? partialStyleNoticeKey = null;
        if (settings.TryGetProperty("partialTextStyles", out JsonElement partialStyles))
        {
            uint scopedLayout = uint.Parse(partialStyles.GetProperty("layoutId").GetString()!,
                NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (scopedLayout != layoutId)
                throw new InvalidDataException("Partial text style profile belongs to a different source layout.");
            partialStyleNoticeKey = partialStyles.GetProperty("noticeKey").GetString()!;
            foreach (JsonProperty entry in partialStyles.GetProperty("nodes").EnumerateObject())
            {
                if (!nodes.Any(node => node.Name == entry.Name && node.Type == 2))
                    throw new InvalidDataException($"Partial text style profile has no source text node {entry.Name}.");
                partialTextStyles.Add(entry.Name, entry.Value);
            }
        }
        var partiallyRenderedText = new List<string>();
        foreach (JsonProperty binding in bindings.EnumerateObject())
            if (!knownNames.Contains(binding.Name))
                throw new InvalidDataException($"Layout no longer contains bound node {binding.Name}.");
        if (progressBars.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty bar in progressBars.EnumerateObject())
                if (!nodes.Any(node => node.Name == bar.Name && node.Type == 0))
                    throw new InvalidDataException($"Layout no longer contains progress bar node {bar.Name}.");
        foreach (string requiredName in hiddenNames.Append(portraitNode))
            if (!knownNames.Contains(requiredName))
                throw new InvalidDataException($"Layout no longer contains configured node {requiredName}.");
        Vector2 origin = new(settings.GetProperty("origin")[0].GetSingle(),
            settings.GetProperty("origin")[1].GetSingle());
        var positions = new Vector2[nodes.Count];
        var scales = new Vector2[nodes.Count];
        var hidden = new bool[nodes.Count];
        foreach (LcskNodeRecord node in nodes)
        {
            IReadOnlyList<float> fields = node.HeaderValues;
            if (Math.Abs(fields[2]) > 0.00001f || Math.Abs(fields[3]) > 0.00001f ||
                Math.Abs(fields[4]) > 0.00001f || Math.Abs(fields[5]) > 0.00001f ||
                Math.Abs(fields[6] - 1) > 0.00001f || fields[7] <= 0 || fields[8] <= 0)
                throw new InvalidDataException($"Detail renderer has an unsupported node transform: {node.Name}.");
            Vector2 parentPosition = node.ParentIndex is int parent ? positions[parent] : origin;
            Vector2 parentScale = node.ParentIndex is int scaleParent ? scales[scaleParent] : Vector2.One;
            positions[node.Index] = parentPosition + new Vector2(fields[0], -fields[1]) * parentScale;
            scales[node.Index] = parentScale * new Vector2(fields[7], fields[8]);
            hidden[node.Index] = hiddenNames.Contains(node.Name) ||
                (node.ParentIndex is int hiddenParent && hidden[hiddenParent]);
            if (hidden[node.Index]) continue;
            Vector2 size = new Vector2(node.Value44, node.Value46) * scales[node.Index];
            Vector2 position = positions[node.Index];
            if (node.Value48 == 0x1111) position -= size / 2;
            else if (node.Value48 != 0x1100)
                throw new InvalidDataException($"Unsupported detail anchor {node.Value48:X4}: {node.Name}.");
            Rect2 rectangle = new(position, size);
            if (node.Name == portraitNode)
            {
                if (portraitAppearanceCode is int code)
                {
                    var portrait = new PortraitView { Name = node.Name };
                    _canvas.AddChild(portrait);
                    portrait.Configure(repository, Assets, code, rectangle);
                }
                continue;
            }
            if (node.Type is 1 or 3)
            {
                if (sourceTypography) sourceLayout!.Art(_canvas, node.Index, rectangle, Assets, _sourceShader!);
                else DrawSprite(source.Layout, node, rectangle, scales[node.Index]);
            }
            else if (node.Type == 2)
            {
                string value;
                string? valueKey = null;
                bool caption = false;
                if (bindings.TryGetProperty(node.Name, out JsonElement binding))
                {
                    caption = binding.TryGetProperty("key", out JsonElement key);
                    valueKey = caption ? null : binding.GetProperty("value").GetString()!;
                    value = caption ? text.Get(key.GetString()!) : values[valueKey!];
                }
                else
                {
                    _unbound.Add(node.Name);
                    value = text.Get("detail_unknown");
                }
                if (sourceTypography)
                {
                    sourceTextPresets.TryGetValue(node.Index, out MapTextPreset? preset);
                    if (valueKey is not null && valueTextPresets?.TryGetValue(valueKey, out int currentPreset) == true)
                        preset = textPresets[currentPreset];
                    var sourceLabel = sourceLayout!.Text(_canvas, node.Index, default, value, _sourceFont!, preset, rectangle);
                    if (valueKey == "force" && forceAppearance?.DisplayName is { Length: > 0 })
                        sourceLabel.FirstGlyphColour = forceAppearance.Fill.CpuPacked;
                    continue;
                }
                // The full source pair is not a proven alignment enum. Existing profiles
                // accept only their inspected zero-high-byte cases. A separate scoped
                // profile permits partial rendering while retaining every unknown byte.
                ushort rawPair150 = BinaryPrimitives.ReadUInt16LittleEndian(node.RawBytes.AsSpan(150, 2));
                int placementCode = rawPair150;
                bool partialStyle = partialTextStyles.TryGetValue(node.Name, out JsonElement style);
                if (partialStyle)
                {
                    if (node.RawBytes[150] != style.GetProperty("rawByte150").GetByte() ||
                        node.RawBytes[151] != style.GetProperty("rawByte151").GetByte() ||
                        node.RawBytes[153] != style.GetProperty("rawByte153").GetByte())
                        throw new InvalidDataException($"Source text style differs from the inspected partial profile: {node.Name}.");
                    placementCode = node.RawBytes[150];
                    partiallyRenderedText.Add(node.Name);
                    _unbound.Add($"{node.Name} [additional text style]");
                }
                HorizontalAlignment align = placementCode switch
                {
                    0x00 or 0x01 => HorizontalAlignment.Left,
                    0x11 => HorizontalAlignment.Center,
                    0x21 => HorizontalAlignment.Right,
                    _ => throw new InvalidDataException($"Unsupported detail text placement code {rawPair150:X4}: {node.Name}.")
                };
                float fontScale = BinaryPrimitives.ReadSingleLittleEndian(node.RawBytes.AsSpan(140, 4));
                if (!float.IsFinite(fontScale) || fontScale <= 0 || fontScale > 8)
                    throw new InvalidDataException($"Unsupported detail text size: {node.Name}.");
                Label label = AddLabel(value, rectangle, (int)Math.Round(32 * fontScale * scales[node.Index].Y),
                    align, caption ? new Color("#e9e6b3") : new Color("#dddddd"));
                label.Name = node.Name;
                if (partialStyle)
                {
                    label.SetMeta("source_node_index", node.Index);
                    label.SetMeta("source_raw_record", node.RawBytes);
                    label.SetMeta("source_raw_style", node.RawBytes.AsSpan(136).ToArray());
                    label.SetMeta("source_raw_byte150", node.RawBytes[150]);
                    label.SetMeta("source_raw_byte151", node.RawBytes[151]);
                    label.SetMeta("source_raw_byte153", node.RawBytes[153]);
                    label.SetMeta("source_requested_rect", rectangle);
                    label.SetMeta("source_font_scale", fontScale);
                    label.SetMeta("additional_text_style_decoded", false);
                }
                if (!bindings.TryGetProperty(node.Name, out _)) label.TooltipText = text.Get("detail_pending_field");
            }
            else if (node.Type == 0 && progressBars.ValueKind == JsonValueKind.Object &&
                     progressBars.TryGetProperty(node.Name, out JsonElement progressBar))
                DrawProgressBar(node.Name, rectangle, progressBar, values);
            else if (node.Type != 0)
                throw new InvalidDataException($"Unsupported detail node type {node.Type}: {node.Name}.");
        }
        if (sourceTypography)
        {
            string miniMapName = settings.GetProperty("miniMapNode").GetString()!;
            int miniMapIndex = nodes.Single(n => n.Name == miniMapName).Index;
            Vector2 miniMapMount = sourceLayout!.Rectangle(miniMapIndex).Position;
            var map = new UiSourceMiniMap { Name = "DetailMiniMap",
                Position = origin + new Vector2((int)miniMapMount.X, (int)miniMapMount.Y) };
            _canvas.AddChild(map);
            map.Configure(repository, Assets, _sourceShader!, miniMap);
            var nameplateSource = new UiSourceLayout(repository, 0x3858CC07);
            Vector2 local = sourceLayout!.Rectangle(4).Position;
            var nameplate = new Control { Name = "DetailNameplate", Position = origin + new Vector2((int)local.X, (int)local.Y),
                Size = new(380, 94), MouseFilter = MouseFilterEnum.Ignore };
            _canvas.AddChild(nameplate);
            foreach (int index in new[] { 1, 2, 3 })
                nameplateSource.Art(nameplate, index, nameplateSource.Rectangle(index), Assets, _sourceShader!);
            nameplateSource.Text(nameplate, 7, default, selectionCaption, _sourceFont!, textPresets[0]);
            if (forceAppearance is not null)
            {
                bool embedded = neighborFlags is not null;
                Vector2 flagMount = nameplateSource.Rectangle(4).Position;
                if (!embedded) flagMount = new((int)flagMount.X, (int)flagMount.Y);
                var flag = new UiSourceForceFlag { Name = "NameplateForceFlag", Position = flagMount };
                nameplate.AddChild(flag);
                flag.Configure(repository, Assets, _sourceShader!, _sourceFont!, forceAppearance, embedded);
            }
            if (neighborFlags is not null)
            {
                for (int i = 0; i < neighborFlags.Count; i++)
                {
                    if (neighborFlags[i] is not { } appearance) continue;
                    var flag = new UiSourceForceFlag { Name = "NeighborForceFlag" + i,
                        Position = origin + new Vector2(i < 4 ? 677 : 957, 490 + i % 4 * 40) };
                    _canvas.AddChild(flag);
                    flag.Configure(repository, Assets, _sourceShader!, _sourceFont!, appearance, neighbor: true);
                }
            }
        }
        JsonElement selection = settings.GetProperty("selectionLabel");
        if (settings.TryGetProperty("selectionHeaderKey", out JsonElement selectionHeaderKey))
        {
            // Common shell positions observed in the city/area reference. These are
            // captions for the current context; navigation requires a separate command.
            if (sourceTypography)
            {
                var shell = new UiSourceLayout(frame);
                Vector2 mount = new((1920 - shell.Nodes[0].Value44) / 2, 514 - shell.Nodes[0].Value46 / 2);
                string caption = text.Get("detail_basic");
                // The original counts UTF-16 units, rather than glyph pixels.
                var tab = new UiSourceTabButton { Name = "BasicTab", Position = shell.Rectangle(4, mount).Position,
                    Size = new(caption.Length * 28 + 36, 64), Selected = true };
                _canvas.AddChild(tab);
                tab.Configure(repository, Assets, _sourceShader!, _sourceFont!, caption);
            }
            else
            {
                AddShellPanel("BasicTab", "000_cmn_btn_tab__2", new Rect2(233, 154, 84, 52));
                AddLabel(text.Get("detail_basic"), new Rect2(233, 160, 84, 40), 28,
                    HorizontalAlignment.Center, new Color("#f4e59d")).Name = "BasicCaption";
            }
            Rect2 chosen = new(selection[0].GetSingle(), selection[1].GetSingle(),
                selection[2].GetSingle(), selection[3].GetSingle());
            if (sourceTypography)
            {
                var configuredHeader = settings.GetProperty("selectionHeader");
                var header = new UiSourceColumnButton { Name = "SelectionHeader",
                    Position = new(configuredHeader.GetProperty("x").GetSingle(), configuredHeader.GetProperty("y").GetSingle()),
                    Size = new(configuredHeader.GetProperty("width").GetSingle(), 48) };
                _canvas.AddChild(header);
                header.Configure(repository, Assets, _sourceShader!, _sourceFont!, text.Get(selectionHeaderKey.GetString()!));
            }
            else
            {
                AddShellPanel("SelectionHeader", "000_cmn_btn_cmd_hanyo_0",
                    new Rect2(chosen.Position.X - 6, 211, chosen.Size.X + 12, 48));
                AddLabel(text.Get(selectionHeaderKey.GetString()!),
                    new Rect2(chosen.Position.X, 217, chosen.Size.X, 40), 24,
                    HorizontalAlignment.Center, new Color("#514936")).Name = "SelectionCaption";
            }
            if (sourceTypography)
            {
                var configuredRow = settings.GetProperty("selectionRow");
                var row = new UiSourceListRow { Name = "CurrentSelection", Selected = true,
                    Position = new(configuredRow.GetProperty("x").GetSingle(), configuredRow.GetProperty("y").GetSingle()) };
                _canvas.AddChild(row);
                row.Configure(repository, Assets, _sourceShader!, _sourceFont!, selectionCaption,
                    settings.GetProperty("selectionHeader").GetProperty("width").GetSingle(),
                    configuredRow.GetProperty("leadingCell").GetBoolean(), selectionIcon);
                if (configuredRow.GetProperty("leadingCell").GetBoolean())
                {
                    var kindHeader = new UiSourceColumnButton { Name = "SelectionKindHeader",
                        Position = new(row.Position.X, settings.GetProperty("selectionHeader").GetProperty("y").GetSingle()),
                        Size = new(48, 48) };
                    _canvas.AddChild(kindHeader);
                    kindHeader.Configure(repository, Assets, _sourceShader!, _sourceFont!, "", "000_cmn_icon_kyoten");
                }
                var configuredScroll = settings.GetProperty("selectionScrollbar");
                var scroll = new UiSourceScrollbar { Name = "SelectionScrollbar",
                    Position = new(configuredScroll.GetProperty("x").GetSingle(), configuredScroll.GetProperty("y").GetSingle()),
                    Size = new(16, configuredScroll.GetProperty("height").GetSingle()) };
                _canvas.AddChild(scroll);
                scroll.ConfigureNoRange(repository, Assets, _sourceShader!);
            }
            else AddShellPanel("CurrentSelection", "000_cmn_btn_cmd_hanyo_2",
                new Rect2(chosen.Position - new Vector2(6, 6), chosen.Size + new Vector2(12, 8)));
        }
        if (!sourceTypography)
            AddLabel(selectionCaption, new Rect2(selection[0].GetSingle(), selection[1].GetSingle(),
                selection[2].GetSingle(), selection[3].GetSingle()), 28, HorizontalAlignment.Center,
                new Color("#efdd8e"));
        TextureButton back;
        if (sourceTypography)
        {
            var footer = new UiSourceLayout(repository, 0xD8913AB2);
            Vector2 footerMount = new(0, 1080 - footer.Nodes[0].Value46);
            var sourceBack = new UiSourceRoundButton { Name = $"Close{viewName}", Position = footer.Rectangle(1, footerMount).Position };
            _canvas.AddChild(sourceBack);
            sourceBack.Configure(repository, Assets, _sourceShader!, text.SourceLocale,
                "000_cmn_txt_decision_modoru", RequestClose, cue: UiAudioCue.Cancel);
            sourceBack.FocusMode = FocusModeEnum.All;
            back = sourceBack;
        }
        else
        {
            back = new TextureButton
            {
            Name = $"Close{viewName}", Position = new Vector2(896, 952), Size = new Vector2(128, 128),
            TextureNormal = Assets.Sprite("000_cmn_btn_decision_0").Texture,
            TextureHover = Assets.Sprite("000_cmn_btn_decision_1").Texture,
            TexturePressed = Assets.Sprite("000_cmn_btn_decision_2").Texture,
            IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
            TooltipText = text.Get("detail_return")
            };
            back.Pressed += RequestClose;
            _canvas.AddChild(back);
            Label returnLabel = AddLabel(text.Get("detail_return"), new Rect2(920, 993, 80, 40),
                32, HorizontalAlignment.Center, new Color("#35261b"));
            returnLabel.MouseFilter = MouseFilterEnum.Ignore;
        }
        _close = close;
        _returnButton = back;
        if (partiallyRenderedText.Count != 0)
        {
            SetMeta("partial_text_style_nodes", partiallyRenderedText.ToArray());
            Label notice = AddLabel(string.Format(CultureInfo.InvariantCulture,
                text.Get(partialStyleNoticeKey!), partiallyRenderedText.Count),
                new Rect2(850, 1032, 1040, 35), 18, HorizontalAlignment.Left, new Color("#eadcb5"));
            notice.Name = "PartialTextStyleNotice";
        }
        GD.Print($"{viewName} source {source.Source.ResourceUri}: {nodes.Count} nodes; " +
            $"pending bindings {_unbound.Count}: {string.Join(", ", _unbound)}.");
        Resized += FitCanvas;
        FitCanvas();
        FocusReturn();
    }

    internal void FocusReturn()
    {
        if (_returnButton is not null && _returnButton.IsInsideTree() && !_returnButton.IsQueuedForDeletion())
            _returnButton.GrabFocus();
    }

    private void AddShellPanel(string name, string spriteName, Rect2 rectangle)
    {
        _canvas.AddChild(new NinePatchRect
        {
            Name = name, Position = rectangle.Position, Size = rectangle.Size,
            Texture = Assets.Sprite(spriteName).Texture, MouseFilter = MouseFilterEnum.Ignore,
            PatchMarginLeft = 6, PatchMarginTop = 6, PatchMarginRight = 6, PatchMarginBottom = 6
        });
    }

    internal void RequestClose()
    {
        if (_close is not Action close || !IsVisibleInTree() || IsQueuedForDeletion()) return;
        _close = null;
        Hide();
        close();
    }

    public override void _ExitTree()
    {
        _sourceFont?.Dispose(); _sourceFont = null;
        _sourceShader?.Dispose(); _sourceShader = null;
        _assets?.Dispose();
        _assets = null;
        _returnButton = null;
        _close = null;
    }

    private void DrawSprite(LcskDocument layout, LcskNodeRecord node, Rect2 rectangle, Vector2 scale)
    {
        LcskSpriteNode sprite = LcskSpriteNodeReader.Read(layout, node);
        if (sprite.RawTintBytes.Any(value => value != 255))
            throw new InvalidDataException($"Detail sprite has an unverified tint: {node.Name}.");
        float border = 0;
        if (node.Type == 3)
        {
            border = sprite.SourceMargins[0];
            if (border < 0 || sprite.SourceMargins.Any(value => Math.Abs(value - border) > 0.00001f))
                throw new InvalidDataException($"Detail nine-part sprite has unverified unequal margins: {node.Name}.");
        }
        foreach (LcskSpriteNodeGroup group in sprite.Groups)
        {
            if (group.ComponentCount == 0) continue;
            if (group.ComponentCount != 1)
                throw new InvalidDataException($"Detail sprite has unverified component composition: {node.Name}.");
            Rect2 destination = rectangle;
            if (node.Type == 3)
            {
                float bx = border * scale.X, by = border * scale.Y;
                if (rectangle.Size.X < 2 * bx || rectangle.Size.Y < 2 * by)
                    throw new InvalidDataException($"Detail nine-part sprite is smaller than its borders: {node.Name}.");
                float[] xs = [0, bx, rectangle.Size.X - bx, rectangle.Size.X];
                float[] ys = [0, by, rectangle.Size.Y - by, rectangle.Size.Y];
                int x = group.Index / 3, y = group.Index % 3;
                destination = new Rect2(rectangle.Position + new Vector2(xs[x], ys[y]),
                    new Vector2(xs[x + 1] - xs[x], ys[y + 1] - ys[y]));
            }
            LcskSpriteNodePart part = group.Parts[0];
            TextureRect picture = AddPicture(Assets.SpritePart(part.SpriteName, part.Uv), destination);
            picture.Name = sprite.Groups.Count == 1 ? node.Name : $"{node.Name}_{group.Index}";
            picture.SetMeta("source_node_index", node.Index);
            picture.SetMeta("source_raw_record", node.RawBytes);
            picture.SetMeta("source_sprite_name", part.SpriteName);
            picture.SetMeta("source_local_uv", new float[] { part.Uv.TopLeft.U, part.Uv.TopLeft.V,
                part.Uv.TopRight.U, part.Uv.TopRight.V, part.Uv.BottomLeft.U, part.Uv.BottomLeft.V,
                part.Uv.BottomRight.U, part.Uv.BottomRight.V });
        }
    }

    private void FitCanvas()
    {
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        _canvas.Scale = Vector2.One * factor;
        _canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }

    private void AddColor(Rect2 rectangle, Color color) => _canvas.AddChild(new ColorRect
    {
        Position = rectangle.Position, Size = rectangle.Size, Color = color, MouseFilter = MouseFilterEnum.Ignore
    });

    private void DrawProgressBar(string nodeName, Rect2 rectangle, JsonElement profile,
        IReadOnlyDictionary<string, string> values)
    {
        string valueKey = profile.GetProperty("value").GetString()!;
        string capacityKey = profile.GetProperty("capacity").GetString()!;
        if (!values.TryGetValue(valueKey, out string? valueText) ||
            !values.TryGetValue(capacityKey, out string? capacityText))
            throw new InvalidDataException($"Progress bar {nodeName} has no bound state value.");
        if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int value) ||
            !int.TryParse(capacityText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int capacity))
        {
            _unbound.Add(nodeName);
            return;
        }
        if (capacity <= 0 || value < 0 || value > capacity)
            throw new InvalidDataException($"Progress bar {nodeName} has invalid current/capacity values.");
        var track = new ColorRect
        {
            Name = nodeName, Position = rectangle.Position, Size = rectangle.Size,
            Color = new Color(profile.GetProperty("track").GetString()!),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _canvas.AddChild(track);
        var fill = new ColorRect
        {
            Name = "Fill", Size = new Vector2(rectangle.Size.X * value / capacity, rectangle.Size.Y),
            Color = new Color(profile.GetProperty("fill").GetString()!),
            MouseFilter = MouseFilterEnum.Ignore
        };
        track.AddChild(fill);
        track.SetMeta("source_node_name", nodeName);
        track.SetMeta("source_progress_value", value);
        track.SetMeta("source_progress_capacity", capacity);
        track.SetMeta("original_progress_art_verified", false);
    }

    private TextureRect AddPicture(Texture2D texture, Rect2 rectangle,
        TextureRect.StretchModeEnum stretch = TextureRect.StretchModeEnum.Scale)
    {
        var picture = new TextureRect
        {
            // Remove intrinsic texture minimums before assigning the requested size.
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = stretch,
            Texture = texture, Position = rectangle.Position, Size = rectangle.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Ignore
        };
        _canvas.AddChild(picture);
        return picture;
    }

    private Label AddLabel(string value, Rect2 rectangle, int size, HorizontalAlignment align, Color color)
    {
        var label = new Label
        {
            Text = value, Position = rectangle.Position, Size = rectangle.Size,
            HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center,
            ClipText = true, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        _canvas.AddChild(label);
        return label;
    }
}
