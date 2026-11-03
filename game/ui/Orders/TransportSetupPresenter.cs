using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The local transport Make page. Its selector, calculator and food
/// warning do not issue orders. Accepted quantities enter the separate mode2
/// target page; only that page's narrated completion can publish creation.</summary>
internal sealed class TransportSetupPresenter : IDisposable
{
    private readonly TransportSetupView view;
    private readonly GameResourceRepository repository;
    private readonly PreviewText text;
    private GameState world;
    private readonly ScenarioCityPreview scenario;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly int source;
    private readonly UiSourceHeaderStack headers;
    private readonly Func<bool> current;
    private readonly Func<int> scrollSpeed;
    private readonly Action<TransportSetupSelection> accepted;
    private readonly Func<int, TransportFoodWarning> prepareFoodWarning;
    private readonly Action<string> unavailable;
    private readonly AreaAdministrationOfficerContent officerContent;
    private TransportPersonSelectionView? personSelection;
    private DeploymentTargetConfirmation? foodConfirmation;
    private TransportFoodWarning? foodWarning;
    private AreaAdministrationOfficerValues? cardValues;
    private int? cardOfficer;
    private bool hasCard;
    private bool disposed;
    internal TransportSetupDraft Draft { get; }

    internal TransportSetupPresenter(TransportSetupView view, GameResourceRepository repository, PreviewText text,
        GameState world, ScenarioCityPreview scenario, ArmyCampaignRuleAdapter rules, int source,
        UiSourceHeaderStack headers, Func<bool> current, Func<int> scrollSpeed,
        Action<TransportSetupSelection> accepted, Func<int, TransportFoodWarning> prepareFoodWarning,
        Action returned, Action<string> unavailable, AdministrationPageChrome.Resources? uiResources=null)
    {
        this.view = view; this.repository = repository; this.text = text; this.world = world;
        this.scenario = scenario; this.rules = rules; this.source = source; this.headers = headers;
        this.current = current; this.scrollSpeed = scrollSpeed; this.accepted = accepted; this.unavailable = unavailable;
        this.prepareFoodWarning = prepareFoodWarning;
        officerContent = new(); Draft = new(world, rules, source);
        view.Configure(repository, text, Presentation(), OpenOfficerSelection, (quantity, value) =>
        {
            if (!Admitted()) return;
            Draft.Edit(quantity, value); Refresh();
        }, RequestDecision, () => { if (Admitted()) returned(); }, Admitted,resources:uiResources);
        view.RegisterHeader(headers);
    }
    private bool Admitted() => !disposed && current() && personSelection is null && foodConfirmation is null;
    private string Unknown => text.Get("detail_unknown");
    private TransportSetupPresentation Presentation()
    {
        var setup = Draft.Setup; var quantities = Draft.Quantities;
        if (!hasCard || cardOfficer != Draft.OfficerId)
        {
            var card = rules.QueryTransportOfficerCard(world, Draft.OfficerId);
            cardValues = officerContent.Create(card.Available ? card.Value : null);
            cardOfficer = Draft.OfficerId; hasCard = true;
        }
        string hint = text.Get("play_transport_setup_unavailable");
        if (setup.DefaultHintMessageId is int id)
        {
            var message = repository.LoadMessages(text.SourceLocale, MessageKind.Game)
                .Messages.Sections[id / 10000].Records[id % 10000];
            hint = message.Literals.Single().Text;
        }
        return new(world.Cities[source].Name, setup.SourceTroops, setup.SourceGold, setup.SourceSupplies,
            setup.CommandBooks, setup.CommandBooksAfter, setup.CommandBookCost, cardValues!,
            Amount(quantities?.Troops, setup.TroopsMinimum, quantities?.MaximumTroops),
            Amount(quantities?.Gold, 0, quantities?.MaximumGold),
            Amount(quantities?.Supplies, 0, quantities?.MaximumSupplies),
            setup.Persons is { Count: > 0 }, Draft.CanAccept, hint);

        TransportSetupAmountState Amount(int? value, int? minimum, int? maximum)
        {
            bool known = value.HasValue && minimum.HasValue && maximum.HasValue && setup.AmountStep.HasValue;
            // Unknown controls use inert geometry and an explicit unknown
            // caption. These zero/one values cannot be edited or submitted.
            int low = minimum ?? 0, high = maximum ?? 0, step = setup.AmountStep ?? 1;
            bool enabled = known && Draft.OfficerId.HasValue && high >= low;
            int calculator = enabled ? Math.Clamp(value!.Value, low, high) : 0;
            return new(value ?? 0, low, high, step, enabled, calculator, maximum is int limit ? "/" + limit.ToString(System.Globalization.CultureInfo.InvariantCulture) : Unknown,
                ValueKnown: value.HasValue, ProfileKnown: known);
        }
    }
    private void Refresh() { if (!disposed) view.Apply(Presentation()); }

