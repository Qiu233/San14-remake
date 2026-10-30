using System.Buffers.Binary;
using Godot;
using San14.Data;

internal partial class UiSourceStandingPortrait
{
    private StandingPortraitFrameAnimation? frameAnimation;
    private UiSourceSprite? animatedImage;
    private IReadOnlyList<LoadedSprite>? animationFrames;
    private Func<int, Texture2D>? frameTexture;
    private int animatedSpriteIndex = -1;
    private bool animateFrames = true;

    // 841F00's actual caller flag. Ordinary Story passes1; the event-editor
    // help adviser passes0 and keeps the source clip's initial sample.
    internal bool AnimateFrames
    {
        get => animateFrames;
        set
        {
            animateFrames = value;
            if (motion is not null) ApplyMotion(motion.SetAnimationEnabled(value));
            RefreshProcessing();
        }
    }
    internal int? AnimatedSpriteIndex => frameAnimation is null ? null : animatedSpriteIndex;

    private void ConfigureAnimated(GameResourceRepository repository, UiAssets assets, Shader shader,
        IReadOnlyList<LoadedSprite> frames)
    {
        if (frames.Count != StandingPortraitFrameAnimation.FrameCount)
            throw new InvalidDataException("Animated standing portrait requires all thirty published bank12 frames.");
        MouseFilter = MouseFilterEnum.Ignore;
        var layout = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_ani_chara_sprite_cn"));
        storyLayout = layout.Source.Layout;
        Size = layout.Rectangle(0).Size;
        var node = layout.Nodes.Single(node => node.Name == "stand_000");
        var source = San14.Data.Formats.LcskSpriteNodeReader.Read(storyLayout, node);
        if (node.Type != 1 || source.Groups.Count != 1 || source.Groups[0].ComponentCount != 1 ||
            source.Groups[0].Parts.Count != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(node.RawBytes.AsSpan(84, 4)) != 0x00554400 ||
            source.RawTintBytes.Any(value => value != 255))
            throw new InvalidDataException("Animated standing portrait source composition differs.");
        var uv = source.Groups[0].Parts[0].Uv;
        var loadedFrames = frames.ToArray();
        animationFrames = loadedFrames;
        var textures = new Texture2D?[frames.Count];
        frameTexture = index =>
        {
            if (textures[index] is { } cached) return cached;
            var sprite = loadedFrames[index];
            return textures[index] = MapPortraitQuad.UsesSrgbView(repository, assets, sprite.Definition.Name, sprite)
                ? assets.SrgbSpritePart(sprite.Definition.Name, uv, sprite)
                : assets.SpritePart(sprite.Definition.Name, uv, sprite);
        };
        frameAnimation = StandingPortraitFrameAnimation.Read(storyLayout, node.Index);
        animatedSpriteIndex = frameAnimation.SpriteIndex;
        var rectangle = layout.Rectangle(node.Index);
        animatedImage = new UiSourceSprite { Name = "StandingFrame0", Position = rectangle.Position, Size = rectangle.Size };
        AddChild(animatedImage);
        animatedImage.Configure(shader, frameTexture(animatedSpriteIndex), UiSourceSprite.Blend.Ordinary);
        ConfigureMaterial(animatedImage, node);
        storyLayers.Add((node.Index, animatedImage));
        animatedImage.SetMeta("portrait_sprite", frames[animatedSpriteIndex].Definition.Name);
        animatedImage.SetMeta("source_node_index", node.Index);
        SetMeta("source_layout", layout.Source.Source.ResourceUri);
        SetMeta("portrait_kind", "animated_frames");
        SetProcess(AnimateFrames);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        if (AnimateFrames && frameAnimation is not null)
        {
            int next = frameAnimation.SpriteIndex;
            if (next != animatedSpriteIndex)
            {
                animatedImage!.SetSource(frameTexture!(next));
                animatedImage.SetMeta("portrait_sprite", animationFrames![next].Definition.Name);
                animatedSpriteIndex = next;
            }
            // Native layout applies this dirty sample before advancing.
            frameAnimation.Advance((float)delta);
        }
        if (motion is not null) ApplyMotion(motion.Update((float)delta));
        RefreshProcessing();
    }

    public override void _ExitTree()
    {
        SetProcess(false);
        frameAnimation = null; animatedImage = null; frameTexture = null; animationFrames = null;
        // Textures belong to the caller's UiAssets. Retiring this borrower
        // releases its frame list and delegates without disposing shared assets.
        storyLayers.Clear(); motionLayers.Clear(); motion = null;
    }
}
