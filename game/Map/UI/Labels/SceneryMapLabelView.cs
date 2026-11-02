using Godot;
using San14.Data;

/// <summary>One actual Scenery root, its two replacement layout children and
/// the shared hit control. The controller supplies resolved display and the
/// projection of the actual Unit; this view never derives a Hex position.</summary>
internal partial class SceneryMapLabelView : Control
{
    private readonly Control textRoot = new() { Name = "SceneryText" };
    private readonly Control flagRoot = new() { Name = "SceneryFlag" };
    private readonly SceneryMapLabelHitArea overlay = new() { Name = "SceneryHitArea" };
    private readonly List<Control> textStates = [];
    private readonly List<Control> flagStates = [];
    private readonly Dictionary<int, Control> flagParts = [];
    private readonly Dictionary<int, bool> appliedFlagVisibility = [];
    private readonly Dictionary<int, MapCornerColors> paintedFlagColors = [];
    private bool? paintedShowOutline;
    private UiSourceLayout textLayout = null!, flagLayout = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapSpritePalettes palettes = null!;
    private MapTextPresets presets = null!;
    private MapFont font = null!;
    private MapFontProfile nameProfile = null!;
    private UiSourceText caption = null!;
    private SceneryMapFlagUpdate? appearance;
    private Action? released;
    private int display = -1, interaction = 4, appliedInteraction = -1;
    private int appliedLayoutDisplay = int.MinValue;
    private byte? appliedTextAlpha, appliedFlagAlpha;
    private int focusOverride, pushOverride;
    private Vector2I canonicalPosition;
    private bool abandoned, configured;
    private bool controlEnabled = true;
    private bool rootVisible = true;
    private bool? appliedDisabled;
    private bool presentationInputAdmitted = true;
    private bool sourceInputOwned,nativeFocus,nativePush,focusKnown=true,commandPressQualified;
    private Action? pointerEnter,pointerLeave,pointerDown,pointerRelease,inputChanged;

    internal string SourceName => caption.Text;
    internal Vector2I CanonicalPosition => canonicalPosition;
    internal bool RootVisible => rootVisible;
    internal void SetRootVisible(bool value)
    {
        if(rootVisible==value)return;
        Visible=value;rootVisible=value;
    }
    internal Rect2 HitRectangle => new(overlay.Position, overlay.Size);
    internal SceneryMapNameWidthRequests WidthRequests { get; private set; }
    internal bool FillKnown => appearance?.Fill is not null;
    internal bool OutlineKnown => appearance?.ShowOutline is false ||
        appearance is { ShowOutline: true, Outline: not null };

    public SceneryMapLabelView()
    {
        // Own the native children before any resource parsing can fail.
        // A factory can then free this entire partially initialized subtree.
        AddChild(textRoot); AddChild(flagRoot); AddChild(overlay);
    }

