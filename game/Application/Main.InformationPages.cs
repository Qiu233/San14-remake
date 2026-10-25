using System.Globalization;
using Godot;
using San14.Client.Gameplay;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private void OpenInformationBrowser()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || _detailsView is not null ||
            _repository is null || _campaignComposition is not { } composition ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { } world || _selectedSnapshot is null ||
            _residentMap is not { IsPlayableMapReady: true } map) return;
        var rules = composition.PrepareArmyQueries(world);
        var page = rules.PrepareInformationBrowserPage(world, player);
        var initial = page.Menu(InformationTab.OwnForce);
        if (initial.InitialTab is not { } initialTab || initial.TabEnabled.Length != 3 ||
            initial.TabEnabled[(int)initialTab] != true)
        {
            _playMessage.Text = _ui.Get("detail_unknown");
            GD.PushWarning(initial.UnresolvedOwner ?? "information.initial_tab");
            return;
        }
        var browser = new MapInformationBrowserView();
        bool Current() => !_failed && !_shuttingDown && _commandDetailsView == browser &&
            ReferenceEquals(_session, session) && ReferenceEquals(_world, world) &&
            ReferenceEquals(session.World, world) && _residentMap == map;
        void AdoptWorld(GameState next)
        {
            world = next;
            rules = composition.PrepareArmyQueries(world);
            page = rules.PrepareInformationBrowserPage(world, player);
        }
        OpenCommandDetails(browser, close => browser.Configure(_repository, _ui, _sourceHeaders,
            Current, ReadMenu, (tab, node) => RunUiAction(() => OpenList(tab, node)),
            () => RunUiAction(OpenHistory), close, CurrentListScrollSpeed, Literal(0x5F00), Literal(0x2733),
            historyEnabled: true, (int)initialTab, initial.TabEnabled.Select(value => value == true).ToArray()));

        string Literal(int id) => _repository.LoadMessages(_ui.SourceLocale, MessageKind.Game)
            .Messages.Sections[id / 10000].Records[id % 10000].Literals.Single().Text;

        IReadOnlyList<MapInformationMenuEntry> ReadMenu(int tab) => page.Menu((InformationTab)tab).Entries
            .Select(entry => new MapInformationMenuEntry(entry.Node, entry.Caption, entry.Visible,
                entry.Enabled == true, entry.UnresolvedOwner is null ? null : _ui.Get("information_partial"))).ToArray();

        void OpenHistory()
        {
            if (!Current()) return;
            var publication = page.QueryHistory();
            if (publication.UnresolvedOwner is { } missing) GD.PushWarning(missing);
            var detail = new MapInformationHistoryView();
            browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, publication,
                Current, close, CurrentListScrollSpeed));
        }

        void OpenList(int tab, int node)
        {
            if (!Current()) return;
            if ((InformationTab)tab == InformationTab.OwnForce && node == 48)
            {
                OpenBelongings();
                return;
            }
            var publication = page.OpenList((InformationTab)tab, node);
            if (publication.UnresolvedOwner is { } missing) GD.PushWarning(missing);
            if (publication.DestinySummary is { } popularity)
            {
                var detail = new MapInformationDestinyView();
                browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, popularity,
                    Current, close, CurrentListScrollSpeed));
                return;
            }
            //803430 opens the current player's force detail directly. Its
            //retained force collection is not the all-forces list route.
            if ((InformationTab)tab == InformationTab.OwnForce && node == 20)
            {
                var ownForce = publication.Rows.FirstOrDefault(row =>
                    row.Detail is { Kind: InformationListKind.Forces } force && force.Id == player);
                if (ownForce is null) browser.ApplyNotice(_ui.Get("information_partial"));
                else OpenInformationRecord(browser, rules, world, publication, ownForce, AdoptWorld);
                return;
            }
            if (publication.Rows.IsEmpty && publication.BodyText is { } body)
            {
                var record = new MapInformationRecordView();
                browser.ShowDetail(record, close => record.Configure(_repository, _ui, publication.Title,
                    [new(publication.Title, body)], close, CurrentListScrollSpeed));
                return;
            }
            if (publication.Columns.IsEmpty)
            {
                browser.ApplyNotice(_ui.Get("information_partial"));
                return;
            }
            var columns = publication.Columns.Select(column => new MapInformationListColumn(column.Key,
                InformationColumnCaption(column), column.Key is "description" ? 640 : column.Key is "name" ? 260 :
                column.Key is "force" or "location" or "ruler" or "governor" or "owner" ? 200 : 148)).ToArray();
            var indices = new Dictionary<InformationRow, int>(ReferenceEqualityComparer.Instance);
            for (int index = 0; index < publication.Rows.Length; index++) indices.Add(publication.Rows[index], index);
            IReadOnlyList<MapInformationListRow> Rows(InformationList ordered) => ordered.Rows
                .Select(row => new MapInformationListRow(indices[row], publication.Columns
                    .Select(column => InformationCellText(row.Cells.GetValueOrDefault(column.Key))).ToArray(), true)).ToArray();
            browser.ShowList(publication.Title, columns, Rows(publication), index => RunUiAction(() =>
            {
                if (!Current() || (uint)index >= publication.Rows.Length) return;
                OpenInformationRecord(browser, rules, world, publication, publication.Rows[index], AdoptWorld);
            }), (key, ascending) => Rows(publication.Sort(key, ascending)),
                publication.UnresolvedOwner is null ? null : _ui.Get("information_partial"));
        }

        void OpenBelongings()
        {
            var publication = page.QueryBelongings();
            if (publication.UnresolvedOwner is { } missing) GD.PushWarning(missing);
            if (publication.Sections.IsEmpty)
            {
                browser.ApplyNotice(_ui.Get("information_partial"));
                return;
            }
            var sections = publication.Sections.Select(section =>
            {
                var list = section.List;
                var columns = list.Columns.Select(column => new MapInformationListColumn(column.Key,
                    InformationColumnCaption(column), column.CaptionMessageId switch
                    {
                        0x41E => 188, 0x41F => 62, 0x234 => 160,
                        0x455 => 160, 0x456 => 132, 0x4B4 => 104,
                        _ => column.Key == "name" ? 300 : column.Key == "description" ? 640 : 200
                    })).ToArray();
                var indices = new Dictionary<InformationRow, int>(ReferenceEqualityComparer.Instance);
                for (int index = 0; index < list.Rows.Length; index++) indices.Add(list.Rows[index], index);
                IReadOnlyList<MapInformationListRow> Rows(InformationList ordered) => ordered.Rows
                    .Select(row => new MapInformationListRow(indices[row], list.Columns
                        .Select(column => InformationCellText(row.Cells.GetValueOrDefault(column.Key))).ToArray(),
                        row.Detail is not null)).ToArray();
                return new MapInformationListSection(section.Caption, columns, Rows(list), index => RunUiAction(() =>
                {
                    if (!Current() || (uint)index >= list.Rows.Length || list.Rows[index].Detail is null) return;
                    OpenInformationRecord(browser, rules, world, list, list.Rows[index], AdoptWorld);
                }), (key, ascending) => Rows(list.Sort(key, ascending)),
                    list.UnresolvedOwner is null ? null : _ui.Get("information_partial"));
            }).ToArray();
            var detail = new MapInformationBelongingsView();
            browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, publication.Title,
                sections, Current, close, CurrentListScrollSpeed));
        }
    }

    private string InformationCellText(InformationCell? cell) => cell is null || cell.UnresolvedOwner is not null
        ? _ui.Get("detail_unknown") : cell.Text ?? cell.Number?.ToString(CultureInfo.InvariantCulture) ?? "";

    private string InformationColumnCaption(InformationColumn column) => column.Caption.Length != 0 ? column.Caption :
        _ui.Get(column.Key switch
        {
            "command_cost" => "information_column_command_cost",
            "command_limit" => "information_column_command_limit",
            "doctrine_points" => "information_column_doctrine_points",
            "department" => "information_column_department",
            "description" => "information_column_description",
            "style" => "information_column_style",
            "campaign_day" => "information_column_date",
            "troops" => "information_column_troops",
            "leader" => "information_column_leader",
            "commander" => "information_column_commander",
            "overseer" => "information_column_overseer",
            "intelligence" => "information_column_intelligence",
            _ => "information_column_value"
        });

    private void OpenInformationRecord(MapInformationBrowserView browser, ArmyCampaignRuleAdapter rules,
        GameState world, InformationList publication, InformationRow row, Action<GameState> adoptWorld)
    {
        if (_repository is null || _selectedSnapshot is null || _campaignComposition is not { } composition) return;
        if (row.Detail is { Kind: InformationListKind.Cities } city && world.Cities.ContainsKey(city.Id))
        {
            var detail = new CityDetailsView();
            browser.ShowDetail(detail, close => detail.Configure(_repository,
                CampaignDetailViews.City(world, _selectedSnapshot, city.Id, _ui, CurrentCityCapacity(city.Id),
                    CurrentCityGeography(city.Id), CurrentCityForce(city.Id), CurrentCityMiniMap(city.Id)), _ui, close));
        }
        else if (row.Detail is { Kind: InformationListKind.Forces } force && world.Forces.ContainsKey(force.Id))
        {
            var detail = new ForceDetailsView();
            browser.ShowDetail(detail, close => detail.Configure(_repository,
                CampaignDetailViews.Force(world, _selectedSnapshot, force.Id, _ui,
                    composition.ForceDetailNameplate(world, force.Id), rules.ReadForceDetailMiniMap(world, force.Id)), _ui, close));
        }
        else if (row.Detail is { Kind: InformationListKind.Officers } officer && world.Officers.ContainsKey(officer.Id))
        {
            var detail = new OfficerDetailsView();
            browser.ShowDetail(detail, close => ConfigureCurrentOfficerDetail(detail, world, officer.Id, close,
                () => _commandDetailsView == browser, adoptWorld));
        }
        else if (row.Detail is { Kind: InformationListKind.Areas } area && world.Areas.ContainsKey(area.Id))
        {
            var detail = new AreaDetailsView();
            browser.ShowDetail(detail, close => detail.Configure(_repository,
                AreaDetailView.Create(world, _selectedSnapshot, area.Id, _ui,
                    new CeAreaDetailQueries(_repository.SourceDirectory, _ui.SourceLocale),
                    rules.ReadAreaRepresentativeSurfaceCode(world, area.Id), CurrentAreaForces(area.Id),
                    rules.ReadAreaDetailMiniMap(world, area.Id)), _ui, close));
        }
        else if (row.Detail is { Kind: InformationListKind.Armies } army && world.Units.ContainsKey(army.Id) &&
            rules.QueryUnitSourceDetails(world, army.Id) is { Exists: true } publicationArmy)
        {
            var detail = new ArmyDetailsView();
            browser.ShowDetail(detail, close => ConfigureUnitSourceDetails(detail, world, rules, army.Id, publicationArmy, close));
        }
        else
        {
            var detail = new MapInformationRecordView();
            var fields = publication.Columns.Select(column => new MapInformationRecordField(InformationColumnCaption(column),
                InformationCellText(row.Cells.GetValueOrDefault(column.Key)))).ToArray();
            browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, row.Name, fields, close, CurrentListScrollSpeed));
        }
    }
}
