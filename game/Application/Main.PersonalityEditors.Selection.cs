using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    //Import occurrence keys are selector identities, never personality IDs.
    //The captured definition keeps its actual portable/domain representation.
    private sealed record PersonalitySelectionRow(int Key, CreatedPersonality Definition);

    private CommandSelectionColumn[] PersonalityEditorColumns(PersonalityEditorCatalog catalog) =>
        [new("name", catalog.Data(0x5A8), 280), new("condition", catalog.Data(0x7FB), 300),
         new("option", catalog.Data(0x7FC), 260), new("effect1", catalog.Data(0x404), 320),
         new("effect2", catalog.Data(0x405), 320)];

    private CommandSelectionRow PersonalityEditorRow(PersonalitySelectionRow row, PersonalityEditorCatalog catalog)
    {
        var value = row.Definition;
        //Current definitions and their original resource captions own display
        //values. No source ordering key is synthesized from a localized name.
        return new(row.Key,
            [new(value.Name, SortUnavailable: true),
             new(Amount(catalog.ConditionName(value.ConditionKind), catalog.ConditionAmount(value)), SortUnavailable: true),
             new(catalog.OptionName(value.ConditionKind, value.OptionKind), SortUnavailable: true),
             new(Amount(catalog.EffectName(value.Effect1Code), catalog.EffectAmount(value.Effect1Code, value.Effect1Value)), SortUnavailable: true),
             new(Amount(catalog.EffectName(value.Effect2Code), catalog.EffectAmount(value.Effect2Code, value.Effect2Value)), SortUnavailable: true)], true);

        static string Amount(string caption, string amount) => amount.Length == 0 ? caption : caption + " " + amount;
    }

    private void SelectPersonalities(int title, IReadOnlyList<CreatedPersonality> rows, int maximum,
        Action<IReadOnlyList<int>> selected, string? guide = null) =>
        SelectPersonalityRows(title, rows.Select(value => new PersonalitySelectionRow(value.Id, value)).ToArray(),
            maximum, selected, guide);

    private void SelectPersonalityRows(int title, IReadOnlyList<PersonalitySelectionRow> rows, int maximum,
        Action<IReadOnlyList<int>> selected, string? guide = null)
    {
        if (_repository is null || !IsPersonalityEditorsCurrent || _personalityEditorSelector is not null ||
            _personalityEditorPage is not null || _personalityEditorQuestion is not null || _personalityEditorNotice is not null) return;
        var catalog = GetPersonalityEditorCatalog();
        var view = new CommandSelectionView(); _personalityEditorSelector = view; AddChild(view);
        _personalityEditorMenu!.SetBusy(true); _personalityEditorMenu.Hide();
        try
        {
            view.Configure(_repository, _ui, title, PersonalityEditorColumns(catalog),
                rows.Select(row => PersonalityEditorRow(row, catalog)).ToArray(), [], maximum,
                () => IsPersonalityEditorsCurrent && _personalityEditorSelector == view &&
                    _personalityEditorQuestion is null && _personalityEditorNotice is null,
                result => RunUiAction(() =>
                {
                    if (!IsPersonalityEditorsCurrent || _personalityEditorSelector != view) return;
                    _personalityEditorSelector = null; view.Abandon(); view.QueueFree();
                    _personalityEditorMenu!.Show(); _personalityEditorMenu.SetBusy(false);
                    if (result is not null) selected(result);
                }), CurrentListScrollSpeed, guide, requireSelection: true, singleSelection: maximum == 1);
            view.Name = title switch
            {
                0x7F5 => "PersonalityEditSelection", 0x7F8 => "PersonalityDeleteSelection",
                0x7F9 => "PersonalityExportSelection", 0x7FA => "PersonalityImportSelection",
                _ => "PersonalitySelection"
            };
        }
        catch
        {
            _personalityEditorSelector = null; view.Abandon(); view.QueueFree();
            _personalityEditorMenu!.Show(); _personalityEditorMenu.SetBusy(false); throw;
        }
    }

    private void SelectPersonalityAction(int action)
    {
        if (_repository is null || !IsPersonalityEditorsCurrent || _personalityEditorPage is not null ||
            _personalityEditorSelector is not null || _personalityEditorQuestion is not null ||
            _personalityEditorNotice is not null || !PersonalityEditorMenuEnabled(action)) return;
        var library = GetPersonalityEditorStore().Current;
        switch (action)
        {
            case 0:
                OpenPersonalityEditor(null);
                break;
            case 1:
                SelectPersonalities(0x7F5, library.Created, 1, ids =>
                {
                    if (ids.Count != 0) OpenPersonalityEditor(library.Find(ids[0]) ??
                        throw new InvalidDataException("The selected personality no longer belongs to this library."));
                });
                break;
            case 2:
                SelectPersonalities(0x7F8, library.Created, library.Created.Length, ids =>
                {
                    if (ids.Count != 0) AskPersonalityQuestion(0x2921, () => SavePersonalityLibrary(library.Delete(ids)));
                }, PersonalityMessage(0x2920));
                break;
            case 3:
                //The source500-file test belongs to menu/export entry. Its
                //multiselect can export all current records in that operation.
                SelectPersonalities(0x7F9, library.Created, library.Created.Length,
                    ids => { if (ids.Count != 0) ExportPersonalities(library, ids); }, PersonalityMessage(0x2922));
                break;
            case 4:
                OpenPersonalityImport();
                break;
        }
    }

    private void OpenPersonalityImport()
    {
        if (_repository is null || !IsPersonalityEditorsCurrent || _personalityEditorSelector is not null ||
            _personalityEditorPage is not null || _personalityEditorQuestion is not null || _personalityEditorNotice is not null) return;
        string directory = ProjectSettings.GlobalizePath("user://PERSONALITYDATA");
        PersonalityImportPublication publication;
        try
        {
            publication = PersonalityEditorExchange.LoadCandidates(directory);
            RefreshPersonalityExchangeCount();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { PersonalityEditorStorageFailure(error); return; }

        var candidates = publication.Candidates;
        var notices = new Queue<(int Id, string? File)>();
        foreach (var failure in publication.Failures)
        {
            //211570 uses2909 for a failed file;2907 is its capacity notice.
            notices.Enqueue((0x2909, failure.FileName));
            GD.PushWarning(failure.FileName + ": " + failure.Detail);
        }
        if (publication.LimitReached) notices.Enqueue((0x2907, null));
        PresentNext();

        void PresentNext()
        {
            if (!IsPersonalityEditorsCurrent) return;
            if (notices.TryDequeue(out var notice))
            { ShowPersonalityNotice(notice.Id, file: notice.File, closed: PresentNext); return; }
            if (candidates.Count == 0) { _personalityEditorMenu!.Refresh(); return; }

            int free = 100 - GetPersonalityEditorStore().Current.Created.Length;
            var rows = candidates.Select((candidate, index) => new PersonalitySelectionRow(index, candidate.Definition)).ToArray();
            SelectPersonalityRows(0x7FA, rows, Math.Min(rows.Length, free), selected =>
            {
                if (selected.Count == 0) return;
                try
                {
                    var library = GetPersonalityEditorStore().Current;
                    var next = PersonalityEditorExchange.Import(library, candidates, selected);
                    if (SavePersonalityLibrary(next)) ShowPersonalityNotice(0x2926, selected.Count);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
                { PersonalityEditorStorageFailure(error); }
            }, PersonalityMessage(0x2924, free) + " " + directory);
            _personalityEditorSelector?.SetMeta("exchange_directory", directory);
        }
    }

    private void ExportPersonalities(PersonalityEditorLibrary library, IReadOnlyList<int> ids)
    {
        if (!IsPersonalityEditorsCurrent || ids.Count == 0) return;
        try
        {
            //555980 ->513C70 exports immediately after the normal multiselect.
            //The source has no additional export confirmation dialog.
            var written = PersonalityEditorExchange.Export(ProjectSettings.GlobalizePath("user://PERSONALITYDATA"), library, ids);
            RefreshPersonalityExchangeCount(); _personalityEditorMenu!.Refresh();
            ShowPersonalityNotice(0x2923, written.Count);
        }
        catch (PersonalityExportException error)
        {
            //A genuine partial file write is retained and reported as such;
            //the unsaved remainder is never described as completed.
            RefreshPersonalityExchangeCount(); _personalityEditorMenu!.Refresh(); PersonalityEditorStorageFailure(error);
            if (error.WrittenFiles.Count != 0) ShowPersonalityNotice(0x2923, error.WrittenFiles.Count);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { PersonalityEditorStorageFailure(error); }
    }
}
