using San14.Core;

namespace San14.Data;

public sealed record DoctrineResearchPoint(int DoctrineId, string Name, int Before, int After);
public sealed record DoctrineResearchCost(int DoctrineId, string Name, int Before, int After, int Amount)
{ public int OriginalAmount { get; init; } }
public sealed record DoctrineResearchBudget(int BooksBefore, int BooksAfter, int BookCost,
    int GoldBefore, int GoldAfter, int GoldCost);
public sealed record DoctrineResearchRow(int PanelId, string Name, string Description, int LayoutSlot,
    int Category, bool Researched, bool Pending, bool Available, bool? CanResearch,
    IReadOnlyList<DoctrineResearchCost> Costs, IReadOnlyList<int> PrerequisiteIds,
    string? UnresolvedOwner)
{
    public bool Enabled => Researched && !Pending;
    public int FirstDoctrineId { get; init; }
    public bool RestrictToFirstDoctrine { get; init; }
    public IReadOnlyList<int> LinkedLayoutSlots { get; init; } = [];
    public IReadOnlyList<int> UnlockedPanelIds { get; init; } = [];
}

internal sealed record DoctrineResearchRules(int ForceId, int DistrictId, int RulerId,
    int RulerDoctrineId, int MatchingDiscount, int Books, int Gold, int GoldCapacity,
    int BookCostPerPanel, int RedefineGold, int ResearchCapacity, bool HostAvailable,
    bool? ApplicationRestricted, DoctrineResearchWorkspace Baseline,
    IReadOnlyDictionary<int, DoctrineResearchPanelDefinition> Panels,
    IReadOnlyDictionary<int, int> PanelByLayoutSlot, IReadOnlyDictionary<int, string> DoctrineNames,
    Func<int, string> Description, Func<DoctrineResearchWorkspace, bool> RedefineCapacityAvailable,
    Func<int, int?, string?, AreaAdministrationQuery<string>> Message,
    Func<DoctrineResearchWorkspace, Func<int, bool>, int?, DoctrineResearchAutomaticSession> Automatic)
{
    internal string ForceName { get; init; } = "";
    internal string TitleName { get; init; } = "";
}

/// <summary>Research is a local ordered draft. Confirm replays its actual
/// actions through the same current rules in one normal command transaction.</summary>
public sealed class DoctrineResearchDraft
{
    private readonly DoctrineResearchRules? rules;
    private DoctrineResearchWorkspace? working;
    private readonly List<DoctrineResearchStep> steps = [];
    private IReadOnlyList<DoctrineResearchRow> rows = [];
    private IReadOnlyList<DoctrineResearchPoint> points = [];
    private readonly Dictionary<int, string> descriptions = [];
    private DoctrineResearchAutomaticSession? automatic;

    internal DoctrineResearchDraft(DoctrineResearchRules rules)
    { this.rules = rules; ForceId = rules.ForceId; working = rules.Baseline.Copy(); Refresh(); }
    internal DoctrineResearchDraft(int forceId, string missing)
    { ForceId = forceId; UnresolvedOwner = missing; }

