using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private CurrentWorldCityEditValues? _worldCityOriginal, _worldCityDraft;
    private CurrentWorldCityEditPublication? _worldCityPublication;
    private CityInformationPage? _worldCityInformation;

    private void OpenCurrentWorldCity(int id)
    {
        var query = CurrentWorldEditRules.QueryCurrentWorldCityEdit(_worldEditWorld!, id);
        if (!query.Available || query.Value is not { } publication) { CurrentWorldEntityUnavailable(query.UnresolvedInput); return; }
        _worldCityPublication = publication; _worldCityOriginal = _worldCityDraft = CityEditValues(publication);
        _worldCityInformation = CurrentWorldEditRules.QueryCityInformation(_worldEditWorld!, id);
        var view = new CurrentWorldCityEditView();
        MountCurrentWorldEntityPage(view, view.Abandon, view.RequestBack, view.Refresh, view.SetBusy);
        view.Configure(_repository!, _ui, () => new(_worldCityInformation!, _worldCityDraft!.Size,
            CityEditNumberFields(_worldCityPublication!), CurrentCityCounterTacticName(), _worldCityDraft != _worldCityOriginal,
            new AdministrationMessages(_repository!, _ui.SourceLocale).Format(0x5F1A)),
            () => CanEditCurrentWorldEntity && _worldEntityPage == view,
            () => RunUiAction(() => RequestLeaveCurrentWorldEntity(_worldCityDraft != _worldCityOriginal)),
            () => RunUiAction(() => SubmitCurrentWorldEntity(new EditCurrentWorldCityOrder(_worldEditSession!.PlayerForceId!.Value, id, _worldCityDraft!))),
            () => RunUiAction(OpenCurrentWorldCityResources), () => RunUiAction(() =>
            {
                if (_worldCityPublication is not { } current) return;
                OpenCurrentWorldEntityOptions(0x77, current.CounterTactics, _worldCityDraft!.CounterTactic,
                    selected =>
                    {
                        _worldCityDraft = _worldCityDraft! with { CounterTactic = selected };
                        _worldCityPublication = current with { CounterTactic = current.CounterTactic with { Value = selected },
                            CounterTactics = current.CounterTactics.OrderBy(option => option.Id == selected ? 0 : 1).ToArray() };
                    },
                    new AdministrationMessages(_repository!, _ui.SourceLocale).Format(0x5F31));
            }));
    }
    private static CurrentWorldCityEditValues CityEditValues(CurrentWorldCityEditPublication publication) => new(
        publication.Size.Value, publication.Gold.Value, publication.Food.Value, publication.Troops.Value,
        publication.Durability.Value, publication.Morale.Value, publication.CounterTactic.Value);

    private IReadOnlyList<CurrentWorldEditNumberField> CityEditNumberFields(CurrentWorldCityEditPublication publication)
    {
        CurrentWorldEditNumber[] numbers = [publication.Gold, publication.Food, publication.Troops, publication.Durability, publication.Morale];
        int[] captions = [0x20C, 0x20D, 0x20E, 0x20B, 0x225];
        bool forceValid = _worldCityInformation?.ForceId is int force && force != 0;
        var catalog = GetItemEditorCatalog(); string separator = catalog.Data(0x60C), unknown = _ui.Get("detail_unknown");
        return numbers.Select((number, index) => new CurrentWorldEditNumberField(3 + index * 31, catalog.Data(captions[index]),
            number.Value, number.Minimum, number.Maximum, Enabled: index is not (2 or 4) || forceValid,
            ValueAnnotationNode: 33 + index * 31, FormatAnnotation: (_, maximum) => separator + (maximum?.ToString() ?? unknown))).ToArray();
    }
    private string CurrentCityCounterTacticName() => _worldCityDraft?.CounterTactic is int tactic &&
        _worldEditWorld!.Tactics.TryGetValue(tactic, out var value) ? value.Name : _ui.Get("detail_unknown");

    private bool RefreshCurrentWorldCityDraft(CurrentWorldCityEditValues proposed)
    {
        var query = CurrentWorldEditRules.QueryCurrentWorldCityEditPreview(_worldEditWorld!, _worldEntityId, proposed);
        if (!query.Available || query.Value is not { } publication) { CurrentWorldEntityUnavailable(query.UnresolvedInput); return false; }
        _worldCityPublication = publication; _worldCityDraft = CityEditValues(publication); return true;
    }
    private void OpenCurrentWorldCityResources()
    {
        if (_repository is null || !CanEditCurrentWorldEntity || _worldCityDraft is not { } before || _worldCityPublication is not { } original) return;
        var view = new CurrentWorldEditNumbersView();
        MountCurrentWorldEntitySubpage(view, view.Abandon, view.RequestBack);
        var catalog = GetItemEditorCatalog();
        view.Configure(_repository, _ui, "11_01_12_edit_city_num", 0x77,
            new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5F32), CityEditNumberFields(original),
            UiSourceEditSlider.FunctionIcons, () => IsCurrentWorldEditCurrent && _worldEntitySubpage == view && _worldEditQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldEntitySubpage != view) return;
                CloseCurrentWorldEntitySubpage(resume: false);
                if (result is not null) RefreshCurrentWorldCityDraft(FromNumbers(result));
                if (_worldEntityPage is { } page) { page.Show(); _worldEntityPageBusy?.Invoke(false); _worldEntityPageRefresh?.Invoke(); }
            }), Enumerable.Range(1, 4).Select(size => new CurrentWorldEditRadioOption(159 + size, size, catalog.Data(0x46B + size))).ToArray(),
            before.Size, new Dictionary<int, string> { [159] = catalog.Data(0x5EA) }, result =>
            {
                var query = CurrentWorldEditRules.QueryCurrentWorldCityEditPreview(_worldEditWorld!, _worldEntityId, FromNumbers(result));
                if (!query.Available || query.Value is not { } preview) throw new InvalidDataException(query.UnresolvedInput);
                return CityEditNumberFields(preview);
            });

        CurrentWorldCityEditValues FromNumbers(CurrentWorldEditNumberResult result) => before with
        {
            Size = result.Choice, Gold = result.Values[0], Food = result.Values[1], Troops = result.Values[2],
            Durability = result.Values[3], Morale = result.Values[4]
        };
    }
    private void ClearCurrentWorldCityDraft()
    { _worldCityOriginal = _worldCityDraft = null; _worldCityPublication = null; _worldCityInformation = null; }
}
