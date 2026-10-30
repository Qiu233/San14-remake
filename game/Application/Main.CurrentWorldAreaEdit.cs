using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private CurrentWorldAreaEditValues? _worldAreaOriginal, _worldAreaDraft;
    private CurrentWorldAreaEditPublication? _worldAreaPublication;
    private AreaInformationPage? _worldAreaInformation;

    private void OpenCurrentWorldArea(int id)
    {
        var query = CurrentWorldEditRules.QueryCurrentWorldAreaEdit(_worldEditWorld!, id);
        if (!query.Available || query.Value is not { } publication) { CurrentWorldEntityUnavailable(query.UnresolvedInput); return; }
        _worldAreaPublication = publication;
        _worldAreaOriginal = _worldAreaDraft = new(publication.Commerce.Value, publication.Agriculture.Value,
            publication.Barracks.Value, publication.Population.Value, publication.Security.Value);
        _worldAreaInformation = CurrentWorldEditRules.QueryAreaInformation(_worldEditWorld!, id);
        var view = new CurrentWorldAreaEditView();
        MountCurrentWorldEntityPage(view, view.Abandon, view.RequestBack, view.Refresh, view.SetBusy);
        view.Configure(_repository!, _ui, () => new(_worldAreaInformation!, _worldAreaDraft!.Population, _worldAreaDraft.Security,
            _worldAreaDraft != _worldAreaOriginal, new AdministrationMessages(_repository!, _ui.SourceLocale).Format(0x5F1B)),
            () => CanEditCurrentWorldEntity && _worldEntityPage == view,
            () => RunUiAction(() => RequestLeaveCurrentWorldEntity(_worldAreaDraft != _worldAreaOriginal)),
            () => RunUiAction(() => SubmitCurrentWorldEntity(new EditCurrentWorldAreaOrder(_worldEditSession!.PlayerForceId!.Value, id, _worldAreaDraft!))),
            entry => RunUiAction(() =>
            {
                if (!CanEditCurrentWorldEntity || _worldAreaDraft is not { } draft) return;
                if (entry == 0) OpenCurrentWorldAreaDevelopment();
                else if (entry == 1) OpenCurrentWorldEntityCalculator(draft.Population,
                    publication.Population.Minimum, publication.Population.Maximum, 0x298,
                    value => RefreshCurrentWorldAreaDraft(draft with { Population = value }));
                else OpenCurrentWorldEntityCalculator(draft.Security,
                    publication.Security.Minimum, publication.Security.Maximum, 0x2C3,
                    value => RefreshCurrentWorldAreaDraft(draft with { Security = value }));
            }));
    }
    private bool RefreshCurrentWorldAreaDraft(CurrentWorldAreaEditValues proposed)
    {
        var preview = CurrentWorldEditRules.QueryCurrentWorldAreaEditPreview(_worldEditWorld!, _worldEntityId, proposed);
        if (!preview.Available || preview.Value is not { } information) { CurrentWorldEntityUnavailable(preview.UnresolvedInput); return false; }
        _worldAreaDraft = proposed; _worldAreaInformation = information; return true;
    }
    private void OpenCurrentWorldAreaDevelopment()
    {
        if (_repository is null || !CanEditCurrentWorldEntity || _worldAreaDraft is not { } before || _worldAreaPublication is not { } publication) return;
        var rules = CurrentWorldEditRules;
        var levels = CeInitialAreaDevelopment.LoadCurrent(_repository.SourceDirectory, _ui.SourceLocale);
        var catalog = GetItemEditorCatalog(); string unknown = _ui.Get("detail_unknown");
        int?[] current = [before.Commerce, before.Agriculture, before.Barracks];
        CurrentWorldEditNumber[] bounds = [publication.Commerce, publication.Agriculture, publication.Barracks];
        int[] captions = [0x2B3, 0x2B4, 0x2B5];
        var fields = bounds.Select((number, index) => new CurrentWorldEditNumberField(3 + index * 31, catalog.Data(captions[index]),
            current[index], number.Minimum, number.Maximum, ValueAnnotationNode: 33 + index * 31,
            FormatAnnotation: (value, _) => value is int raw ? rules.FormatCurrentWorldEditNumberText(0x384, levels.ReadLevel(checked((ushort)raw))) : unknown)).ToArray();
        var view = new CurrentWorldEditNumbersView();
        MountCurrentWorldEntitySubpage(view, view.Abandon, view.RequestBack);
        view.Configure(_repository, _ui, "11_01_12_edit_area_num", 0x78,
            new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5F36), fields, UiSourceEditSlider.FunctionIcons,
            () => IsCurrentWorldEditCurrent && _worldEntitySubpage == view && _worldEditQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldEntitySubpage != view) return;
                CloseCurrentWorldEntitySubpage(resume: false);
                if (result is not null) RefreshCurrentWorldAreaDraft(before with
                    { Commerce = result.Values[0], Agriculture = result.Values[1], Barracks = result.Values[2] });
                if (_worldEntityPage is { } page) { page.Show(); _worldEntityPageBusy?.Invoke(false); _worldEntityPageRefresh?.Invoke(); }
            }));
    }
    private void ClearCurrentWorldAreaDraft()
    { _worldAreaOriginal = _worldAreaDraft = null; _worldAreaPublication = null; _worldAreaInformation = null; }
}
