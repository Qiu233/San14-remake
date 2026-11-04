using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private CommandSelectionView? _registeredOfficerExchangeSelection;
    private DeploymentNarrationView? _registeredOfficerExchangeNotice;
    private RegisteredOfficerLibrary? _registeredOfficerExchangeLibrary;
    private IReadOnlyList<RegisteredOfficerImportCandidate> _registeredOfficerImportCandidates = [];
    private IReadOnlyList<string> _registeredOfficerExchangeLastWrittenPaths = [];
    private IReadOnlyList<int> _registeredOfficerExchangeLastImportedIds = [];
    private readonly Queue<(int Id, int? Count, string? File)> _registeredOfficerExchangeNotices = [];
    private Action? _registeredOfficerExchangeAfterNotices;
    private bool _registeredOfficerExchangeImporting;
    private string _registeredOfficerExchangeDirectory = "";
    private int _registeredOfficerExchangeMaximum;
    private OfficerEditorPortraitCatalog? _registeredOfficerExchangePortraitCatalog;
    private bool HasRegisteredOfficerExchange => _registeredOfficerExchangeLibrary is not null;
    private bool IsRegisteredOfficerExchangeCurrent => IsRegisteredOfficersCurrent &&
        _registeredOfficerExchangeLibrary is { } library && ReferenceEquals(_registeredOfficerStore?.Current, library);

    private void OpenRegisteredOfficerExchange(bool importing)
    {
        if (!IsRegisteredOfficersCurrent || _repository is null || HasRegisteredOfficerExchange ||
            HasRegisteredOfficerBirthYears || _registeredOfficerEditor is not null || _registeredOfficerList is not null ||
            _registeredOfficerQuestion is not null || _registeredOfficerStore is not { } store) return;
        _registeredOfficerExchangeLibrary = store.Current; _registeredOfficerExchangeImporting = importing;
        _registeredOfficerExchangeDirectory = ProjectSettings.GlobalizePath("user://PERSONDATA");
        _registeredOfficerExchangeLastWrittenPaths = []; _registeredOfficerExchangeLastImportedIds = [];
        try
        {
            _registeredOfficers!.SetBusy(true); _registeredOfficers.Hide();
            _registeredOfficerExchangePortraitCatalog=OfficerEditorPortraitCatalog.Load(_repository,_ui.SourceLocale,
                CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current),store.Current);
            if (importing)
            {
                var source = _repository.LoadCollectionOfficerCatalog(_ui.SourceLocale,
                    CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current));
                var publication = RegisteredOfficerExchange.LoadCandidates(_registeredOfficerExchangeDirectory, _ui.SourceLocale, source,
                    _registeredOfficerExchangePortraitCatalog);
                _registeredOfficerImportCandidates = publication.Candidates;
                _registeredOfficerExchangeMaximum = Math.Min(publication.Candidates.Count, 150 - store.Current.Officers.Length);
                foreach (var failure in publication.Failures)
                { _registeredOfficerExchangeNotices.Enqueue((0x2837, null, failure.FileName)); GD.PushWarning(failure.FileName + ": " + failure.Detail); }
                if (publication.LimitReached) _registeredOfficerExchangeNotices.Enqueue((0x2836, null, null));
                _registeredOfficerExchangeAfterNotices = () =>
                {
                    if (_registeredOfficerImportCandidates.Count == 0) CloseRegisteredOfficerExchange();
                    else OpenRegisteredOfficerExchangeSelection();
                };
                PresentNextRegisteredOfficerExchangeNotice();
            }
            else
            {
                _registeredOfficerExchangeMaximum = Math.Min(store.Current.Officers.Length,
                    Math.Max(0, RegisteredOfficerExchange.MaximumDirectoryRecords - RegisteredOfficerExchange.CountSignatureFiles(_registeredOfficerExchangeDirectory)));
                OpenRegisteredOfficerExchangeSelection();
            }
        }
        catch (Exception error) when (RegisteredOfficerStorageFailure(error))
        { CloseRegisteredOfficerExchange(); RegisteredOfficerSaveFailure(error); }
    }

    private void OpenRegisteredOfficerExchangeSelection(string? notice = null, IReadOnlyList<int>? initial = null)
    {
        if (!IsRegisteredOfficerExchangeCurrent || _repository is null || _registeredOfficerExchangeSelection is not null) return;
        var library = _registeredOfficerExchangeLibrary!; bool importing = _registeredOfficerExchangeImporting;
        var messages = _repository.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing registered exchange caption.");
        CommandSelectionColumn[] columns = [new("name", Caption(0x211), 400),
            new("leadership", Caption(0x238), 180, true), new("strength", Caption(0x239), 180, true),
            new("intelligence", Caption(0x23A), 180, true), new("politics", Caption(0x23B), 180, true),
            new("charm", Caption(0x23C), 180, true)];
        var people = importing ? _registeredOfficerImportCandidates.Select(candidate => candidate.Officer).ToArray() : library.Officers.ToArray();
        var rows = people.Select((person, index) => new CommandSelectionRow(importing ? index : person.Id,
            [CommandSelectionCell.PersonName(person.Name, null), new(person.Leadership.ToString(), person.Leadership),
                new(person.Strength.ToString(), person.Strength), new(person.Intelligence.ToString(), person.Intelligence),
                new(person.Politics.ToString(), person.Politics), new(person.Charm.ToString(), person.Charm)], _registeredOfficerExchangeMaximum > 0)).ToArray();
        var view = new CommandSelectionView(); _registeredOfficerExchangeSelection = view; AddChild(view);
        try
        {
            string hint = importing ? RegisteredOfficerExchangeMessages.Format(_repository, _ui.SourceLocale, 0x27F9) + " " : "";
            view.Configure(_repository, _ui, importing ? 0x5CA : 0x5C9, columns, rows, initial ?? [], _registeredOfficerExchangeMaximum,
                () => IsRegisteredOfficerExchangeCurrent && _registeredOfficerExchangeSelection == view &&
                    _registeredOfficerQuestion is null && _registeredOfficerExchangeNotice is null,
                selection => RunUiAction(() =>
                {
                    if (!IsRegisteredOfficerExchangeCurrent || _registeredOfficerExchangeSelection != view) return;
                    if (selection is null) { CloseRegisteredOfficerExchange(); return; }
                    if (importing) { RetireRegisteredOfficerExchangeSelection(); CompleteRegisteredOfficerExchange(selection); }
                    else ConfirmRegisteredOfficerExport(view, selection);
                }), CurrentListScrollSpeed, guide: notice ?? hint + _registeredOfficerExchangeDirectory,
                requireSelection: true, retainOnAccept: true);
            view.Name = importing ? "RegisteredOfficerImportSelection" : "RegisteredOfficerExportSelection";
            view.SetMeta("exchange_directory", _registeredOfficerExchangeDirectory);
        }
        catch { RetireRegisteredOfficerExchangeSelection(); throw; }
    }

    private void ConfirmRegisteredOfficerExport(CommandSelectionView selector, IReadOnlyList<int> selected)
    {
        if (!IsRegisteredOfficerExchangeCurrent || _repository is null || _registeredOfficerQuestion is not null) return;
        var question = new DeploymentTargetConfirmation(); _registeredOfficerQuestion = question; AddChild(question);
        question.ConfigureWrappedMessage(_repository, _ui,
            RegisteredOfficerExchangeMessages.Format(_repository, _ui.SourceLocale, 0x2834), yes => RunUiAction(() =>
        {
            if (!IsRegisteredOfficerExchangeCurrent || _registeredOfficerQuestion != question || _registeredOfficerExchangeSelection != selector) return;
            _registeredOfficerQuestion = null; question.Abandon(); question.QueueFree();
            if (!yes) { selector.Show(); return; }
            RetireRegisteredOfficerExchangeSelection(); CompleteRegisteredOfficerExchange(selected);
        }));
        question.Name = "RegisteredOfficerExportConfirmation"; question.SetMeta("message_record", 0x2834);
    }

    private void CompleteRegisteredOfficerExchange(IReadOnlyList<int> selected)
    {
        if (!IsRegisteredOfficerExchangeCurrent) return;
        try
        {
            int count;
            if (_registeredOfficerExchangeImporting)
            {
                var original = _registeredOfficerExchangeLibrary!;
                var next = RegisteredOfficerExchange.Import(original, _registeredOfficerImportCandidates, selected);
                GetRegisteredOfficerStore().Save(next); _registeredOfficerExchangeLibrary = next;
                _registeredOfficerExchangeLastImportedIds = next.Officers.Select(person => person.Id)
                    .Except(original.Officers.Select(person => person.Id)).ToArray(); count = _registeredOfficerExchangeLastImportedIds.Count;
            }
            else
            {
                _registeredOfficerExchangeLastWrittenPaths = RegisteredOfficerExchange.Export(_registeredOfficerExchangeDirectory,
                    _registeredOfficerExchangeLibrary!, selected, _ui.SourceLocale,_registeredOfficerExchangePortraitCatalog); count = _registeredOfficerExchangeLastWrittenPaths.Count;
            }
            _registeredOfficerExchangeNotices.Enqueue((_registeredOfficerExchangeImporting ? 0x2835 : 0x2833, count, null));
            _registeredOfficerExchangeAfterNotices = () => CloseRegisteredOfficerExchange(); PresentNextRegisteredOfficerExchangeNotice();
        }
        catch (RegisteredOfficerExportException error)
        {
            _registeredOfficerExchangeLastWrittenPaths = error.WrittenFiles;
            OpenRegisteredOfficerExchangeSelection(error.Message, selected.Except(error.WrittenOfficerIds).ToArray()); GD.PushWarning(error.ToString());
        }
        catch (Exception error) when (RegisteredOfficerStorageFailure(error))
        { OpenRegisteredOfficerExchangeSelection(error.Message, selected); GD.PushWarning(error.ToString()); }
    }

    private void PresentNextRegisteredOfficerExchangeNotice()
    {
        if (!IsRegisteredOfficerExchangeCurrent || _repository is null || _registeredOfficerExchangeNotice is not null) return;
        if (!_registeredOfficerExchangeNotices.TryDequeue(out var item))
        { var next = _registeredOfficerExchangeAfterNotices; _registeredOfficerExchangeAfterNotices = null; next?.Invoke(); return; }
        var view = new DeploymentNarrationView(); _registeredOfficerExchangeNotice = view; AddChild(view);
        try
        {
            view.ConfigureAnnouncement(_repository, _ui,
                RegisteredOfficerExchangeMessages.Format(_repository, _ui.SourceLocale, item.Id, item.Count, item.File),
                () => RunUiAction(() =>
                {
                    if (!IsRegisteredOfficerExchangeCurrent || _registeredOfficerExchangeNotice != view) return;
                    _registeredOfficerExchangeNotice = null; view.Abandon(); view.QueueFree(); PresentNextRegisteredOfficerExchangeNotice();
                }), admitted: () => IsRegisteredOfficerExchangeCurrent && _registeredOfficerExchangeNotice == view,
                    resources: TryBorrowApplicationUiResources(view));
            view.Name = "RegisteredOfficerExchangeNotice"; view.SetMeta("message_record", item.Id);
        }
        catch
        {
            if (_registeredOfficerExchangeNotice == view) _registeredOfficerExchangeNotice = null;
            view.Abandon(); view.QueueFree(); throw;
        }
    }

    private void RetireRegisteredOfficerExchangeSelection()
    {
        var view = _registeredOfficerExchangeSelection; _registeredOfficerExchangeSelection = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
    }
    private void RequestRegisteredOfficerExchangeBack()
    {
        if (_registeredOfficerQuestion is { } question) question.RequestCancel();
        else if (_registeredOfficerExchangeNotice is null) _registeredOfficerExchangeSelection?.RequestCancel();
    }
    private void CloseRegisteredOfficerExchange(bool resumeMenu = true)
    {
        RetireRegisteredOfficerExchangeSelection();
        var notice = _registeredOfficerExchangeNotice; _registeredOfficerExchangeNotice = null;
        if (notice is not null) { notice.Abandon(); notice.QueueFree(); }
        _registeredOfficerExchangeLibrary = null; _registeredOfficerImportCandidates = []; _registeredOfficerExchangeNotices.Clear();
        _registeredOfficerExchangeAfterNotices = null; _registeredOfficerExchangeDirectory = "";
        if (resumeMenu && IsRegisteredOfficersCurrent)
        { _registeredOfficers!.SetBusy(false); _registeredOfficers.Show(); _registeredOfficers.Refresh(); }
    }
}
