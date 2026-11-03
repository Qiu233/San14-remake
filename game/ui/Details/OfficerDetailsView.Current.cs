using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

public partial class OfficerDetailsView
{
    private GameResourceRepository currentRepository = null!;
    private PreviewText currentText = null!;
    private OfficerDetailPublication publication = null!;
    private UiAssets? currentAssets;
    private MapFont? currentFont;
    private Shader? currentShader;
    private Control currentCanvas = null!;
    private Control currentBodyCanvas = null!;
    private readonly Control?[] currentPages = new Control?[3];
    private readonly List<UiSourceCommandButton> pageButtons = [];
    private readonly Dictionary<(string Pack, string Name), LoadedSprite> detailSprites = [];
    private UiSourceSimpleTooltip? detailTooltip;
    private Func<bool>? currentAdmission;
    private Action<int>? prioritySelected;
    private int selectedPage;
    private UiSourceDetailSelector? currentSelector;
    internal int CurrentOfficerId => publication.OfficerId;
    internal void SetPageNavigationLayer(int layer)
    {
        foreach (var button in pageButtons) button.ZIndex = layer;
    }
    internal OfficerDetailSelectionReturn ReadSelectionReturn() => currentSelector?.ReadOfficerReturn(publication.OfficerId)
        ?? new([publication.OfficerId],publication.OfficerId,null);
    private int AvailablePages => publication.PageCount.Available ? publication.PageCount.Value : 1;
    private bool currentConfigured;
    private bool ownsCurrentResources=true;
    private string? captiveAdviceAtSource;
    private static readonly uint[] PersonPageLayouts = [0x9EA0EB77, 0xA7BDE2D2, 0x7D57CF94];
    private UiAssets CurrentAssets => currentAssets!;
    private MapFont CurrentFont => currentFont!;
    private Shader CurrentShader => currentShader!;

