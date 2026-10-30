using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original dispatch/call panels over one page-owned movement draft.</summary>
internal partial class CityOfficerMovementView : Control
{
    private AdministrationPageChrome chrome = null!;
    private AreaAdministrationOfficerView officer = null!;
    private UiSourceCommandButton? destination;
    private UiSourceText city = null!, people = null!, books = null!, afterBooks = null!, bookDelta = null!;
    private UiSourceText? journey, dayUnit;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Func<bool>? admitted;
    private Action? returned, accepted, choosePersons, chooseDestination;
    private CityOfficerMovementPage? page;
    private AreaAdministrationOfficerValues card = new(null, null, null, null, null);
    private bool abandoned, busy;
    private string unknown = "", countFormat = "", defaultHint = "";
    internal CityOfficerMovementPage? Publication => page;
    internal AdministrationPageChrome.Resources SharedResources => new(chrome.Assets, chrome.Font, chrome.SpriteShader);

    internal void Configure(GameResourceRepository repository, PreviewText text, OfficerMoveMode mode,
        Func<bool> admitted, Action returned, Action accepted, Action choosePersons, Action chooseDestination)
    {
        this.repository = repository; this.text = text; this.admitted = admitted;
        this.returned = returned; this.accepted = accepted; this.choosePersons = choosePersons;
        this.chooseDestination = chooseDestination;
        Name = "CityOfficerMovement"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bool summon = mode == OfficerMoveMode.Summon;
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, summon ? 0x0271 : 0x0270, RequestBack,
            () => { if (ReadAdmission() && page?.CanConfirm == true) this.accepted?.Invoke(); });
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing movement caption {id:X4}.");
        unknown = text.Get("detail_unknown"); countFormat = Message(0x0323);
        var narration = new AdministrationMessages(repository, text.SourceLocale);
        defaultHint = narration.Format(summon ? 0x57A7 : 0x578A);
        chrome.ApplyReturnHint(narration.Format(0x2733));
        var source = new UiSourceLayout(repository, summon ? 0x55A1E9DAu : 0xC17AA822u);
        var panel = new Control { Name = "MovementSourcePanel", Position = summon ? new(450,341) : new(450,313),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Stop };
        chrome.Canvas.AddChild(panel);
        int officerNode = summon ? 13 : 30;
        foreach (var node in source.Nodes.Where(n => n.Type is 1 or 3))
            if (!Within(node.Index, officerNode) && (summon || !Within(node.Index, 12)))
                source.Art(panel, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        bool Within(int index, int root)
        {
            for (int? current = index; current is int i; current = source.Nodes[i].ParentIndex)
                if (i == root) return true;
            return false;
        }
        foreach (var (node, message) in summon
            ? new[] { (5,0x0322), (11,0x0542), (82,0x0292) }
            : new[] { (5,0x0321), (11,0x0322), (21,0x026C), (28,0x0542), (98,0x0292) })
            source.Text(panel, node, default, Message(message), chrome.Font, MapTextPresets.Create()[13]);
        city = source.Text(panel, 7, default, "", chrome.Font); city.Name = "MovementExecutingCity";
        people = source.Text(panel, summon ? 77 : 104, default, "", chrome.Font); people.Name = "MovementSelectedOfficerCount";
        books = source.Text(panel, summon ? 84 : 100, default, unknown, chrome.Font); books.Name = "MovementCommandBooks";
        afterBooks = source.Text(panel, summon ? 86 : 102, default, unknown, chrome.Font); afterBooks.Name = "MovementAfterCommandBooks";
        bookDelta = source.Text(panel, summon ? 87 : 103, default, unknown, chrome.Font); bookDelta.Name = "MovementCommandBookCost";
        officer = new() { Name = "MovementOfficerCard", Position = source.Rectangle(officerNode).Position };
        panel.AddChild(officer);
        officer.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
            () => { if (ReadAdmission()) this.choosePersons?.Invoke(); }, tooltipHost: this);
        officer.SelectionControl.Name = "ChooseMovementOfficers";
        if (!summon)
        {
            var box = source.Rectangle(12);
            destination = new() { Name = "ChooseMovementDestination", Position = box.Position, Size = box.Size };
            panel.AddChild(destination);
            destination.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, unknown, OpenCities);
            journey = source.Text(panel, 23, default, unknown, chrome.Font); journey.Name = "MovementJourney";
            dayUnit = source.Text(panel, 24, default, Message(0x0469), chrome.Font);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_officer_mount_node", officerNode);
        SetMeta("original_movement_layout_recovered", true);
        RefreshAdmission();
    }

    internal void Apply(CityOfficerMovementPage publication, string? hint = null)
    {
        if (abandoned) return;
        page = publication; city.Text = publication.CityName;
        people.Text = countFormat.Replace("%d",
            publication.SelectedOfficerIds.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        books.Text = Number(publication.AvailableBooks);
        afterBooks.Text = publication.AvailableBooks is int available && publication.RequiredBooks is int cost ? Number(available-cost) : unknown;
        if (publication.RequiredBooks is int debit)
            bookDelta.SetRuns(new("("), new("-"+Number(debit), MapTextPresets.Create()[40].LT.CpuPacked), new(")"));
        else bookDelta.Text = unknown;
        if (journey is not null) journey.Text = publication.DestinationCityId is null?"":Number(publication.TravelDays);
        if (dayUnit is not null) dayUnit.Visible = publication.DestinationCityId is not null;
        if (destination is not null) destination.Caption = publication.DestinationCityId is null?"":publication.Destinations.FirstOrDefault(v => v.CityId == publication.DestinationCityId)?.Name ?? unknown;
        chrome.ApplyDefaultHint(hint ?? (publication.UnresolvedInput is null ? defaultHint : unknown));
        RefreshAdmission();
    }
    internal void ApplyOfficerCard(AreaAdministrationOfficerValues values)
    { if (!abandoned) { card = values; RefreshAdmission(); } }

    private void OpenCities()
    { if (ReadAdmission() && page is { Mode: OfficerMoveMode.Dispatch }) chooseDestination?.Invoke(); }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
    private void RefreshAdmission()
    {
        bool enabled = !busy;
        string? name = page is { SelectedOfficerIds.Count: > 0 } value
            ? value.Candidates.FirstOrDefault(p => p.OfficerId == value.SelectedOfficerIds[0])?.Name : string.Empty;
        officer?.Apply(card with { Name = name }, enabled && page is { Candidates.Count: > 0, MaximumOfficers: > 0 });
        if (destination is not null) destination.Disabled = !enabled || page is not { Destinations.Count: > 0 };
        chrome?.ApplyReturn(enabled); chrome?.ApplyDecision(enabled && page?.CanConfirm == true, true);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; RefreshAdmission(); } }
    internal void ApplyNotice(string message) { if (!abandoned) chrome.ApplyDefaultHint(message); }
    internal void RequestBack()
    { if (ReadAdmission()) returned?.Invoke(); }
    public override void _Process(double delta) { if (!abandoned && chrome is not null) chrome.ApplyHoverHint(null); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; returned = accepted = choosePersons = chooseDestination = null; page = null;
        destination?.Abandon(); officer?.Abandon(); chrome?.Abandon(); SetProcess(false); Hide();
    }
}
