using Godot;
using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

/// <summary>The two authored layouts owned by the actual cutin command.
/// The scene supplies accepted logical updates; clips retain their own clock.</summary>
internal partial class UniqueTacticCutinView : Control
{
    private partial class ClipView : UiSourceAnimationView
    {
        internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
            string name, string clip, IReadOnlyDictionary<int, OrdinaryTacticNameArt> replacements,
            int hiddenDecoration = -1, bool darkChip = false)
        {
            AutomaticAnimationClock = false;
            var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, name));
            ConfigureAnimation(source, new HashSet<string> { clip }, Enumerable.Range(0, source.Nodes.Count));
            //The factory binds by the shared sprite-table name. A portrait
            //can appear in several authored panes with different UV/blends.
            var boundImages = replacements.GroupBy(pair =>
                LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[pair.Key]).Groups[0].Parts[0].SpriteName)
                .ToDictionary(group => group.Key, group => group.Last().Value);
            if (hiddenDecoration >= 0) HiddenAnimationPanes.Add(hiddenDecoration);
            if (darkChip)
            {
                //The factory replaces control1 with a child layout, not an
                //imaginary Common sprite named after that layout.
                var child = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_prt_chip_dark"));
                var mount = new Control { Name = "DarkChipLayout", Size = child.Rectangle(0).Size,
                    MouseFilter = MouseFilterEnum.Ignore };
                AnimationPanes[1].AddChild(mount);
                foreach (var node in child.Nodes.Where(value => value.Type is 1 or 3))
                    child.Art(mount, node.Index, child.Rectangle(node.Index), assets, shader);
            }
            foreach (var node in source.Nodes.Where(value => value.Type is 1 or 3))
            {
                if (node.Index == hiddenDecoration || darkChip && node.Index == 1 ||
                    node.Name is "ktglkids_scl_ofsrender_view3" or "ktglkids_scl_ofsrender_view4") continue;
                var rectangle = new Rect2(Vector2.Zero, new(node.Value44, node.Value46));
                var groups = LcskSpriteNodeReader.Read(source.Source.Layout, node).Groups;
                if (groups.Count > 0 && groups[0].Parts.Count > 0 &&
                    boundImages.TryGetValue(groups[0].Parts[0].SpriteName, out var art))
                {
                    if (groups.Count == 1 && groups[0].ComponentCount == 2)
                    {
                        var sprite = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(art.ResourceName, art.SpriteName);
                        var image = new NewGameRulerPortrait { Size = rectangle.Size, MouseFilter = MouseFilterEnum.Ignore };
                        AnimationPanes[node.Index].AddChild(image);
                        image.ConfigureAuthored(repository, assets, shader, sprite, source, node.Index);
                    }
                    else source.Art(AnimationPanes[node.Index], node.Index, rectangle, assets, shader,
                        commonSpriteOverride: art.SpriteName, commonTexturePack: art.ResourceName, localCoordinates: true);
                }
                else source.Art(AnimationPanes[node.Index], node.Index, rectangle, assets, shader, localCoordinates: true);
            }
            SetMeta("source_layout", source.Source.Source.ResourceUri);
            selectedClip = clip;
        }

        private string selectedClip = "";
        internal void Start() => PlayAnimation(selectedClip);
        internal Control MountEffectOutput(int sourceView)
        {
            var node = AnimationLayout.Nodes.Single(value => value.Name == "ktglkids_scl_ofsrender_view" + sourceView);
            var mount = new Control { Name = "UniqueEffectView" + sourceView, Size = new(node.Value44, node.Value46),
                MouseFilter = MouseFilterEnum.Ignore };
            AnimationPanes[node.Index].AddChild(mount); return mount;
        }
        protected override byte AuthoredPaneAlpha(int index)
        {
            var node = AnimationLayout.Nodes[index];
            int offset = node.Type switch { 0 => 0x48, 1 or 3 => 0x58,
                _ => throw new InvalidDataException("unique.cutin.pane_alpha_format") };
            return unchecked((byte)(int)(BinaryPrimitives.ReadSingleLittleEndian(node.RawBytes.AsSpan(offset, 4)) * 255f));
        }
        internal byte ReadEffectAlpha(int sourceView)
        {
            int node = AnimationLayout.Nodes.Single(value => value.Name == "ktglkids_scl_ofsrender_view" + sourceView).Index;
            return Effective(node);
            byte Effective(int index)
            {
                byte own = AnimationPaneAlphas[index];
                if (AnimationLayout.Nodes[index].ParentIndex is not int parent) return own;
                var source = AnimationLayout.Nodes[parent];
                int offset = source.Type switch { 0 => 0x40, 1 or 3 => 0x50,
                    _ => throw new InvalidDataException("unique.cutin.pane_render_flags_format") };
                uint flags = BinaryPrimitives.ReadUInt32LittleEndian(source.RawBytes.AsSpan(offset, 4));
                return (flags & 1) == 0 ? own :
                    unchecked((byte)(int)((Effective(parent) / 255f) * (own / 255f) * 255f));
            }
        }
        internal Vector2 InitialMount(int index)
        {
            var position = AnimationLayout.Rectangle(index).Position;
            return new((int)position.X, (int)position.Y);
        }
    }

    private ClipView cutin = null!, title = null!;
    private UniqueTacticLayoutPublication source = null!;
    private Action<int, int>? createEffect;
    private Action? stopLinkedEffect, stopEffects;
    private Action<int>? playSound;
    private UiAssets? ownedAssets;
    private Shader? ownedShader;
    private int counter, stage;
    internal int Stage => stage;
    internal int Counter => counter;
    internal bool IsFinished => stage == 7;
    internal Control BackEffects { get; private set; } = null!;
    internal Control FrontEffects { get; private set; } = null!;
    internal object EffectOwner => cutin;
    internal byte ReadEffectAlpha(int selector) => selector is 2 or 3
        ? cutin.ReadEffectAlpha(selector + 1) : throw new ArgumentOutOfRangeException(nameof(selector));

    internal void Configure(GameResourceRepository repository, UniqueTacticLayoutPublication publication,
        Action<int, int> createEffect, Action stopLinkedEffect, Action stopEffects, Action<int> playSound)
    {
        source = publication; this.createEffect = createEffect; this.stopLinkedEffect = stopLinkedEffect;
        this.stopEffects = stopEffects; this.playSound = playSound;
        Name = "UniqueTacticCutin"; Size = new(1920, 1080); MouseFilter = MouseFilterEnum.Ignore;
        var assets = ownedAssets = new UiAssets(repository); var shader = ownedShader = UiSourceSprite.CreateShader();
        cutin = new() { Name = "CutinLayout" }; AddChild(cutin);
        var images = publication.LinkedArts.ToDictionary(value => value.NodeIndex, value => value.Art);
        images.Add(publication.MainNode, publication.MainArt);
        cutin.Configure(repository, assets, shader, publication.LayoutName,
            "senpou_" + (publication.LinkedArts.Count + 1), images, publication.HiddenDecorationNode, darkChip: true);
        BackEffects = cutin.MountEffectOutput(3); FrontEffects = cutin.MountEffectOutput(4);
        title = new() { Name = "UniqueTitleLayout" }; AddChild(title);
        title.Configure(repository, assets, shader, publication.TitleLayoutName,
            "senpou_txt_" + publication.NameLength,
            publication.TitleImageNodes.ToDictionary(index => index, _ => publication.TitleArt));
        title.Position = cutin.InitialMount(publication.HiddenDecorationNode);
        SetMeta("presentation_sequence", publication.Sequence);
    }

    //36E630 advances at most one stage on each logical update. Time continues
    //for both clips even while the command is waiting at a later stage.
    internal bool UpdateCommand(double elapsedSeconds, int logicalFramesPerSecond, bool skip)
    {
        counter = unchecked(counter + 1);
        if (skip)
        {
            cutin.StopAnimation(); title.StopAnimation(); stopEffects?.Invoke(); stage = 7;
            return false;
        }
        cutin.AdvanceAnimation(elapsedSeconds); title.AdvanceAnimation(elapsedSeconds);
        float factor = logicalFramesPerSecond / 60f;
        switch (stage)
        {
            case 0: cutin.Start(); stage = 1; break;
            case 1 when counter >= (int)(20 * factor): createEffect?.Invoke(98 + source.EffectClass, 2); stage = 2; break;
            case 2 when counter >= (int)(40 * factor):
                if (source.LinkedOfficerCount is >= 1 and <= 4) createEffect?.Invoke(102 + source.LinkedOfficerCount, 2);
                stage = 3; break;
            case 3 when counter >= (int)(85 * factor):
                title.Start(); playSound?.Invoke(42 + source.NameLength); stage = 4; break;
            case 4 when counter >= (int)(140 * factor): createEffect?.Invoke(107 + source.EffectClass, 3); stage = 5; break;
            case 5 when counter >= (int)(290 * factor): stopLinkedEffect?.Invoke(); stage = 6; break;
            case 6 when !cutin.IsPlaying && !title.IsPlaying: cutin.Hide(); title.Hide(); stage = 7; break;
            case 7: return false;
        }
        return true;
    }

    internal void Cancel()
    {
        cutin.StopAnimation(); title.StopAnimation(); stopEffects?.Invoke(); stage = 7;
    }

    public override void _ExitTree()
    { ownedAssets?.Dispose(); ownedAssets = null; ownedShader?.Dispose(); ownedShader = null; }
}
