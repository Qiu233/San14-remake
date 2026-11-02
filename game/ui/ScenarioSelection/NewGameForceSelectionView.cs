using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record NewGameForcePageHeader(string Year, string Month, string Title,
    LoadedSprite? EditionBadge, bool ScenarioTypeFlag, bool? CompletedMarkVisible);

/// <summary>The original startup force page. A single captured publication
/// supplies every preview; hovering never changes the official selection.</summary>
internal partial class NewGameForceSelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceLayout source = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private NewGameForceSelectionPreview publication = null!;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private UiSourceParagraph annotation = null!;
    private Control body = null!, portraitHost = null!;
    private Control? flag;
    private TextureRect? scenarioMap;
    private NewGameForceFaceList faces = null!;
    private UiSourceBottomButton? government;
    private Action<int>? governmentRequested;
    private UiSourceBottomButton? officers;
    private Action<int>? officersRequested;
    private UiSourceBottomButton? forceOverview;
    private Action? forceOverviewRequested;
    private UiSourceRoundButton? abort;
    private NewGameGeographicAdvantagesButton? geographicAdvantages;
    private Action? abortRequested;
    private Action<NewGameForcePreview>? geographicAdvantagesRequested;
    private Control? rulerPortrait;
    private int? renderedPortraitForce;
    private Func<NewGameForcePreview, Control?>? createRulerPortrait;
    private Func<bool>? admitted;
    private Action<int>? selectionChanged, accepted;
    private Action<int?>? previewChanged;
    private Action? returned;
    private bool abandoned, busy;
    internal int? SelectedForceId { get; private set; }
    internal int? PreviewForceId { get; private set; }
    internal NewGameForceSelectionPreview Publication => publication;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        NewGameForceSelectionPreview publication, NewGameForcePageHeader header,
        IReadOnlyList<NewGameForceFaceContent> faceContent, Texture2D? map,
        Func<NewGameForcePreview, Control?> createRulerPortrait,
        Func<bool> admitted, Action<int> selectionChanged, Action<int?> previewChanged,
        Action<int> accepted, Action returned, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository = repository; this.text = text; this.publication = publication;
        this.createRulerPortrait = createRulerPortrait; this.admitted = admitted;
        this.selectionChanged = selectionChanged; this.previewChanged = previewChanged;
        this.accepted = accepted; this.returned = returned;
        Name = "NewGameForceSelection"; ZIndex = 2; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x36D, RequestBack, RequestConfirm, threeButtonFooter: true, resources: resources);
        var messages = new AdministrationMessages(repository, text.SourceLocale);
        chrome.ApplyDefaultHint(messages.Format(0x2739));
        chrome.ApplyReturnHint(messages.Format(0x2733));
        body = new Control { Name = "ForcePageBody", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body); chrome.Canvas.MoveChild(body, 0);
        ApplyMap(map);
        source = new(repository, 0xB468AE65);
        var background = new UiSourceLayout(repository, 0x53910979);
        foreach (int node in new[] { 2, 3 })
            background.Art(body, node, background.Rectangle(node, source.Rectangle(1).Position), chrome.Assets, chrome.SpriteShader,
                sourceMaterial: true);

        portraitHost = new Control { Name = "RulerPortrait", Position = source.Rectangle(2).Position,
            Size = source.Rectangle(2).Size, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(portraitHost);
        // Replaced source subtrees2/7/12/79 are configured explicitly below.
        // Header shine and independent animation tracks remain deferred.
        foreach (int node in new[] { 4, 6, 8, 49, 50, 51, 52, 53 })
        {
            if (source.Nodes[node].Type == 6) source.Blur(body, node, source.Rectangle(node));
            else source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        }
        source.Art(body, 55, source.Rectangle(55), chrome.Assets, chrome.SpriteShader);
        flag = source.Art(body, 56, source.Rectangle(56), chrome.Assets, chrome.SpriteShader);
        source.Art(body, 57, source.Rectangle(57), chrome.Assets, chrome.SpriteShader);

        ConfigureScenarioHeader(header);
        var captions = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        int[] captionNodes = [61, 63, 65, 67, 69, 71, 75, 77];
        int[] captionIds = [0x20C, 0x20D, 0x20E, 0x21B, 0x207, 0x218, 0x83E, 0x2E1];
        var presets = MapTextPresets.Create();
        for (int index = 0; index < captionNodes.Length; index++)
            source.Text(body, captionNodes[index], default,
                captions.Records[captionIds[index]].Text ?? throw new InvalidDataException("Missing force-page caption."),
                chrome.Font, presets[13]);
        foreach (int node in new[] { 58, 60, 62, 64, 66, 68, 70, 72, 73, 74, 76, 78 })
        {
            if (node == 74)
            {
                // The annotation is an authored paragraph: its actual messages
                // contain LF and colour7, followed by ordinary literal glyphs.
                Rect2 box = source.Rectangle(node);
                annotation = new UiSourceParagraph { Name = source.Nodes[node].Name,
                    Position = box.Position, Size = box.Size };
                body.AddChild(annotation);
                var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)
                    .WithPreset(presets[0]);
                annotation.Configure(chrome.Font, profile with { RawCharacterMode = 2 });
            }
            else fields[node] = source.Text(body, node, default, "", chrome.Font, presets[node == 73 ? 41 : 0]);
        }
        faces = new NewGameForceFaceList { Name = "RulerFaceList", Position = source.Rectangle(12).Position };
        body.AddChild(faces);
        faces.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, faceContent, null,
            PreviewForce, SelectForce, ReadAdmission);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("scenario_annotation_available", false);
        if (publication.DefaultForceId is int first) SelectForce(first);
        else { chrome.ApplyDecision(false, true); ApplyPreview(null); }
    }

    private void ConfigureScenarioHeader(NewGameForcePageHeader content)
    {
        NewGameScenarioHeader.Configure(body, source.Rectangle(7).Position,
            repository, text.SourceLocale, chrome.Assets, chrome.SpriteShader, chrome.Font, content);
    }

    internal void SelectForce(int forceId)
    {
        if (!ReadAdmission() || !publication.ByForceId.TryGetValue(forceId, out var row) || row.ForceValid != true) return;
        SelectedForceId = forceId; PreviewForceId = forceId;
        faces.ApplySelected(row.RulerId);
        ApplyPreview(row);
        chrome.ApplyDecision(row.CanConfirm == true, true);
        if (government is not null) government.Disabled = row.CanOpenGovernment != true;
        if (officers is not null) officers.Disabled = row.CanOpenOfficers != true;
        selectionChanged?.Invoke(forceId);
        previewChanged?.Invoke(forceId);
    }

    internal void PreviewForce(int? forceId)
    {
        if (!ReadAdmission()) return;
        int? effective = forceId ?? SelectedForceId;
        if (PreviewForceId == effective) return;
        PreviewForceId = effective;
        ApplyPreview(effective is int id ? publication.ByForceId.GetValueOrDefault(id) : null);
        previewChanged?.Invoke(effective);
    }

    private void ApplyPreview(NewGameForcePreview? row)
    {
        if (geographicAdvantages is not null)
            geographicAdvantages.Disabled = busy || row?.CanOpenGeographicAdvantages != true;
        if (row is null || row.ForceValid == false)
        {
            // 521110's invalid receiver clears the value panel and hides the
            // portrait. It does not replace missing numbers with zeroes.
            foreach (int node in new[] { 62, 64, 66, 68, 70, 72, 73, 76, 78 }) fields[node].Text = "";
            annotation.Apply("");
            portraitHost.Hide();
            return;
        }
        portraitHost.Show();
        string Unknown(string? value) => value ?? text.Get("detail_unknown");
        string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");
        fields[58].Text = row?.ForceDisplayName ?? "";
        fields[60].Text = Unknown(row?.RulerName);
        fields[62].Text = Number(row?.Gold); fields[64].Text = Number(row?.Food);
        fields[66].Text = Number(row?.Troops); fields[68].Text = Unknown(row?.RankName);
        fields[70].Text = Number(row?.CityCount); fields[72].Text = Number(row?.ActiveOfficerCount);
        fields[73].Text = Unknown(row?.DoctrineName);
        annotation.Apply(row?.ScenarioAnnotation ?? "");
        fields[76].Text = Unknown(row?.HeavenTiming);
        fields[78].Text = Number(row?.GeographicAdvantageCount);
        SetMeta("scenario_annotation_available", row?.ScenarioAnnotation is not null);
        if (row?.FlagAppearance is { } appearance && flag is { } flagNode)
        {
            var fill = appearance.Fill;
            foreach (var image in flagNode.GetChildren().OfType<UiSourceSprite>())
                image.SetColors(new(fill, fill, fill, fill));
            flagNode.Show();
            uint color = appearance.Letter.CpuPacked;
            var profile = MapFontProfile.FromNode(source.Nodes[58], source.Source.Source.ResourceUri)
                .WithPreset(MapTextPresets.Create()[0]);
            fields[58].Configure(chrome.Font, profile with { ColourLT = color, ColourRT = color, ColourLB = color, ColourRB = color });
        }
        else flag?.Hide();
        if (renderedPortraitForce == row?.ForceId) return;
        renderedPortraitForce = row?.ForceId;
        if (rulerPortrait is not null) { portraitHost.RemoveChild(rulerPortrait); rulerPortrait.QueueFree(); rulerPortrait = null; }
        if (row.RulerValid != true || createRulerPortrait?.Invoke(row) is not { } next) return;
        portraitHost.AddChild(next); rulerPortrait = next;
    }

    // The host owns the normal scenario raster. Updating a preview borrows its
    // texture without recapturing the force publication or changing selection.
    internal void ApplyMap(Texture2D? texture)
    {
        if (abandoned) return;
        if (scenarioMap is null && texture is not null)
        {
            scenarioMap = new TextureRect { Name = "ScenarioMap", Size = new(1920, 1080),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,TextureFilter=TextureFilterEnum.Nearest,
                StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(scenarioMap); body.MoveChild(scenarioMap, 0);
            AttachSourceMapInput(scenarioMap);
        }
        if (scenarioMap is not null) scenarioMap.Texture = texture;
    }

    internal void RequestConfirm()
    { if (ReadAdmission() && SelectedForceId is int id && publication.ByForceId.GetValueOrDefault(id)?.CanConfirm == true) accepted?.Invoke(id); }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void SetBusy(bool value)
    {
        busy = value; chrome.ApplyReturn(!value);
        chrome.ApplyDecision(!value && SelectedForceId is int id && publication.ByForceId.GetValueOrDefault(id)?.CanConfirm == true, true);
        if (government is not null) government.Disabled = value || SelectedForceId is not int force ||
            publication.ByForceId.GetValueOrDefault(force)?.CanOpenGovernment != true;
        if (officers is not null) officers.Disabled = value || SelectedForceId is not int officerForce ||
            publication.ByForceId.GetValueOrDefault(officerForce)?.CanOpenOfficers != true;
        if (abort is not null) abort.Disabled = value;
        if (forceOverview is not null) forceOverview.Disabled = value;
        if (geographicAdvantages is not null) geographicAdvantages.Disabled = value || PreviewForceId is not int preview ||
            publication.ByForceId.GetValueOrDefault(preview)?.CanOpenGeographicAdvantages != true;
    }
    internal void ConfigureGovernment(Action<int> requested)
    {
        governmentRequested = requested;
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        government = new UiSourceBottomButton { Name = "PreviewForceGovernment",
            Position = footer.Rectangle(17, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(government);
        string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1]
            .Records[0x242].Text ?? throw new InvalidDataException("Missing force government caption.");
        government.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, () =>
        {
            if (ReadAdmission() && SelectedForceId is int force &&
                publication.ByForceId.GetValueOrDefault(force)?.CanOpenGovernment == true) governmentRequested?.Invoke(force);
        });
        SetBusy(busy);
    }
    internal void ConfigureNavigation(Action aborted, Action<NewGameForcePreview> geographicRequested)
    {
        abortRequested = aborted; geographicAdvantagesRequested = geographicRequested;
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        abort = new UiSourceRoundButton { Name = "AbortNewGame", Position = footer.Rectangle(3, new(0, 904)).Position,
            ActionMode = BaseButton.ActionModeEnum.Release, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(abort);
        abort.ConfigureAbort(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale,
            () => { if (ReadAdmission()) abortRequested?.Invoke(); });
        var messages = new AdministrationMessages(repository, text.SourceLocale);
        abort.MouseEntered += () => { if (ReadAdmission()) chrome.ApplyHoverHint(messages.Format(0x273E)); };
        abort.MouseExited += () => chrome.ApplyHoverHint(null);
        geographicAdvantages = new NewGameGeographicAdvantagesButton { Name = "PreviewGeographicAdvantages", InputAdmission = ReadAdmission };
        body.AddChild(geographicAdvantages);
        string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1]
            .Records[0x35F].Text ?? throw new InvalidDataException("Missing advantage list caption.");
        geographicAdvantages.Configure(source, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, () =>
        {
            if (ReadAdmission() && PreviewForceId is int preview &&
                publication.ByForceId.GetValueOrDefault(preview) is { CanOpenGeographicAdvantages: true } row)
                geographicAdvantagesRequested?.Invoke(row);
        });
        geographicAdvantages.MouseEntered += () => { if (ReadAdmission()) chrome.ApplyHoverHint(messages.Format(0x273A)); };
        geographicAdvantages.MouseExited += () => chrome.ApplyHoverHint(null);
        SetBusy(busy);
    }
    internal void ConfigureOfficers(Action<int> requested)
    {
        officersRequested = requested;
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        officers = new UiSourceBottomButton { Name = "PreviewForceOfficers",
            Position = footer.Rectangle(18, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(officers);
        string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1]
            .Records[0x371].Text ?? throw new InvalidDataException("Missing subordinate officer caption.");
        officers.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, () =>
        {
            if (ReadAdmission() && SelectedForceId is int force &&
                publication.ByForceId.GetValueOrDefault(force)?.CanOpenOfficers == true) officersRequested?.Invoke(force);
        });
        SetBusy(busy);
    }
    internal void ConfigureForceOverview(Action requested)
    {
        forceOverviewRequested = requested;
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        forceOverview = new UiSourceBottomButton { Name = "PreviewForceOverview",
            Position = footer.Rectangle(14, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(forceOverview);
        string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1]
            .Records[0x36E].Text ?? throw new InvalidDataException("Missing force overview caption.");
        forceOverview.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
            () => { if (ReadAdmission()) forceOverviewRequested?.Invoke(); });
        var messages = new AdministrationMessages(repository, text.SourceLocale);
        forceOverview.MouseEntered += () => { if (ReadAdmission()) chrome.ApplyHoverHint(messages.Format(0x273C)); };
        forceOverview.MouseExited += () => chrome.ApplyHoverHint(null);
        SetBusy(busy);
    }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void Abandon()
    {
        RetireSourceMapInput();
        abandoned = true; admitted = null; createRulerPortrait = null;
        selectionChanged = null; previewChanged = null; accepted = null; returned = null;
        governmentRequested = null;
        officersRequested = null;
        forceOverviewRequested = null;
        if (forceOverview is not null) { forceOverview.InputAdmission = null; forceOverview.Disabled = true; }
        if (officers is not null) { officers.InputAdmission = null; officers.Disabled = true; }
        abortRequested = null; geographicAdvantagesRequested = null;
        if (abort is not null) { abort.InputAdmission = null; abort.Disabled = true; }
        if (geographicAdvantages is not null) { geographicAdvantages.InputAdmission = null; geographicAdvantages.Disabled = true; }
        if (government is not null) { government.InputAdmission = null; government.Disabled = true; }
        if (scenarioMap is not null) scenarioMap.Texture = null;
        faces?.Abandon(); chrome?.Abandon(); Hide();
    }
    public override void _ExitTree()
    {
        if (scenarioMap is not null) scenarioMap.Texture = null;
        admitted = null; createRulerPortrait = null; selectionChanged = null; previewChanged = null; accepted = null; returned = null;
        governmentRequested = null;
        officersRequested = null;
        forceOverviewRequested = null;
        abortRequested = null; geographicAdvantagesRequested = null;
    }
}
