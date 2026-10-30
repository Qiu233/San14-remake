using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private enum RegisteredBirthSelection { Targets, Reference }
    private CommandSelectionView? _registeredOfficerBirthYearSelection;
    private RegisteredOfficerBirthYearView? _registeredOfficerBirthYears;
    private RegisteredOfficerLibrary? _registeredOfficerBirthYearLibrary;
    private IReadOnlyList<RegisteredOfficerBirthYearPerson>? _registeredOfficerBirthYearPeople;
    private IReadOnlyList<RegisteredOfficerBirthYearScenario>? _registeredOfficerBirthYearScenarios;
    private IReadOnlyList<int> _registeredOfficerBirthYearIds = [];
    private int? _registeredOfficerBirthYearReference;
    private bool HasRegisteredOfficerBirthYears => _registeredOfficerBirthYearLibrary is not null;
    private bool IsRegisteredOfficerBirthYearsCurrent => IsRegisteredOfficersCurrent &&
        _registeredOfficerBirthYearLibrary is { } library && ReferenceEquals(_registeredOfficerStore?.Current, library);

    private void OpenRegisteredOfficerBirthYears()
    {
        if (!IsRegisteredOfficersCurrent || _repository is null || HasRegisteredOfficerBirthYears ||
            _registeredOfficerEditor is not null || _registeredOfficerList is not null ||
            _registeredOfficerQuestion is not null || _registeredOfficerStore is not { } store || store.Current.Officers.Length == 0) return;
        var definitions = _registeredOfficerDefinitions ??= _campaignComposition?.Scenario ??
            (_shownScenarios.Count > 0 ? _repository.LoadScenario(_shownScenarios[0].Selection)
                : throw new InvalidDataException("Registered officer editor definitions are unavailable."));
        _registeredOfficerBirthYearLibrary = store.Current;
        _registeredOfficerBirthYearIds = []; _registeredOfficerBirthYearReference = null;
        try
        {
            _registeredOfficerBirthYearPeople = RegisteredOfficerBirthYears.LoadSourcePeople(_repository, definitions);
            _registeredOfficerBirthYearScenarios = RegisteredOfficerBirthYears.LoadHistoryScenarios(_repository, _ui.SourceLocale);
            _registeredOfficers!.SetBusy(true); _registeredOfficers.Hide();
            OpenRegisteredOfficerBirthYearSelection(RegisteredBirthSelection.Targets);
        }
        catch { CloseRegisteredOfficerBirthYears(); throw; }
    }

    private void OpenRegisteredOfficerBirthYearSelection(RegisteredBirthSelection purpose, string? notice = null)
    {
        if (!IsRegisteredOfficerBirthYearsCurrent || _repository is null || _registeredOfficerBirthYearSelection is not null) return;
        var library = _registeredOfficerBirthYearLibrary!;
        var people = purpose == RegisteredBirthSelection.Targets ? library.Officers.ToArray()
            : _registeredOfficerBirthYearIds.Select(id => library.Find(id) ?? throw new InvalidDataException("Selected registered officer has retired.")).ToArray();
        var text = _repository.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => text.Records[id].Text ?? throw new InvalidDataException("Missing registered officer selector caption.");
        CommandSelectionColumn[] columns = [new("name", Caption(0x211), 400),
            new("birth", Caption(0x4E7), 240, true), new("death", Caption(0x4E8), 240, true),
            new("lifespan", Caption(0x4C6), 240, true)];
        var rows = people.Select(person => new CommandSelectionRow(person.Id,
            [CommandSelectionCell.PersonName(person.Name, null), new(person.BirthYear.ToString(), person.BirthYear),
                new(person.DeathYear.ToString(), person.DeathYear),
                new((person.DeathYear - person.BirthYear + 1).ToString(), person.DeathYear - person.BirthYear + 1)], true)).ToArray();
        var view = new CommandSelectionView(); _registeredOfficerBirthYearSelection = view;
        AddChild(view);
        try
        {
            IReadOnlyList<int> initial = purpose == RegisteredBirthSelection.Targets ? _registeredOfficerBirthYearIds
                : _registeredOfficerBirthYearReference is int id ? new[] { id } : Array.Empty<int>();
            view.Configure(_repository, _ui, 0x5DE, columns, rows, initial,
                purpose == RegisteredBirthSelection.Targets ? people.Length : 1,
                () => IsRegisteredOfficerBirthYearsCurrent && _registeredOfficerBirthYearSelection == view && _registeredOfficerQuestion is null,
                result => RunUiAction(() =>
                {
                    if (!IsRegisteredOfficerBirthYearsCurrent || _registeredOfficerBirthYearSelection != view) return;
                    _registeredOfficerBirthYearSelection = null; view.Abandon(); view.QueueFree();
                    if (purpose == RegisteredBirthSelection.Targets)
                    {
                        if (result is null) { CloseRegisteredOfficerBirthYears(); return; }
                        _registeredOfficerBirthYearIds = result.ToArray();
                        if (_registeredOfficerBirthYearReference is int previous && !result.Contains(previous)) _registeredOfficerBirthYearReference = null;
                        OpenRegisteredOfficerBirthYearSelection(RegisteredBirthSelection.Reference);
                    }
                    else if (result is null) OpenRegisteredOfficerBirthYearSelection(RegisteredBirthSelection.Targets);
                    else
                    {
                        _registeredOfficerBirthYearReference = result.Single();
                        OpenRegisteredOfficerBirthYearEditor();
                    }
                }), CurrentListScrollSpeed,
                guide: notice ?? (purpose == RegisteredBirthSelection.Reference ? new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x2831) : ""),
                requireSelection: true, singleSelection: purpose == RegisteredBirthSelection.Reference);
            view.Name = purpose == RegisteredBirthSelection.Targets ? "RegisteredOfficerBirthYearTargets" : "RegisteredOfficerBirthYearReference";
        }
        catch { _registeredOfficerBirthYearSelection = null; view.Abandon(); view.QueueFree(); throw; }
    }

    private void OpenRegisteredOfficerBirthYearEditor()
    {
        if (!IsRegisteredOfficerBirthYearsCurrent || _repository is null || _registeredOfficerBirthYearReference is not int reference) return;
        var library = _registeredOfficerBirthYearLibrary!;
        RegisteredOfficerBirthYearPlan plan;
        try { plan = RegisteredOfficerBirthYears.Preview(library, _registeredOfficerBirthYearIds, reference); }
        catch (InvalidDataException error)
        {
            // A rejected group must retain a real route back to target selection.
            OpenRegisteredOfficerBirthYearSelection(RegisteredBirthSelection.Reference, error.Message);
            return;
        }
        var person = library.Find(reference) ?? throw new InvalidDataException("Registered birth-year reference has retired.");
        var view = new RegisteredOfficerBirthYearView(); _registeredOfficerBirthYears = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, person, plan, _registeredOfficerBirthYearScenarios!,
                () => IsRegisteredOfficerBirthYearsCurrent && _registeredOfficerBirthYears == view && _registeredOfficerQuestion is null,
                () => RunUiAction(() =>
                {
                    if (!IsRegisteredOfficerBirthYearsCurrent || _registeredOfficerBirthYears != view) return;
                    CloseRegisteredOfficerBirthYearEditor(); OpenRegisteredOfficerBirthYearSelection(RegisteredBirthSelection.Reference);
                }), year => RunUiAction(() => ConfirmRegisteredOfficerBirthYears(view, plan, year)));
        }
        catch { CloseRegisteredOfficerBirthYearEditor(); throw; }
    }

    private void ConfirmRegisteredOfficerBirthYears(RegisteredOfficerBirthYearView view, RegisteredOfficerBirthYearPlan plan, int birthYear)
    {
        if (!IsRegisteredOfficerBirthYearsCurrent || _registeredOfficerBirthYears != view || _repository is null) return;
        var reference = _registeredOfficerBirthYearLibrary!.Find(plan.ReferenceId)!;
        string message = new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x2832, officerName: reference.Name);
        AskRegisteredOfficerBirthYearQuestion(message, 0x2832, () =>
        {
            try
            {
                bool relationships = RegisteredOfficerBirthYears.RequiresRelationshipConfirmation(
                    _registeredOfficerBirthYearLibrary!, plan, birthYear, _registeredOfficerBirthYearPeople!);
                if (relationships)
                    AskRegisteredOfficerBirthYearQuestion(new AdministrationMessages(_repository!, _ui.SourceLocale).Format(0x2839),
                        0x2839, () => SaveRegisteredOfficerBirthYears(view, plan, birthYear, true));
                else SaveRegisteredOfficerBirthYears(view, plan, birthYear, false);
            }
            catch (InvalidDataException error) { view.ApplyNotice(error.Message); GD.PushWarning(error.Message); }
        });
    }

    private void AskRegisteredOfficerBirthYearQuestion(string message, int messageId, Action confirmed)
    {
        if (!IsRegisteredOfficerBirthYearsCurrent || _registeredOfficerQuestion is not null || _registeredOfficerBirthYears is not { } view || _repository is null) return;
        var question = new DeploymentTargetConfirmation(); _registeredOfficerQuestion = question;
        view.SetBusy(true); AddChild(question);
        try
        {
            question.ConfigureWrappedMessage(_repository, _ui, message, yes => RunUiAction(() =>
            {
                if (!IsRegisteredOfficerBirthYearsCurrent || _registeredOfficerQuestion != question || _registeredOfficerBirthYears != view) return;
                _registeredOfficerQuestion = null; question.Abandon(); question.QueueFree(); view.SetBusy(false);
                if (yes) confirmed();
            }));
            question.Name = "RegisteredOfficerBirthYearConfirmation"; question.SetMeta("message_record", messageId);
        }
        catch { _registeredOfficerQuestion = null; question.Abandon(); question.QueueFree(); view.SetBusy(false); throw; }
    }

    private void SaveRegisteredOfficerBirthYears(RegisteredOfficerBirthYearView view, RegisteredOfficerBirthYearPlan plan,
        int birthYear, bool relationshipsConfirmed)
    {
        if (!IsRegisteredOfficerBirthYearsCurrent || _registeredOfficerBirthYears != view) return;
        view.SetBusy(true);
        try
        {
            var next = RegisteredOfficerBirthYears.Apply(_registeredOfficerBirthYearLibrary!, plan, birthYear,
                _registeredOfficerBirthYearPeople!, relationshipsConfirmed);
            GetRegisteredOfficerStore().Save(next);
            CloseRegisteredOfficerBirthYears();
        }
        catch (Exception error) when (RegisteredOfficerStorageFailure(error))
        { view.SetBusy(false); view.ApplyNotice(error.Message); GD.PushWarning(error.ToString()); }
    }

    private void CloseRegisteredOfficerBirthYearEditor()
    {
        var view = _registeredOfficerBirthYears; _registeredOfficerBirthYears = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
    }

    private void RequestRegisteredOfficerBirthYearsBack()
    {
        if (_registeredOfficerQuestion is { } question) question.RequestCancel();
        else if (_registeredOfficerBirthYears is { } editor) editor.RequestBack();
        else _registeredOfficerBirthYearSelection?.RequestCancel();
    }

    private void CloseRegisteredOfficerBirthYears(bool resumeMenu = true)
    {
        CloseRegisteredOfficerBirthYearEditor();
        var selection = _registeredOfficerBirthYearSelection; _registeredOfficerBirthYearSelection = null;
        if (selection is not null) { selection.Abandon(); selection.QueueFree(); }
        _registeredOfficerBirthYearLibrary = null; _registeredOfficerBirthYearPeople = null; _registeredOfficerBirthYearScenarios = null;
        _registeredOfficerBirthYearIds = []; _registeredOfficerBirthYearReference = null;
        if (resumeMenu && IsRegisteredOfficersCurrent)
        { _registeredOfficers!.SetBusy(false); _registeredOfficers.Show(); _registeredOfficers.Refresh(); }
    }
}
