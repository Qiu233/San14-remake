using San14.Core;
using San14.Data;

/// <summary>Owns the city's discardable draft. Selector completion stays on
/// the same UI action: close its input surface, then stage the chosen physical
/// person. A policy change refreshes only policy and command books.</summary>
internal sealed class CityDomesticPresenter : IDisposable
{
    private sealed class Selection(CityDomesticPersonSelection publication, long revision)
    {
        internal readonly CityDomesticPersonSelection Publication = publication;
        internal readonly long Revision = revision;
        internal Action? Close;
        internal bool Opening = true, HasPendingChoice;
        internal CityDomesticCandidate? PendingChoice;
    }
    private readonly CityDomesticView view;
    private readonly CityDomesticDraft draft;
    private readonly PreviewText text;
    private readonly AdministrationMessages messages;
    private readonly AreaAdministrationOfficerContent officers;
    private readonly Func<bool> current;
    private readonly Action<Action> dispatch;
    private readonly Action<CityDomesticTerminal> completionRequested;
    private readonly Action<string> unavailable;
    private readonly Func<CityDomesticDuty, bool> selectorReady;
    private readonly Func<CityDomesticPersonSelection, UiSourceCommonBlur, Action<CityDomesticCandidate?>, Action> openSelector;
    private Selection? selection;
    private bool disposed, busy, sourceCanConfirm;
    internal bool OfficerSelectionOpen => selection is { Opening: false };
    internal CityDomesticDuty? ActiveSelectionDuty => selection?.Publication.Duty;
    internal long OfficerSelectionAcceptedSequence { get; private set; }
    internal CityDomesticDuty? OfficerSelectionAcceptedDuty { get; private set; }

    internal CityDomesticPresenter(CityDomesticView view, GameResourceRepository repository, PreviewText text,
        CityDomesticDraft draft, Func<bool> current, Action<Action> dispatch,
        Action<CityDomesticTerminal> completionRequested, Action<string> unavailable, Func<CityDomesticDuty, bool> selectorReady,
        Func<CityDomesticPersonSelection, UiSourceCommonBlur, Action<CityDomesticCandidate?>, Action> openSelector)
    {
        this.view = view; this.draft = draft; this.text = text; this.current = current;
        this.dispatch = dispatch; this.completionRequested = completionRequested;
        this.unavailable = unavailable; this.selectorReady = selectorReady; this.openSelector = openSelector;
        messages = new(repository, text.SourceLocale); officers = new(repository);
        view.Configure(repository, text, () => Run(Return), () => Run(Confirm),
            role => Run(() => Choose((CityDomesticDuty)role)),
            role => Run(() => Dismiss((CityDomesticDuty)role)), policy => Run(() => SetPolicy((CityDomesticPolicy)policy)));
        Refresh();
    }
    private bool Active => !disposed && current();
    private void Run(Action action) => dispatch(() => { if (Active && !busy && selection is null) action(); });
    private bool Accept(AreaAdministrationDraftResult result)
    {
        if (result.Available) return true;
        unavailable(result.UnresolvedInput ?? "city-domestic.unavailable"); return false;
    }

