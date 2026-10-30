using San14.Core;

namespace San14.Data;

/// <summary>One ordinary proposal controller. Selection changes do not regenerate
/// offers, sample summary text, or execute their handlers.</summary>
public sealed class ProposalDraft
{
    private readonly ArmyCampaignRuleAdapter package;
    private readonly int forceId;
    private ProposalLifecycleSource? source;
    private ProposalPage basePage;
    private readonly HashSet<int> selected = [];
    private bool officerPage;
    private AreaAdministrationQuery<string>? confirmation;
    public string? UnresolvedOwner { get; private set; }
    public AreaAdministrationQuery<ProposalAdmission> Admission { get; private set; } =
        AreaAdministrationQuery<ProposalAdmission>.Unknown("proposal.admission_not_published");
    public ProposalPage Page { get; private set; }

    internal ProposalDraft(ArmyCampaignRuleAdapter package, GameState state, int forceId)
    {
        this.package = package; this.forceId = forceId;
        basePage = Page = new(forceId, string.Empty, [], []);
        Refresh(state);
    }

    public void Refresh(GameState state)
    {
        selected.Clear(); confirmation = null; source = null;
        try
        {
            if (package.ReadProposalEntryPolicy(state) is { } blocked)
            {
                Admission = AreaAdministrationQuery<ProposalAdmission>.Known(blocked);
                basePage = new(forceId, string.Empty, [], []); UnresolvedOwner = null;
            }
            else
            {
                source = package.CreateProposalLifecycle(state, forceId);
                var admission = package.ReadProposalAdmissionCurrent(source);
                Admission = AreaAdministrationQuery<ProposalAdmission>.Known(admission);
                basePage = admission.Allowed ? source.PublishPage() : new(forceId, string.Empty, [], []);
                if (!admission.Allowed) source = null;
                UnresolvedOwner = null;
            }
        }
        catch (InvalidDataException missing)
        {
            Admission = AreaAdministrationQuery<ProposalAdmission>.Unknown(missing.Message);
            source = null; UnresolvedOwner = missing.Message;
            basePage = new(forceId, string.Empty, [], []) { UnresolvedOwner = missing.Message };
        }
        PublishSelection();
    }

    public bool Select(int slotId, bool officerPage)
    {
        var rows = officerPage ? basePage.OfficerRows : basePage.FirstRows;
        var row = rows.FirstOrDefault(value => value.Id == slotId);
        if (row is null || row.OpensOfficerPage) { selected.Clear(); confirmation = null; PublishSelection(); return row is not null; }
        this.officerPage = officerPage;
        if (!officerPage) selected.Clear();
        else if (selected.Remove(slotId)) { confirmation = null; PublishSelection(); return true; }
        if (!row.CanAdopt || selected.Sum(id => FindRow(id)?.Cost ?? 0) + (row.Cost ?? 0) > basePage.AvailableGold)
        { PublishSelection(); return false; }
        selected.Add(slotId); confirmation = null; PublishSelection(); return true;
    }

    public void SelectAll()
    {
        officerPage = true; confirmation = null;
        var eligible = basePage.OfficerRows.Where(row => row.CanAdopt && !row.Adopted).ToArray();
        if (eligible.Length != 0 && eligible.All(row => selected.Contains(row.Id))) selected.Clear();
        else
        {
            long remaining = (basePage.AvailableGold ?? 0) - selected.Sum(id => (long)(FindRow(id)?.Cost ?? 0));
            foreach (var row in eligible)
                if (!selected.Contains(row.Id) && row.Cost is int cost && cost <= remaining)
                { selected.Add(row.Id); remaining -= cost; }
        }
        PublishSelection();
    }

    public AreaAdministrationQuery<string> QueryConfirmation()
    {
        if (confirmation is not null) return confirmation;
        if (!Page.CanSubmit || source is null) return AreaAdministrationQuery<string>.Unknown(UnresolvedOwner ?? "proposal.selection");
        try { return confirmation = AreaAdministrationQuery<string>.Known(source.Confirmation(OrderedSelection(), officerPage)); }
        catch (InvalidDataException missing) { return confirmation = AreaAdministrationQuery<string>.Unknown(missing.Message); }
    }
    public AdoptProposalBatchOrder? BuildOrder() => Page.CanSubmit ? new(forceId, OrderedSelection()) : null;
    private int[] OrderedSelection() => (officerPage ? basePage.OfficerRows : basePage.FirstRows)
        .Where(row => selected.Contains(row.Id)).Select(row => row.Id).ToArray();
    private ProposalRow? FindRow(int id) => basePage.OfficerRows.Concat(basePage.FirstRows).FirstOrDefault(row => row.Id == id);
    private void PublishSelection()
    {
        bool enabled = source is not null && selected.Count != 0 && selected.All(id => FindRow(id)?.CanAdopt == true) &&
            selected.Sum(id => (long)(FindRow(id)?.Cost ?? 0)) <= basePage.AvailableGold &&
            basePage.AvailableActionPoints >= basePage.ActionPointCost;
        Page = basePage with { SelectedProposalIds = OrderedSelection(), CanSubmit = enabled };
    }
}

internal sealed partial class ProposalLifecycleSource
{
    internal string Confirmation(IReadOnlyList<int> slots, bool officerPage)
    {
        var selected = slots.Select(slot => State.Proposals!.Read(slot)).ToArray();
        if (selected.Length == 0) throw Missing("selection");
        var abilities = new TacticRemakeCurrentAbilities(Current, Scenario, Language, SimulationRuleIdentity.ForLocale(Language));
        var advisor = new PersonDeathHistorySelection(State, Current,
            Current.ReadOwnedCurrentListPool() ?? throw Missing("confirmation_pool"), abilities,
            Package.ProposalGovernorInputs, (force, effect) => Panels.ReadPanelModifier(State, force, effect))
            .Select(ForcePrimaryDistrictSource.Resolve(Current, PlayerForce));
        int speaker = K(Current.ReadPersonIdentity(K(advisor.PhysicalSlot, "confirmation_advisor_pointer")), "confirmation_advisor_identity");
        int root = officerPage ? selected.Length == 1 ? 0x4F09 : 0x4F0A :
            selected[0].Kind is >= ProposalKind.DevelopmentUp and <= ProposalKind.SecurityDown or >= ProposalKind.BuyFood and <= ProposalKind.FreePrisoner ? 0x4EF2 : 0x4EF3;
        int[] values = officerPage && selected.Length > 1
            ? [selected.Length, AvailableBooks, AvailableBooks - BookCostPerProposal, BookCostPerProposal, 0]
            : [AvailableBooks, AvailableBooks - BookCostPerProposal, 0, 0, 0];
        return Package.FormatProposalReportCurrent(State, Current, root,
            new(Person0: speaker, Person1: RulerId(), Person2: selected[0].ProposerPersonId, Integers: values)).Text;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OwnedArmyGovernorComparatorInputs ProposalGovernorInputs => _governorInputs;
    public ProposalDraft OpenProposalDraft(GameState state, int forceId) => new(this, state, forceId);
}
