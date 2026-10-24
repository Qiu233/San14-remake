using Godot;
using San14.Data;
using San14.Client.Gameplay;

/// <summary>Edits a detached ordered roster. The owner evaluates a snapshot on
/// actual edits; selection, rendering and cancellation never submit an order.</summary>
internal partial class GovernanceRosterPage : Control
{
    private AdministrationPageChrome? chrome;
    private GovernanceRosterPageModel source = null!;
    private PreviewText ui = null!;
    private readonly Dictionary<int, List<int>> members = [];
    private readonly List<BaseButton> edits = [];
    private OptionButton? advisor, departments, candidates;
    private ItemList? roster;
    private UiSourceText capacity = null!, expansion = null!, advisorStatus = null!;
    private UiSourceText cost = null!, books = null!, status = null!;
    private BaseButton? add, remove, up, down, clear;
    private Action<GovernanceRosterSelection>? changed;
    private GovernanceRosterEvaluationView? evaluation;
    private int? advisorId;
    private bool busy, abandoned;
    private const int VacantAdvisor = int.MaxValue;

    internal GovernanceRosterSelection Selection => new(advisorId, source.Departments.Select(group =>
        new GovernanceDepartmentSelection(group.Group, members[group.Group].ToArray())).ToArray());
    internal bool HasChanges => advisorId != source.Advisor?.Id || source.Departments.Any(group =>
        !members[group.Group].SequenceEqual(group.Members.Select(person => person.Id)));

