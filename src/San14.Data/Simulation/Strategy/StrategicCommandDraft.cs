using San14.Core;

namespace San14.Data;

public sealed record StrategicCommandTargetChoice(int Id, string Name, int? ForceId, int? HomeId,
    int? HexId, bool? Enabled, string? DisabledReason, string? UnresolvedOwner);
public sealed record StrategicCommandPreviewEffect(string Kind, int? Before, int? Change, int? After);
public sealed record StrategicCommandBudget(int? GoldBefore, int? GoldAfter, int? GoldCost,
    int? BooksBefore, int? BooksAfter, int? BookCost,
    int? MilitaryBefore, int? MilitaryAfter, int? MilitaryCost);
public sealed record StrategicCommandTargetDetails(string? ForceName, string? GovernorName,
    int? Loyalty, string? RelationshipText, string? RelatedOfficerName)
{ public string? HomeLocationName { get; init; } }
public sealed record StrategicCommandPreview(int? ChancePercent, int? TravelDays,
    IReadOnlyList<StrategicCommandPreviewEffect> Effects, string? Guide, string? UnresolvedOwner)
{ public string? AdviceText { get; init; } }
public sealed record StrategicCommandIndependenceChoice(int Value, string Caption, bool Selected, bool Enabled);
public sealed record StrategicCommandMilitaryAmountOptions(int Current, int? Minimum, int? Maximum,
    int? Step, IReadOnlyList<int> Presets, string? UnresolvedOwner);
public sealed record StrategicCommandAdvicePresentation(string? AdviceText,
    PersonnelDevelopmentOfficer? AdvisorOfficer, CampaignAppRandomState? AppRandomSnapshot);

internal sealed record StrategicCommandPageSelection(
    IReadOnlyList<PersonnelDevelopmentOfficer> Executors,
    IReadOnlyList<StrategicCommandTargetChoice> Targets,
    IReadOnlyList<StrategicCommandTargetChoice> SecondaryForces,
    StrategicCommandBudget Budget, StrategicCommandPreview Preview,
    bool? CanConfirm, bool? CanContinue, string? UnresolvedOwner)
{
    internal int? RequiredMilitaryIntelligence { get; init; }
    internal int? MinimumMilitaryIntelligence { get; init; }
    internal int? MaximumMilitaryIntelligence { get; init; }
    internal int? MilitaryIntelligenceStep { get; init; }
    internal StrategicCommandTargetDetails TargetDetails { get; init; } = new(null, null, null, null, null);
    internal PersonnelDevelopmentOfficer? AdvisorOfficer { get; init; }
}

/// <summary>The three original layouts share one strategic packet. Selection
/// refreshes this page's captured candidates; it never writes the campaign.</summary>
public sealed class StrategicCommandDraft
{
    private readonly Func<int?, int?, int?, int, int, StrategicCommandPageSelection>? read;
    private readonly IReadOnlyList<(int Value, string Caption)> independenceChoices = [];
    private StrategicCommandPageSelection? selection;
    private readonly Func<int, int, int?, int, int, CampaignAppRandomState,
        AreaAdministrationQuery<StrategicCommandAdvicePresentation>>? prepareAdvice;
    private (int Executor, int Target, int? Second, int Military, int Independence)? adviceKey;
    private AreaAdministrationQuery<StrategicCommandAdvicePresentation>? advice;

    internal StrategicCommandDraft(int forceId, int kind, string title,
        IReadOnlyList<(int Value, string Caption)> independenceChoices,
        Func<int?, int?, int?, int, int, StrategicCommandPageSelection> read,
        Func<int, int, int?, int, int, CampaignAppRandomState,
            AreaAdministrationQuery<StrategicCommandAdvicePresentation>>? prepareAdvice = null)
    {
        ForceId = forceId; Kind = kind; Title = title;
        this.independenceChoices = independenceChoices; this.read = read;
        this.prepareAdvice = prepareAdvice; Refresh();
    }
    internal StrategicCommandDraft(int forceId, int kind, string missing)
    { ForceId = forceId; Kind = kind; UnresolvedOwner = missing; }

