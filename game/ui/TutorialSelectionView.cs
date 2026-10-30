using Godot;
using San14.Data;

/// <summary>The original eight-course title directory. Selecting a course
/// requests preparation; this page never acknowledges a lesson paragraph.</summary>
internal partial class TutorialSelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceParagraph description = null!;
    private Control rewards = null!;
    private UiSourceLayout rewardSet = null!, rewardCard = null!;
    private GameResourceRepository repository = null!;
    private TutorialCatalog catalog = null!;
    private PreviewText text = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? selected;
    private Action<Action>? runUiAction;
    private readonly List<TutorialCourseButton> buttons = [];
    private bool abandoned;
    internal int? SelectedCourseIndex { get; private set; }
    internal int PreviewCourseIndex { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text, TutorialCatalog catalog,
        Func<bool> admitted, Action returned, Action<int> selected, Action<Action> runUiAction)
    {
        this.repository = repository; this.text = text; this.catalog = catalog;
        this.admitted = admitted; this.returned = returned; this.selected = selected; this.runUiAction = runUiAction;
        Name = "TutorialSelection"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x2B, RequestBack, () => { }, returnOnly: true, compactReturn: true);
        chrome.ApplyDefaultHint(catalog.FooterTitle);
        chrome.ApplyReturnHint(catalog.FooterGuide);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, catalog.LayoutName));
        var background = new UiSourceLayout(repository, 0x53910979);
        foreach (int index in new[] { 2, 3 })
            background.Art(chrome.Canvas, index, background.Rectangle(index, source.Rectangle(1).Position), chrome.Assets, chrome.SpriteShader,
                sourceMaterial: true);
        var list = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, catalog.ButtonSetLayoutName));
        Vector2 listMount = source.Rectangle(3).Position;
        foreach (int index in new[] { 1, 2 })
            list.Art(chrome.Canvas, index, list.Rectangle(index, listMount), chrome.Assets, chrome.SpriteShader);
        var sprites = new LocalizedUiSpriteSource(repository);
        string lang = text.SourceLocale == "TC" ? "tw" : "ch";
        LoadedSprite completed = sprites.LoadTexturePackSprite("texture_lang_" + lang, lang + "_020_sho_seiha");
        foreach (var course in catalog.Courses)
        {
            int index = course.Index;
            var button = new TutorialCourseButton { Name = "TutorialCourse" + index,
                Position = list.Rectangle(catalog.ButtonMountNodes[index], listMount).Position };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, course, completed,
                () => Run(() => Preview(index)), () => Run(() => Select(index)));
            button.InputAdmission = CanUse;
        }
        // The ninth source mount has no lesson and is intentionally absent.
        var pagerSource = new UiSourceLayout(repository, 0x9668AE5A);
        var pager = new Control { Name = "TutorialPager", Position = list.Rectangle(30, listMount).Position,
            Size = pagerSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(pager);
        pagerSource.Art(pager, 1, pagerSource.Rectangle(1), chrome.Assets, chrome.SpriteShader);
        pagerSource.Text(pager, 2, default, "1/1", chrome.Font);
        foreach (bool right in new[] { false, true })
        {
            int node = right ? 5 : 3;
            var arrow = new ScenarioSelectionPageButton { Name = right ? "NextTutorialPage" : "PreviousTutorialPage",
                Position = pagerSource.Rectangle(node).Position, Disabled = true };
            pager.AddChild(arrow); arrow.Configure(repository, chrome.Assets, chrome.SpriteShader, right, () => { });
        }
        var descriptionSource = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, catalog.DescriptionLayoutName));
        Vector2 descriptionMount = source.Rectangle(22).Position;
        descriptionSource.Blur(chrome.Canvas, 1, descriptionSource.Rectangle(1, descriptionMount));
        descriptionSource.Art(chrome.Canvas, 2, descriptionSource.Rectangle(2, descriptionMount), chrome.Assets, chrome.SpriteShader);
        Rect2 textBox = descriptionSource.Rectangle(3, descriptionMount);
        description = new() { Name = "TutorialDescription", Position = textBox.Position, Size = textBox.Size };
        chrome.Canvas.AddChild(description);
        description.Configure(chrome.Font, MapFontProfile.FromNode(descriptionSource.Nodes[3], descriptionSource.Source.Source.ResourceUri));
        rewardSet = new(MapMarkerLayout.LoadNamed(repository, catalog.RewardSetLayoutName));
        rewardCard = new(MapMarkerLayout.LoadNamed(repository, catalog.RewardLayoutName));
        rewards = new() { Name = "TutorialRewards", Position = source.Rectangle(24).Position,
            Size = rewardSet.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(rewards);
        Preview(0);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("lesson_running", false);
    }

    private void Preview(int index)
    {
        PreviewCourseIndex = index;
        var course = catalog.ReadCourse(index); description.Apply(course.Description);
        foreach (Node child in rewards.GetChildren()) { rewards.RemoveChild(child); child.QueueFree(); }
        foreach (var node in rewardSet.Nodes.Where(node => node.Type is 1 or 3 or 6))
        {
            if (node.Type == 6) rewardSet.Blur(rewards, node.Index, rewardSet.Rectangle(node.Index));
            else rewardSet.Art(rewards, node.Index, rewardSet.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        }
        foreach (var reward in course.Rewards)
        {
            if (reward.OfficerId == 0 || reward.Available == false) continue;
            int mountNode = 9 - reward.Slot;
            var card = new Control { Name = "TutorialReward" + reward.Slot,
                Position = rewardSet.Rectangle(mountNode).Position, Size = rewardCard.Rectangle(0).Size,
                MouseFilter = MouseFilterEnum.Ignore };
            rewards.AddChild(card);
            foreach (int node in new[] { 6, 7, 8 })
                rewardCard.Art(card, node, rewardCard.Rectangle(node), chrome.Assets, chrome.SpriteShader);
            rewardCard.Text(card, 9, default, reward.Name + (reward.Available is null || reward.Unlocked is null ? " ?" : ""), chrome.Font);
            card.SetMeta("officer_id", reward.OfficerId);
            card.SetMeta("availability_known", reward.Available.HasValue);
            card.SetMeta("unlock_known", reward.Unlocked.HasValue);
            // The application availability/unlock owner is not manufactured
            // from prototype presence. Unknown portraits stay uninstantiated.
            if (reward.UnresolvedOwner is { } missing) card.SetMeta("unresolved_owner", missing);
        }
        SetMeta("preview_course_index", index);
    }

    private void Select(int index)
    {
        Preview(index); SelectedCourseIndex = index;
        SetMeta("selected_course_index", index); SetMeta("preparation_state", "preparing");
        chrome.ApplyDefaultHint(text.Get("tutorial_preparing"));
        selected?.Invoke(index);
    }

    internal void ApplyPreparation(TutorialLessonPreparation preparation)
    {
        if (abandoned || SelectedCourseIndex != preparation.Course.Index) return;
        bool prepared = preparation.UnresolvedOwner is null && preparation.World is not null;
        SetMeta("preparation_state", prepared ? "prepared" : "unresolved");
        SetMeta("lesson_running", false);
        if (preparation.UnresolvedOwner is { } missing) SetMeta("preparation_unresolved_owner", missing);
        else RemoveMeta("preparation_unresolved_owner");
        chrome.ApplyDefaultHint(text.Get(prepared ? "tutorial_host_unavailable" : "tutorial_prepare_failed"));
    }

    internal void ApplyPreparationFailure(string missing)
    {
        if (abandoned) return;
        SetMeta("preparation_state", "unresolved"); SetMeta("preparation_unresolved_owner", missing);
        chrome.ApplyDefaultHint(text.Get("tutorial_prepare_failed"));
    }

    private void Run(Action action) { if (CanUse()) runUiAction?.Invoke(action); }
    private bool CanUse() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (CanUse()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; selected = null; runUiAction = null;
        foreach (var button in buttons) button.InputAdmission = null;
        chrome?.Abandon(); Hide();
    }
}