    internal void Configure(GameResourceRepository repository, OfficerDetailPublication view,
        PreviewText text, Action close, Func<bool>? admitted = null, Action<int>? choosePriority = null,
        bool showNavigation = true, InformationDetailSelectionContext? selection = null, Action<int>? select = null,
        AdministrationPageChrome.Resources? resources = null, bool showPageButtons = false, string? captiveAdviceAtSource = null)
    {
        currentConfigured = true;
        currentRepository = repository; currentText = text; publication = view;
        currentAdmission = admitted; prioritySelected = choosePriority;this.captiveAdviceAtSource=captiveAdviceAtSource;
        ownsCurrentResources=resources is null;
        currentAssets = resources?.Assets ?? new(repository); currentFont = resources?.Font ?? new(repository, text.SourceLocale);
        currentShader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();
        if (showNavigation) ConfigureCommonBackdrop(repository);
        UiSourceHeader? header = showNavigation ? new() { Name = "OfficerDetailsTitle" } : null;
        ConfigureSourcePage("OfficerDetails", header, close);
        currentCanvas = new() { Name = "OfficerSourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(currentCanvas);
        var bodyMotion = new Node2D { Name = "OfficerDetailsBodyMotion" }; currentCanvas.AddChild(bodyMotion);
        currentBodyCanvas = new() { Name = "OfficerDetailsBody", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        bodyMotion.AddChild(currentBodyCanvas);
        var headerMotion = new Node2D { Name = "OfficerDetailsHeaderMotion" }; currentCanvas.AddChild(headerMotion);
        var footerMotion = new Node2D { Name = "OfficerDetailsFooterMotion" }; currentCanvas.AddChild(footerMotion);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(currentBodyCanvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(currentBodyCanvas, 3, shell.Rectangle(3, new(191, 147)), CurrentAssets, CurrentShader);
        if (header is not null)
        {
            headerMotion.AddChild(header);
            header.Configure(repository, CurrentAssets, CurrentShader, CurrentFont, PersonMessage(0x468));
            var footer = new UiSourceLayout(repository, 0xD8913AB2);
            var back = new UiSourceRoundButton { Name = "CloseOfficerDetails", Position = footer.Rectangle(1, new(0, 904)).Position,
                InputAdmission = DetailInputAdmitted };
            footerMotion.AddChild(back);
            back.ConfigureScenarioReturn(repository, CurrentAssets, CurrentShader, text.SourceLocale, RequestClose);
        }
        if (showNavigation || showPageButtons)
        {
            float x = 188;
            int[] captions = [0x3CD, 0x452, 0x297];
            for (int page = 0; page < 3; page++)
            {
                int target = page;
                string caption = PersonMessage(captions[page]);
                float width = Math.Max(96, caption.Length * 28 + 20);
                var button = new UiSourceCommandButton { Name = "OfficerPage" + page,
                    Position = new(x, 889), Size = new(width, 48), InputAdmission = DetailInputAdmitted };
                footerMotion.AddChild(button);
                button.Configure(repository, CurrentAssets, CurrentShader, CurrentFont, caption,
                    () => { if (DetailInputAdmitted()) ShowCurrentPage(target); });
                pageButtons.Add(button); x += width;
            }
        }
        if (showNavigation && selection is not null)
        {
            currentSelector = new(); currentBodyCanvas.AddChild(currentSelector);
            // The selector's transparent full-canvas host must remain below
            // the footer commands so it cannot consume their pointer input.
            currentBodyCanvas.MoveChild(currentSelector, 2);
            currentSelector.Configure(repository, CurrentAssets, CurrentShader, CurrentFont, text,
                selection, InformationListKind.Officers, view.OfficerId, DetailInputAdmitted, select);
            // Only the selector's actual header, rows and scrollbar accept
            // input. Its empty full-canvas host must not cover body controls.
            currentSelector.MouseFilter = MouseFilterEnum.Ignore;
        }
        UpdatePageAvailability();
        detailTooltip = new(); currentBodyCanvas.AddChild(detailTooltip);
        detailTooltip.Configure(repository, CurrentAssets, CurrentShader, CurrentFont);
        ShowCurrentPage(0);
        if (showNavigation)
            ConfigureReturnTransition(CurrentAssets.SourceLayout(0xF9C5DB14), new Dictionary<int, (Node2D, Vector2)>
            { [1] = (bodyMotion, Vector2.Zero), [4] = (headerMotion, Vector2.Zero), [6] = (footerMotion, new(0, 904)) });
        Resized += FitCurrentCanvas; FitCurrentCanvas();
        SetMeta("officer_id", view.OfficerId);
        SetMeta("current_unresolved_owners", view.UnresolvedOwners.ToArray());
        SetMeta("source_shell", shell.Source.Source.ResourceUri);
    }

    // A real accepted command replaces this publication. Navigation itself
    // never asks for another world capture or recalculates a value.
    internal void ApplyCurrent(OfficerDetailPublication view)
    {
        if (!currentConfigured || view.OfficerId != publication.OfficerId)
            throw new InvalidOperationException("Officer details cannot change their inspected identity.");
        ReplaceCurrentPublication(view);
    }

    // A caller-owned candidate replaces the inspected person, retaining the
    // active page and clamping it to this person's actual available pages.
    internal void SelectCurrent(OfficerDetailPublication view,string? captiveAdviceAtSource=null)
    {
        if (!currentConfigured) throw new InvalidOperationException("Officer details are not configured.");
        this.captiveAdviceAtSource=captiveAdviceAtSource;
        if(view.IsNeutral)view=view with {MiniMap=publication.MiniMap};
        ReplaceCurrentPublication(view);
    }

    private void ReplaceCurrentPublication(OfficerDetailPublication view)
    {
        publication = view;
        UpdatePageAvailability();
        detailTooltip?.DisplayText(null);
        for (int page = 0; page < currentPages.Length; page++)
        {
            if (currentPages[page] is not { } old) continue;
            old.Hide(); old.GetParent()?.RemoveChild(old); old.QueueFree(); currentPages[page] = null;
        }
        ShowCurrentPage(Math.Clamp(selectedPage, 0, AvailablePages - 1));
        currentSelector?.Select(view.OfficerId);
        SetMeta("officer_id", view.OfficerId);
        SetMeta("current_unresolved_owners", view.UnresolvedOwners.ToArray());
    }

    private void UpdatePageAvailability()
    {
        if (publication.PageCount.Available && publication.PageCount.Value is not (2 or 3))
            throw new InvalidDataException("Officer detail page count must be two or three.");
        // The basic page exists in either source branch. If its owning flag
        // is unknown, keep that page and expose the missing navigation input.
        for (int page = 0; page < pageButtons.Count; page++)
            pageButtons[page].Visible = publication.PageCount.Available && page < AvailablePages;
        SetMeta("available_page_count", publication.PageCount.Available ? publication.PageCount.Value : -1);
        SetMeta("page_count_unresolved", publication.PageCount.Available ? "" : "officer-detail.page_count");
    }

    private bool DetailInputAdmitted() => currentConfigured && IsVisibleInTree() && CanProcess() &&
        !IsQueuedForDeletion() && (currentAdmission?.Invoke() ?? true);

    private void ShowCurrentPage(int page)
    {
        if (page < 0 || page >= AvailablePages) return;
        detailTooltip?.DisplayText(null);
        selectedPage = page;
        if (currentPages[page] is null)
        {
            var body = new Control { Name = "OfficerBody" + page, Position = new(191, 197), Size = new(1540, 688),
                MouseFilter = MouseFilterEnum.Ignore };
            currentBodyCanvas.AddChild(body);
            // Navigation and tooltip stay above all lazily created pages.
            currentBodyCanvas.MoveChild(body, 2);
            currentPages[page] = body;
            BuildCurrentPage(body, page);
        }
        for (int index = 0; index < 3; index++)
        {
            if (currentPages[index] is { } body) body.Visible = index == page;
            if (index < pageButtons.Count) pageButtons[index].Selected = index == page;
        }
        SetMeta("selected_page", page);
    }

    // The same actual placeholder replacement set is consumed by the
    // startup resource owner. Dynamic portraits never become dummy dependencies.
    internal static HashSet<int> CurrentPageHiddenNodes(int page,bool priorityVisible=true)
    {
        var hidden = new HashSet<int> { 2, 4, 17, page switch { 0 => 124, 1 => 100, _ => 99 } };
        if (page < 2)
        {
            hidden.Add(page == 0 ? 47 : 37);
            int traitStart = page == 0 ? 60 : 50;
            for (int i = 0; i < 5; i++) hidden.Add(traitStart + i);
        }
        if (page == 0)
        {
            hidden.Add(39); hidden.Add(43); hidden.Add(123);
            if (!priorityVisible) hidden.Add(37);
        }
        if (page == 1)
        {
            for (int index = 72; index <= 94; index++) hidden.Add(index);
            hidden.Add(101);
        }
        if (page == 2)
            foreach (int index in new[] { 24, 31, 39, 48, 54, 61 }) hidden.Add(index);
        return hidden;
    }

    private void BuildCurrentPage(Control body, int page)
    {
        var source = new UiSourceLayout(currentRepository, PersonPageLayouts[page]);
        var hidden = CurrentPageHiddenNodes(page,publication.Priority.Visible);
        AddStandingPortrait(body, source.Rectangle(2));
        PaintArtwork(source, body, default, hidden);
        var values = PageValues(page);
        foreach (var (node, message) in PageCaptions(page)) values[node] = PersonMessage(message);
        foreach (var node in source.Nodes.Where(node => node.Type == 2 && node.Index != 18 && !IsHiddenNode(source, node.Index, hidden)))
        {
            var label = source.Text(body, node.Index, default, values.GetValueOrDefault(node.Index, ""), CurrentFont);
            if (node.Index == 23 && publication.ForceFlag is { Available: true, Value: { } force })
                label.FirstGlyphColour = force.Fill.CpuPacked;
        }
        AddPersonNameplate(body, source.Rectangle(4));
        if (publication.Overview.HealthCode is { Available: true, Value: int health })
        {
            string sprite = health switch { 0 => "000_cmn_mrk_base_green", 1 => "000_cmn_mrk_base_yellow",
                2 or 3 => "000_cmn_mrk_base_red", _ => throw new InvalidDataException("Unsupported officer health marker.") };
            source.Art(body, 17, source.Rectangle(17), CurrentAssets, CurrentShader, commonSpriteOverride: sprite);
        }
        source.Text(body, 18, default, TextValue(publication.Overview.Health), CurrentFont);
        if (page < 2)
        {
            if(!publication.IsNeutral)AddAbilities(body, source.Rectangle(page == 0 ? 47 : 37));
            AddTraits(body, source, page == 0 ? 60 : 50);
        }
        if (page == 0)
        {
            AddPriority(body, source);
            var map = new UiSourceMiniMap { Position = source.Rectangle(123).Position };
            body.AddChild(map);
            map.Configure(currentRepository, CurrentAssets, CurrentShader,
                publication.MiniMap.Available ? publication.MiniMap.Value : null);
        }
        else if (page == 1) AddMilitary(body, source);
        else AddRelations(body, source);
        AddCaptiveAdvice(body,source,page);
        body.SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    // Shogu page49 fills the separately mounted source speech pane with
    // 66B6C0. Core owns its fixed trial/message expansion; this view only
    // consumes that text through the ordinary original 30D290 paragraph.
    private void AddCaptiveAdvice(Control body,UiSourceLayout page,int index)
    {
        if(captiveAdviceAtSource is not {} advice)return;
        var source=CurrentAssets.SourceLayout("11_01_02_00_info_busho_offset_00");
        Vector2 mount=page.Rectangle(index switch{0=>124,1=>100,_=>99}).Position;
        source.Art(body,1,source.Rectangle(1,mount),CurrentAssets,CurrentShader);
        Rect2 rectangle=source.Rectangle(2,mount);
        var paragraph=new UiSourceParagraph {Name="CaptiveRecruitmentAdvice",Position=rectangle.Position,Size=rectangle.Size};
        body.AddChild(paragraph);
        paragraph.ConfigureFormattedMessage(CurrentFont,MapFontProfile.FromNode(source.Nodes[2],source.Source.Source.ResourceUri));
        paragraph.Apply(advice);
    }

    private void AddPersonNameplate(Control body, Rect2 rectangle)
    {
        var source = new UiSourceLayout(currentRepository, 0x2E73723A);
        var nameplate = new Control { Name = "OfficerNameplate", Position = rectangle.Position,
            Size = rectangle.Size, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(nameplate);
        PaintArtwork(source, nameplate, default, new HashSet<int> { 4 });
        var name = source.Text(nameplate, 7, default, publication.Name, CurrentFont);
        name.Configure(CurrentFont, MapFontProfile.FromNode(source.Nodes[7], source.Source.Source.ResourceUri) with { WidthFitMode = 3 });
        var reading = source.Text(nameplate, 8, default, "", CurrentFont); // No Chinese name reading is synthesized.
        reading.Configure(CurrentFont, MapFontProfile.FromNode(source.Nodes[8], source.Source.Source.ResourceUri) with { WidthFitMode = 4 });
        source.Text(nameplate, 9, default, TextValue(publication.Overview.CourtesyName), CurrentFont);
        if (publication.ForceFlag is { Available: true, Value: { } value })
        {
            var flag = new UiSourceForceFlag { Position = source.Rectangle(4).Position };
            nameplate.AddChild(flag);
            flag.Configure(currentRepository, CurrentAssets, CurrentShader, CurrentFont,
                new(value.Letter.HasValue, value.Glyph, value.Fill, value.Letter ?? default, null));
        }
    }

    private void AddStandingPortrait(Control body, Rect2 rectangle)
    {
        if (publication.StandingPortrait is not { Available: true, Value: { } route }) return;
        var layers = route.LoadSources(CurrentAssets.Portraits.RequireSource);
        var portrait = new UiSourceStandingPortrait { Position = rectangle.Position };
        body.AddChild(portrait); portrait.Configure(currentRepository, CurrentAssets, CurrentShader, layers);
        portrait.Scale = rectangle.Size / portrait.Size;
    }

    private void AddAbilities(Control body, Rect2 rectangle)
    {
        var chart = new AreaAdministrationOfficerChart { Position = rectangle.Position };
        body.AddChild(chart); chart.Configure(currentRepository, CurrentAssets, CurrentShader, CurrentFont, currentText);
        chart.Scale = rectangle.Size / chart.Size;
        chart.Apply(publication.Abilities.Select(value => new AreaAdministrationAbilityValue(
            value.Base.Available ? value.Base.Value : null, value.Effective.Available ? value.Effective.Value : null)).ToArray());
    }

    private void AddTraits(Control body, UiSourceLayout source, int firstNode)
    {
        foreach (var trait in publication.Traits)
        {
            if (trait.Slot is < 0 or >= 5 || trait.PhysicalId is not int identity || trait.Name is not { } name || trait.Style is not byte style) continue;
            var rectangle = source.Rectangle(firstNode + trait.Slot);
            var marker = new AreaAdministrationTraitMarker { Position = rectangle.Position };
            body.AddChild(marker); marker.Configure(currentRepository, CurrentAssets, CurrentShader, CurrentFont, rectangle.Size);
            marker.Apply(identity, name, style);
            AttachTooltip(marker, trait.Description.Available ? trait.Description.Value : null);
        }
    }

    private void AddPriority(Control body, UiSourceLayout source)
    {
        if (!publication.Priority.Visible) return;
        foreach (var choice in publication.Priority.Choices)
        {
            int node = choice.Value == 1 ? 39 : 43;
            var rectangle = source.Rectangle(node);
            var button = new UiSourceRadioButton { Name = "OfficerPriority" + choice.Value,
                Position = rectangle.Position, Size = rectangle.Size, Selected = publication.Priority.SelectedValue == choice.Value,
                Disabled = !choice.Enabled || prioritySelected is null, InputAdmission = DetailInputAdmitted };
            body.AddChild(button);
            button.Configure(currentRepository, CurrentAssets, CurrentShader, CurrentFont,
                choice.Value == 1 ? UiSourceRadioSegment.Left : UiSourceRadioSegment.Right, choice.Caption,
                () => { if (DetailInputAdmitted() && choice.Enabled) prioritySelected?.Invoke(choice.Value); });
        }
    }

    private LoadedSprite DetailSprite(string pack, string name)
    {
        var key = (pack, name);
        if (!detailSprites.TryGetValue(key, out var sprite))
        { sprite = new LocalizedUiSpriteSource(currentRepository).LoadTexturePackSprite(pack, name); detailSprites.Add(key, sprite); }
        return sprite;
    }
    private string PersonMessage(int id) => currentRepository.LoadMessages(currentText.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Officer detail caption is missing.");
    private string TextValue<T>(AreaAdministrationQuery<T> value) => value.Available
        ? value.Value switch { null => "", OfficerDetailEntity entity => entity.Name,
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture), _ => value.Value.ToString() ?? "" }
        : currentText.Get("detail_unknown");
    private static bool IsHiddenNode(UiSourceLayout source, int index, IReadOnlySet<int> hidden)
    {
        for (int? at = index; at is int node; at = source.Nodes[node].ParentIndex)
            if (hidden.Contains(node)) return true;
        return false;
    }
    private void PaintArtwork(UiSourceLayout source, Control parent, Vector2 mount, IReadOnlySet<int> hidden)
    {
        foreach (var node in source.Nodes)
        {
            if (IsHiddenNode(source, node.Index, hidden)) continue;
            if (node.Type is 1 or 3) source.Art(parent, node.Index, source.Rectangle(node.Index, mount), CurrentAssets, CurrentShader);
            else if (node.Type == 6) source.Blur(parent, node.Index, source.Rectangle(node.Index, mount));
        }
    }
    private void AttachTooltip(Control owner, string? content)
    {
        if (content is null) return;
        if (content.Any(code => code == '^' || char.IsControl(code) && code != '\n'))
        {
            owner.SetMeta("tooltip_format_unresolved", true);
            GD.PushWarning("Officer detail tooltip still requires its source text formatting consumer.");
            content = currentText.Get("detail_unknown");
        }
        owner.MouseEntered += () => { if (DetailInputAdmitted()) detailTooltip?.DisplayText(content); };
        owner.MouseExited += () => detailTooltip?.DisplayText(null);
    }
    private void FitCurrentCanvas()
    {
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        currentCanvas.Scale = Vector2.One * factor;
        currentCanvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }
    public override void _Process(double delta)
    {
        if (currentConfigured && detailTooltip?.IsVisibleInTree() == true)
            detailTooltip.UpdatePointer(currentCanvas.GetLocalMousePosition());
    }
    public override void _ExitTree()
    {
        currentAdmission = null; prioritySelected = null; currentConfigured = false;
        if(ownsCurrentResources){currentFont?.Dispose(); currentShader?.Dispose(); currentAssets?.Dispose();}
        detailSprites.Clear();
        base._ExitTree();
    }
}
