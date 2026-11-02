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
        if (TryRequestPeriodMenu(PeriodMenuKind.Information)) return;
        if (_session is { } session) OpenInformationBrowserOwned(session.World, duringPeriod: false);
    }

    private void OpenInformationBrowserFromPeriod(GameState borrowedWorld)
        => OpenInformationBrowserOwned(borrowedWorld, duringPeriod: true);

    private void OpenInformationBrowserOwned(GameState world, bool duringPeriod)
    {
        if (!TutorialEntryAllowed) { if (duringPeriod) ReleasePeriodMenu(PeriodMenuKind.Information); return; }
        if (_failed || _shuttingDown || (_advancingPeriod && !duringPeriod) || IsReturningToTitle || _detailsView is not null ||
            _repository is null || _campaignComposition is not { } composition ||
            _session is not { PlayerForceId: int player } session || (!duringPeriod && session.HasPendingOperation) ||
            !IsInspectionWorldCurrent(session, world) || _selectedSnapshot is null ||
            _residentMap is not { IsPlayableMapReady: true } map)
        { if (duringPeriod) ReleasePeriodMenu(PeriodMenuKind.Information); return; }
        var rules = composition.PrepareArmyQueries(world);
        var page = rules.PrepareInformationBrowserPage(world, player);
        var initial = page.Menu(InformationTab.OwnForce);
        if (initial.InitialTab is not { } initialTab || initial.TabEnabled.Length != 3 ||
            initial.TabEnabled[(int)initialTab] != true)
        {
            _playMessage.Text = _ui.Get("detail_unknown");
            GD.PushWarning(initial.UnresolvedOwner ?? "information.initial_tab");
            if (duringPeriod) ReleasePeriodMenu(PeriodMenuKind.Information);
            return;
        }
        var browser = new MapInformationBrowserView();
        bool Current() => !_failed && !_shuttingDown && IsInformationBrowserCurrent(browser) &&
            IsInspectionWorldCurrent(session, world) && _residentMap == map;
        void AdoptWorld(GameState next)
        {
            world = next;
            rules = composition.PrepareArmyQueries(world);
            page = rules.PrepareInformationBrowserPage(world, player);
        }
        OpenCommandDetails(browser, close =>
        {
            RetainCommandDetailSpeechWorld(browser, AdoptWorld);
            browser.Configure(_repository, _ui, _sourceHeaders,
                Current, ReadMenu, (tab, node) => RunInspectionUiAction(world, () => OpenList(tab, node)),
                () => RunInspectionUiAction(world, OpenHistory), close, CurrentListScrollSpeed, Literal(0x5F00), Literal(0x2733),
                historyEnabled: true, (int)initialTab, initial.TabEnabled.Select(value => value == true).ToArray(),
                TryBorrowApplicationUiResources(browser));
        }, allowDuringPeriod: duringPeriod, inspectionWorld: world);
        if (duringPeriod && _commandDetailsView != browser) ReleasePeriodMenu(PeriodMenuKind.Information);

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
            var entry = page.Menu((InformationTab)tab).Entries.FirstOrDefault(value => value.Node == node);
            if(entry is {Enabled:true} && TryOpenTypedInformationPages(browser,rules,world,player,
                (InformationTab)tab,entry.Kind,AdoptWorld))return;
            var publication = page.OpenList((InformationTab)tab, node);
            if (publication.UnresolvedOwner is { } missing) GD.PushWarning(missing);
            if (publication.DestinySummary is { } popularity)
            {
                var detail = new MapInformationDestinyView();
                browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, popularity,
                    Current, close, CurrentListScrollSpeed, TryBorrowApplicationUiResources(detail)));
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
            var presented = publication;
            browser.ShowList(publication.Title, columns, Rows(publication), index => RunInspectionUiAction(world, () =>
            {
                if (!Current() || (uint)index >= publication.Rows.Length) return;
                OpenInformationRecord(browser, rules, world, presented, publication.Rows[index], AdoptWorld,
                    returned =>
                    {
                        presented = ApplyOfficerDetailReturn(presented, returned);
                        var selected = presented.Rows.First(row => row.Detail?.Id == returned.SelectedId);
                        browser.ApplyListDetailReturn(Rows(presented), indices[selected], returned.NameSortAscending);
                    }, browser.CurrentListNameSortAscending);
            }), (key, ascending) => Rows(presented = presented.Sort(key, ascending)),
                publication.UnresolvedOwner is null ? null : _ui.Get("information_partial"));
        }

        void OpenBelongings()
        {
            var prepared=rules.PrepareBelongingsInformationPages(world,player);
            if(!prepared.Available||prepared.Value is not { } publication)
            {GD.PushWarning(prepared.UnresolvedInput??"belongings.current");browser.ApplyNotice(_ui.Get("information_partial"));return;}
            var detail=new MapInformationBelongingsView();
            browser.ShowDetail(detail,close=>detail.Configure(_repository!,_ui,publication,Current,close,
                item=>RunInspectionUiAction(world,()=>
                {
                    if(!Current()||!browser.IsCurrentDetail(detail))return;
                    OpenInformationItem(browser,rules,world,publication.Items.CurrentDefinitionIds,item,
                        detail.ApplyItemDetailReturn,publication.Items.NameSortAscending);
                }),CurrentListScrollSpeed));
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
        GameState world, InformationList publication, InformationRow row, Action<GameState> adoptWorld,
        Action<OfficerDetailSelectionReturn>? returnOfficerSelection = null, bool? officerNameSortAscending = null)
    {
        if (_repository is null || _selectedSnapshot is null || _campaignComposition is not { } composition) return;
        if (row.Detail is { Kind: InformationListKind.Cities } city && world.Cities.ContainsKey(city.Id))
        {
            var detail = new CityDetailsView();
            browser.ShowDetail(detail, close => ConfigureCurrentCityDetail(detail, world, city.Id, close,
                preparedRules: rules, selectionIds: DetailSelectionIds(publication, InformationListKind.Cities)));
        }
        else if (row.Detail is { Kind: InformationListKind.Gates } gate && world.Gates?.ContainsKey(gate.Id) == true)
        {
            OpenInformationGate(browser, rules, world, gate.Id, adoptWorld);
        }
        else if (row.Detail is { Kind: InformationListKind.Forces } force && world.Forces.ContainsKey(force.Id))
        {
            var detail = new ForceDetailsView();
            browser.ShowDetail(detail, close => ConfigureCurrentForceDetail(detail, world, force.Id, close,
                preparedRules: rules, selectionIds: DetailSelectionIds(publication, InformationListKind.Forces)));
        }
        else if (row.Detail is { Kind: InformationListKind.Districts } district && world.Districts.ContainsKey(district.Id))
        {
            OpenInformationDistrict(browser, rules, world, district.Id, adoptWorld, DetailSelectionIds(publication, InformationListKind.Districts));
        }
        else if (row.Detail is { Kind: InformationListKind.Officers } officer && world.Officers.ContainsKey(officer.Id))
        {
            var detail = new OfficerDetailsView();
            browser.ShowDetail(detail, close => ConfigureCurrentOfficerDetail(detail, world, officer.Id, close,
                () => IsInformationBrowserCurrent(browser), adoptWorld,
                DetailSelectionIds(publication, InformationListKind.Officers), returnOfficerSelection, officerNameSortAscending));
        }
        else if (row.Detail is { Kind: InformationListKind.Areas } area && world.Areas.ContainsKey(area.Id))
        {
            var detail = new AreaDetailsView();
            browser.ShowDetail(detail, close => ConfigureCurrentAreaDetail(detail, world, area.Id, close,
                preparedRules: rules, selectionIds: DetailSelectionIds(publication, InformationListKind.Areas)));
        }
        else if (row.Detail is { Kind: InformationListKind.Items } item && world.Items?.ContainsKey(item.Id) == true)
        {
            OpenInformationItem(browser, rules, world, publication, item.Id);
        }
        else if (row.Detail is { Kind: InformationListKind.Armies } army && world.Units.ContainsKey(army.Id) &&
            rules.QueryUnitSourceDetails(world, army.Id) is { Exists: true } publicationArmy)
        {
            var detail = new ArmyDetailsView();
            browser.ShowDetail(detail, close => ConfigureUnitSourceDetails(detail, world, rules, army.Id, publicationArmy, close, browser, adoptWorld));
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
