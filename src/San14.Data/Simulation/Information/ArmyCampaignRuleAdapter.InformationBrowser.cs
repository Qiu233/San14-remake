using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public InformationBrowserPage PrepareInformationBrowserPage(GameState state, int playerForceId) =>
        new(new InformationSource(this, state, playerForceId));

    public InformationMenuProjection QueryInformationMenu(GameState state, int playerForceId, InformationTab tab) =>
        PrepareInformationBrowserPage(state, playerForceId).Menu(tab);

    public InformationList QueryInformationPage(GameState state, int playerForceId,
        InformationListKind kind, bool ownForceOnly) =>
        PrepareInformationBrowserPage(state, playerForceId).Query(kind, ownForceOnly);

    /// <summary>Formats an actual caller-supplied Person list. Membership and
    /// its order belong to that producer, not the global information filter.</summary>
    public InformationList QueryProvidedOfficerInformation(GameState state, int playerForceId,
        IReadOnlyList<int> officerIds) => new InformationSource(this,state,playerForceId).QueryProvidedOfficers(officerIds);

    private Dictionary<string, (int Id, string Text)>? informationColumnCaptions;
    internal InformationColumn InformationColumnFor(string key, string? nameTitle = null)
    {
        if (key == "name") return new(key, null, nameTitle ?? "");
        if (informationColumnCaptions is null)
        {
            //Only actual msgdata text is published. These words identify a
            //semantic caption, not an assertion of the original column preset.
            var words = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["force"]=["勢力","势力"], ["district"]=["軍團","军团"],
                ["ruler"]=["君主"], ["governor"]=["太守"], ["leader"]=["軍團長","军团长"],
                ["commander"]=["主將","主将"], ["troops"]=["兵力"], ["wounded"]=["傷兵","伤兵"],
                ["gold"]=["資金","资金","金"], ["supplies"]=["兵糧","軍糧","兵粮","军粮"],
                ["morale"]=["士氣","士气"], ["durability"]=["耐久"], ["location"]=["所在"],
                ["leadership"]=["統率","统率"], ["strength"]=["武力"], ["intelligence"]=["知力"],
                ["politics"]=["政治"], ["charm"]=["魅力"], ["rank"]=["爵位"], ["office"]=["官職","官职"],
                ["doctrine"]=["主義","主义"], ["description"]=["說明","说明"], ["owner"]=["所有者"],
                ["overseer"]=["擔當官","担当官"], ["command_books"]=["命令書","命令书"],
                ["command_limit"]=["統率兵數","统率兵数","指揮兵力","指挥兵力"],
                ["action"]=["行動","行动"], ["formation"]=["陣形","阵形"], ["formations"]=["陣形","阵形"],
                ["mobility"]=["機動","机动"], ["army_attack"]=["攻軍","攻军"], ["garrison_attack"]=["攻城"],
                ["durability_attack"]=["破城"], ["defense"]=["防禦","防御"], ["cost"]=["費用","费用"],
                ["command_cost"]=["指揮","指挥"], ["department"]=["部署"], ["style"]=["類型","类型"],
                ["value"]=["效果"], ["merit"]=["功績","功绩"], ["doctrine_points"]=["主義經驗","主义经验"],
                ["campaign_day"]=["日期"], ["event"]=["事件"],
                ["count"]=["持有數","持有数","數量","数量"], ["tactic"]=["戰法","战法"],
                ["required_cities"]=["必要都市數","必要都市数","所需都市數","所需都市数"], ["popularity"]=["民望"]
            };
            informationColumnCaptions = new(StringComparer.Ordinal);
            var records = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages.Sections[1].Records;
            foreach (var (semantic, candidates) in words)
                for (int id = 0; id < records.Count; id++)
                    if (records[id].Text is string text && candidates.Contains(text, StringComparer.Ordinal))
                    { informationColumnCaptions[semantic] = (id, text); break; }
        }
        return informationColumnCaptions.TryGetValue(key, out var caption)
            ? new(key, caption.Id, caption.Text) : new(key, null, "");
    }

    private sealed class InformationSource(ArmyCampaignRuleAdapter owner, GameState state, int playerForceId)
        : IInformationBrowserSource
    {
        private SourceWorldContext? current;
        private readonly Dictionary<InformationTab, InformationMenuProjection> menus = [];
        private readonly Dictionary<(InformationListKind, bool), InformationList> lists = [];
        private readonly Dictionary<(InformationTab, int), InformationList> opened = [];
        private InformationBelongingsPage? belongings;
        private InformationHistoryPage? history;
        private bool applicationRestrictionRead;
        private bool? applicationRestriction;
        private SourceWorldContext C
        {
            get
            {
                if (current is not null) return current;
                var captured = owner.CaptureDeploymentPersonSelectionCurrent(state);
                captured.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle() ?? throw Missing("army.lifecycle"));
                ForceUnregisterInputs.BindCurrent(state, captured);
                OwnedArmyReturnServiceInputs.BindCurrent(state, captured);
                //The lightweight Person selection capture intentionally clears
                //receivers outside its playable projection. Information lists
                //also traverse those receivers. Their saved detached owner
                //retains actual inactive Force rulers and Person sentinels;
                //absence from a Domain dictionary is not native invalidity.
                OwnedArmyLifecycleDetachedInputs.BindCurrent(state, captured);
                //Item50 and holder4A are independent current fields. Use the
                //same saved owner as item commands, never the opening catalog.
                PersonArmyCaptureInputs.BindCurrent(state, captured);
                current = captured;
                return captured;
            }
        }

        private static readonly InformationListKind[] Dynamic = [InformationListKind.Forces,
            InformationListKind.Cities, InformationListKind.Districts, InformationListKind.Areas,
            InformationListKind.Officers, InformationListKind.Gates, InformationListKind.Armies,
            InformationListKind.Items, InformationListKind.Advantages, InformationListKind.Scenery,
            InformationListKind.Destiny];
        private static readonly InformationListKind[] Definitions = [InformationListKind.Traits,
            InformationListKind.Tactics, InformationListKind.Doctrines, InformationListKind.Formations,
            InformationListKind.Policies, InformationListKind.Offices, InformationListKind.Facilities,
            InformationListKind.Ranks, InformationListKind.ForeignTechnologies,
            InformationListKind.DoctrinePanels, InformationListKind.CreditRanks];
        private static readonly int[] DynamicCaptions = [0x1fd,0x207,0x1fe,0x202,0x211,0x41d,0x224,
            0x246,0x2e1,0xad,0xd0];
        private static readonly int[] DefinitionCaptions = [0x23d,0x234,0x244,0x22b,0x241,0x21c,0x20a,
            0x21b,0x8b,0x829,0x821];

        public InformationMenuProjection Menu(InformationTab tab)
        {
            if (menus.TryGetValue(tab, out var saved)) return saved;
            if (!Enum.IsDefined(tab)) throw new ArgumentOutOfRangeException(nameof(tab));
            var entries = ImmutableArray.CreateBuilder<InformationMenuEntry>();
            bool? ownEnabled = null;
            string? issue = null;
            try { ownEnabled = (K(C.ReadWorldTransferServiceFlags(), "menu.World16A8") & 0x100) == 0; }
            catch (InvalidDataException error) { issue = error.Message; }
            for (int index = 0; index < 11; index++)
            {
                var kind = tab == InformationTab.Data ? Definitions[index] : Dynamic[index];
                int caption = tab == InformationTab.Data ? DefinitionCaptions[index] : DynamicCaptions[index];
                bool? enabled = tab == InformationTab.OwnForce ? ownEnabled : true;
                string? missing = enabled is null ? issue : null;
                if (tab == InformationTab.OwnForce && index == 7) caption = 0x69a;
                if (tab == InformationTab.OwnForce && index == 10)
                {
                    kind = InformationListKind.Chronology; caption = 0x7f;
                    if (enabled == true)
                    {
                        var history = state.CampaignChronicle;
                        if (history is null || history.PrimaryEntries.IsDefaultOrEmpty || history.ImportantEntries.IsDefaultOrEmpty)
                        { enabled = null; missing = "information.menu.chronology_first_writable_owner"; }
                        else if (history.PrimaryEntries[0].EntryKind == 0 && history.ImportantEntries[0].EntryKind == 0)
                            enabled = false;
                    }
                }
                if (enabled == true && index >= 8)
                {
                    if (!applicationRestrictionRead)
                    {
                        applicationRestriction = owner.ReadApplicationInformationRestriction(state);
                        applicationRestrictionRead = true;
                    }
                    enabled = applicationRestriction is bool restricted ? !restricted : null;
                    if (enabled is null) missing = "information.menu.application_information_restriction";
                }
                string text;
                try { text = owner.ReadHistoryDataText(1, caption); }
                catch (InvalidDataException error) { text = ""; missing ??= error.Message; }
                entries.Add(new(20 + 4 * index, kind, caption, text, true, enabled, missing));
            }
            return menus[tab] = new(ownEnabled is bool own ? own ? InformationTab.OwnForce : InformationTab.AllForces : null,
                [ownEnabled, true, true], entries.ToImmutable(), issue);
        }

        public InformationList OpenList(InformationTab tab, int node)
        {
            if (opened.TryGetValue((tab, node), out var saved)) return saved;
            var entry = Menu(tab).Entries.FirstOrDefault(row => row.Node == node);
            if (entry is null) return Empty("", "information.menu.node");
            if (entry.Enabled != true) return opened[(tab, node)] = Empty(entry.Caption,
                entry.UnresolvedOwner ?? "information.menu.disabled");
            if (tab == InformationTab.OwnForce && node == 48)
                return opened[(tab, node)] = QueryBelongings().Sections[0].List;
            return opened[(tab, node)] = Query(entry.Kind, tab == InformationTab.OwnForce);
        }

        public InformationList Query(InformationListKind kind, bool ownForceOnly)
        {
            if (lists.TryGetValue((kind, ownForceOnly), out var saved)) return saved;
            string title = Title(kind, ownForceOnly);
            try
            {
                saved = Definitions.Contains(kind)
                    ? owner.ReadInformationDefinitionList(state, C, kind, title, playerForceId)
                    : ReadDynamic(kind, ownForceOnly, title);
            }
            catch (InvalidDataException error) { saved = Empty(title, error.Message); }
            return lists[(kind, ownForceOnly)] = saved;
        }

        public InformationBelongingsPage QueryBelongings()
        {
            if (belongings is not null) return belongings;
            string title = owner.ReadHistoryDataText(1, 0x69b);
            string itemTitle = owner.ReadHistoryDataText(1, 0x246);
            string gemTitle = owner.ReadHistoryDataText(1, 0x699);
            InformationList items;
            InformationList gems;
            try { items = owner.ReadInformationBelongingsItems(state, C, playerForceId, itemTitle); }
            catch (InvalidDataException error) { items = Empty(itemTitle, error.Message); }
            try { gems = owner.ReadInformationBelongingsGems(state, C, playerForceId, gemTitle); }
            catch (InvalidDataException error) { gems = Empty(gemTitle, error.Message); }
            var missing = new[] { items.UnresolvedOwner, gems.UnresolvedOwner }.Where(value => value is not null);
            string joined = string.Join(";", missing);
            return belongings = new(title, [new(0, 0x246, itemTitle, items), new(1, 0x699, gemTitle, gems)],
                joined.Length == 0 ? null : joined);
        }

        public InformationHistoryPage QueryHistory() => history ??= owner.ReadInformationHistoryPage(state);

        private string Title(InformationListKind kind, bool own)
        {
            int index = Array.IndexOf(Definitions, kind);
            if (index >= 0) return owner.ReadHistoryDataText(1, DefinitionCaptions[index]);
            if (kind == InformationListKind.Chronology) return owner.ReadHistoryDataText(1, 0x7f);
            index = Array.IndexOf(Dynamic, kind);
            return index >= 0 ? owner.ReadHistoryDataText(1, own && kind == InformationListKind.Items ? 0x69a : DynamicCaptions[index]) : "";
        }

        internal InformationList QueryProvidedOfficers(IReadOnlyList<int> officerIds)
        {
            string title=Title(InformationListKind.Officers,false);
            try { return ReadDynamic(InformationListKind.Officers,false,title,officerIds); }
            catch(InvalidDataException error) { return Empty(title,error.Message); }
        }

        internal InformationList QueryArmyCaptives(int unitId)
        {
            var unit = state.Units.GetValueOrDefault(unitId) ?? throw Missing("captives.Army_owner");
            int[] people = owner.EnumerateUnitDetailCaptivePhysicalSlots(state, C, unit)
                .Select(physical => (int)K(C.ReadPersonIdentity(physical), "captives.Person_id")).ToArray();
            //45E8D0 starts with selected-first sorting and no selected People.
            //Its strict stable comparison therefore preserves this input order.
            //Only these actual candidates receive the ordinary Person cells.
            return ReadDynamic(InformationListKind.Officers, false,
                owner.ReadHistoryDataText(1, 0x5d1), people);
        }

        private InformationList ReadDynamic(InformationListKind kind, bool own, string title,
            IReadOnlyList<int>? officerIds=null)
        {
            var rows = new List<InformationRow>();
            var issues = new HashSet<string>(StringComparer.Ordinal);
            var columns = new List<string> { "name" };
            var player = C.ReadForceTableReference(playerForceId is >= 0 and < 52 ? playerForceId : 0)
                ?? throw Missing("player_force_lookup");
            int actualPlayer = K(player.PhysicalSlot, "player_force_pointer");
            switch (kind)
            {
                case InformationListKind.Cities:
                case InformationListKind.Gates:
                    columns.AddRange(["force", "district", "governor", "troops", "gold", "supplies", "morale", "durability"]);
                    Table(kind == InformationListKind.Cities ? RegistryKind.City : RegistryKind.Gate,
                        kind == InformationListKind.Cities ? 52 : 11, p =>
                    {
                        var reference = new BaseReference(kind == InformationListKind.Cities ? BaseKind.City : BaseKind.Gate, p);
                        int force = Force(K(C.ReadBaseForceSlot(reference), "base.force"));
                        if (own && force != actualPlayer) return;
                        int id = kind == InformationListKind.Cities ? p : p + 51;
                        string name; Dictionary<string, InformationCell> cells;
                        if (kind == InformationListKind.Cities)
                        {
                            var city = state.Cities.GetValueOrDefault(id) ?? throw Missing("city.domain_owner");
                            name = city.Name; cells = Cells(name);
                            cells["troops"] = Number(city.Troops); cells["gold"] = Number(city.Gold);
                            cells["supplies"] = Number(city.Supplies); cells["morale"] = Number(city.Morale, "city.morale");
                            cells["durability"] = Number(city.Durability, "city.durability");
                            cells["district"] = District(city.DistrictId); cells["governor"] = Person(city.GovernorId);
                        }
                        else
                        {
                            var gate = state.Gates?.GetValueOrDefault(id) ?? throw Missing("gate.domain_owner");
                            name = gate.Name; cells = Cells(name); cells["troops"] = Number(gate.Troops);
                            cells["durability"] = Number(gate.Durability, "gate.durability");
                            cells["district"] = District(gate.DistrictId); cells["governor"] = Person(gate.GovernorId);
                            cells["gold"] = Unknown("gate.gold"); cells["supplies"] = Unknown("gate.supplies");
                            cells["morale"] = Unknown("gate.morale");
                        }
                        cells["force"] = ForceName(force); Add(id, name, cells);
                    }); break;
                case InformationListKind.Forces:
                    columns.AddRange(["ruler", "rank", "doctrine"]);
                    var normal = new List<InformationRow>(); var special = new List<InformationRow>();
                    if (own) ReadForce(actualPlayer, false);
                    Table(RegistryKind.Force, 52, p => { if (!own || p != actualPlayer) ReadForce(p, true); });
                    rows.AddRange(normal); rows.AddRange(special); break;

                    void ReadForce(int p, bool partition)
                    {
                        int ruler = K(C.ReadForceRecord(p)?.RawRuler10, "force.ruler");
                        var force = state.Forces.GetValueOrDefault(p);
                        string name = force?.Name ?? state.Officers.GetValueOrDefault(ruler)?.Name ?? "";
                        var cells = Cells(name); cells["ruler"] = Person(ruler);
                        cells["rank"] = force?.RankId is int rank && state.ForceRanks.TryGetValue(rank, out var rankValue)
                            ? new(rankValue.Name, Number: rank) : Unknown("force.rank");
                        cells["doctrine"] = force?.DoctrineId is int doctrine && state.Doctrines.TryGetValue(doctrine, out var doctrineValue)
                            ? new(doctrineValue.Name, Number: doctrine) : Unknown("force.doctrine");
                        var row = Row(p, name, cells);
                        if (!partition) rows.Add(row);
                        else if ((uint)(ruler - 3001) < 150 || (uint)(ruler - 2001) < 88 || (uint)(ruler - 5101) < 100)
                            special.Add(row);
                        else normal.Add(row);
                    }
                case InformationListKind.Districts:
                    columns.AddRange(["force", "leader", "command_books"]);
                    Table(RegistryKind.District, 52, p =>
                    {
                        if (p == 51) return;
                        int force = Force(K(C.ReadDistrictForceId(p), "district.force"));
                        if (own && force != actualPlayer) return;
                        var district = state.Districts.GetValueOrDefault(p) ?? throw Missing("district.domain_owner");
                        string name = district.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        var cells = Cells(name); cells["name"] = new(name, Number: district.Number);
                        cells["force"] = ForceName(force); cells["leader"] = Person(district.LeaderId);
                        cells["command_books"] = Number(district.OrdersRemaining, "district.command_books"); Add(p, name, cells);
                    }); break;
                case InformationListKind.Areas:
                    columns.AddRange(["force", "location", "overseer"]);
                    Table(RegistryKind.Area, 501, p =>
                    {
                        int force = Force(K(C.ReadAreaCurrentForceSlot(new(p)), "area.force"));
                        if (own && force != actualPlayer) return;
                        var area = state.Areas.GetValueOrDefault(p) ?? throw Missing("area.domain_owner");
                        var cells = Cells(area.Name); cells["force"] = ForceName(force);
                        cells["location"] = new(area.CityId is int city ? state.Cities.GetValueOrDefault(city)?.Name : null, Number: area.CityId);
                        cells["overseer"] = Person(area.Administration?.OverseerId); Add(p, area.Name, cells);
                    }); break;
                case InformationListKind.Officers:
                    columns.AddRange(["force", "location", "district", "leadership", "strength", "intelligence", "politics", "charm"]);
                    void ReadOfficerRow(int p)
                    {
                        if(officerIds is null)
                        {
                            byte status = K(C.ReadPersonStatus(p), "person.status");
                            ushort flags = K(C.ReadArmyReturnPersonFlags196(p), "person.flags");
                            if (status is < 1 or > 6 || (status == 5 && (flags & 0x4000) != 0)) return;
                        }
                        int force = K((C.ReadPersonForce(p) ?? throw Missing("person.force_lookup")).PhysicalSlot, "person.force_pointer");
                        ushort home = K(C.ReadPersonFirstLocation(p), "person.home");
                        if (officerIds is null && (force == 51 || ((home is < 1 or > 61) && force is >= 46 and <= 50))) return;
                        if (own && force != actualPlayer) return;
                        int id = K(C.ReadPersonIdentity(p), "person.id");
                        var person = state.Officers.GetValueOrDefault(id) ?? throw Missing("person.domain_owner");
                        string name = person.CurrentSurname is {} surname && person.CurrentGivenName is {} given ? surname + given : person.Name;
                        var cells = Cells(name); cells["name"] = person.CurrentNameSortKey is string sort ? new(name, SortText: sort) : Unknown("person.name_sort_key");
                        cells["force"] = ForceName(force); cells["location"] = Location(home);
                        cells["district"] = District(person.DistrictId);
                        var abilities = abilitySource ??= new(C, owner._scenario, owner.Seed.Language, SimulationRuleIdentity.ForLocale(owner.Seed.Language));
                        if (abilities.Resolve(state, id) is {} a)
                        {
                            cells["leadership"] = Number(a.Leadership); cells["strength"] = Number(a.Strength);
                            cells["intelligence"] = Number(a.Intelligence); cells["politics"] = Number(a.Politics); cells["charm"] = Number(a.Charm);
                        }
                        else foreach (string key in new[] { "leadership", "strength", "intelligence", "politics", "charm" }) cells[key] = Unknown("person.current_abilities");
                        Add(id, name, cells);
                    }
                    if(officerIds is null)Table(RegistryKind.Person,1851,ReadOfficerRow);
                    else foreach(int requested in officerIds)
                    {
                        if(requested is < 1 or > 6000)throw Missing("provided_person.identity");
                        ReadOfficerRow(K(C.ReadPersonLookupReference(requested)?.PhysicalSlot,"provided_person.pointer"));
                    }
                    if(officerIds is null)rows = rows.OrderBy(row => row, Comparer<InformationRow>.Create((left, right) =>
                    {
                        var a = left.Cells["name"]; var b = right.Cells["name"];
                        if (a.UnresolvedOwner is not null || b.UnresolvedOwner is not null) return 0;
                        return CompareNameSortKeys(a.SortText!, b.SortText!);
                    })).ToList(); break;
                case InformationListKind.Armies:
                    columns.AddRange(["force", "commander", "troops", "morale", "location", "action", "formation"]);
                    var receivers = state.ReadArmyLifecycle()?.ReceiverSlots() ?? throw Missing("army.lifecycle");
                    Table(RegistryKind.Army, 501, p =>
                    {
                        int force = K((C.ReadArmyUnitForceReference(new(p)) ?? throw Missing("army.force_lookup")).PhysicalSlot, "army.force_pointer");
                        if (own && force != actualPlayer) return;
                        var slot = receivers[p];
                        var unit = slot.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : slot.PendingImage;
                        if (unit is null) throw Missing("army.current_image");
                        string name = state.Officers.GetValueOrDefault(unit.OfficerId)?.Name ?? "";
                        var cells = Cells(name); cells["force"] = ForceName(force); cells["commander"] = Person(unit.OfficerId);
                        cells["troops"] = Number(unit.Troops); cells["morale"] = Number(unit.MoraleState.CurrentValue, "army.morale");
                        cells["location"] = Number(K(C.ReadArmyUnitRawHex2A(new(p)), "army.hex"));
                        cells["action"] = Number(ArmyConfigurationSourceAdapter.ReadAction(state, unit), "army.action");
                        cells["formation"] = unit.FormationId is int f && state.Formations.TryGetValue(f, out var formation)
                            ? new(formation.Name, Number: f) : Unknown("army.formation"); Add(unit.Id, name, cells);
                    }); break;
                case InformationListKind.Items:
                    return owner.ReadInformationItems(state, C, playerForceId, title, own, false);
                case InformationListKind.Scenery:
                    C.MaterializeFixedScenery(owner._landmarkDefinitions);
                    for (int slot = 0; slot < 101; slot++) C.SetSceneryFlags47(slot, state.LandmarkInvestments?.GetValueOrDefault(slot)?.Flags);
                    columns.AddRange(["force", "location"]);
                    Table(RegistryKind.Scenery, 101, p =>
                    {
                        var scenery = new SceneryReference(p);
                        if ((K(C.ReadSceneryFlags47(scenery), "scenery.flags") & 1) == 0) return;
                        int force = K(ReadLandmarkMapOwner(C, scenery), "scenery.force");
                        if (own && force != actualPlayer) return;
                        string name = C.SceneryDefinitions!.Sceneries[p].Name;
                        var cells = Cells(name); cells["force"] = ForceName(force);
                        cells["location"] = Number(C.ReadSceneryHexIndex(scenery), "scenery.hex"); Add(p, name, cells, false);
                    }); break;
                case InformationListKind.Chronology:
                    columns.AddRange(["campaign_day", "event", "description"]);
                    var chronicle = state.CampaignChronicle ?? throw Missing("chronology.saved_owner");
                    int sequence = 0;
                    foreach (var record in chronicle.PrimaryEntries.Concat(chronicle.ImportantEntries))
                    {
                        int id = sequence++; if (record.EntryKind == 0) continue;
                        string name = Text(record.MainLabel); var cells = Cells(name);
                        cells["campaign_day"] = Number(record.CampaignDay); cells["event"] = Number(record.EventCode);
                        cells["description"] = new(string.Join(" ", new[] { Text(record.Label0), Text(record.Label1), Text(record.Label2) }.Where(s => s.Length != 0)));
                        Add(id, name, cells, false);
                    }
                    break;
                case InformationListKind.Advantages:
                    return owner.ReadInformationAdvantageList(state, C, playerForceId, own, title);
                case InformationListKind.Destiny:
                    return owner.ReadInformationDestinyList(state, C, playerForceId, title);
                default: issues.Add("information.dynamic_kind"); break;
            }
            return new(title, columns.Select(key => owner.InformationColumnFor(key, title)).ToImmutableArray(),
                rows.ToImmutableArray(), issues.Count == 0 ? null : string.Join(";", issues));

            void Table(RegistryKind registry, int count, Action<int> read)
            {
                for (int lookup = 0; lookup < count; lookup++)
                    try
                    {
                        var pointer = C.ReadRegistryTableReference(registry, lookup) ?? throw Missing("table." + registry);
                        if (!K(C.ReadRegistryObjectNativeValidity(pointer), "validity." + registry)) continue;
                        read(K(pointer.PhysicalSlot, "receiver." + registry));
                    }
                    catch (InvalidDataException error) { issues.Add(error.Message); }
            }
            InformationRow Row(int id, string name, Dictionary<string, InformationCell> cells, bool detail = true) =>
                new((int)kind, id, name, cells.ToImmutableDictionary(StringComparer.Ordinal), detail ? new(kind, id) : null);
            void Add(int id, string name, Dictionary<string, InformationCell> cells, bool detail = true) => rows.Add(Row(id, name, cells, detail));
            InformationCell ForceName(int id) => id == 0 ? new("") : state.Forces.TryGetValue(id, out var force)
                ? new(force.Name, Number: id) : Unknown("force.name");
            InformationCell Person(int? id) => id is null ? Unknown("person.identity") : id == 0 ? new("") :
                state.Officers.TryGetValue(id.Value, out var officer) ? new(officer.Name, Number: id) : Unknown("person.name");
            InformationCell District(int? id) => id is null ? Unknown("district.identity") :
                state.Districts.TryGetValue(id.Value, out var d) ? new(d.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), Number: d.Number) : Unknown("district.name");
            InformationCell Location(int id) => id == 0 ? new("") : state.Cities.TryGetValue(id, out var city) ? new(city.Name, Number: id) :
                state.Gates?.GetValueOrDefault(id) is {} gate ? new(gate.Name, Number: id) : Unknown("person.location");
        }

        private TacticRemakeCurrentAbilities? abilitySource;
        private int Force(int raw) => K((C.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0)
            ?? throw Missing("force.lookup")).PhysicalSlot, "force.pointer");
        private static Dictionary<string, InformationCell> Cells(string name) => new(StringComparer.Ordinal) { ["name"] = new(name, SortText: name) };
        private static InformationCell Number(long? value, string field = "value") => value.HasValue ? new(Number: value) : Unknown(field);
        private static InformationCell Unknown(string field) => new(UnresolvedOwner: "information." + field);
        private static string Text(ImmutableArray<ushort> text) => new(text.TakeWhile(c => c != 0).Select(c => (char)c).ToArray());
        private static InformationList Empty(string title, string missing) => new(title, [], [], missing);
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("information." + field);
    }
}
