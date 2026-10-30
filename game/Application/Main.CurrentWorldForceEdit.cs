using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private CurrentWorldForceEditValues? _worldForceOriginal, _worldForceDraft;
    private CurrentWorldForceEditPublication? _worldForceOriginalPublication, _worldForcePublication;
    private ForceInformationPage? _worldForceInformation;
    private readonly Dictionary<int, string> _worldForceTitleNames = [];

    private bool CurrentWorldForceDirty => _worldForceDraft is { } draft && _worldForceOriginal is { } original &&
        (draft.RankId != original.RankId || draft.NationalTitleId != original.NationalTitleId ||
         draft.DoctrineExperience != original.DoctrineExperience || draft.CivilReputation != original.CivilReputation ||
         draft.DoctrinePoints.Count != 0 || draft.Relationships.Count != 0 || draft.DistrictMilitaryInformation.Count != 0);

    private void OpenCurrentWorldForce(int id)
    {
        var query = CurrentWorldEditRules.QueryCurrentWorldForceEdit(_worldEditWorld!, id);
        if (!query.Available || query.Value is not { } publication) { CurrentWorldEntityUnavailable(query.UnresolvedInput); return; }
        _worldForceOriginalPublication = _worldForcePublication = publication;
        _worldForceOriginal = _worldForceDraft = new(publication.RankId.Value, publication.NationalTitleId.Value,
            publication.DoctrineExperience.Value, publication.CivilReputation.Value);
        _worldForceInformation = CurrentWorldEditRules.QueryForceInformation(_worldEditWorld!, id);
        _worldForceTitleNames.Clear();
        if (publication.NationalTitleId.Value is int initialTitle)
            _worldForceTitleNames[initialTitle] = _worldForceInformation.Text.GetValueOrDefault(14, _ui.Get("detail_unknown"));
        foreach (var title in publication.NationalTitles) _worldForceTitleNames[title.Id] = title.Name;
        var view = new CurrentWorldForceEditView();
        MountCurrentWorldEntityPage(view, view.Abandon, view.RequestBack, view.Refresh, view.SetBusy);
        view.Configure(_repository!, _ui, ReadCurrentWorldForcePage,
            () => CanEditCurrentWorldEntity && _worldEntityPage == view,
            () => RunUiAction(() => RequestLeaveCurrentWorldEntity(CurrentWorldForceDirty)),
            () => RunUiAction(() => SubmitCurrentWorldEntity(new EditCurrentWorldForceOrder(_worldEditSession!.PlayerForceId!.Value, id, _worldForceDraft!))),
            entry => RunUiAction(() => EditCurrentWorldForceField(entry)),
            target => RunUiAction(() => EditCurrentWorldForceRelationship(target)), CurrentListScrollSpeed);
    }

    private CurrentWorldForcePageValues ReadCurrentWorldForcePage()
    {
        var publication = _worldForcePublication!; var catalog = GetItemEditorCatalog(); var rules = CurrentWorldEditRules;
        string unknown = _ui.Get("detail_unknown");
        string rank = publication.RankId.Value is int rankId ? rankId == 9 ? catalog.Data(0x53F) :
            _worldEditWorld!.ForceRanks.GetValueOrDefault(rankId)?.Name ?? unknown : unknown;
        string title = publication.NationalTitleId.Value is int titleId ? titleId == 0 ? catalog.Data(0x53F) :
            _worldForceTitleNames.GetValueOrDefault(titleId, unknown) : unknown;
        string doctrine = unknown;
        if (_worldForceInformation!.RulerId is int ruler && _worldEditWorld!.Officers.TryGetValue(ruler, out var person))
        {
            int? raw = person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId;
            if (raw is int value && _worldEditWorld.Doctrines.TryGetValue(value is >= 0 and <= 6 ? value : 0, out var definition) &&
                publication.DoctrineLevel.Value is int level)
                doctrine = definition.Name + " " + rules.FormatCurrentWorldEditNumberText(0x384, level);
        }
        return new(_worldForceInformation, rank, title, doctrine, publication.DoctrineExperience.Value, publication.CivilReputation.Value,
            publication.TotalMilitaryInformation.Value?.ToString() ?? unknown,
            publication.DoctrinePoints.Select(point => new CurrentWorldForcePointDisplay(point.Name, point.Points.Value)).ToArray(),
            publication.Relationships.Select(relation => new CurrentWorldForceRelationDisplay(relation.ForceId, relation.Name,
                relation.FriendshipLevel is int category ? publication.FriendshipLevels.FirstOrDefault(option => option.Id == category)?.Name ?? unknown : unknown,
                relation.AllianceMonths.Value is int months ? months == 0 ? catalog.Data(0x353) :
                    rules.FormatCurrentWorldEditNumberText(0x45F, months) : unknown)).ToArray(),
            publication.CanEditRank, publication.CanEditNationalTitle, publication.CanEditDoctrineLevel, CurrentWorldForceDirty,
            new AdministrationMessages(_repository!, _ui.SourceLocale).Format(0x5F1E));
    }

    private bool RefreshCurrentWorldForceDraft(CurrentWorldForceEditValues proposed, int? selectedDistrict = null)
    {
        var query = CurrentWorldEditRules.QueryCurrentWorldForceEditPreview(_worldEditWorld!, _worldEntityId, proposed,
            selectedDistrict ?? _worldForcePublication!.SelectedDistrictId);
        if (!query.Available || query.Value is not { } publication) { CurrentWorldEntityUnavailable(query.UnresolvedInput); return false; }
        var originals = _worldForceOriginalPublication!.DistrictMilitaryInformationValues.ToDictionary(row => row.DistrictId);
        var military = publication.DistrictMilitaryInformationValues
            .Where(row => row.MilitaryInformation.Value is not null &&
                (!originals.TryGetValue(row.DistrictId, out var original) || row.MilitaryInformation.Value != original.MilitaryInformation.Value))
            .Select(row => new CurrentWorldDistrictMilitaryInformationEdit(row.DistrictId, row.MilitaryInformation.Value!.Value)).ToArray();
        _worldForceDraft = proposed with { DistrictMilitaryInformation = military, MilitaryDistrictId = null, MilitaryInformation = null };
        _worldForcePublication = publication;
        foreach (var title in publication.NationalTitles) _worldForceTitleNames[title.Id] = title.Name;
        return true;
    }

    private void EditCurrentWorldForceField(int entry)
    {
        if (!CanEditCurrentWorldEntity || _worldForcePublication is not { } publication || _worldForceDraft is not { } draft) return;
        if (entry == 0 && publication.CanEditRank)
            OpenCurrentWorldEntityOptions(0x21B, publication.Ranks, publication.RankId.Value, selected =>
            {
                if (!RefreshCurrentWorldForceDraft(draft with { RankId = selected, NationalTitleId = selected == 1 ? draft.NationalTitleId : 0 })) return;
                if (selected == 1 && _worldForcePublication!.NationalTitleId.Value == 0) OpenCurrentWorldForceTitle();
            });
        else if (entry == 1 && publication.CanEditNationalTitle) OpenCurrentWorldForceTitle();
        else if (entry == 2 && publication.CanEditDoctrineLevel)
            OpenCurrentWorldEntityCalculator(publication.DoctrineLevel.Value, 1, 10, 0x244, selected =>
            {
                var option = publication.DoctrineLevels.FirstOrDefault(row => row.Level == selected);
                if (option is null) { CurrentWorldEntityUnavailable("current-world-edit.force_level_choice"); return; }
                RefreshCurrentWorldForceDraft(draft with { DoctrineExperience = option.Experience });
            });
        else if (entry == 3)
            OpenCurrentWorldEntityCalculator(publication.CivilReputation.Value, publication.CivilReputation.Minimum,
                publication.CivilReputation.Maximum, 0x83F, selected => RefreshCurrentWorldForceDraft(draft with { CivilReputation = selected }));
        else if (entry == 4)
            OpenCurrentWorldEntityOptions(0x835, publication.Districts, publication.SelectedDistrictId, selected =>
            {
                if (!RefreshCurrentWorldForceDraft(draft, selected)) return;
                var current = _worldForcePublication!;
                OpenCurrentWorldEntityCalculator(current.MilitaryInformation.Value, current.MilitaryInformation.Minimum,
                    current.MilitaryInformation.Maximum, 0x835, value =>
                    {
                        var requests = _worldForceDraft!.DistrictMilitaryInformation.Where(row => row.DistrictId != selected).ToList();
                        requests.Add(new(selected, value));
                        RefreshCurrentWorldForceDraft(_worldForceDraft with { DistrictMilitaryInformation = requests }, selected);
                    });
            });
        else if (entry == 5) OpenCurrentWorldForcePoints();
    }
    private void OpenCurrentWorldForceTitle()
    {
        if (_worldForcePublication is not { CanEditNationalTitle: true } publication) return;
        OpenCurrentWorldEntityOptions(0x3C8, publication.NationalTitles, publication.NationalTitleId.Value,
            selected => RefreshCurrentWorldForceDraft(_worldForceDraft! with { NationalTitleId = selected }));
    }
    private void OpenCurrentWorldForcePoints()
    {
        if (_repository is null || !CanEditCurrentWorldEntity || _worldForcePublication is not { } publication || _worldForceDraft is not { } before) return;
        var fields = publication.DoctrinePoints.Select((point, index) => new CurrentWorldEditNumberField(3 + index * 29,
            point.Name, point.Points.Value, point.Points.Minimum, point.Points.Maximum)).ToArray();
        var view = new CurrentWorldEditNumbersView(); MountCurrentWorldEntitySubpage(view, view.Abandon, view.RequestBack);
        view.Configure(_repository, _ui, "11_01_12_edit_seiryoku_num_cn", 0x76,
            new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5F29), fields, UiSourceEditSlider.FunctionIcons,
            () => IsCurrentWorldEditCurrent && _worldEntitySubpage == view && _worldEditQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldEntitySubpage != view) return;
                CloseCurrentWorldEntitySubpage(resume: false);
                if (result is not null)
                {
                    var changes = publication.DoctrinePoints.Select((point, index) => (point.DoctrineId, Value: result.Values[index]))
                        .Where(row => row.Value is not null && row.Value != _worldForceOriginalPublication!.DoctrinePoints.Single(point => point.DoctrineId == row.DoctrineId).Points.Value)
                        .Select(row => new CurrentWorldForceDoctrinePointsEdit(row.DoctrineId, row.Value!.Value)).ToArray();
                    RefreshCurrentWorldForceDraft(before with { DoctrinePoints = changes });
                }
                if (_worldEntityPage is { } page) { page.Show(); _worldEntityPageBusy?.Invoke(false); _worldEntityPageRefresh?.Invoke(); }
            }));
    }
    private void EditCurrentWorldForceRelationship(int target)
    {
        if (_repository is null || !CanEditCurrentWorldEntity || _worldForcePublication is not { } publication || _worldForceDraft is not { } before) return;
        var row = publication.Relationships.Single(relation => relation.ForceId == target);
        var original = _worldForceOriginalPublication!.Relationships.Single(relation => relation.ForceId == target);
        var catalog = GetItemEditorCatalog();
        CurrentWorldEditNumberField Field(int? amount) => new(6, catalog.Data(0x414), amount, row.AllianceMonths.Minimum, row.AllianceMonths.Maximum);
        var view = new CurrentWorldEditNumbersView(); MountCurrentWorldEntitySubpage(view, view.Abandon, view.RequestBack);
        view.Configure(_repository, _ui, "11_01_12_edit_seiryoku_num", 0x76,
            new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5F1E), [Field(row.AllianceMonths.Value)], UiSourceEditSlider.FunctionIcons,
            () => IsCurrentWorldEditCurrent && _worldEntitySubpage == view && _worldEditQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldEntitySubpage != view) return;
                CloseCurrentWorldEntitySubpage(resume: false);
                if (result is not null)
                {
                    int? category = result.Choice == original.FriendshipLevel ? null : result.Choice;
                    int? months = result.Values[0] == original.AllianceMonths.Value ? null : result.Values[0];
                    var requests = before.Relationships.Where(relation => relation.ForceId != target).ToList();
                    if (category is not null || months is not null) requests.Add(new(target, AllianceMonths: months) { FriendshipLevel = category });
                    RefreshCurrentWorldForceDraft(before with { Relationships = requests });
                }
                if (_worldEntityPage is { } page) { page.Show(); _worldEntityPageBusy?.Invoke(false); _worldEntityPageRefresh?.Invoke(); }
            }), publication.FriendshipLevels.Select(option => new CurrentWorldEditRadioOption(37 + option.Id, option.Id, option.Name)).ToArray(),
            row.FriendshipLevel, preview: result => [Field(result.Choice is >= 0 and <= 2 ? 0 : result.Values[0])]);
    }
    private void ClearCurrentWorldForceDraft()
    {
        _worldForceOriginal = _worldForceDraft = null; _worldForceOriginalPublication = _worldForcePublication = null;
        _worldForceInformation = null; _worldForceTitleNames.Clear();
    }
}
