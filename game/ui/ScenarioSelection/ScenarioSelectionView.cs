using Godot;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

// A null portrait means a known invalid/absent source person. Unresolved
// bank10 artwork or table coordinates must be resolved before this boundary.
internal sealed record ScenarioSelectionPreviewPortrait(LoadedSprite Sprite, Vector2I Center);
internal sealed record ScenarioSelectionPreview(string Description,
    IReadOnlyList<ScenarioSelectionPreviewPortrait?> Portraits);

/// <summary>The ordinary scenario page's presentation and retirement. The
/// application supplies catalog/history/capability knowledge and resolves
/// activation requests; configuring artwork does not make its state ready.</summary>
internal partial class ScenarioSelectionView : Control
{
    private Control canvas = null!, body = null!, footer = null!, guide = null!;
    private UiSourceHeader title = null!;
    private UiSourceRoundButton back = null!;
    private UiSourceParagraph description = null!;
    private ScenarioSelectionList list = null!;
    private readonly UiSourcePortrait?[] portraits = new UiSourcePortrait?[3];
    private readonly LoadedSprite?[] portraitSprites = new LoadedSprite?[3];
    private readonly Control[] portraitHosts = new Control[3];
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private Func<int, ScenarioSelectionPreview>? resolvePreview;
    private Func<bool>? inputAdmitted;
    private Action<int>? activationRequested;
    private Action? returnRequested, retired;
    private Action<Exception>? failed;
    private bool configured, closing, abandoned;

    internal int? PreviewScenarioId => list?.PreviewScenarioId;
    internal int? RequestedScenarioId { get; private set; }

    internal void ConfigureOriginalCatalog(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string locale, ScenarioSelectionCatalog catalog, Func<S14Entry, short, LoadedSprite> resolvePortrait,
        bool inputGuidesVisible, Func<bool> inputAdmitted, Action<int> activationRequested, Action returnRequested,
        Action retired, Action<Exception> failed, UiSourceHeaderStack headers)
    {
        ArgumentNullException.ThrowIfNull(resolvePortrait);
        Configure(repository, assets, shader, font, locale, catalog.Rows, ScenarioSelectionPlacement.Mounts,
            id => ScenarioSelectionPlacement.Read(catalog.Scenario(id), resolvePortrait), inputGuidesVisible,
            inputAdmitted, activationRequested, returnRequested, retired, failed, headers);
    }