    internal void Configure(GameResourceRepository repository, PreviewText text, Theme theme,
        GovernanceRosterPageModel model, Action returned, Action accepted, Action<GovernanceRosterSelection> changed)
    {
        source = model; ui = text; this.changed = changed; advisorId = model.Advisor?.Id;
        Name = "GovernanceRosterPage"; MouseFilter = MouseFilterEnum.Stop; ZIndex = 3;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var group in source.Departments) members.Add(group.Group, group.Members.Select(person => person.Id).ToList());
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x223, returned, accepted);
        chrome.ApplyTitle(text.Get("play_governance"));
        chrome.ApplyDefaultHint(text.Get("play_governance_roster_hint"));
        var narration = new UiSourceLayout(repository, 0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(narration.Nodes.Single(node => node.Type == 2),
            narration.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[13]);
        Line("GovernanceForce", model.ForceName, new(320, 110));
        Line("GovernanceAdvisorCaption", text.Get("play_governance_advisor"), new(320, 160));
        advisor = Choice("GovernanceAdvisor", new(460, 158), 500);
        advisor.AddItem(text.Get("play_governance_vacant"), VacantAdvisor);
        foreach (var person in model.AdvisorCandidates) AddAdvisor(person);
        if (model.Advisor is { } current && advisor.GetItemIndex(current.Id) < 0) AddAdvisor(current);
        advisor.Select(advisor.GetItemIndex(advisorId ?? VacantAdvisor));
        advisor.ItemSelected += _ =>
        {
            if (busy || abandoned || !source.CanEdit) return;
            int selected = advisor.GetSelectedId(); advisorId = selected == VacantAdvisor ? null : selected;
            RenderAdvisor(); NotifyChanged();
        };
        advisorStatus = Line("GovernanceAdvisorStatus", "", new(1120, 160));
        departments = Choice("GovernanceDepartment", new(320, 235), 350);
        foreach (var group in source.Departments) departments.AddItem(group.Name, group.Group);
        departments.ItemSelected += _ => RenderDepartment();
        capacity = Line("GovernanceCapacity", "", new(760, 235));
        expansion = Line("GovernanceExpansion", "", new(1120, 235));
        Line("GovernanceMembersCaption", text.Get("play_governance_roster_members"), new(320, 290));
        Line("GovernanceCandidatesCaption", text.Get("play_governance_roster_candidates"), new(1120, 290));
        roster = new ItemList { Name = "GovernanceMembers", Position = new(320, 330), Size = new(650, 280), Theme = theme };
        chrome.Canvas.AddChild(roster); roster.ItemSelected += _ => RefreshEditors();
        candidates = Choice("GovernanceCandidate", new(1120, 330), 600);
        candidates.ItemSelected += _ => RefreshEditors();
        add = Command("GovernanceAdd", "play_governance_roster_add", new(1120, 385), () =>
        {
            if (Group() is not { Capacity: int limit } group || candidates.Selected < 0) return;
            var rows = members[group.Group]; int id = candidates.GetSelectedId();
            if (rows.Count >= limit || rows.Contains(id)) return;
            rows.Add(id); RenderDepartment(); NotifyChanged();
        });
        remove = Command("GovernanceRemove", "play_governance_roster_remove", new(320, 650), () =>
        {
            if (Group() is not { } group) return;
            int index = SelectedMember(); var rows = members[group.Group];
            if ((uint)index >= (uint)rows.Count) return;
            rows.RemoveAt(index); RenderDepartment(); NotifyChanged();
        });
        up = Command("GovernanceMoveUp", "play_governance_roster_up", new(500, 650), () => Move(-1));
        down = Command("GovernanceMoveDown", "play_governance_roster_down", new(680, 650), () => Move(1));
        clear = Command("GovernanceClear", "play_governance_roster_clear", new(860, 650), () =>
        {
            if (Group() is not { } group || members[group.Group].Count == 0) return;
            members[group.Group].Clear(); RenderDepartment(); NotifyChanged();
        });
        cost = Line("GovernancePolicyCost", "", new(1120, 510));
        books = Line("GovernanceCommandBooks", "", new(1120, 550));
        status = Line("GovernanceEvaluation", "", new(1120, 600));
        RenderAdvisor(); RenderDepartment(); NotifyChanged();

        void AddAdvisor(GovernanceRosterOfficer person)
        {
            advisor.AddItem(PersonCaption(person), person.Id);
            advisor.SetItemDisabled(advisor.ItemCount - 1, !person.Selectable && person.Id != model.Advisor?.Id);
        }
        UiSourceText Line(string name, string value, Vector2 position)
        {
            var line = new UiSourceText { Name = name, Position = position, Size = new(650, 32) };
            chrome.Canvas.AddChild(line); line.Configure(chrome.Font, profile); line.Text = value; return line;
        }
        OptionButton Choice(string name, Vector2 position, int width)
        {
            var choice = new OptionButton { Name = name, Position = position, Size = new(width, 36), Theme = theme };
            chrome.Canvas.AddChild(choice); return choice;
        }
        BaseButton Command(string name, string caption, Vector2 position, Action action)
        {
            var button = new UiSourceBottomButton { Name = name, Position = position };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text.Get(caption), () =>
            { if (!busy && !abandoned && source.CanEdit) action(); });
            edits.Add(button); return button;
        }
    }

    private GovernanceRosterDepartment? Group() => departments is { Selected: >= 0 } menu
        ? source.Departments.First(group => group.Group == menu.GetSelectedId()) : null;
    private int SelectedMember() => roster?.GetSelectedItems().FirstOrDefault(-1) ?? -1;
    private string PersonCaption(GovernanceRosterOfficer person) => ui.Format("play_governance_roster_person",
        person.Name, person.PolicyName ?? ui.Get("detail_unknown"), person.PolicyCost?.ToString() ?? ui.Get("detail_unknown"));
    private string Activation(int? pending) => pending switch
    {
        0 => ui.Get("play_governance_roster_active"),
        > 0 => ui.Get("play_governance_roster_pending"),
        _ => ui.Get("detail_unknown")
    };
    private void RenderAdvisor()
    {
        advisorStatus.Text = advisorId == source.Advisor?.Id && source.Advisor is { } current
            ? Activation(current.PendingValue) : advisorId is null ? ui.Get("play_governance_vacant") : ui.Get("play_governance_roster_draft");
    }
    private void RenderDepartment(int? selectIndex = null)
    {
        if (roster is null || candidates is null) return;
        roster.Clear(); candidates.Clear();
        if (Group() is { } group)
        {
            var names = group.Members.Concat(group.Candidates).GroupBy(person => person.Id).ToDictionary(pair => pair.Key, pair => pair.First());
            foreach (int id in members[group.Group])
            {
                var person = names[id];
                var current = group.Members.FirstOrDefault(row => row.Id == id);
                roster.AddItem(ui.Format("play_governance_roster_member", PersonCaption(person),
                    current is null ? ui.Get("play_governance_roster_draft") : Activation(current.PendingValue)));
            }
            foreach (var person in group.Candidates)
            {
                candidates.AddItem(PersonCaption(person), person.Id);
                candidates.SetItemDisabled(candidates.ItemCount - 1, !person.Selectable || members[group.Group].Contains(person.Id));
            }
            int available = Enumerable.Range(0, candidates.ItemCount).FirstOrDefault(index => !candidates.IsItemDisabled(index), -1);
            candidates.Select(available);
            capacity.Text = ui.Format("play_governance_roster_capacity", members[group.Group].Count,
                group.Capacity?.ToString() ?? ui.Get("detail_unknown"));
            expansion.Text = ui.Format("play_governance_roster_expansion", group.Expansion?.ToString() ?? ui.Get("detail_unknown"));
            if (selectIndex is int selected && (uint)selected < (uint)roster.ItemCount) roster.Select(selected);
        }
        RefreshEditors();
    }
    private void Move(int delta)
    {
        if (Group() is not { } group) return;
        int from = SelectedMember(), to = from + delta;
        var rows = members[group.Group];
        if ((uint)from >= (uint)rows.Count || (uint)to >= (uint)rows.Count) return;
        (rows[from], rows[to]) = (rows[to], rows[from]); RenderDepartment(to); NotifyChanged();
    }
    private void NotifyChanged() { if (!abandoned) changed?.Invoke(Selection); RefreshEditors(); }
    internal void ApplyEvaluation(GovernanceRosterEvaluationView value)
    {
        evaluation = value;
        cost.Text = ui.Format("play_governance_roster_cost", value.PolicyCost?.ToString() ?? ui.Get("detail_unknown"),
            value.PolicyCostLimit?.ToString() ?? ui.Get("detail_unknown"));
        books.Text = ui.Format("play_governance_roster_books", value.CommandBooks?.ToString() ?? ui.Get("detail_unknown"));
        status.Text = value.Status; RefreshEditors();
    }
    internal void SetBusy(bool value) { busy = value; RefreshEditors(); }
    internal void ApplyHint(string text) => chrome?.ApplyDefaultHint(text);
    private void RefreshEditors()
    {
        bool blocked = busy || abandoned || !source.CanEdit;
        chrome?.ApplyReturn(!busy); chrome?.ApplyDecision(!blocked && HasChanges &&
            evaluation is { Allowed: true, CommandBooks: not null, ExceedsLevelLimit: not null }, true);
        if (advisor is not null) advisor.Disabled = blocked;
        if (departments is not null) departments.Disabled = busy || abandoned || source.Departments.Count == 0;
        if (roster is not null) roster.MouseFilter = busy || abandoned ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        if (candidates is not null) candidates.Disabled = blocked || candidates.ItemCount == 0;
        foreach (var button in edits) button.Disabled = blocked;
        var group = Group(); int index = SelectedMember(), count = group is null ? 0 : members[group.Group].Count;
        if (add is not null) add.Disabled = blocked || group?.Capacity is not int limit || count >= limit ||
            candidates is null || candidates.Selected < 0 || candidates.IsItemDisabled(candidates.Selected);
        if (remove is not null) remove.Disabled = blocked || index < 0;
        if (up is not null) up.Disabled = blocked || index <= 0;
        if (down is not null) down.Disabled = blocked || index < 0 || index >= count - 1;
        if (clear is not null) clear.Disabled = blocked || count == 0;
    }
    internal void Abandon() { abandoned = true; changed = null; SetBusy(true); chrome?.Abandon(); Hide(); }
}