    public int ForceId { get; }
    public int DistrictId => rules?.DistrictId ?? 0;
    public int? RulerId => rules?.RulerId;
    public int? RulerDoctrineId => rules?.RulerDoctrineId;
    public string? ForceName => rules?.ForceName;
    public string? TitleName => rules?.TitleName;
    public int? ResearchCapacity => rules?.ResearchCapacity;
    public int? ResearchCount => rules is not null && working is not null
        ? DoctrineResearchSource.ResearchCount(working, rules.Panels) : null;
    public IReadOnlyList<DoctrineResearchRow> Rows => rows;
    public IReadOnlyList<DoctrineResearchRow> EffectRows => rules is null ? [] : rows.Where(row =>
        row.Category != 7 && row.Enabled && rules.Panels[row.PanelId].Value != 0).ToArray();
    public IReadOnlyList<DoctrineResearchPoint> ReservePoints => points;
    public DoctrineResearchBudget? Budget { get; private set; }
    public int? SelectedPanelId { get; private set; }
    public DoctrineResearchRow? SelectedPanel => rows.FirstOrDefault(row => row.PanelId == SelectedPanelId);
    public int SelectedActionCaptionId => SelectedPanel is not { } row ? 0x82c
        : row.Researched ? row.PanelId is >= 74 and <= 79 ? 0x8b8 : row.Pending ? 0x8b0 : 0x82d
        : row.PanelId is >= 74 and <= 79 ? row.CanResearch == true ? 0x8b6 : 0x8b7
        : row.CanResearch == true ? 0x82b : 0x82c;
    public string? UnresolvedOwner { get; private set; }
    public string? LastError { get; private set; }
    public bool Redefined => steps.Any(step => step.Operation == DoctrineResearchOperation.Redefine);
    public bool HasChanges => rules is not null && working is not null &&
        (!working.Points.SequenceEqual(rules.Baseline.Points) ||
         !working.Researched.SetEquals(rules.Baseline.Researched) ||
         !working.Pending.SetEquals(rules.Baseline.Pending) ||
         !working.Available.SetEquals(rules.Baseline.Available));
    public bool CanConfirm => rules is not null && UnresolvedOwner is null && rules.HostAvailable &&
        HasChanges && Budget is { BooksAfter: >= 0, GoldAfter: >= 0 } && Budget.GoldBefore >= Budget.GoldCost;
    public bool CanAutoResearch => rules is not null && rules.HostAvailable &&
        rules.ApplicationRestricted == false && rows.Any(row => row.CanResearch == true);
    public bool CanRedefine
    {
        get
        {
            if (rules is null || working is null || !rules.HostAvailable || rules.ApplicationRestricted != false ||
                Budget is null || Budget.BookCost >= rules.Books || rules.Gold < rules.RedefineGold ||
                DoctrineResearchSource.ResearchCount(working, rules.Panels) == 0) return false;
            try { return rules.RedefineCapacityAvailable(working); }
            catch (InvalidDataException error) { LastError = error.Message; return false; }
        }
    }

    public bool SelectPanel(int panelId)
    {
        if (!rows.Any(row => row.PanelId == panelId)) return false;
        SelectedPanelId = panelId; return true;
    }
    public void ClearSelection() => SelectedPanelId = null;