    internal void Configure(SceneryMapMarkerSources source, UiAssets sharedAssets,
        Shader spriteShader, MapFont font, MapTextPresets presets,
        MapSpritePalettes spritePalettes, string nativeValidName, Action released)
    {
        assets = sharedAssets; shader = spriteShader; palettes = spritePalettes;
        this.font = font; this.presets = presets;
        this.released = released;
        MouseFilter = MouseFilterEnum.Ignore;
        var root = Layout(source.Root);
        Size = root.Rectangle(0).Size;
        // Replace the authored stub subtrees at their actual mounts. Their
        // old artwork is not drawn in addition to these real child layouts.
        textRoot.Position = root.Rectangle(SceneryMapMarkerSources.TextMountNode).Position;
        flagRoot.Position = root.Rectangle(SceneryMapMarkerSources.FlagMountNode).Position;
        textRoot.MouseFilter = flagRoot.MouseFilter = MouseFilterEnum.Ignore;

        WidthRequests = source.NameWidthRequests(nativeValidName);
        var geometry = new Dictionary<int, UiSourceNodeGeometry>();
        SetWidth(SceneryMapMarkerSources.NameNode, WidthRequests.Name);
        foreach (int index in new[] { 6, 7, 13, 14 }) SetWidth(index, WidthRequests.Panel);
        SetWidth(1, WidthRequests.HitArea);
        textLayout = Layout(source.Text, geometry);
        flagLayout = Layout(source.Flag);
        // Control+28 is an int32 width. The writer changes the materialized
        // node widths as signed16, without resizing the authored root node.
        textRoot.Size = new(WidthRequests.Root, textLayout.Rectangle(0).Size.Y);
        flagRoot.Size = flagLayout.Rectangle(0).Size;
        for (int state = 0; state < 4; state++)
        {
            textStates.Add(Group(textRoot, state));
            flagStates.Add(Group(flagRoot, state));
        }
        // Text flags 3/4/8..11/15..18/20/21 are explicitly hidden by its
        // initializer. The independent Flag child supplies the flag instead.
        foreach (int index in new[] { 6, 7 })
            textLayout.Art(textStates[1], index, textLayout.Rectangle(index), assets, shader, palettes[5]);
        foreach (int index in new[] { 13, 14 })
            textLayout.Art(textStates[2], index, textLayout.Rectangle(index), assets, shader, palettes[6]);
        caption = textLayout.Text(textRoot, SceneryMapMarkerSources.NameNode, default,
            nativeValidName, font, presets[SceneryMapMarkerSources.NameTextPreset]);
        caption.Name = "SceneryName";
        nameProfile = MapFontProfile.FromNode(source.Text.Nodes[SceneryMapMarkerSources.NameNode],
            source.Text.Source.ResourceUri);
        // Flag18 is a hidden fixed direction sprite, not a force glyph.
        CreateFlagParts();

        overlay.FocusMode = FocusModeEnum.None;
        overlay.IgnoreTextureSize = true;
        overlay.ActionMode = BaseButton.ActionModeEnum.Release;
        overlay.MouseEntered += ()=>{pointerEnter?.Invoke();UpdateInteraction();};
        overlay.MouseExited += ()=>{pointerLeave?.Invoke();UpdateInteraction();};
        overlay.ButtonDown += ()=>{commandPressQualified=false;pointerDown?.Invoke();UpdateInteraction();};
        overlay.ButtonUp += ()=>{pointerRelease?.Invoke();UpdateInteraction();};
        overlay.Pressed += () =>
        {
            bool qualified=!sourceInputOwned||commandPressQualified;
            commandPressQualified=false;
            if(qualified&&!abandoned&&IsVisibleInTree()&&
                (!sourceInputOwned||presentationInputAdmitted&&controlEnabled&&ActualHovered))
                this.released?.Invoke();
        };
        SetPresentation(-1, false, 255, 255);
        // Binding initializes the raw state to4 even though enabled bit2
        // is true. The first ordinary update resolves the settled state.
        configured = true;
        ApplyInteraction();

        void SetWidth(int index, int request) => geometry.Add(index,
            new(Size: new(unchecked((short)request), source.Text.Nodes[index].Value46)));
    }

    internal void RefreshFlag(SceneryMapFlagUpdate current)
    {
        // A proven early return does not rewrite already materialized colors.
        // An unknown current result is different and retains its missing fields.
        if (appearance == current) return;
        appearance = current;
        if (current.Kind != SceneryMapFlagUpdateKind.Update) return;
        ApplyColor(4,current.Fill); ApplyColor(17,current.InactiveFill);
        ApplyColor(3,current.Outline); ApplyColor(16,current.InactiveOutline);
        if(current.ShowOutline is bool show)paintedShowOutline=show;
        ApplyFlagVisibility();
        // Dirty refresh must not rewrite the initialized name or its widths.
    }

