using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original new-game officer management menu. All edits belong
/// to one reversible draft; the application alone publishes the accepted copy.</summary>
internal partial class RegisteredOfficerNewGameView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private RegisteredOfficerNewGameDraft draft = null!;
    private AdministrationPageChrome chrome = null!;
    private Func<bool>? admitted;
    private Action<RegisteredOfficerNewGameSelection>? accepted;
    private Action? returned;
    private Action? historicalOfficers;
    private Func<int> scrollSpeed = () => 3;
    private Action<IReadOnlyList<int>, Action<int?>>? chooseCity;
    private readonly List<UiSourceCommandButton> commands = [];
    private CommandSelectionView? selector;
    private RegisteredOfficerNewGameForceView? forces;
    private OfficerEditorPortraitCatalog? portraits;
    private DeploymentTargetConfirmation? confirmation;
    private bool abandoned, busy, picking;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        RegisteredOfficerNewGameDraft draft, Func<bool> admitted,
        Action<RegisteredOfficerNewGameSelection> accepted, Action returned,
        Func<int>? scrollSpeed = null, Action<IReadOnlyList<int>, Action<int?>>? chooseCity = null,
        Action? historicalOfficers = null,OfficerEditorPortraitCatalog? portraits=null)
    {
        this.repository = repository; this.text = text; this.draft = draft;this.portraits=portraits;
        this.admitted = admitted; this.accepted = accepted; this.returned = returned;
        this.historicalOfficers = historicalOfficers;
        this.chooseCity = chooseCity; if (scrollSpeed is not null) this.scrollSpeed = scrollSpeed;
        Name = "RegisteredOfficerNewGame"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x06AD, RequestBack, Accept);
        var source = new UiSourceLayout(RegisteredOfficerNewGameLayouts.LoadMenu(repository));
        source.Blur(chrome.Canvas, 1, source.Rectangle(1));
        source.Art(chrome.Canvas, 2, source.Rectangle(2), chrome.Assets, chrome.SpriteShader);
        var heading = new UiSourceLayout(RegisteredOfficerNewGameLayouts.LoadHeading(repository));
        Vector2 headingMount = source.Rectangle(3).Position;
        heading.Art(chrome.Canvas, 1, heading.Rectangle(1, headingMount), chrome.Assets, chrome.SpriteShader);
        heading.Text(chrome.Canvas, 2, headingMount, Message(0x6AE), chrome.Font);
        Vector2 historicalHeadingMount = source.Rectangle(21).Position;
        heading.Art(chrome.Canvas, 1, heading.Rectangle(1, historicalHeadingMount), chrome.Assets, chrome.SpriteShader);
        heading.Text(chrome.Canvas, 2, historicalHeadingMount, Message(0x6AF), chrome.Font);
        int[] mounts = [6, 9, 12, 15, 18, 24]; int[] captions = [0x48, 0x4A, 0x49, 0x6B8, 0x69, 0x6A];
        Action[] actions = [ChooseAdopted, OpenForces, ChoosePlacement, ChooseReset, ResetAll,
            () => { if (CanEdit()) this.historicalOfficers?.Invoke(); }];
        for (int index = 0; index < mounts.Length; index++)
        {
            var box = source.Rectangle(mounts[index]);
            var button = new UiSourceCommandButton { Name = "NewOfficerManagement" + index,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(captions[index]), actions[index]);
            commands.Add(button);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Refresh();
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing registered-officer management caption.");
    private string GameMessage(int id, int? city = null) => RegisteredOfficerNewGameMessages.Format(repository, text.SourceLocale, id, draft, city);
    private bool PageAdmitted() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private bool CanEdit() => PageAdmitted() && selector is null && confirmation is null && forces is null && !picking;
    private void Refresh()
    {
        if (abandoned || chrome is null) return;
        bool active = CanEdit();
        for (int index = 0; index < commands.Count; index++) commands[index].Disabled = !active || (index switch
        {
            0 or 4 => draft.Library.Officers.IsEmpty,
            1 => draft.Selection.AdoptedOfficerIds.Length == 0,
            2 => !draft.Selection.AdoptedOfficerIds.Any(id => !draft.IsForceMember(id)),
            3 => !draft.Selection.Placements.Any(entry => !draft.IsForceMember(entry.OfficerId)),
            5 => historicalOfficers is null,
            _ => false
        });
        chrome.ApplyDecision(active, true); chrome.ApplyReturn(active);
    }

    private CommandSelectionRow PersonRow(int id, bool enabled = true, bool locked = false)
    {
        var officer = draft.Officer(id);
        string location = draft.Location(id) is int city ? draft.Cities.First(value => value.Slot == city).Name : "";
        return new(id, [CommandSelectionCell.PersonName(officer.Name, officer.FamilyNameSortKey + officer.GivenNameSortKey),
            new(draft.Age(id).ToString(), draft.Age(id)), new(location)], enabled, LockedSelected: locked);
    }

    private void ChooseAdopted()
    {
        if (!CanEdit()) return;
        var rows = draft.Library.Officers.Select(officer => PersonRow(officer.Id, true, draft.IsForceMember(officer.Id))).ToArray();
        SelectPeople(0x48, rows, draft.Selection.AdoptedOfficerIds, result => draft.Adopt(result));
    }

    private void OpenForces()
    {
        if (!CanEdit()) return;
        forces = new(); AddChild(forces); chrome.Hide();
        forces.Configure(draft, repository, text, PageAdmitted, Refresh, () =>
        {
            var old = forces; forces = null;
            if (old is not null) { old.Abandon(); RemoveChild(old); old.QueueFree(); }
            if (!abandoned) { chrome.Show(); Refresh(); }
        }, scrollSpeed, chooseCity,portraits);
        Refresh();
    }

    private void ChoosePlacement()
    {
        if (!CanEdit()) return;
        var rows = draft.Selection.AdoptedOfficerIds.Where(id => !draft.IsForceMember(id)).Select(id => PersonRow(id)).ToArray();
        SelectPeople(0x621, rows, [], selected => { if (selected.Count > 0) PickPlacementCity(selected); }, GameMessage(0x27E5));
    }

    private void ChooseReset()
    {
        if (!CanEdit()) return;
        var rows = draft.Selection.Placements.Where(entry => !draft.IsForceMember(entry.OfficerId)).Select(entry => PersonRow(entry.OfficerId)).ToArray();
        SelectPeople(0x6B8, rows, [], selected => { if (selected.Count > 0) draft.ClearPlacements(selected); });
    }

    private void SelectPeople(int title, IReadOnlyList<CommandSelectionRow> rows, IReadOnlyList<int> initial,
        Action<IReadOnlyList<int>> apply, string? guide = null)
    {
        selector = new(); AddChild(selector);
        selector.Configure(repository, text, title,
            [new("name", Message(0x51), 480), new("age", Message(0x4E7), 180, true), new("city", Message(0x205), 480)],
            rows, initial, rows.Count, PageAdmitted, result =>
            {
                var old = selector; selector = null;
                if (old is not null) { old.Abandon(); RemoveChild(old); old.QueueFree(); }
                if (abandoned) return;
                if (result is not null) apply(result); Refresh();
            }, scrollSpeed, guide);
        Refresh();
    }

    private void PickPlacementCity(IReadOnlyList<int> officers)
    {
        if (abandoned) return;
        var eligible = draft.Cities.Where(city => city.Slot is >= 1 and <= 51 && city.ForceSlot is not (>= 46 and <= 50))
            .Select(city => city.Slot).ToArray();
        if (chooseCity is null) throw new InvalidDataException("registered_officers.new_game.city_picker_owner");
        picking = true; chrome.Hide(); Refresh();
        chooseCity(eligible, city =>
        {
            if (abandoned) return;
            picking = false; chrome.Show(); Refresh();
            if (city is not int selected) return;
            if (!eligible.Contains(selected)) throw new InvalidDataException("registered_officers.new_game.city_picker_result");
            if (draft.CityHasForce(selected)) Confirm(0x27E8, yes =>
            {
                if (yes) { draft.Place(officers, selected, true); Refresh(); }
                else Confirm(0x27E9, free =>
                { if (free) { draft.Place(officers, selected); Refresh(); } else PickPlacementCity(officers); });
            }, selected);
            else Confirm(0x27EB, yes =>
            { if (yes) { draft.Place(officers, selected); Refresh(); } else PickPlacementCity(officers); });
        });
    }

    private void ResetAll()
    {
        if (CanEdit()) Confirm(0x27CB, yes => { if (yes) draft.Reset(); Refresh(); });
    }

    private void Confirm(int message, Action<bool> closed, int? city = null)
    {
        confirmation = new(); AddChild(confirmation);
        confirmation.ConfigureWrappedMessage(repository, text, GameMessage(message, city), result =>
        {
            var old = confirmation; confirmation = null;
            if (old is not null) { old.Abandon(); RemoveChild(old); old.QueueFree(); }
            if (!abandoned) { closed(result); Refresh(); }
        });
        Refresh();
    }

    private void Accept()
    {
        if (!CanEdit()) return;
        draft.Selection.Validate(draft.Library); accepted?.Invoke(draft.Selection);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; forces?.SetBusy(value); Refresh(); } }
    internal void RequestBack()
    {
        if (selector is not null) { selector.RequestCancel(); return; }
        if (confirmation is not null) { confirmation.RequestCancel(); return; }
        if (forces is not null) { forces.RequestBack(); return; }
        if (CanEdit()) returned?.Invoke();
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; accepted = null; returned = null; chooseCity = null; historicalOfficers = null;
        selector?.Abandon(); confirmation?.Abandon(); forces?.Abandon(); chrome?.Abandon();
        foreach (var button in commands) button.Abandon(); Hide();
    }
}
