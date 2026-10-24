using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record PersonnelDevelopmentOfficer(int OfficerId, string Name,
    OfficerPortraitRoute? Portrait, AreaAdministrationOfficerCard? OfficerCard,
    bool? Enabled = true, string? DisabledReason = null, string? UnresolvedOwner = null)
{
    public string? NameSortKey { get; init; }
    /// <summary>Independent hire-template identity; not a main-world Person ID.</summary>
    public int? TemplateId { get; init; }
}

/// <summary>The three ordered mediation participants belong to this local page.
/// All candidate and price reads borrow the page's single captured current.</summary>
public sealed class PersonnelMediationDraft
{
    private readonly Func<IReadOnlyList<int>, AreaAdministrationQuery<PersonnelMediationView>>? query;
    private readonly Func<PersonnelMediationCandidate, PersonnelDevelopmentOfficer>? project;
    private readonly Func<IReadOnlyList<int>, AreaAdministrationQuery<string>>? confirmation;
    private AreaAdministrationQuery<string>? confirmationCache;
    private readonly int forceId;
    private ImmutableArray<int> selected = [0, 0, 0];
    private PersonnelMediationView? view;
    private IReadOnlyList<IReadOnlyList<PersonnelDevelopmentOfficer>> candidates = [];
    private IReadOnlyList<PersonnelDevelopmentOfficer> targets = [];
    private readonly Dictionary<int, PersonnelDevelopmentOfficer> cards = [];

    internal PersonnelMediationDraft(int forceId,
        Func<IReadOnlyList<int>, AreaAdministrationQuery<PersonnelMediationView>> query,
        Func<PersonnelMediationCandidate, PersonnelDevelopmentOfficer> project,
        Func<IReadOnlyList<int>, AreaAdministrationQuery<string>> confirmation)
    { this.forceId = forceId; this.query = query; this.project = project; this.confirmation = confirmation; Refresh(); }
    internal PersonnelMediationDraft(int forceId, string missing)
    { this.forceId = forceId; UnresolvedOwner = missing; }

    public int ForceId => forceId;
    public int DistrictId => view?.DistrictId ?? 0;
    public IReadOnlyList<int> SelectedOfficerIds => selected;
    public IReadOnlyList<PersonnelDevelopmentOfficer?> TargetSlots => selected
        .Select(id => cards.GetValueOrDefault(id)).ToArray();
    public IReadOnlyList<IReadOnlyList<PersonnelDevelopmentOfficer>> CandidatesBySlot => candidates;
    public IReadOnlyList<PersonnelDevelopmentOfficer> CardTargets => targets;
    public PersonnelMediationView? View => view;
    public bool? IsMarriage => view?.IsMarriage;
    public string? UnresolvedOwner { get; private set; }
    public bool CanConfirm => view?.CanSubmit == true && UnresolvedOwner is null;
    public bool CanMarry => IsMarriage == true && CanConfirm;
    public bool CanSwear => IsMarriage == false && CanConfirm;

    public bool SelectSlot(int slot, int? officerId)
    {
        if (query is null || slot < 0 || slot > 2) return false;
        if (officerId is int id && (slot >= candidates.Count ||
            !candidates[slot].Any(row => row.OfficerId == id && row.Enabled == true))) return false;
        selected = selected.SetItem(slot, officerId ?? 0); Refresh(); return true;
    }
    public void Clear() { if (query is not null) { selected = [0, 0, 0]; Refresh(); } }
    public GameOrder? BuildOrder() => CanConfirm && selected[0] > 0 && selected[1] > 0
        ? new MediationOrder(forceId, DistrictId, selected.Where(id => id > 0).ToArray()) : null;
    public AreaAdministrationQuery<string> QueryConfirmation() => confirmationCache ??=
        CanConfirm && confirmation is not null ? confirmation(selected.Where(id => id > 0).ToArray())
            : AreaAdministrationQuery<string>.Unknown(UnresolvedOwner ?? "personnel-mediation.page.selection");

    private void Refresh()
    {
        if (query is null || project is null) return;
        confirmationCache = null;
        var result = query(selected);
        view = result.Value; UnresolvedOwner = result.UnresolvedInput ?? view?.UnresolvedOwner;
        if (view is null) { candidates = []; targets = []; return; }
        candidates = view.CandidatesBySlot.Select(rows => (IReadOnlyList<PersonnelDevelopmentOfficer>)
            rows.Select(project).ToArray()).ToArray();
        foreach (var row in candidates.SelectMany(rows => rows)) cards[row.OfficerId] = row;
        targets = selected.Where(cards.ContainsKey).Select(id => cards[id]).ToArray();
    }
}