    /// <summary>Apply the actual source control bit2, independently of name
    /// visibility. The common constructor's final reset enables it; modes
    /// can subsequently change it through their actual setter.</summary>
    internal void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if(!enabled)CancelCommandPress();
        UpdateInputAdmission();
        UpdateInteraction();
    }

    /// <summary>The real source+200 and+204 overrides, initialized to zero
    /// by the child constructor and changed by its explicit setters.</summary>
    internal void SetInteractionOverrides(int focus, int push)
    {
        focusOverride = focus; pushOverride = push;
        UpdateInteraction();
    }

    internal void SetCanonicalPosition(Vector2I position)
    {
        if (canonicalPosition == position) return;
        canonicalPosition = position;
        Position = position;
        if (configured) UpdateHitGeometry();
    }

    internal void BindCanonicalPointer(Func<Vector2I?> readCurrentPointer)
        => overlay.ReadCanonicalPointer=readCurrentPointer;

    internal bool SourceInputAdmitted=>configured&&!abandoned&&presentationInputAdmitted&&IsVisibleInTree();
    internal bool ActualHovered=>ReferenceEquals(GetViewport().GuiGetHoveredControl(),overlay);
    internal void BindSourceInput(Action enter,Action leave,Action down,Action release,Action changed)
    {
        sourceInputOwned=true;
        pointerEnter=enter;pointerLeave=leave;pointerDown=down;pointerRelease=release;inputChanged=changed;
        // The completed constructor/bind has180 clear. Later enabled changes
        // do not reset these persistent bits.
        nativeFocus=false;nativePush=false;focusKnown=true;commandPressQualified=false;
    }
    internal void SourcePointerEnter()
    {nativeFocus=true;focusKnown=true;UpdateInteraction();inputChanged?.Invoke();}
    internal void SourcePointerLeave()
    {CancelCommandPress();nativeFocus=false;nativePush=false;focusKnown=true;UpdateInteraction();inputChanged?.Invoke();}
    internal void SourcePointerDown()
    {
        if(!controlEnabled)return;
        commandPressQualified=true;
        if(nativePush)return;
        nativePush=true;UpdateInteraction();inputChanged?.Invoke();
    }
    internal void SourcePointerRelease()
    {
        if(!controlEnabled||!nativePush)return;
        nativePush=false;nativeFocus=true;focusKnown=true;UpdateInteraction();inputChanged?.Invoke();
    }
    internal void UpdateSelectionFocus(bool? primaryMatches,bool? auxiliaryMatches)
    {
        bool oldKnown=focusKnown,oldFocus=nativeFocus;
        if(focusOverride!=0||pushOverride!=0||!nativeFocus)focusKnown=true;
        else if(primaryMatches==true||auxiliaryMatches==true)focusKnown=true;
        else if(primaryMatches==false&&auxiliaryMatches==false){nativeFocus=false;focusKnown=true;}
        else focusKnown=false;
        if(oldKnown!=focusKnown||oldFocus!=nativeFocus){UpdateInteraction();inputChanged?.Invoke();}
    }
    internal SceneryMapAlphaInputs? ReadCurrentAlphaInputs()
        => !configured||abandoned||!sourceInputOwned?null:
            new(nativePush?true:focusKnown?nativeFocus:null,false,false,false);

    internal void SetPresentationInputAdmitted(bool admitted)
    {
        presentationInputAdmitted=admitted;
        UpdateInputAdmission();
    }
    internal void CancelCommandPress()=>commandPressQualified=false;

    internal void SetAlpha(byte? textAlpha,byte? flagAlpha,bool forceRootVisible)
    {
        if(textAlpha is byte text)ApplyTextAlpha(text);
        if(flagAlpha is byte flag)ApplyFlagAlpha(flag);
        if(forceRootVisible)SetRootVisible(true);
        UpdateInputAdmission();
    }

    internal void SetPresentation(int selectedDisplay, bool rootVisible, byte textAlpha, byte flagAlpha)
    {
        display = selectedDisplay;
        bool layoutChanged = appliedLayoutDisplay != display;
        if (layoutChanged)
        {
            var children = SceneryMapMarkerSources.VisibleChildren(display);
            textRoot.Visible = children.Text;
            flagRoot.Visible = children.Flag;
        }
        ApplyTextAlpha(textAlpha);
        ApplyFlagAlpha(flagAlpha);
        if (layoutChanged)
        {
            Control selected = display == 0 ? textRoot : flagRoot;
            Rect2 hit = (display == 0 ? textLayout : flagLayout).Rectangle(1);
            overlay.Position = selected.Position + hit.Position;
            overlay.Size = hit.Size;
            UpdateHitGeometry();
            appliedLayoutDisplay = display;
        }
        SetRootVisible(rootVisible);
        UpdateInputAdmission();
        UpdateInteraction();
        ApplyInteraction();
    }

    private void ApplyTextAlpha(byte value)
    {
        if (appliedTextAlpha == value) return;
        textRoot.Modulate = new(1, 1, 1, value / 255f);
        appliedTextAlpha = value;
    }

    private void ApplyFlagAlpha(byte value)
    {
        if (appliedFlagAlpha == value) return;
        flagRoot.Modulate = new(1, 1, 1, value / 255f);
        appliedFlagAlpha = value;
    }

    private void UpdateHitGeometry()
    {
        // The name writer expands Text, not the parent source control.
        // Native mouse admission requires parent own AND selected tester.
        var testerOrigin = new Vector2I((int)overlay.Position.X, (int)overlay.Position.Y);
        overlay.SetCanonicalGeometry(new(canonicalPosition, new((int)Size.X, (int)Size.Y)),
            new(canonicalPosition + testerOrigin, new((int)overlay.Size.X, (int)overlay.Size.Y)));
    }

    internal void Abandon()
    {
        CancelCommandPress();
        abandoned = true; released = null; ApplyDisabled(true);
        overlay.ReadCanonicalPointer=null;
        pointerEnter=null;pointerLeave=null;pointerDown=null;pointerRelease=null;inputChanged=null;
        SetInteraction(4);
    }

    private void SetInteraction(int state)
    {
        if (!configured || abandoned && state != 4) return;
        if (interaction == state) return;
        interaction = state; ApplyInteraction();
    }

    private void UpdateInteraction()
    {
        if(sourceInputOwned)
        {
            if(controlEnabled&&focusOverride==0&&pushOverride==0&&!nativePush&&!focusKnown)return;
            SetInteraction(!controlEnabled||abandoned?4:pushOverride!=0||nativePush?2:
                focusOverride!=0||nativeFocus?1:0);
            return;
        }
        int pointerState = overlay.GetDrawMode() switch
        {
            BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed => 2,
            BaseButton.DrawMode.Hover => 1,
            _ => 0
        };
        SetInteraction(!controlEnabled || abandoned ? 4 : pushOverride != 0 || pointerState == 2 ? 2
            : focusOverride != 0 || pointerState == 1 ? 1 : 0);
    }

    private void UpdateInputAdmission()
        // Preparing a frame temporarily closes the controller's logical input
        // gate. Toggling BaseButton.Disabled there destroys its live press.
        // Keep the physical button state across those ordinary frame updates.
        => ApplyDisabled(abandoned || !controlEnabled || !rootVisible ||
            !SceneryMapMarkerSources.VisibleChildren(display).Flag);

    private void ApplyDisabled(bool disabled)
    {
        if(appliedDisabled==disabled)return;
        overlay.Disabled=disabled;appliedDisabled=disabled;
    }

    private void ApplyInteraction()
    {
        if (!configured || appliedInteraction == interaction) return;
        // Actual raw state4 chooses clip3_disable; there is no raw state3
        // in the common normal/focus/push/disable update path.
        int selected = interaction == 4 ? 3 : interaction;
        for (int state = 0; state < textStates.Count; state++)
        {
            textStates[state].Visible = state == selected;
            flagStates[state].Visible = state == selected;
        }
        int preset = interaction switch { 0 => 19, 1 => 4, 2 => 5, 4 => 23,
            _ => throw new InvalidDataException("Unsupported current Scenery control state.") };
        caption.Configure(font, nameProfile.WithPreset(presets[preset]));
        caption.QueueRedraw();
        appliedInteraction = interaction;
    }

    private void CreateFlagParts()
    {
        Add(3, 0, null); Add(4, 0, null);
        foreach (int index in new[] { 6, 7, 8, 9 }) Add(index, 1, palettes[5]);
        foreach (int index in new[] { 11, 12, 13, 14 }) Add(index, 2, palettes[6]);
        Add(16, 3, null); Add(17, 3, null);
        ApplyFlagVisibility();

        void Add(int index, int state, MapCornerColors? colors)
        {
            string sprite = index is 3 or 6 or 7 or 11 or 12 or 16
                ? "000_cmn_icon_scenic_outline" : "000_cmn_icon_scenic";
            flagParts.Add(index, flagLayout.Art(flagStates[state], index,
                flagLayout.Rectangle(index), assets, shader, colors, commonSpriteOverride: sprite,
                commonTexturePack: "texture_ce_common"));
            if(colors is { } known)paintedFlagColors[index]=known;
        }
    }

    private void ApplyColor(int index, MapCornerColors? colors)
    {
        if(colors is not { } known)return;
        if(paintedFlagColors.TryGetValue(index,out var applied)&&applied==known)return;
        paintedFlagColors[index]=known;
        foreach(var sprite in flagParts[index].GetChildren().OfType<UiSourceSprite>())sprite.SetColors(known);
    }

    private void ApplyFlagVisibility()
    {
        foreach(var (index,part) in flagParts)
        {
            bool visible=paintedFlagColors.ContainsKey(index) &&
                (index is not (3 or 6 or 7 or 11 or 12 or 16) || paintedShowOutline is true);
            if(appliedFlagVisibility.TryGetValue(index,out bool applied)&&applied==visible)continue;
            part.Visible=visible;
            appliedFlagVisibility[index]=visible;
        }
    }

    private static UiSourceLayout Layout(MapMarkerLayout source,
        IReadOnlyDictionary<int, UiSourceNodeGeometry>? geometry = null)
        => new(new LoadedLayout(source.Layout, source.Source), geometry);

    private static Control Group(Control parent, int state)
    {
        var group = new Control { Name = "State" + state, MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(group); return group;
    }

    public override void _Process(double delta)
    {
        if (configured && !abandoned) UpdateInteraction();
    }

    public override void _ExitTree() => Abandon();
}