    public int ForceId { get; }
    public int Kind { get; }
    public string Title { get; } = "";
    public int? ExecutorId { get; private set; }
    public int? TargetId { get; private set; }
    public int? SecondForceId { get; private set; }
    public int MilitaryIntelligence { get; private set; }
    public int IndependenceChoice { get; private set; }
    public IReadOnlyList<PersonnelDevelopmentOfficer> Executors => selection?.Executors ?? [];
    public IReadOnlyList<StrategicCommandTargetChoice> Targets => selection?.Targets ?? [];
    public IReadOnlyList<StrategicCommandTargetChoice> SecondaryForces => selection?.SecondaryForces ?? [];
    public PersonnelDevelopmentOfficer? Executor => Executors.FirstOrDefault(row => row.OfficerId == ExecutorId);
    public StrategicCommandTargetChoice? SelectedTarget => Targets.FirstOrDefault(row => row.Id == TargetId);
    public StrategicCommandTargetChoice? SelectedSecondForce => SecondaryForces.FirstOrDefault(row => row.Id == SecondForceId);
    public PersonnelDevelopmentOfficer? AdvisorOfficer => advice?.Value?.AdvisorOfficer ?? selection?.AdvisorOfficer;
    public PersonnelDevelopmentOfficer? Advisor => AdvisorOfficer;
    public StrategicCommandTargetDetails TargetDetails => selection?.TargetDetails ?? new(null, null, null, null, null);
    public StrategicCommandBudget Budget => selection?.Budget ?? new(null, null, null, null, null, null, null, null, null);
    public StrategicCommandPreview Preview => selection?.Preview is { } preview
        ? preview with { AdviceText = advice?.Value?.AdviceText ?? preview.AdviceText }
        : new(null, null, [], null, UnresolvedOwner);
    public int? RequiredMilitaryIntelligence => selection?.RequiredMilitaryIntelligence;
    public int? MinimumMilitaryIntelligence => selection?.MinimumMilitaryIntelligence;
    public int? MaximumMilitaryIntelligence => selection?.MaximumMilitaryIntelligence;
    public int? MilitaryIntelligenceStep => selection?.MilitaryIntelligenceStep;
    public string? UnresolvedOwner { get; private set; }
    public bool CanConfirm => selection?.CanConfirm == true && UnresolvedOwner is null;
    public bool CanContinue => selection?.CanContinue == true && CanConfirm;
    public IReadOnlyList<StrategicCommandIndependenceChoice> IndependenceChoices => Kind != 11 ? [] :
        independenceChoices.Select(row => new StrategicCommandIndependenceChoice(row.Value,
            row.Caption, row.Value == IndependenceChoice, true)).ToArray();

    public bool SelectExecutor(int id)
    {
        if (!Executors.Any(row => row.OfficerId == id && row.Enabled == true)) return false;
        ExecutorId = id; Refresh(); return true;
    }
    public bool SelectTarget(int id)
    {
        if (!Targets.Any(row => row.Id == id && row.Enabled == true)) return false;
        TargetId = id; SecondForceId = null; Refresh(); return true;
    }
    public bool SelectSecondForce(int id)
    {
        if (Kind != 10 || !SecondaryForces.Any(row => row.Id == id && row.Enabled == true)) return false;
        SecondForceId = id; Refresh(); return true;
    }
    public bool SetMilitaryIntelligence(int amount)
    {
        if (MinimumMilitaryIntelligence is not int minimum || MaximumMilitaryIntelligence is not int maximum ||
            amount < minimum || amount > maximum) return false;
        MilitaryIntelligence = amount; Refresh(); return true;
    }
    public bool UseRequiredMilitaryIntelligence() => RequiredMilitaryIntelligence is int required &&
        SetMilitaryIntelligence(required);
    public bool SetIndependenceChoice(int value)
    {
        if (Kind != 11 || !independenceChoices.Any(row => row.Value == value)) return false;
        IndependenceChoice = value; Refresh(); return true;
    }
    public StrategicCommandMilitaryAmountOptions QueryMilitaryIntelligenceAmountOptions() =>
        new(MilitaryIntelligence, MinimumMilitaryIntelligence, MaximumMilitaryIntelligence,
            MilitaryIntelligenceStep, [], MinimumMilitaryIntelligence is null || MaximumMilitaryIntelligence is null
                ? UnresolvedOwner ?? "strategic.page.military_range" : null);
    public GameOrder? BuildOrder() => CanConfirm ? new IssueStrategicCommandOrder(ForceId, Kind,
        ExecutorId!.Value, TargetId!.Value, MilitaryIntelligence, IndependenceChoice, SecondForceId) : null;
    public AreaAdministrationQuery<string> QueryConfirmation() => AreaAdministrationQuery<string>.Known("");
    public AreaAdministrationQuery<StrategicCommandAdvicePresentation> PrepareAdvice(CampaignAppRandomState appRandomSnapshot)
    {
        if (ExecutorId is not int executor || TargetId is not int target || Kind == 10 && SecondForceId is null)
            return AreaAdministrationQuery<StrategicCommandAdvicePresentation>.Known(new(null, AdvisorOfficer, appRandomSnapshot));
        var key = (executor, target, SecondForceId, MilitaryIntelligence, IndependenceChoice);
        if (adviceKey == key && advice is not null)
            return advice.Value is { } cached
                ? AreaAdministrationQuery<StrategicCommandAdvicePresentation>.Known(cached with { AppRandomSnapshot = appRandomSnapshot })
                : advice;
        adviceKey = key;
        return advice = prepareAdvice is not null
            ? prepareAdvice(executor, target, SecondForceId, MilitaryIntelligence, IndependenceChoice, appRandomSnapshot)
            : AreaAdministrationQuery<StrategicCommandAdvicePresentation>.Unknown("strategic.page.advice_source");
    }

    private void Refresh()
    {
        if (read is null) return;
        try
        {
            selection = read(ExecutorId, TargetId, SecondForceId, MilitaryIntelligence, IndependenceChoice);
            UnresolvedOwner = selection.UnresolvedOwner;
            if (ExecutorId is null || TargetId is null || Kind == 10 && SecondForceId is null)
            { adviceKey = null; advice = null; }
            else if (adviceKey != (ExecutorId.Value, TargetId.Value, SecondForceId, MilitaryIntelligence, IndependenceChoice))
                advice = null;
        }
        catch (InvalidDataException error) { UnresolvedOwner = error.Message; }
    }
}
