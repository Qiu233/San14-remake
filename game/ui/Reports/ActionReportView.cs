using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The report composition and prepared strategist page lifetime. The
/// application supplies real admission/transition knowledge and decoded input;
/// dialog closure is separate from the outer turn continuation.</summary>
internal partial class ActionReportView : Control
{
    private Control canvas = null!;
    private ActionReportListsView lists = null!;
    private ActionReportStrategistView strategist = null!;
    private UiSourceReportFooter footer = null!;
    private UiAssets? ownedAssets;
    private MapFont? ownedFont;
    private Shader? ownedShader;
    private Func<byte, IReadOnlyList<string>>? preparePages;
    private Func<bool>? inputAdmitted;
    private Action? closed;
    private Action<Exception>? failed;
    private string[] pages = [];
    private bool configured, closing, abandoned;
    private bool? inputAllowed;

    internal int SelectedCategory => lists.SelectedCategory;
    internal int PageIndex { get; private set; }
    internal int PageCount => pages.Length;
    internal bool ConfirmEnabled
    {
        get => !closing && !abandoned && footer.ConfirmEnabled;
        set => footer.ConfirmEnabled = value && !closing && !abandoned;
    }

    internal void ConfigureOwned(GameResourceRepository repository, string locale,
        IReadOnlyDictionary<byte, ActionReportListContent> content,
        IReadOnlyList<LoadedSprite>? standingLayers, bool inputGuidesVisible, int scrollSpeedSetting,
        Func<int>? currentScrollSpeed, Func<CampaignHistoryRecord, ActionReportDetailRowContent> resolveDetails,
        Action<byte, int, CampaignHistoryRecord?, int> rowEvent, Func<byte, IReadOnlyList<string>> preparePages,
        Func<bool> inputAdmitted, Action closed, Action<Exception> failed, UiSourceHeaderStack headers,
        byte initialCategory = 1)
    {
        try
        {
            ownedAssets = new UiAssets(repository);
            ownedFont = new MapFont(repository, locale);
            ownedShader = UiSourceSprite.CreateShader();
            Configure(repository, ownedAssets, ownedShader, ownedFont, locale, content, standingLayers,
                inputGuidesVisible, scrollSpeedSetting, currentScrollSpeed, resolveDetails, rowEvent,
                preparePages, inputAdmitted, closed, failed, headers, initialCategory);
        }
        catch
        {
            Abandon();
            ReleaseOwnedResources();
            throw;
        }
    }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string locale, IReadOnlyDictionary<byte, ActionReportListContent> content,
        IReadOnlyList<LoadedSprite>? standingLayers, bool inputGuidesVisible, int scrollSpeedSetting,
        Func<int>? currentScrollSpeed, Func<CampaignHistoryRecord, ActionReportDetailRowContent> resolveDetails,
        Action<byte, int, CampaignHistoryRecord?, int> rowEvent, Func<byte, IReadOnlyList<string>> preparePages,
        Func<bool> inputAdmitted, Action closed, Action<Exception> failed, UiSourceHeaderStack headers,
        byte initialCategory = 1, LoadedStandingPortraitSources? preparedStandingLayers = null)
    {
        ArgumentNullException.ThrowIfNull(preparePages);
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        ArgumentNullException.ThrowIfNull(closed);
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(headers);
        this.preparePages = preparePages; this.inputAdmitted = inputAdmitted;
        this.closed = closed; this.failed = failed;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        canvas = new Control { Name = "ActionReportCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Pass };
        AddChild(canvas);
        var source = assets.SourceLayout(0xDBB537B1);
        source.Blur(canvas, 3, source.Rectangle(3));
        source.Art(canvas, 4, source.Rectangle(4), assets, shader);
        strategist = new ActionReportStrategistView { Name = "ReportStrategist", Position = source.Rectangle(85).Position };
        canvas.AddChild(strategist);
        strategist.Configure(repository, assets, shader, font, standingLayers, preparedStandingLayers);
        lists = new ActionReportListsView { Name = "ReportLists" };
        canvas.AddChild(lists);
        lists.Configure(repository, assets, shader, font, locale, content, scrollSpeedSetting,
            currentScrollSpeed, resolveDetails,
            (category, index, record, code) => RunInput(() =>
            {
                UpdateInputAdmission();
                if (inputAllowed == true) rowEvent(category, index, record, code);
            }), category =>
            {
                if (!configured) PrepareCategory(category);
                else RunInput(() => PrepareCategory(category));
            }, ReadInputAdmission, initialCategory);
        footer = new UiSourceReportFooter { Name = "ReportFooter" };
        canvas.AddChild(footer);
        footer.Configure(repository, assets, shader, locale, inputGuidesVisible, RequestClose, ReadInputAdmission);
        string caption = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records[0x5E9].Text
            ?? throw new InvalidDataException("Missing action report title.");
        var title = new UiSourceHeader { Name = "ReportTitle" };
        canvas.AddChild(title);
        title.Configure(repository, assets, shader, font, caption);
        title.Publish(headers);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Resized += Fit;
        Fit();
        configured = true;
        UpdateInputAdmission();
    }

    internal void ApplyStrategistPortrait(IReadOnlyList<LoadedSprite>? standingLayers) => strategist.ApplyPortrait(standingLayers);
    internal void SetInputGuidesVisible(bool visible) => footer.SetInputGuidesVisible(visible);
    internal void RequestAcknowledge() => ProcessSourceInput(true, false, default);

    private void PrepareCategory(byte category)
    {
        var prepared = preparePages!(category)
            ?? throw new InvalidDataException("Strategist pages are unknown for report category " + category);
        var next = prepared.ToArray();
        if (next.Any(page => page is null)) throw new InvalidDataException("A prepared strategist page is unknown.");
        pages = next;
        PageIndex = 0;
        // A known zero count leaves the source's cleared first string empty.
        // Unknown preparation is rejected above, rather than shown as empty.
        strategist.ApplyPage(pages.Length == 0 ? "" : pages[0]);
    }

    // Inputs are already resolved by the application's actual input-mode and
    // cancel owners. No Escape/Enter alias or missing-mode default is supplied.
    internal void ProcessSourceInput(bool cancelRequested, bool advanceRequested, Vector2I canonicalPointer)
    {
        if (!configured) return;
        RunInput(() =>
        {
            UpdateInputAdmission();
            if (inputAllowed != true) return;
            if (cancelRequested) { footer.RequestConfirmation(); return; }
            if (advanceRequested && PageIndex < pages.Length - 1 &&
                strategist.ContainsAdvancePoint((Vector2)canonicalPointer - strategist.Position))
                strategist.ApplyPage(pages[++PageIndex]);
        });
    }

    public override void _Process(double delta) => RunInput(UpdateInputAdmission);

    private bool ReadInputAdmission()
    {
        bool allowed = false;
        RunInput(() =>
        {
            UpdateInputAdmission();
            allowed = configured && inputAllowed == true;
        });
        return allowed;
    }

    private void UpdateInputAdmission()
    {
        if (!configured) return;
        bool allowed = !closing && !abandoned && IsInsideTree() && IsVisibleInTree() &&
            CanProcess() && inputAdmitted!();
        if (allowed == inputAllowed) return;
        inputAllowed = allowed;
        lists.ProcessMode = footer.ProcessMode = allowed ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }

    private void RequestClose() => RunInput(() =>
    {
        UpdateInputAdmission();
        if (inputAllowed != true) return;
        closing = true;
        footer.ConfirmEnabled = false;
        RetireFooter();
        // Animations are deferred. The static view really retires; completion
        // is published after tree exit, never on a fixed frame or last page.
        Hide();
        QueueFree();
    });

    private void RunInput(Action action)
    {
        if (closing || abandoned) return;
        try { action(); }
        catch (Exception error)
        {
            var notify = failed;
            Abandon();
            notify?.Invoke(error);
        }
    }

    internal void Abandon()
    {
        abandoned = true;
        closed = null; failed = null; preparePages = null; inputAdmitted = null;
        if (!GodotObject.IsInstanceValid(this)) return;
        RetireFooter();
        Hide();
        if (!IsQueuedForDeletion()) QueueFree();
    }

    private void RetireFooter()
    {
        if (footer is null || !GodotObject.IsInstanceValid(footer)) return;
        // The report owns this explicit cleanup order. Tree traversal order
        // alone does not guarantee footer retirement before title restoration.
        footer.Hide();
        if (footer.GetParent() is Node parent) parent.RemoveChild(footer);
        if (!footer.IsQueuedForDeletion()) footer.QueueFree();
    }

    public override void _ExitTree()
    {
        var notify = closing && !abandoned ? closed : null;
        configured = false;
        inputAllowed = false;
        closed = null; failed = null; preparePages = null; inputAdmitted = null;
        ReleaseOwnedResources();
        if (notify is not null) Callable.From(() => { if (!abandoned) notify(); }).CallDeferred();
    }

    private void ReleaseOwnedResources()
    {
        ownedFont?.Dispose(); ownedFont = null;
        ownedAssets?.Dispose(); ownedAssets = null;
        ownedShader?.Dispose(); ownedShader = null;
    }

    internal bool ContainsStrategistAdvancePoint(Vector2 viewportPoint) =>
        strategist.ContainsAdvancePoint(strategist.GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint);

    internal void AdvanceAt(Vector2 viewportPoint)
    {
        var point = canvas.GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;
        ProcessSourceInput(false, true, new((int)point.X, (int)point.Y));
    }

    private void Fit()
    {
        if (canvas is null) return;
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * factor;
        canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }
}
