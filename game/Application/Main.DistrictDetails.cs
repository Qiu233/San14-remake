using San14.Core;
using San14.Data;

public partial class Main
{
    private void ConfigureCurrentDistrictDetail(DistrictDetailsView view, GameState world, int districtId,
        Action close, ArmyCampaignRuleAdapter? preparedRules = null, Action<InformationListKind>? related = null, IReadOnlyList<int>? selectionIds = null,
        Action<InformationDetailSelectionReturn>? returnSelection = null, bool? initialNameSortAscending = null,
        Action<GameState>? adoptSpeechWorld = null)
    {
        var rules = preparedRules ?? _campaignComposition?.PrepareArmyQueries(world)
            ?? throw new InvalidDataException("district-information.current_campaign_rules");
        RetainCommandDetailSpeechWorld(view, next =>
        {
            world = next;
            rules = _campaignComposition!.PrepareArmyQueries(next);
            adoptSpeechWorld?.Invoke(next);
        });
        view.Configure(_repository ?? throw new InvalidDataException("district-information.resource_owner"),
            rules.QueryDistrictInformation(world, districtId), _ui, Return, related,
            PrepareDetailSelection(rules, world, InformationListKind.Districts, districtId, selectionIds)
                with { InitialNameSortAscending = initialNameSortAscending },
            id => RunInspectionUiAction(world, () => view.Apply(rules.QueryDistrictInformation(world, id))));
        void Return()
        {
            returnSelection?.Invoke(view.ReadSelectionReturn());
            close();
        }
    }

    private void OpenInformationDistrict(MapInformationBrowserView browser, ArmyCampaignRuleAdapter rules,
        GameState world, int districtId, Action<GameState> adoptWorld, IReadOnlyList<int>? selectionIds = null,
        Action<InformationDetailSelectionReturn>? returnSelection = null, bool? initialNameSortAscending = null)
    {
        if (_repository is null) return;
        var detail = new DistrictDetailsView();
        void Adopt(GameState next)
        {
            world = next; rules = _campaignComposition!.PrepareArmyQueries(next); adoptWorld(next);
            detail.Apply(rules.QueryDistrictInformation(next, detail.CurrentDistrictId));
        }
        browser.ShowDetail(detail, close => ConfigureCurrentDistrictDetail(detail, world, districtId, close,
            preparedRules: rules, related: kind => RunInspectionUiAction(world, () =>
                OpenInformationDistrictRelated(browser, rules, world, detail.CurrentDistrictId, kind, Adopt)), selectionIds: selectionIds,
                returnSelection: returnSelection, initialNameSortAscending: initialNameSortAscending,
                adoptSpeechWorld: next =>
                {
                    world = next;
                    rules = _campaignComposition!.PrepareArmyQueries(next);
                }));
    }

    private void OpenInformationDistrictRelated(MapInformationBrowserView browser, ArmyCampaignRuleAdapter rules,
        GameState world, int districtId, InformationListKind kind, Action<GameState> adoptWorld)
    {
        if (_repository is null || !IsInformationBrowserCurrent(browser) || _session?.PlayerForceId is not int player) return;
        if (kind is InformationListKind.Officers or InformationListKind.Armies or InformationListKind.Cities or InformationListKind.Gates)
        {
            var membership = rules.QueryDistrictInformationRelatedIds(world, districtId, kind);
            if (membership.Available && membership.Value is { } ids)
            {
                if (kind == InformationListKind.Officers)
                    OpenInformationOfficerPages(browser, rules, world,
                        rules.PrepareOfficerInformationPages(world, player, ids), adoptWorld);
                else if (kind == InformationListKind.Cities)
                    OpenInformationCityPages(browser, rules, world, rules.PrepareCityInformationPages(world, player, ids));
                else if (kind == InformationListKind.Gates)
                    OpenInformationGatePages(browser,rules,world,rules.PrepareGateInformationPages(world,player,ids),adoptWorld);
                else OpenInformationArmyPages(browser, rules, world,
                    rules.PrepareArmyInformationPages(world, player, ids), adoptWorld);
            }
            else browser.ApplyNotice(_ui.Get("information_partial"));
            return;
        }
        var publication = rules.QueryDistrictSubordinateInformation(world, player, districtId, kind);
        var columns = publication.Columns.Select(column => new MapInformationListColumn(column.Key,
            InformationColumnCaption(column), column.Key == "name" ? 188 : 200)).ToArray();
        // Indices identify retained publication rows, including heterogeneous source identities.
        var indices = new Dictionary<InformationRow, int>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < publication.Rows.Length; index++) indices.Add(publication.Rows[index], index);
        IReadOnlyList<MapInformationListRow> Rows(InformationList value) => value.Rows.Select(row =>
            new MapInformationListRow(indices[row], publication.Columns.Select(column => InformationCellText(
                row.Cells.GetValueOrDefault(column.Key))).ToArray(), row.Detail is not null)).ToArray();
        var presented = publication;
        var list = new DistrictRelatedListView();
        browser.ShowDetail(list, close =>
        {
            RetainCommandDetailSpeechWorld(list, next =>
            {
                world = next;
                rules = _campaignComposition!.PrepareArmyQueries(next);
            });
            list.Configure(_repository, _ui, publication.Title, columns, Rows(publication),
                () => IsInformationBrowserCurrent(browser), close, index => RunInspectionUiAction(world, () =>
                {
                    if ((uint)index < (uint)publication.Rows.Length)
                        OpenInformationRecord(browser, rules, world, presented, publication.Rows[index], adoptWorld);
                }), CurrentListScrollSpeed, (column, ascending) => Rows(presented = publication.Sort(column, ascending)),
                publication.UnresolvedOwner is null ? null : _ui.Get("information_partial"));
        });
    }
}
