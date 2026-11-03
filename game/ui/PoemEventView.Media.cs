using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class PoemEventView
{
    private sealed class Pose(float x, float y, float sx, float sy)
    {
        internal float X = x, Y = y, ScaleX = sx, ScaleY = sy, Alpha = 1, U, V;
    }
    private sealed class ImagePane(UiAssets resources, Node2D root,
        Vector2 size, Func<UiAssets, Control> build, Action<Control> refresh) : IDisposable
    {
        private Control? art;
        internal void Update(Rect2 viewport)
        {
            bool visible = (root.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, size)).Intersects(viewport);
            if (!visible) { Dispose(); return; }
            if (art is null) art = build(resources);
            refresh(art);
        }
        public void Dispose()
        {
            if (art is { } old) { old.Hide(); old.GetParent()?.RemoveChild(old); old.QueueFree(); art = null; }
        }
    }
    private UiSourceLayout source = null!;
    private Node2D[] visuals = [];
    private Pose[] poses = [];
    private Transform2D[] baseTransforms = [];
    private readonly List<ImagePane> imagePanes = [];
    private readonly List<(int Node, UiSourceSprite Sprite)> movingMasks = [];
    private bool scrollAcquired;

    private void BuildMedia()
    {
        source = assets.SourceLayout(publication.LayoutName);
        visuals = new Node2D[source.Nodes.Count]; poses = new Pose[source.Nodes.Count]; baseTransforms = new Transform2D[source.Nodes.Count];
        foreach (var node in source.Nodes)
        {
            Transform2D transform = source.NodeTransform(node.Index);
            if (node.ParentIndex is int parent) transform = source.NodeTransform(parent).AffineInverse() * transform;
            baseTransforms[node.Index] = transform;
            poses[node.Index] = new(node.HeaderValues[0], node.HeaderValues[1], node.HeaderValues[7], node.HeaderValues[8]);
            var visual = new Node2D { Name = "PoemNode" + node.Index, Transform = transform }; visuals[node.Index] = visual;
            (node.ParentIndex is int parentIndex ? (Node)visuals[parentIndex] : canvas).AddChild(visual);
        }
        var replacements = publication.Images.ToDictionary(image => image.NodeId);
        var glyphs = new HashSet<int>();
        if (publication.MaxTextScrollOffsetX > 0)
        {
            foreach (var image in publication.Images)
            {
                int glyph = image.NodeId, colour = glyph + 1; glyphs.Add(glyph); glyphs.Add(colour);
                var size = new Vector2(source.Nodes[colour].Value44, source.Nodes[colour].Value46);
                imagePanes.Add(new(assets, visuals[colour], size, local =>
                {
                    var alphaPart = LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[glyph]).Groups.Single().Parts.Single();
                    var colorPart = LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[colour]).Groups.Single().Parts.Single();
                    var clipPart = LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[4]).Groups.Single().Parts.Single();
                    var glyphTexture = LoadPart(local, alphaPart, image.Image, out _);
                    var colorTexture = LoadPart(local, colorPart, null, out var flip);
                    var clipTexture = LoadPart(local, clipPart, null, out _);
                    var draw = new UiSourceSprite { Name = "PoemScrollPiece" + image.Image.Index, Size = size };
                    visuals[colour].AddChild(draw); draw.Configure(shader, colorTexture, UiSourceSprite.Blend.PoemScroll);
                    draw.SetUvFlip(flip.X < 0, flip.Y < 0);
                    draw.SetClipping(colorTexture, glyphTexture, new(Vector2.Zero, size), clipTexture, new(Vector2.Zero, size));
                    return draw;
                }, draw =>
                {
                    var sprite = (UiSourceSprite)draw;
                    // Texture binding is prepared once; only these two source
                    // rectangles and vertex-alpha inputs change during scrolling.
                    sprite.SetMaskRectangles(RelativeRectangle(colour, glyph), RelativeRectangle(colour, 4));
                    sprite.SetMaskAlphas(GlobalAlpha(glyph), GlobalAlpha(4));
                }));
            }
            glyphs.Add(4); // alpha-only clip write is contracted into each visible piece.
        }
        foreach (var node in source.Nodes.Where(node => (node.Type is 1 or 3) && !glyphs.Contains(node.Index)))
        {
            var size = new Vector2(node.Value44, node.Value46);
            if (replacements.TryGetValue(node.Index, out var replacement))
            {
                imagePanes.Add(new(assets, visuals[node.Index], size,
                    local => BuildPane(node.Index, local, replacement.Image),
                    draw => { if (draw is UiSourceSprite mask) mask.SetMaskUvOffset(new(poses[node.Index].U, poses[node.Index].V)); }));
            }
            else
            {
                var draw = BuildPane(node.Index, assets, null);
                if (draw is UiSourceSprite mask) movingMasks.Add((node.Index, mask));
            }
        }
        var initial = publication.Animations.Single(clip => clip.Name == "loop");
        Apply(initial.Sample(initial.StartFrame));
    }

    private Control BuildPane(int index, UiAssets local, CollectionImageRoute? replacement)
    {
        var node = source.Nodes[index];
        var parts = LcskSpriteNodeReader.Read(source.Source.Layout, node).Groups;
        if (node.Type == 1 && parts.Single().ComponentCount == 2)
        {
            var pair = parts.Single().Parts;
            if (pair[0].RawBytes[6] != 0 || pair[1].RawBytes[6] != 13)
                throw new InvalidDataException("Unsupported poem image-mask composition.");
            var color = LoadPart(local, pair[0], replacement, out var flip);
            var mask = LoadPart(local, pair[1], null, out var maskFlip);
            var size = new Vector2(node.Value44, node.Value46);
            var draw = new UiSourceSprite { Name = "PoemMaskedPane" + index, Size = size };
            visuals[index].AddChild(draw); draw.Configure(shader, color, UiSourceSprite.Blend.TranslatedAlphaMask);
            draw.SetUvFlip(flip.X < 0, flip.Y < 0);
            var box = new Rect2(new(maskFlip.X < 0 ? size.X : 0, maskFlip.Y < 0 ? size.Y : 0), size * maskFlip);
            draw.SetClipping(color, mask, box); return draw;
        }
        return source.Art(visuals[index], index, new(Vector2.Zero, new(node.Value44, node.Value46)), local, shader,
            commonSpriteOverride: replacement?.SpriteName, commonTexturePack: replacement?.TexturePack, localCoordinates: true);
    }

    private Texture2D LoadPart(UiAssets local, LcskSpriteNodePart part, CollectionImageRoute? replacement, out Vector2 flip)
    {
        var uv = part.Uv; bool u = uv.BottomRight.U < uv.TopLeft.U, v = uv.BottomRight.V < uv.TopLeft.V;
        if (u) uv = new(uv.TopRight, uv.TopLeft, uv.BottomRight, uv.BottomLeft);
        if (v) uv = new(uv.BottomLeft, uv.BottomRight, uv.TopLeft, uv.TopRight);
        flip = new(u ? -1 : 1, v ? -1 : 1);
        return replacement is null ? local.SrgbLayoutSpritePart(source.Source, part.SpriteName, uv)
            : local.SrgbTexturePackSpritePart(replacement.TexturePack, replacement.SpriteName, uv);
    }

    private Rect2 RelativeRectangle(int target, int mask) =>
        (visuals[target].GetGlobalTransform().AffineInverse() * visuals[mask].GetGlobalTransform()) *
        new Rect2(Vector2.Zero, new(source.Nodes[mask].Value44, source.Nodes[mask].Value46));
    private float GlobalAlpha(int node)
    {
        float alpha = 1;
        for (int? index = node; index is int current; index = source.Nodes[current].ParentIndex) alpha *= poses[current].Alpha;
        return alpha;
    }
    private void Apply(IReadOnlyList<LcskAnimationValue> values)
    {
        foreach (var value in values)
        {
            var pose = poses[value.NodeId];
            switch (value.Channel)
            {
                case 0: pose.X = (float)value.Value; break;
                case 1: pose.Y = (float)value.Value; break;
                case 2: break; // Z does not change this source's planar UI projection.
                case 6: pose.ScaleX = (float)value.Value; break;
                case 7: pose.ScaleY = (float)value.Value; break;
                case 10: pose.Alpha = (byte)(long)Math.Clamp(value.Value * 255, 0, 255) / 255f; break;
                case 41: pose.U = (float)value.Value; break;
                case 42: pose.V = (float)value.Value; break;
                default: throw new InvalidDataException("Unsupported poem animation channel.");
            }
            ApplyPose(value.NodeId);
        }
    }
    private void ApplyPose(int index)
    {
        var pose = poses[index]; var original = source.Nodes[index].HeaderValues; var transform = baseTransforms[index];
        transform.Origin += new Vector2(pose.X - original[0], -(pose.Y - original[1]));
        transform.X *= pose.ScaleX / original[7]; transform.Y *= pose.ScaleY / original[8];
        visuals[index].Transform = transform; visuals[index].Modulate = new(1, 1, 1, pose.Alpha);
    }
    private void SetScroll(int x) { poses[5].X = x; ApplyPose(5); }
    private void RefreshMedia()
    {
        foreach (var (node, sprite) in movingMasks) sprite.SetMaskUvOffset(new(poses[node].U, poses[node].V));
        foreach (var image in imagePanes) image.Update(GetViewport().GetVisibleRect());
    }

    private void BuildFooters()
    {
        var footer = assets.SourceLayout(0xD8913AB2);
        var help = assets.SourceLayout(0xAB6ADAA7);
        Control Make(string name, bool back, int message)
        {
            var root = new Control { Name = name, Size = canvas.Size, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 }; canvas.AddChild(root);
            var button = new UiSourceRoundButton { Name = name + "Button", Position = footer.Rectangle(1, new(0, 904)).Position,
                InputAdmission = ReadAdmission }; root.AddChild(button);
            button.Configure(repository, assets, shader, text.SourceLocale, back ? "000_cmn_txt_decision_modoru" : "000_cmn_txt_decision_kettei",
                () => { if (!ReadAdmission()) return; if (back) Enter(9); else OpenInformation(); }, back ? UiAudioCue.Cancel : UiAudioCue.ButtonSelect);
            var mount = footer.Rectangle(19, new(0, 904)).Position;
            foreach (var node in help.Nodes.Where(node => node.Type is 1 or 3)) help.Art(root, node.Index, help.Rectangle(node.Index, mount), assets, shader);
            var label = help.Text(root, help.Nodes.Single(node => node.Type == 2).Index, mount, Game(message), font);
            if (!back) hint = label;
            return root;
        }
        normalFooter = Make("PoemInformation", false, 0x2987); infoFooter = Make("PoemReturn", true, publication.InformationFooterHintMessageId);
    }

    private void BuildInformation()
    {
        var info = assets.SourceLayout(publication.Info.Layout.Source.FileId); var poem = publication.Info.Poem;
        var footer = assets.SourceLayout(0xD8913AB2);
        information = new() { Name = "PoemInformationPanel", Size = new(info.Nodes[0].Value44, info.Nodes[0].Value46),
            Position = new((1920 - info.Nodes[0].Value44) / 2f,
                (footer.Rectangle(19, new(0, 904)).Position.Y - info.Nodes[0].Value46) / 2),
            MouseFilter = MouseFilterEnum.Ignore, ZIndex = 5 }; canvas.AddChild(information);
        foreach (var node in info.Nodes)
        {
            if (node.Type is 1 or 3)
                info.Art(information, node.Index, info.Rectangle(node.Index), assets, shader,
                    commonSpriteOverride: node.Index == 7 ? poem.Image.SpriteName : null,
                    commonTexturePack: node.Index == 7 ? poem.Image.TexturePack : null,
                    blend: node.Index == 2 ? UiSourceSprite.Blend.AlphaMask : UiSourceSprite.Blend.Ordinary);
            else if (node.Type == 6) info.Blur(information, node.Index, info.Rectangle(node.Index));
        }
        var presets = MapTextPresets.Create();
        foreach (var (node, value) in new[] { (14, poem.Name), (16, poem.Author), (17, poem.Era), (21, publication.Info.DescriptionCaption) })
            info.Text(information, node, default, value, font, presets[node == 21 ? 13 : 0]);
        var box = info.Rectangle(22);
        var paragraph = new UiSourceParagraph { Name = "PoemInformationDescription", Position = box.Position, Size = box.Size };
        information.AddChild(paragraph); paragraph.Configure(font,
            MapFontProfile.FromNode(info.Nodes[22], info.Source.Source.ResourceUri).WithPreset(presets[0]) with { RawAlign = 0, RawLineArrange = 1 });
        paragraph.Apply(poem.Description);
    }
}