    public bool Research(int panelId)
    {
        if (rules is null || working is null || !rows.Any(row => row.PanelId == panelId && row.CanResearch == true)) return false;
        try
        {
            DoctrineResearchSource.Apply(working, rules.Baseline, rules.Panels[panelId],
                rules.RulerDoctrineId, rules.MatchingDiscount, rules.PanelByLayoutSlot);
            steps.Add(new(DoctrineResearchOperation.Research, panelId));
            SelectedPanelId = null; LastError = null; Refresh(); return true;
        }
        catch (InvalidDataException error) { LastError = error.Message; return false; }
    }
    public bool Redefine()
    {
        if (!CanRedefine || rules is null || working is null) return false;
        try
        {
            var candidate = working.Copy();
            DoctrineResearchSource.Redefine(candidate, rules.Baseline, rules.Panels,
                rules.RulerDoctrineId, rules.MatchingDiscount, rules.PanelByLayoutSlot);
            working = candidate; steps.Add(new(DoctrineResearchOperation.Redefine));
            SelectedPanelId = null; automatic = null; LastError = null; Refresh(); return true;
        }
        catch (InvalidDataException error) { LastError = error.Message; return false; }
    }
    public bool AutoResearch()
    {
        if (!CanAutoResearch || rules is null || working is null) return false;
        try
        {
            automatic = rules.Automatic(working, id => CanResearchCode(id) == 0, automatic?.SecondFocusPanelId);
            bool changed = false;
            //Each successful step activates a previously absent panel. This
            //finite bound is the definition domain, not an invented budget.
            for (int i = 0; i < 79; i++)
            {
                int? id = automatic.SelectNext();
                if (id is null || !Research(id.Value)) break;
                changed = true;
                if (Budget!.BookCost >= rules.Books) break;
                automatic.Applied(id.Value);
            }
            return changed;
        }
        catch (InvalidDataException error) { LastError = error.Message; return false; }
    }
    public void DiscardChanges()
    {
        if (rules is null) return;
        working = rules.Baseline.Copy(); steps.Clear(); automatic = null;
        SelectedPanelId = null; LastError = null; Refresh();
    }
    public CommitDoctrineResearchOrder BuildOrder()
        => CanConfirm ? new(ForceId, steps.ToArray()) : throw new InvalidOperationException(
            LastError ?? UnresolvedOwner ?? "Doctrine research is not ready to commit.");
    public AreaAdministrationQuery<string> QueryRedefineConfirmation()
        => rules?.Message(0x5c67, rules.RedefineGold, null) ?? AreaAdministrationQuery<string>.Unknown(UnresolvedOwner!);
    public AreaAdministrationQuery<string> QueryDiscardConfirmation()
        => rules?.Message(0x5c65, null, null) ?? AreaAdministrationQuery<string>.Unknown(UnresolvedOwner!);
    public AreaAdministrationQuery<string> QueryEffectsGuide()
        => rules?.Message(0x5c6a, null, null) ?? AreaAdministrationQuery<string>.Unknown(UnresolvedOwner!);
    public AreaAdministrationQuery<string> QueryGuide()
    {
        if (rules is null || Budget is null) return AreaAdministrationQuery<string>.Unknown(UnresolvedOwner!);
        int root = !rules.HostAvailable ? 0x5c64 : Budget.BookCost >= rules.Books ? 0x57c7
            : rows.Any(row => row.CanResearch == true) ? 0x5c61 : 0x5c62;
        return rules.Message(root, null, null);
    }
    public AreaAdministrationQuery<string> QuerySelectedGuide()
    {
        if (SelectedPanelId is not int id || rules is null) return AreaAdministrationQuery<string>.Known("");
        try
        {
            int code = CanResearchCode(id);
            if (code is 0 or -12) return QueryGuide();
            return rules.Message(code == -10 ? 0x5c69 : code == -11 ? 0x5c6b : 0x5c63, null,
                code == -10 ? rules.DoctrineNames.GetValueOrDefault(rules.Panels[id].FirstDoctrineId, "") : null);
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
    }

    internal int CanResearchCode(int id)
    {
        if (rules is null || working is null) throw DoctrineResearchSource.Missing("page_owner");
        return DoctrineResearchSource.CanResearch(rules, working, id);
    }

    private void Refresh()
    {
        if (rules is null || working is null) return;
        int books = DoctrineResearchSource.BookCost(rules.Baseline, working, rules.BookCostPerPanel);
        int gold = Redefined ? rules.RedefineGold : 0;
        Budget = new(rules.Books, rules.Books - books, books, rules.Gold,
            unchecked(rules.Gold - gold), gold);
        points = Enumerable.Range(1, 6).Select(id => new DoctrineResearchPoint(id,
            rules.DoctrineNames.GetValueOrDefault(id, ""), rules.Baseline.Points[id - 1], working.Points[id - 1])).ToArray();
        var projected = new List<DoctrineResearchRow>();
        foreach (var panel in rules.Panels.Values.Where(row => row.Id is >= 2 and <= 79).OrderBy(row => row.Id))
        {
            string? missing = null; bool? allowed = null; string description = "";
            var costs = new List<DoctrineResearchCost>();
            try
            {
                allowed = CanResearchCode(panel.Id) == 0;
                if (!descriptions.TryGetValue(panel.Id, out description!)) descriptions[panel.Id] = description = rules.Description(panel.Id);
                var amounts = DoctrineResearchSource.Costs(panel, rules.RulerDoctrineId, rules.MatchingDiscount);
                Add(panel.FirstDoctrineId, amounts.First, panel.FirstCost); Add(panel.SecondDoctrineId, amounts.Second, panel.SecondCost);
            }
            catch (InvalidDataException error) { missing = error.Message; }
            projected.Add(new(panel.Id, panel.Name, description, panel.LayoutSlot, panel.Category,
                working.Researched.Contains(panel.Id), working.Pending.Contains(panel.Id), working.Available.Contains(panel.Id),
                allowed, costs.AsReadOnly(), rules.Panels.Values.Where(other => other.Id != 0 &&
                    other.Dependencies.Contains(panel.LayoutSlot)).Select(other => other.Id).ToArray(), missing)
                { FirstDoctrineId = panel.FirstDoctrineId, RestrictToFirstDoctrine = panel.RestrictToFirstDoctrine,
                  LinkedLayoutSlots = panel.Dependencies,
                  UnlockedPanelIds = panel.Dependencies.Select(slot => rules.PanelByLayoutSlot.GetValueOrDefault(slot))
                    .Where(id => id != 0).Distinct().ToArray() });
            void Add(int doctrine, int amount, int original)
            {
                if (doctrine == 0) return;
                if (doctrine is < 1 or > 6) throw DoctrineResearchSource.Missing("cost_doctrine");
                costs.Add(new(doctrine, rules.DoctrineNames.GetValueOrDefault(doctrine, ""),
                    working.Points[doctrine - 1], Math.Clamp(working.Points[doctrine - 1] - amount, 0, 99), amount)
                    { OriginalAmount = original });
            }
        }
        rows = projected.AsReadOnly();
    }
}
