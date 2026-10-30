using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record ProposalResultLine(string Name, string Before, string After,
    LoadedSprite? Portrait = null);

/// <summary>The ordinary proposal state: first proposals, the officer selection
/// child, and its ordered result report. The campaign owner makes every decision.</summary>
internal partial class ProposalView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private ProposalPage page = null!;
    private AdministrationPageChrome chrome = null!;
    private Control body = null!;
    private Func<bool>? admitted;
    private Action? returned, adopt;
    private Action<int>? selected;
    private Action<Action>? runUiAction;
    private Func<ProposalRow, LoadedSprite?>? portrait;
    private DeploymentTargetConfirmation? confirmation;
    private readonly HashSet<int> selection = [];
    private readonly Dictionary<int, Action<bool>> selectionWriters = [];
    private bool officerPage, showingResults, abandoned, canSubmit;

    internal event Action<int>? DetailRequested;
    internal event Action? InformationRequested;
    internal event Action? SelectAllRequested;
    internal event Action? ResultClosed;
    internal IReadOnlySet<int> SelectedProposalIds => selection;
    internal bool IsOfficerPage => officerPage;

    internal void Configure(GameResourceRepository repository, PreviewText text, ProposalPage page,
        Func<bool> admitted, Action back, Action<int> selected, Action adopt,
        Action<Action> runUiAction, Func<ProposalRow, LoadedSprite?>? portrait = null)
    {
        this.repository = repository; this.text = text; this.page = page;
        this.admitted = admitted; returned = back; this.selected = selected;
        this.adopt = adopt; this.runUiAction = runUiAction; this.portrait = portrait;
        Name = "Proposals"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x5D2, RequestBack,
            () => Run(() => { if (officerPage && canSubmit) this.adopt?.Invoke(); }),
            returnNode: 1);
        selection.UnionWith(page.SelectedProposalIds); canSubmit = page.CanSubmit;
        ShowFirst();
    }

    internal void ConfigureResults(GameResourceRepository repository, PreviewText text,
        Func<bool> admitted, Action closed, Action<Action> runUiAction,
        IReadOnlyList<ProposalResultLine> lines)
    {
        this.repository = repository; this.text = text; this.admitted = admitted;
        this.runUiAction = runUiAction; ResultClosed = closed;
        Name = "ProposalResults"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x5D2, RequestBack, () => { }, returnOnly: true);
        ShowResults(lines);
    }

    internal void Apply(ProposalPage value)
    {
        if (abandoned) return;
        page = value;
        selection.Clear(); selection.UnionWith(value.SelectedProposalIds); canSubmit = value.CanSubmit;
        if (!showingResults) Rebuild();
    }

    internal void ShowFirst()
    {
        if (abandoned) return;
        officerPage = false; showingResults = false; Rebuild();
    }

    internal void ShowOfficer()
    {
        if (abandoned) return;
        officerPage = true; showingResults = false; Rebuild();
    }

    /// <summary>The draft owns budget checks and the selected set. Rendering
    /// this set does not toggle or execute a proposal a second time.</summary>
    internal void ApplySelection(IEnumerable<int> ids, bool canSubmit)
    {
        if (abandoned) return;
        selection.Clear(); selection.UnionWith(ids); this.canSubmit = canSubmit;
        foreach (var (id, write) in selectionWriters) write(selection.Contains(id));
        chrome.ApplyDecision(officerPage && !showingResults && canSubmit,
            officerPage && !showingResults);
    }

    internal void ShowConfirmation(string expandedMessage, Action<bool> answered)
    {
        if (abandoned || confirmation is not null) return;
        var question = new DeploymentTargetConfirmation(); confirmation = question; AddChild(question);
        try
        {
            question.ConfigureWrappedMessage(repository, text, expandedMessage, answer =>
            {
                if (abandoned || confirmation != question) return;
                confirmation = null; question.Abandon(); RemoveChild(question); question.QueueFree();
                runUiAction?.Invoke(() => answered(answer));
            });
        }
        catch
        {
            confirmation = null; question.Abandon(); RemoveChild(question); question.QueueFree();
            throw;
        }
    }

    internal void ShowResults(IReadOnlyList<ProposalResultLine> lines)
    {
        if (abandoned) return;
        if (lines.Count > 12) throw new InvalidDataException("Proposal report exceeds its twelve source mounts.");
        showingResults = true; NewBody();
        var source = Load("04_00_02_02_result");
        Art(source, body, 9); Art(source, body, 11);
        source.Text(body, 10, default, Message(0x2DB), chrome.Font, MapTextPresets.Create()[13]);
        var rowSource = Load("04_00_02_02_kessai_offset_00");
        int[] mounts = [12, 30, 15, 33, 18, 36, 21, 39, 24, 42, 27, 45];
        for (int i = 0; i < lines.Count; i++)
        {
            var value = lines[i]; var row = Mounted(rowSource, source.Rectangle(mounts[i]).Position, "ProposalResult" + i);
            Art(rowSource, row, 1); Art(rowSource, row, 4);
            ResultText(5, value.Name);
            ResultText(6, value.Before);
            Art(rowSource, row, 7);
            ResultText(8, value.After);
            if (value.Portrait is { } face) Portrait(rowSource, row, 2, face, 4);
            void ResultText(int node, string expanded)
            {
                var label = rowSource.Text(row, node, default, "", chrome.Font);
                if (expanded.Length != 0) label.SetKnownColourLine(expanded);
            }
        }
        chrome.ApplyDecision(false, false);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var close = new UiSourceRoundButton { Name = "ConfirmProposalResults",
            Position = footer.Rectangle(1, new(0, 904)).Position, InputAdmission = CanUse };
        body.AddChild(close);
        close.ConfigureReportConfirm(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale,
            () => Run(CloseResults));
        chrome.ApplyReturn(false); chrome.ApplyDefaultHint("");
        SetMeta("proposal_stage", "result"); SetMeta("result_count", lines.Count);
    }

    internal void ApplyHint(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }

    private void Rebuild()
    {
        NewBody(); chrome.ApplyReturn(true);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        chrome.Canvas.GetNode<Control>("ReturnAdministration").Position =
            footer.Rectangle(officerPage ? 4 : 1, new(0, 904)).Position;
        if (officerPage) BuildOfficer(); else BuildFirst();
        BuildBudget();
        var information = new UiSourceFunctionButton { Name = "ProposalInformation",
            Position = new(0x708, 0x32), InputAdmission = CanUse };
        body.AddChild(information);
        information.Configure(repository, chrome.Assets, chrome.SpriteShader, "000_cmn_icon_info",
            () => Run(() => InformationRequested?.Invoke()));
        chrome.ApplyDecision(officerPage && canSubmit, officerPage);
        SetMeta("proposal_stage", officerPage ? "officer" : "first");
        SetMeta("force_id", page.ForceId);
    }

    private void NewBody()
    {
        selectionWriters.Clear();
        if (body is not null) { body.Hide(); chrome.Canvas.RemoveChild(body); body.QueueFree(); }
        body = new Control { Name = "ProposalBody", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
    }

    private void CloseResults()
    {
        if (ResultClosed is { } close) close(); else ShowFirst();
    }

    internal void RequestBack()
    {
        if (!ReadAdmission()) return;
        if (confirmation is { } question) { question.RequestCancel(); return; }
        if (showingResults) { Run(CloseResults); return; }
        Run(() => { if (officerPage) ShowFirst(); else returned?.Invoke(); });
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private bool CanUse() => ReadAdmission() && confirmation is null;
    private void Run(Action action) { if (CanUse()) runUiAction?.Invoke(action); }
    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Proposal UI message is missing.");
    private UiSourceLayout Load(string name) => new(MapMarkerLayout.LoadNamed(repository, name));
    private void Art(UiSourceLayout source, Control target, int node)
        => source.Art(target, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
    private Control Mounted(UiSourceLayout source, Vector2 position, string name)
    {
        var row = new Control { Name = name, Position = position, Size = source.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(row); return row;
    }

    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; returned = null; adopt = null; selected = null;
        runUiAction = null; portrait = null; DetailRequested = null; InformationRequested = null;
        SelectAllRequested = null; ResultClosed = null;
        selectionWriters.Clear(); confirmation?.Abandon(); confirmation = null;
        chrome?.Abandon(); Hide();
    }

    public override void _ExitTree() => Abandon();
}