    internal void Refresh()
    {
        if (!Active || selection is not null) return;
        var query = draft.QueryPage();
        if (!query.Available)
        {
            unavailable(query.UnresolvedInput ?? "city-domestic.page");
            view.ApplySummary(new(null, null, null, null, null, null, new(null, null), new(null, null), null, null, null, null));
            ApplyDuty(CityDomesticDuty.Recruiter, null); ApplyDuty(CityDomesticDuty.Trainer, null);
            ApplyPolicy(null); sourceCanConfirm = false; view.ApplyDecision(false); return;
        }
        var page = query.Value!; var summary = page.Summary;
        var recruiter = page.Recruiter.Available ? page.Recruiter.Value : null;
        var trainer = page.Trainer.Available ? page.Trainer.Value : null;
        view.ApplySummary(new(summary.CityName, Value(summary.Gold),
            Value(summary.GoldWarning) is bool warning ? !warning : null,
            Value(summary.CommandBooks.Current), Value(summary.CommandBooks.After), Value(summary.CommandBooks.Required),
            new(Value(recruiter?.Before), Value(recruiter?.After)), new(Value(trainer?.Before), Value(trainer?.After)),
            Value(recruiter?.Fee), Value(recruiter?.NativeValid), Value(trainer?.Fee), Value(trainer?.NativeValid)));
        ApplyDuty(CityDomesticDuty.Recruiter, recruiter); ApplyDuty(CityDomesticDuty.Trainer, trainer);
        ApplyPolicy(page.Policy.Available ? page.Policy.Value : null);
        ApplyBooks(summary.CommandBooks);
    }
    private void ApplyDuty(CityDomesticDuty duty, CityDomesticDutyView? value)
    {
        var card = value?.Card.Available == true ? value.Card.Value : null;
        bool? valid = Value(value?.NativeValid);
        int choose = duty == CityDomesticDuty.Recruiter ? 0x5758 : 0x5770;
        int dismiss = duty == CityDomesticDuty.Recruiter ? valid == true ? 0x5759 : 0x575A : valid == true ? 0x5771 : 0x5772;
        view.ApplyOfficer((CityDomesticOfficerRole)duty, officers.Create(card, draft.QueryTraitTooltip),
            !busy && selectorReady(duty), !busy && valid == true, Hint(choose),
            valid is null ? null : Hint(dismiss, card?.Name));
    }
    private void ApplyPolicy(CityDomesticPolicyState? value)
    {
        int? selected = value?.Effective.Available == true && value.Effective.Value is CityDomesticPolicy policy ? (int)policy : null;
        view.ApplyPolicy(new(selected, value is null ? null : true, Value(value?.Commerce), Value(value?.Agriculture),
            Value(value?.Barracks), Value(value?.Delegated)));
        view.ApplyPolicyHints(Hint(0x5A77), PolicyHint(value?.CommerceComplete, 0x5A78, 0x5A7B),
            PolicyHint(value?.AgricultureComplete, 0x5A79, 0x5A7C),
            PolicyHint(value?.BarracksComplete, 0x5A7A, 0x5A7D), Hint(0x5A7E));
    }
    private string? PolicyHint(AreaAdministrationQuery<bool?>? complete, int full, int available) =>
        Value(complete) is bool value ? Hint(value ? full : available) : null;
    private void ApplyBooks(CityDomesticCommandBooks books)
    {
        view.ApplyBooks(Value(books.Current), Value(books.After), Value(books.Required));
        sourceCanConfirm = Value(books.SourceBudgetCanConfirm) == true;
        // This is the original UI budget gate. Formal permission and every
        // reached command rule remain in the session's sole execution path.
        view.ApplyDecision(!busy && selection is null && sourceCanConfirm);
        bool insufficient = Value(books.Current) is int currentBooks && Value(books.Required) is int cost && currentBooks < cost;
        view.ApplyDefaultHint(Hint(insufficient ? 0x57C7 : 0x5A7F) ?? text.Get("detail_unknown"));
    }
    private string? Hint(int id, string? person = null)
    {
        try { return messages.Format(id, officerName: person, cityName: draft.QueryDefaultHintBaseName); }
        catch (InvalidDataException unknown) { unavailable(unknown.Message); return null; }
    }
    private void SetPolicy(CityDomesticPolicy policy)
    {
        if (!Accept(draft.SetPolicy(policy))) return;
        var currentPolicy = draft.QueryPolicy(); ApplyPolicy(currentPolicy.Available ? currentPolicy.Value : null);
        var books = draft.QueryCommandBooks();
        if (books.Available) ApplyBooks(books.Value!);
        else { sourceCanConfirm = false; view.ApplyBooks(null, null, null); view.ApplyDecision(false); unavailable(books.UnresolvedInput!); }
    }
    private void Dismiss(CityDomesticDuty duty)
    { if (Accept(draft.Dismiss(duty))) Refresh(); }
    private void Choose(CityDomesticDuty duty)
    {
        if (!selectorReady(duty)) return;
        var query = draft.QueryPersonSelection(duty);
        if (!query.Available) { unavailable(query.UnresolvedInput!); return; }
        var request = new Selection(query.Value!, draft.Revision); selection = request;
        view.ApplyDecision(false);
        try
        {
            Action close = openSelector(request.Publication, view.CommonBackdrop, chosen => dispatch(() =>
            {
                if (request.Opening)
                {
                    if (!request.HasPendingChoice) { request.PendingChoice = chosen; request.HasPendingChoice = true; }
                    return;
                }
                Complete(request, chosen);
            }));
            if (ReferenceEquals(selection, request))
            {
                request.Close = close; request.Opening = false;
                if (request.HasPendingChoice) Complete(request, request.PendingChoice);
            }
            else close();
        }
        catch { if (ReferenceEquals(selection, request)) selection = null; throw; }
    }
    private void Complete(Selection request, CityDomesticCandidate? chosen)
    {
        if (!Active || !ReferenceEquals(selection, request) || request.Revision != draft.Revision) return;
        selection = null; request.Close?.Invoke(); request.Close = null;
        if (!Active || selection is not null || request.Revision != draft.Revision) return;
        if (chosen is not null)
        {
            if (!request.Publication.Candidates.Contains(chosen)) throw new InvalidDataException("City officer choice is outside its current publication.");
            if (Accept(draft.StageDuty(request.Publication.Duty, chosen)))
            {
                OfficerSelectionAcceptedDuty = request.Publication.Duty;
                OfficerSelectionAcceptedSequence++;
            }
        }
        Refresh();
    }
    // Both terminals are formal commands. A rejected command leaves this
    // editable projection alive; only the host's success/abandon path disposes it.
    internal void RequestBack() => Run(Return);
    private void Return() => completionRequested(CityDomesticTerminal.Cancel);
    private void Confirm() { if (sourceCanConfirm) completionRequested(CityDomesticTerminal.Confirm); }
    internal CityDomesticDraft Draft => draft;
    internal void ApplyNotice(string message) { if (Active) view.ApplyDefaultHint(message); }
    internal void SetBusy(bool value) { busy = value; if (!value) Refresh(); else view.ApplyDecision(false); }
    private static int? Value(AreaAdministrationQuery<int?>? query) => query?.Available == true ? query.Value : null;
    private static bool? Value(AreaAdministrationQuery<bool?>? query) => query?.Available == true ? query.Value : null;
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        var request = selection; selection = null; request?.Close?.Invoke();
        draft.Dispose(); view.Abandon();
    }
}