    internal void OpenOfficerSelection()
    {
        if (!Admitted() || view.HasCalculator) return;
        var query=rules.PrepareTransportPersonSelection(world,source,Draft.OfficerId);
        if(!query.Available||query.Value is not {} model)
        {unavailable(query.UnresolvedInput??"transport.setup.person-list");return;}
        var modal=new TransportPersonSelectionView();personSelection=modal;view.AddChild(modal);view.SetBusy(true);
        try
        {
            modal.Configure(repository,text,model,()=>!disposed&&current()&&personSelection==modal,result=>
            {
                if(disposed||personSelection!=modal)return;
                personSelection=null;modal.Hide();modal.GetParent()?.RemoveChild(modal);modal.QueueFree();view.SetBusy(false);
                // The caller's current Person remains on Back. A legal row
                // release replaces only this local Make selection.
                if(result is int id&&!Draft.SelectOfficer(id))unavailable("transport.setup.person-selection");
                Refresh();
            },scrollSpeed,resources:view.SourceResources);
            modal.RegisterHeader(headers);
        }
        catch{personSelection=null;modal.Abandon();modal.QueueFree();view.SetBusy(false);throw;}
    }
    private void RequestDecision()
    {
        if (!Admitted() || !Draft.CanAccept || Draft.Quantities?.Supplies is not int food) return;
        foodWarning ??= prepareFoodWarning(food);
        if (!Admitted() || !Draft.CanAccept || Draft.Quantities?.Supplies is not int currentFood) return;
        if (foodWarning.Expense is int expense && Draft.Setup.SourceSupplies is int stock)
            foodWarning = foodWarning with { Required = unchecked(stock - 3 * expense - currentFood) < 1 };
        if (foodWarning.Required is null)
        { unavailable(foodWarning.UnresolvedInput ?? "transport.setup.food-warning"); return; }
        if (foodWarning.Required == false) { accepted(Draft.Accept()); return; }
        var confirmation = rules.QueryTransportFoodConfirmation(world, source);
        if (!confirmation.Available || confirmation.Value is not { } confirmationText)
        { unavailable(confirmation.UnresolvedInput ?? "transport.setup.food-confirmation"); return; }
        var modal = new DeploymentTargetConfirmation(); foodConfirmation = modal; view.AddChild(modal); view.SetBusy(true);
        try
        {
            modal.ConfigureWrappedMessage(repository, text, confirmationText, yes =>
            {
                if (disposed || foodConfirmation != modal) return;
                foodConfirmation = null; modal.Hide(); modal.QueueFree(); view.SetBusy(false);
                if (yes && Admitted() && Draft.CanAccept) accepted(Draft.Accept());
            },resources:view.SourceResources);
        }
        catch { foodConfirmation = null; modal.QueueFree(); view.SetBusy(false); throw; }
    }
    internal bool TryCancelModal()
    {
        if (foodConfirmation is { } warning) { warning.RequestCancel(); return true; }
        if (personSelection is { } person) { person.RequestCancel(); return true; }
        return view.TryCancelModal();
    }
    internal void Resume() { if (!disposed) { view.Show(); view.SetBusy(false); Refresh(); } }
    internal void AcceptFoodWarningWorld(GameState previous, GameState replacement)
    {
        Draft.AcceptFoodWarningWorld(previous, replacement); world = replacement;
        hasCard = false; Refresh();
    }
    internal void AcceptPreferenceWorld(GameState previous, GameState replacement)
    {
        Draft.AcceptPreferenceWorld(previous, replacement); world = replacement;
        // Stock/person identities remain the same under this explicit command.
        // No whole-world fingerprint or defensive clone is needed here.
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (personSelection is { } person) { personSelection = null; person.Abandon(); person.QueueFree(); }
        if (foodConfirmation is { } warning) { foodConfirmation = null; warning.Abandon(); warning.QueueFree(); }
    }
}
