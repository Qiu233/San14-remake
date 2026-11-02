using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private void OpenSelectedUnitSourceDetails(IReadOnlyList<int> unitIds, UnitOrderTargetView parent)
    {
        if (unitIds.Count == 1) { OpenUnitSourceDetails(unitIds[0], parent); return; }
        if (_unitOrderTarget != parent || _repository is not { } repository || _campaignComposition is not { } composition ||
            _session is not { } session || !ReferenceEquals(_unitOrderWorld, session.World) ||
            unitIds.Count is < 2 or > 30 || session.PlayerForceId is not int playerForceId) return;
        var world = session.World;
        var pages = composition.PrepareArmyQueries(world).PrepareArmyInformationPages(world, playerForceId, unitIds);
        var list = new ArmyDetailsListView();
        bool Current() => _commandDetailsView == list && _unitOrderTarget == parent &&
            ReferenceEquals(_session, session) && ReferenceEquals(session.World, world);
        OpenCommandDetails(list, close =>
        {
            RetainCommandDetailSpeechWorld(list, next => world = next);
            list.Configure(repository, _ui, pages, Current,
                unitId => OpenUnitSourceDetails(unitId, parent, list), close, CurrentListScrollSpeed);
        }, parent);
    }

    private void OpenUnitSourceDetails(int unitId, UnitOrderTargetView parent,
        ArmyDetailsListView? listParent = null, UnitSourceDetails? published = null)
    {
        if (_unitOrderTarget != parent || _repository is not { } repository || _campaignComposition is not { } composition ||
            _session is not { } session || !ReferenceEquals(_unitOrderWorld, session.World) ||
            !session.World.Units.ContainsKey(unitId)) return;
        var world = session.World;
        var rules = composition.PrepareArmyQueries(world);
        var initial = published ?? rules.QueryUnitSourceDetails(world, unitId);
        if (initial.Exists != true) return;
        var view = new ArmyDetailsView();
        Action<Action> configure = close => ConfigureUnitSourceDetails(view, world, rules, unitId, initial, close);
        if (listParent is null) OpenCommandDetails(view, configure, parent);
        else OpenArmyListMemberDetails(listParent, view, configure);
    }

    private void ConfigureUnitSourceDetails(ArmyDetailsView view, GameState world,
        ArmyCampaignRuleAdapter rules, int unitId, UnitSourceDetails initial, Action close,
        MapInformationBrowserView? browser = null, Action<GameState>? adoptWorld = null)
    {
        var repository = _repository!;
        var composition = _campaignComposition!;
        var session = _session!;
        var map = _residentMap;
        RetainCommandDetailSpeechWorld(view, next =>
        {
            world = next;
            rules = composition.PrepareArmyQueries(next);
        });
        int player = session.PlayerForceId ?? throw new InvalidOperationException("Army information requires a player.");
        var captives = rules.PrepareArmyCaptiveOfficerPages(world, player, unitId);
        var flag = initial.Identity is { ForceLookupId: int force }
            ? composition.DetailForces(world, [force]).GetValueOrDefault(force) : null;
        IReadOnlyList<LoadedSprite>? layers = null;
        if (initial.PortraitRoute is { } route)
        {
            var sprites = new LocalizedUiSpriteSource(repository);
            layers = route.LoadSprites(sprites.LoadTexturePackSprite);
        }
        static int PreviewKey(bool? land) => land is null ? -1 : land.Value ? 1 : 0;
        var detailsCache = new Dictionary<int, UnitSourceDetails>
            { [PreviewKey(initial.Formation?.LandSelected)] = initial };
        var parametersCache = new Dictionary<int, UnitSourceParameters>();
        var axesCache = new Dictionary<int, UnitSourceAxes>();
        view.Configure(repository, _ui, Read, close, ReadParameters,
            officer => RunInspectionUiAction(world, () => OpenOfficer(view, officer, [officer])),
            () => RunInspectionUiAction(world, OpenCaptives));

        bool Current(DetailsView page) => !_failed && !_shuttingDown && !IsReturningToTitle &&
            IsInspectionWorldCurrent(session, world) && _residentMap == map &&
            (browser is not null ? IsInformationBrowserCurrent(browser) && browser.IsCurrentDetail(page) : _commandDetailsView == page);

        void ShowChild(DetailsView parent, DetailsView child, Action<Action> configure)
        {
            if (!Current(parent)) { child.Free(); return; }
            if (browser is not null) browser.ShowDetail(child, configure);
            else OpenNestedCommandDetails(parent, child, configure);
        }

        void Adopt(GameState next)
        {
            world = next;
            rules = composition.PrepareArmyQueries(world);
            initial = rules.QueryUnitSourceDetails(world, unitId);
            captives = rules.PrepareArmyCaptiveOfficerPages(world, player, unitId);
            flag = initial.Identity is { ForceLookupId: int forceId }
                ? composition.DetailForces(world, [forceId]).GetValueOrDefault(forceId) : null;
            layers = initial.PortraitRoute?.LoadSprites(new LocalizedUiSpriteSource(repository).LoadTexturePackSprite);
            detailsCache.Clear(); parametersCache.Clear(); axesCache.Clear();
            detailsCache[PreviewKey(initial.Formation?.LandSelected)] = initial;
            if (browser is null && _commandDetailsUnitParent is not null) _unitOrderAdoptDetailWorld?.Invoke(next);
            adoptWorld?.Invoke(next);
            view.RefreshCurrent();
        }

        void OpenOfficer(DetailsView parent, int officer, IReadOnlyList<int> ids,
            Action<OfficerDetailSelectionReturn>? returned = null, bool? nameSortAscending = null)
        {
            if (!Current(parent)) return;
            var detail = new OfficerDetailsView();
            ShowChild(parent, detail, back => ConfigureCurrentOfficerDetail(detail, world, officer, back,
                () => Current(detail), Adopt, ids, returned, nameSortAscending));
        }

        void OpenCaptives()
        {
            if (!Current(view) || !captives.Available || captives.Value is not { } pages || pages.CurrentOfficerIds.Length == 0) return;
            var list = new OfficerInformationListView();
            ShowChild(view, list, back => ConfigureOfficerInformationList(list, world, rules, pages, back,
                Current, (child, configure) => ShowChild(list, child, configure), Adopt));
        }

        UnitSourceParameters ReadParameters(bool? land)
        {
            int key = PreviewKey(land);
            if (!parametersCache.TryGetValue(key, out var parameters))
            {
                parameters = rules.QueryUnitSourceParameters(world, unitId, land);
                parametersCache.Add(key, parameters);
            }
            return parameters;
        }

        ArmyDetailsContent Read(bool? land)
        {
            var details = initial;
            if (land is not null && !detailsCache.TryGetValue(PreviewKey(land), out details))
            {
                details = rules.QueryUnitSourceDetails(world, unitId, land);
                detailsCache.Add(PreviewKey(land), details);
            }
            int axesKey = PreviewKey(land ?? initial.Formation?.LandSelected);
            if (!axesCache.TryGetValue(axesKey, out var axes))
            {
                axes = rules.QueryUnitSourceAxes(world, unitId, land);
                axesCache.Add(axesKey, axes);
            }
            string unknown = _ui.Get("detail_unknown");
            string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
            var values = new Dictionary<int, string>();
            foreach (int node in new[] { 15, 17, 20, 41, 60, 61, 66, 69, 73, 78, 83,
                116, 121, 126, 131, 136, 142, 147, 152, 157 }) values[node] = unknown;
            values[15] = details.Identity?.ForceName ?? unknown;
            if (details.Identity?.DistrictNumber is int district)
            {
                string pattern = repository.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[0x494].Text
                    ?? throw new InvalidDataException("Missing army district format.");
                values[20] = pattern.Replace("%d", district.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }
            values[41] = details.Formation?.Selected?.Name ?? unknown;
            values[60] = Number(details.Resources.CurrentMorale);
            values[61] = Number(details.Resources.MaximumMorale);
            values[62] = "/";
            values[66] = Number(details.Resources.ReadyTroops);
            values[69] = Number(details.Resources.WoundedTroops);
            values[87] = details.Cargo.IsApplicable == false ? "" : Number(details.Cargo.Gold);
            values[90] = details.Cargo.IsApplicable == false ? "" : Number(details.Cargo.Food);
            foreach (var (node, formatted) in details.FormattedValues) values[node] = formatted;
            return new(values, details.Identity?.CommanderName ?? unknown, null, details.Identity?.NameplateVisible, flag, details.Formation?.Selected, layers,
                details.Formation?.LandSelected, details.Formation?.LandFormationId is not null,
                details.Formation?.NavalFormationId is not null, details.Resources.CurrentMorale, details.Resources.MaximumMorale,
                new(RadarValues(axes.WithoutConditionalModifiers), RadarValues(axes.WithConditionalModifiers), RadarValues(axes.SignedModifiers))) { NodePalettes = details.NodePalettes,
                    CommanderId = details.Identity?.CommanderId,
                    CaptivesAvailable = captives.Available ? captives.Value?.CurrentOfficerIds.Length > 0 : null,
                    CaptivesUnresolvedOwner = captives.UnresolvedInput };
        }

        static int?[] RadarValues(UnitSourceAxisValues? axes) =>
            [axes?.Mobility, axes?.FieldAttack, axes?.SiegeTroopAttack, axes?.WallAttack, axes?.Defense];
    }
}
