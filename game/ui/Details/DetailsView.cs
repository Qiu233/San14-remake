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
    private readonly List<string> _unbound = [];
    private UiAssets Assets => _assets ??
        throw new InvalidOperationException("Detail assets have not been initialized.");

    private protected void ConfigureLayout(GameResourceRepository repository, string profilePath,
        string viewName, string titleKey, PreviewText text, IReadOnlyDictionary<string, string> values,
        int? portraitAppearanceCode, string selectionCaption, Action close)
    {
        Name = viewName;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        using var profileFile = Godot.FileAccess.Open(profilePath, Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidDataException($"Detail binding profile is missing: {profilePath}.");
        using JsonDocument profile = JsonDocument.Parse(profileFile.GetAsText());
        JsonElement settings = profile.RootElement;
        uint layoutId = uint.Parse(settings.GetProperty("layoutId").GetString()!, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
        LoadedLayout source = repository.LoadLayout("ScreenLayout", layoutId);
        IReadOnlyList<LcskNodeRecord> nodes = LcskNodeReader.Read(source.Layout);
        _assets = new UiAssets(repository);
        _canvas = new Control { Size = new Vector2(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        _canvas.Theme = new Theme
        {
            DefaultFont = new SystemFont { FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"] }
        };
        AddChild(_canvas);
        AddColor(new Rect2(0, 0, 1920, 1080), new Color("#50432f"));
        // Candidate common frame: source artwork/size/margins, screenshot placement.
        // The original runtime link between this shell and the content is still pending.
        JsonElement frameSettings = settings.GetProperty("frame");
        uint frameId = uint.Parse(frameSettings.GetProperty("layoutId").GetString()!,
            NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        LoadedLayout frame = repository.LoadLayout("ScreenLayout", frameId);
        LcskNodeRecord frameNode = LcskNodeReader.Read(frame.Layout)
            .Single(node => node.Name == frameSettings.GetProperty("node").GetString());
        DrawSprite(frame.Layout, frameNode, new Rect2(
            frameSettings.GetProperty("origin")[0].GetSingle(),
            frameSettings.GetProperty("origin")[1].GetSingle(), frameNode.Value44, frameNode.Value46), Vector2.One);
        AddPicture(Assets.Sprite("000_cmn_base_title").Texture, new Rect2(0, 0, 540, 88));
        AddLabel(text.Get(titleKey), new Rect2(120, 25, 330, 50), 32,
            HorizontalAlignment.Left, new Color("#eee3a6"));

        HashSet<string> hiddenNames = settings.GetProperty("hiddenNodes").EnumerateArray()
            .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (JsonElement missing in settings.GetProperty("unsupportedNodes").EnumerateArray())
        {
            string name = missing.GetString()!;
            hiddenNames.Add(name);
            _unbound.Add(name);
        }
        string portraitNode = settings.GetProperty("portraitNode").GetString()!;
        JsonElement bindings = settings.GetProperty("texts");
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
                DrawSprite(source.Layout, node, rectangle, scales[node.Index]);
            else if (node.Type == 2)
            {
                string value;
                bool caption = false;
                if (bindings.TryGetProperty(node.Name, out JsonElement binding))
                {
                    caption = binding.TryGetProperty("key", out JsonElement key);
                    value = caption ? text.Get(key.GetString()!) : values[binding.GetProperty("value").GetString()!];
                }
                else
                {
                    _unbound.Add(node.Name);
                    value = text.Get("detail_unknown");
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
                    0x01 => HorizontalAlignment.Left,
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
        JsonElement selection = settings.GetProperty("selectionLabel");
        AddLabel(selectionCaption, new Rect2(selection[0].GetSingle(), selection[1].GetSingle(),
            selection[2].GetSingle(), selection[3].GetSingle()), 28, HorizontalAlignment.Center,
            new Color("#efdd8e"));
        var back = new TextureButton
        {
            Name = $"Close{viewName}", Position = new Vector2(896, 952), Size = new Vector2(128, 128),
            TextureNormal = Assets.Sprite("000_cmn_btn_decision_0").Texture,
            TextureHover = Assets.Sprite("000_cmn_btn_decision_1").Texture,
            TexturePressed = Assets.Sprite("000_cmn_btn_decision_2").Texture,
            IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
            TooltipText = text.Get("detail_return")
        };
        _close = close;
        back.Pressed += RequestClose;
        _returnButton = back;
        _canvas.AddChild(back);
        Label returnLabel = AddLabel(text.Get("detail_return"), new Rect2(920, 993, 80, 40),
            32, HorizontalAlignment.Center, new Color("#35261b"));
        returnLabel.MouseFilter = MouseFilterEnum.Ignore;
        AddLabel(text.Get("detail_comparison_hint"), new Rect2(25, 1032, 810, 35),
            18, HorizontalAlignment.Left, new Color("#eadcb5"));
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

    internal void RequestClose()
    {
        if (_close is not Action close || !IsVisibleInTree() || IsQueuedForDeletion()) return;
        _close = null;
        Hide();
        close();
    }

    public override void _ExitTree()
    {
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