/// <summary>The scenario card's actual tutorial title and completion panes.</summary>
internal partial class TutorialCourseButton : UiSourceButton
{
    private Action? preview, selected;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        TutorialCourse course, LoadedSprite completed, Action preview, Action selected)
    {
        this.preview = preview; this.selected = selected;
        var source = new UiSourceLayout(repository, 0xF2C9CA9A);
        Size = source.Rectangle(0).Size; ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        var title = source.Text(this, 23, default, course.Title, font);
        var profile = MapFontProfile.FromNode(source.Nodes[23], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state => { title.Configure(font, profile.WithPreset(presets[3 + state])); title.QueueRedraw(); });
        if (course.Completed == true)
        {
            Rect2 box = source.Rectangle(22);
            var image = new UiSourceSprite { Position = box.Position, Size = box.Size, Name = "CompletedTutorial" };
            AddChild(image); image.Configure(shader, assets.SrgbSprite(completed.Definition.Name, completed), UiSourceSprite.Blend.Ordinary);
        }
        else if (course.Completed is null)
            source.Text(this, 23, default, "?", font, rectangle: source.Rectangle(22)).Name = "TutorialCompletionUnknown";
        SetMeta("completion_known", course.Completed.HasValue); SetMeta("course_index", course.Index);
        MouseEntered += () => { if (SourceInputAdmitted) this.preview?.Invoke(); };
        FocusEntered += () => { if (SourceInputAdmitted) this.preview?.Invoke(); };
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.selected?.Invoke(); };
    }
    public override void _ExitTree() { preview = null; selected = null; InputAdmission = null; }
}