    internal void ConfigureCatalog(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string locale, ScenarioSelectionCatalog catalog, IReadOnlyList<int> mountIndices,
        Func<S14Entry, IReadOnlyList<ScenarioSelectionPreviewPortrait?>> resolvePortraits, bool inputGuidesVisible,
        Func<bool> inputAdmitted, Action<int> activationRequested, Action returnRequested,
        Action retired, Action<Exception> failed, UiSourceHeaderStack headers)
    {
        ArgumentNullException.ThrowIfNull(resolvePortraits);
        Configure(repository, assets, shader, font, locale, catalog.Rows, mountIndices,
            id => { var scenario = catalog.Scenario(id); return new(scenario.Description, resolvePortraits(scenario)); },
            inputGuidesVisible, inputAdmitted, activationRequested, returnRequested, retired, failed, headers);
    }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string locale, IReadOnlyList<ScenarioSelectionEntry> directory, IReadOnlyList<int> mountIndices,
        Func<int, ScenarioSelectionPreview> resolvePreview, bool inputGuidesVisible,
        Func<bool> inputAdmitted, Action<int> activationRequested, Action returnRequested,
        Action retired, Action<Exception> failed, UiSourceHeaderStack headers)
    {
        if (configured || canvas is not null) throw new InvalidOperationException("The scenario page is already configured.");
        ArgumentNullException.ThrowIfNull(resolvePreview);
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        ArgumentNullException.ThrowIfNull(activationRequested);
        ArgumentNullException.ThrowIfNull(returnRequested);
        ArgumentNullException.ThrowIfNull(retired);
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(headers);
        if (directory.Select(row => row.ScenarioId).Distinct().Count() != directory.Count)
            throw new ArgumentException("Scenario identities must be distinct.", nameof(directory));
        this.repository = repository; this.assets = assets; this.shader = shader;
        this.resolvePreview = resolvePreview; this.inputAdmitted = inputAdmitted;
        this.activationRequested = activationRequested; this.returnRequested = returnRequested;
        this.retired = retired; this.failed = failed;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        canvas = new Control { Name = "ScenarioCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        body = new Control { Name = "ScenarioBody", Size = canvas.Size, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(body);
        var source = new UiSourceLayout(repository, 0xEE041FF9);
        var background = new UiSourceLayout(repository, 0x53910979);
        Vector2 backgroundMount = source.Rectangle(1).Position;
        // Preset node1 has no local artwork. Main2/24 and the replaced
        // placeholder subtrees are intentionally not instantiated.
        background.Art(body, 2, background.Rectangle(2, backgroundMount), assets, shader);
        background.Art(body, 3, background.Rectangle(3, backgroundMount), assets, shader);

        var descriptionSource = new UiSourceLayout(repository, 0x09C88359);
        var descriptionHost = new Control { Name = "ScenarioDescription", Position = source.Rectangle(22).Position,
            Size = descriptionSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(descriptionHost);
        descriptionSource.Blur(descriptionHost, 1, descriptionSource.Rectangle(1));
        descriptionSource.Art(descriptionHost, 2, descriptionSource.Rectangle(2), assets, shader);
        Rect2 textBox = descriptionSource.Rectangle(3);
        description = new UiSourceParagraph { Name = "DescriptionText", Position = textBox.Position, Size = textBox.Size };
        descriptionHost.AddChild(description);
        description.Configure(font, MapFontProfile.FromNode(descriptionSource.Nodes[3], descriptionSource.Source.Source.ResourceUri));

        // Three fixed slots retain their relative ordering across rebinds.
        // A resource change replaces only the quad inside its own slot.
        var portraitSource = new UiSourceLayout(repository, 0xB1FE7E6D);
        for (int index = 0; index < portraitHosts.Length; index++)
        {
            var host = new Control { Name = "ScenarioPortraitSlot" + index, Size = portraitSource.Rectangle(0).Size,
                MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            body.AddChild(host); portraitHosts[index] = host;
        }

        list = new ScenarioSelectionList { Name = "ScenarioRows", Position = source.Rectangle(3).Position };
        body.AddChild(list);
        list.Configure(repository, assets, shader, font, locale, directory, mountIndices,
            id => { if (!configured) ApplyPreview(id); else RunInput(() => ApplyPreview(id)); },
            id => RunInput(() => { RequestedScenarioId = id; this.activationRequested!(id); }), ReadInputAdmission);

        var footerSource = new UiSourceLayout(repository, 0xD8913AB2);
        footer = new Control { Name = "ScenarioFooter", Size = footerSource.Rectangle(0).Size,
            Position = new(0, 1080 - footerSource.Rectangle(0).Size.Y), MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(footer);
        back = new UiSourceRoundButton { Name = "ReturnScenario", Position = footerSource.Rectangle(1).Position,
            ActionMode = BaseButton.ActionModeEnum.Release };
        footer.AddChild(back);
        back.ConfigureScenarioReturn(repository, assets, shader, locale, () => RunInput(() => this.returnRequested!()));
        back.InputAdmission = ReadInputAdmission;
        var buttonSource = new UiSourceLayout(repository, 0x2F276C90);
        Rect2 guideBox = buttonSource.Rectangle(18);
        guide = new Control { Name = "ReturnInputGuide", Position = guideBox.Position, Size = guideBox.Size,
            MouseFilter = MouseFilterEnum.Ignore };
        back.AddChild(guide);
        var pad = new UiSourceLayout(repository, 0x95761708);
        pad.Art(guide, 1, pad.Rectangle(1), assets, shader, commonSpriteOverride: "base_pad", commonTexturePack: "texture_pad");
        pad.Art(guide, 2, pad.Rectangle(2), assets, shader, commonSpriteOverride: "pad_cross", commonTexturePack: "texture_pad");
        SetInputGuidesVisible(inputGuidesVisible);
        string caption = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records[0x578].Text
            ?? throw new InvalidDataException("Missing scenario selection title.");
        title = new UiSourceHeader { Name = "ScenarioTitle" };
        canvas.AddChild(title);
        title.Configure(repository, assets, shader, font, caption);
        title.Publish(headers);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Resized += Fit; Fit();
        configured = true;
    }

    private void ApplyPreview(int id)
    {
        var preview = resolvePreview!(id) ?? throw new InvalidDataException("Scenario preview is unresolved.");
        if (preview.Portraits.Count != portraits.Length)
            throw new InvalidDataException("A scenario preview requires three resolved source person slots.");
        description.Apply(preview.Description);
        for (int index = 0; index < portraits.Length; index++)
        {
            if (preview.Portraits[index] is not { } next) { portraitHosts[index].Hide(); continue; }
            if (!ReferenceEquals(portraitSprites[index], next.Sprite))
            {
                var replacement = new UiSourcePortrait { Name = "ScenarioPortrait" + index };
                try { replacement.ConfigureScenarioPreview(repository, assets, shader, next.Sprite); }
                catch { replacement.Free(); throw; }
                Release(portraits[index]);
                portraits[index] = replacement;
                portraitSprites[index] = next.Sprite;
                portraitHosts[index].AddChild(replacement);
            }
            var portrait = portraits[index]!;
            portraitHosts[index].Position = new Vector2I(unchecked(next.Center.X - (int)portrait.Size.X / 2),
                unchecked(next.Center.Y - (int)portrait.Size.Y / 2));
            portraitHosts[index].Show();
        }
    }

    internal void SetInputGuidesVisible(bool visible) => guide.Visible = visible;

    // The application resolves its real device/cancel input. Use the same
    // source action and feedback without fabricating a Godot Pressed signal.
    internal void ProcessSourceCancel(bool requested)
    {
        if (requested && ReadInputAdmission()) back.ActivateFromSource();
    }

    private bool ReadInputAdmission()
    {
        if (!configured || closing || abandoned || !IsInsideTree() || !IsVisibleInTree() || !CanProcess()) return false;
        try { return inputAdmitted!(); }
        catch (Exception error) { Fail(error); return false; }
    }

    private void RunInput(Action action)
    {
        if (!ReadInputAdmission()) return;
        try { action(); }
        catch (Exception error) { Fail(error); }
    }

    private void Fail(Exception error)
    {
        var notify = failed;
        Abandon();
        notify?.Invoke(error);
    }

    // Selection/return callbacks only submit intent. The actual application
    // owner invokes retirement when its own transition has been admitted.
    internal void Retire()
    {
        if (!configured || closing || abandoned) return;
        closing = true;
        RetireParts(); Hide(); QueueFree();
    }

    internal void Abandon()
    {
        abandoned = true;
        ClearRequests();
        RetireParts(); Hide();
        if (!IsQueuedForDeletion()) QueueFree();
    }

    private void RetireParts()
    {
        // The real leave path releases body and all three previews, footer,
        // then the exact header. Tree traversal order alone is insufficient.
        Release(body); Release(footer); Release(title);
    }

    private static void Release(Control? control)
    {
        if (control is null || !GodotObject.IsInstanceValid(control)) return;
        control.Hide();
        if (control.GetParent() is Node parent) parent.RemoveChild(control);
        if (!control.IsQueuedForDeletion()) control.QueueFree();
    }

    private void ClearRequests()
    {
        resolvePreview = null; inputAdmitted = null; activationRequested = null;
        returnRequested = null; retired = null; failed = null;
    }

    public override void _ExitTree()
    {
        var notify = closing && !abandoned ? retired : null;
        configured = false; RequestedScenarioId = null;
        ClearRequests();
        Array.Clear(portraits); Array.Clear(portraitSprites); Array.Clear(portraitHosts);
        if (notify is not null) Callable.From(() => { if (!abandoned) notify(); }).CallDeferred();
    }

    private void Fit()
    {
        if (canvas is null) return;
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * factor;
        canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }
}
