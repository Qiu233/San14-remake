using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public CampaignDeploymentPage PrepareCampaignDeploymentPage(GameState state, int forceId)
        => new(new CampaignPageSource(this, state, forceId));

    internal void ValidateCampaignDeploymentScope(GameState state, int forceId, int provinceId,
        int districtId, IReadOnlyList<int> sourceLocationIds)
    {
        var page = PrepareCampaignDeploymentPage(state, forceId);
        if (!page.Available) throw new InvalidDataException(page.UnresolvedOwner);
        if (page.QueryTargets().FirstOrDefault(row => row.Id == provinceId)?.Available != true ||
            page.QueryDistricts(provinceId).FirstOrDefault(row => row.Id == districtId)?.Available != true)
            throw CampaignPageMissing("selected_province_or_district");
        var sources = page.QueryFootholds(provinceId, districtId);
        foreach (int location in sourceLocationIds)
            if (sources.FirstOrDefault(row => row.Id == location)?.Available != true)
                throw CampaignPageMissing("selected_source_" + location);
    }

    private static InvalidDataException CampaignPageMissing(string key) =>
        new("campaign.deployment." + key);

    private sealed class CampaignConfirmationContext(int books) : INarrationCurrentContext
    {
        public int IntegerValue(int argument) => argument switch { 0 => books, 1 => unchecked(books - 1),
            _ => throw CampaignPageMissing("confirmation_integer") };
        public int PersonValue(int a, int f) => throw CampaignPageMissing("confirmation_person");
        public string PersonText(int a, int f) => throw CampaignPageMissing("confirmation_person");
        public int Relationship(int f, int a, int b) => throw CampaignPageMissing("confirmation_relation");
        public string AreaText(int a, int f) => throw CampaignPageMissing("confirmation_area");
    }

    private sealed class CampaignPageSource : ICampaignDeploymentPageSource
    {
        private readonly ArmyCampaignRuleAdapter owner;
        private readonly GameState state;
        private readonly SourceWorldContext? current;
        private readonly List<BaseReference> bases = [];
        private readonly Dictionary<int, int> provinces = [];
        private readonly Dictionary<int, BaseReference[]> neighbors = [];
        private readonly Dictionary<int, IReadOnlyList<CampaignDistrictChoice>> districts = [];
        private readonly Dictionary<(int, int), IReadOnlyList<CampaignFootholdChoice>> footholds = [];
        private readonly Dictionary<int, int[]> targetSources = [];
        private readonly Dictionary<int, int[]> districtSources = [];
        private readonly Dictionary<int, ImmutableArray<(int Force, int Troops)>> opposing = [];
        private readonly Dictionary<int, int> opposingForceTotals = [];
        private readonly Dictionary<int, OfficerPortraitRoute> portraits = [];
        private readonly Dictionary<int, byte> personStatuses = [];
        private readonly Dictionary<int, int> personPosts = [];
        private readonly Dictionary<(int Person, int Dimension), int> dimensions = [];
        private IReadOnlyList<CampaignProvinceChoice>? targets;
        public int ForceId { get; }
        public string? UnresolvedOwner { get; }
        private SourceWorldContext C => current ?? throw CampaignPageMissing("current");
        internal CampaignPageSource(ArmyCampaignRuleAdapter owner, GameState state, int forceId)
        {
            this.owner = owner; this.state = state; ForceId = forceId;
            try
            {
                if (state.Phase != TurnPhase.Strategy) throw CampaignPageMissing("strategy_phase");
                if (state.CampaignExpeditions?.ProvinceSequences is null) throw CampaignPageMissing("expedition_controller");
                current = owner.CaptureDeploymentPersonSelectionCurrent(state);
                owner._moraleWaveInputs.BindCurrent(state, current);
                owner._baseCapacityInputs.BindCurrent(state, current);
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                    ?? throw CampaignPageMissing("player_Force");
                if (!K(current.ReadForceNativeValidity(player), "player_validity") ||
                    K(current.ReadForcePhysicalSlot(player), "player_identity") != forceId)
                    throw CampaignPageMissing("player_control");
                foreach (var reference in current.ReadRegisteredBases() ?? throw CampaignPageMissing("Base_registry"))
                {
                    var receiver = reference ?? throw CampaignPageMissing("Base_pointer");
                    if (receiver.PhysicalSlot is null) continue;
                    bases.Add(receiver);
                }
            }
            catch (InvalidDataException error) { UnresolvedOwner = error.Message; }
        }

        public IReadOnlyList<CampaignProvinceChoice> Targets()
        {
            if (targets is not null) return targets;
            var rows = new List<CampaignProvinceChoice>();
            for (int province = 1; province < 23; province++)
            {
                string name = ProvinceName(province);
                try
                {
                    Require();
                    bool reachable = bases.Where(source => IsOwnBase(source) && Valid(source)).Any(source => Neighbors(source).Any(other => Province(other) == province));
                    bool hostile = reachable && bases.Any(receiver => Valid(receiver) && Province(receiver) == province && Hostile(receiver));
                    rows.Add(new(province, name, reachable && hostile, !reachable ? 0x5807 : !hostile ? 0x5808 : null, null));
                }
                catch (InvalidDataException error) { rows.Add(new(province, name, null, null, error.Message)); }
            }
            return targets = rows.AsReadOnly();
        }

        public IReadOnlyList<CampaignDistrictChoice> Districts(int province)
        {
            if (districts.TryGetValue(province, out var cached)) return cached;
            var rows = new List<CampaignDistrictChoice>();
            if (UnresolvedOwner is not null) return [];
            foreach (var reference in C.ReadRegistryReferences(RegistryKind.District) ?? throw CampaignPageMissing("District_registry"))
            {
                if (reference?.PhysicalSlot is not int physical) throw CampaignPageMissing("District_pointer");
                if (K(C.ReadDistrictForceId(physical), "District10") != ForceId) continue;
                int number = K(C.ReadDistrictNumber(physical), "District11");
                try
                {
                    var sources = bases.Where(receiver => K(C.ReadBaseDistrictSlot(receiver), "Base_District") == physical).ToArray();
                    districtSources[physical] = sources.Select(Location).ToArray();
                    bool reachable = sources.Any(receiver => Neighbors(receiver).Any(other => Province(other) == province));
                    bool person = reachable && HasDistrictPerson(physical);
                    rows.Add(new(physical, number.ToString(System.Globalization.CultureInfo.InvariantCulture), reachable && person,
                        !reachable ? 0x580A : !person ? 0x580B : null, null));
                }
                catch (InvalidDataException error) { rows.Add(new(physical, number.ToString(), null, null, error.Message)); }
            }
            return districts[province] = rows.AsReadOnly();
        }

        public IReadOnlyList<CampaignFootholdChoice> Footholds(int province, int district)
        {
            if (footholds.TryGetValue((province, district), out var cached)) return cached;
            var rows = new List<CampaignFootholdChoice>();
            if (Districts(province).FirstOrDefault(row => row.Id == district)?.Available != true) return [];
            int[] counts = new int[21];
            foreach (int location in districtSources[district])
            {
                string name = BaseName(location); int? hex = null;
                try
                {
                    var receiver = BaseLookup.Find(location, C);
                    hex = C.ReadBaseCurrentHexIndex(receiver);
                    var operation = new AutomaticDraftOperation(owner, state, location, C);
                    if (operation.Candidates().Count == 0)
                    { rows.Add(new(location, name, hex, false, 0x580E, 0, 0, null)); continue; }
                    var packets = operation.Create(DeploymentAutomaticStyle.FieldAttack, counts);
                    rows.Add(new(location, name, hex, packets.Count != 0, packets.Count == 0 ? 0x5685 : null,
                        packets.Count, packets.Sum(row => row.Request.Troops), null));
                }
                catch (InvalidDataException error) { rows.Add(new(location, name, hex, null, null, null, null, error.Message)); }
            }
            //727E10 retains the target province's hostile Base list. It is
            //independent of which source is currently checked.
            targetSources[province] = bases.Where(receiver => Valid(receiver) && Province(receiver) == province && Hostile(receiver)).Select(Location).ToArray();
            return footholds[(province, district)] = rows.AsReadOnly();
        }

        public IReadOnlyList<CampaignFootholdChoice> TargetMapFootholds()
        {
            var choices = Targets();
            return bases.Where(receiver => choices.FirstOrDefault(row => row.Id == Province(receiver))?.Available == true)
                .Select(receiver => MapBase(receiver, true)).ToImmutableArray();
        }

        public IReadOnlyList<CampaignFootholdChoice> DistrictMapFootholds(int province)
        {
            var choices = Districts(province);
            return bases.Where(IsOwnBase).Select(receiver =>
            {
                int district = K(C.ReadBaseDistrictSlot(receiver), "marker_Base_District");
                var choice = choices.FirstOrDefault(row => row.Id == district);
                return MapBase(receiver, choice?.Available, choice?.HintMessageId, choice?.UnresolvedOwner);
            }).ToImmutableArray();
        }

        private CampaignFootholdChoice MapBase(BaseReference receiver, bool? available,
            int? hint = null, string? missing = null)
        {
            int location = Location(receiver);
            return new(location, BaseName(location), C.ReadBaseCurrentHexIndex(receiver), available, hint, null, null, missing);
        }

        public ImmutableArray<CampaignArmyDeployment> Prepare(int province, int district, int location)
        {
            var choice = Footholds(province, district).FirstOrDefault(row => row.Id == location);
            if (choice?.Available != true) throw CampaignPageMissing(choice?.UnresolvedOwner ?? "source_not_selectable");
            var source = BaseLookup.Find(location, C);
            int start = C.ReadBaseCurrentHexIndex(source);
            var ranked = targetSources[province].Select((id, ordinal) =>
            {
                var target = BaseLookup.Find(id, C);
                return (Id: id, Ordinal: ordinal, Cost: ArmyRouteSearch.CampaignTargetCost(state, C, ForceId,
                    start, C.ReadBaseCurrentHexIndex(target)));
            }).OrderBy(row => row.Cost).ThenBy(row => row.Ordinal).ToArray();
            if (ranked.Length == 0) throw CampaignPageMissing("target_Foothold");
            var action = new DeploymentAction("attack", new(DeploymentTargetKind.Location, ranked[0].Id));
            var defaults = state.DefaultDeploymentBehavior ?? throw CampaignPageMissing("World_default_behavior");
            return new AutomaticDraftOperation(owner, state, location, C)
                .Create(DeploymentAutomaticStyle.FieldAttack, new int[21])
                .Select(row => new CampaignArmyDeployment(location, row.Request with
                {
                    NavalFormationId = row.NavalFormationId,
                    Actions = ImmutableArray.Create(action),
                    //73D340 overwrites these four fields with the world's
                    //actual preferences after260D70. It does not add points.
                    Behavior = defaults
                })).ToImmutableArray();
        }

        public int? ProvinceAtHex(int hex) => Resolve(() => ProvinceFromArea(AreaAt(hex)));
        public int? ProvinceFocusAtHex(int hex) => Resolve(() =>
        {
            byte raw = K(C.ReadAreaValidityCode(AreaAt(FocusHex(hex))), "focus_Area34");
            var city = C.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw CampaignPageMissing("focus_City_pointer");
            return K(owner._growthQueries.ReadCityState(state, city.PhysicalSlot ?? throw CampaignPageMissing("focus_City_receiver")), "focus_CityCE");
        });
        public int? DistrictAtHex(int hex) => Resolve(() =>
            DistrictFromArea(AreaAt(hex)));
        public int? DistrictFocusAtHex(int hex) => Resolve(() =>
            DistrictFromArea(AreaAt(FocusHex(hex))));
        private int DistrictFromArea(AreaReference area)
        {
            byte raw = K(C.ReadAreaCitySlot(area), "Area35");
            var city = C.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw CampaignPageMissing("City_pointer");
            var district = C.ReadCityDistrictReference(city) ?? throw CampaignPageMissing("City_virtual80");
            return K(C.ReadDistrictPhysicalSlot(district), "District_identity");
        }
        public int? FootholdAtHex(int hex) => Resolve(() =>
        {
            var reference = Hex(hex);
            ushort raw = K(C.ReadHexObjectLink(reference), "Hex1A");
            var city = C.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw CampaignPageMissing("pick_City");
            if (Valid(city)) return Location(city);
            var gate = C.ReadGateTableReference(raw is >= 52 and <= 61 ? raw - 51 : 0) ?? throw CampaignPageMissing("pick_Gate");
            return Valid(gate) ? Location(gate) : 0;
        });

        public bool? CanPickMapHex(int hex)
        {
            if ((uint)hex >= 48400) return false;
            try
            {
                byte code = K(C.ReadHexSurfaceCode(new(hex)), "pick_surface_code");
                var terrain = C.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw CampaignPageMissing("pick_surface_pointer");
                return terrain.PhysicalSlot is not null && K(C.ReadSurfaceTypeRawField2A(terrain), "pick_surface_cost") != 0;
            }
            catch (InvalidDataException) { return null; }
        }

        private AreaReference AreaAt(int hex) => AreaAt(Hex(hex));
        private AreaReference AreaAt(HexReference hex)
        {
            ushort raw = K(C.ReadHexAreaSlot(hex), "Hex12");
            return C.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw CampaignPageMissing("Area_pointer");
        }
        private HexReference Hex(int hex) => (uint)hex < 48400
            ? new(hex) : throw CampaignPageMissing("Hex_range");
        private HexReference FocusHex(int hex) => (uint)hex < 48400
            ? C.ReadHexTableReference(hex) ?? throw CampaignPageMissing("focus_Hex_pointer") : throw CampaignPageMissing("focus_Hex_range");
        private int ProvinceFromArea(AreaReference area)
        {
            byte raw = K(C.ReadAreaValidityCode(area), "Area34");
            var city = C.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw CampaignPageMissing("original_City");
            int physical = city.PhysicalSlot ?? throw CampaignPageMissing("original_City_receiver");
            int province = K(owner._growthQueries.ReadCityState(state, physical), "CityCE");
            var target = C.ReadStaticTableReference(0xDC80, province < 23 ? province : 0) ?? throw CampaignPageMissing("State_pointer");
            return target.PhysicalSlot ?? throw CampaignPageMissing("State_receiver");
        }
        private int Province(BaseReference receiver)
        {
            int location = Location(receiver);
            if (provinces.TryGetValue(location, out int value)) return value;
            return provinces[location] = ProvinceFromArea(PersonFreedomHomeSource.FindBaseArea229FD0(C, receiver));
        }
        private BaseReference[] Neighbors(BaseReference receiver)
        {
            int location = Location(receiver);
            if (neighbors.TryGetValue(location, out var found)) return found;
            var links = receiver.Kind == BaseKind.City ? owner._scenario.Snapshot.Cities[receiver.PhysicalSlot!.Value].LinkedLocationSlots
                : owner._scenario.Snapshot.Gates[receiver.PhysicalSlot!.Value].LinkedLocationSlots;
            int self = K(C.ReadBaseObjectLink(receiver), "Base4E");
            var result = new List<BaseReference>();
            foreach (byte link in links.Take(10))
            {
                if (link == 0) continue;
                var other = BaseLookup.Find(link, C);
                if (Valid(other) && link != self) result.Add(other);
            }
            return neighbors[location] = result.ToArray();
        }
        private bool HasDistrictPerson(int district)
        {
            foreach (int? physical in C.ReadRegisteredPersonPhysicalSlots() ?? throw CampaignPageMissing("Person_registry"))
            {
                int person = physical ?? throw CampaignPageMissing("Person_pointer");
                if (K(C.ReadPersonDistrictSlot(person), "PersonE0") != district ||
                    K(C.ReadPersonStatus(person), "PersonE6") is not (1 or 2 or 3 or 4)) continue;
                if ((K(C.ReadArmyReturnPersonFlags196(person), "Person15E") & 1) == 0) return true;
            }
            return false;
        }
        private bool IsOwnBase(BaseReference receiver) => K(C.ReadBaseForceSlot(receiver), "BaseForce") == ForceId;
        private bool Hostile(BaseReference receiver) => K(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
            ForceId, K(CurrentBaseAffiliationSource.ReadForce(state, C, receiver), "target_Force")), "Force_hostility");
        private bool Valid(BaseReference receiver) => K(C.ReadBaseNativeValidity(receiver), "Base_validity");
        private int Location(BaseReference receiver) => K(receiver.PhysicalSlot, "Base_physical_identity") +
            (receiver.Kind == BaseKind.Gate ? 51 : 0);
        private string BaseName(int location) => location <= 51
            ? state.Cities.GetValueOrDefault(location)?.Name ?? "" : state.Gates.GetValueOrDefault(location)?.Name ?? "";
        private string ProvinceName(int province) => S14ForceNameDefinitionReader.ReadLiteralUnits(
            owner._growthQueries.Definitions.Catalog.States[province].RawBytes.AsSpan(24, 6));
        private void Require() { if (UnresolvedOwner is not null) throw new InvalidDataException(UnresolvedOwner); }
        private static T K<T>(T? value, string key) where T : struct => value ?? throw CampaignPageMissing(key);
        private static int? Resolve(Func<int> read) => read();

        private List<CampaignArmyDeployment> SortArmies(IReadOnlyList<CampaignArmyDeployment> armies, bool handover)
        {
            var rows = armies.ToList();
            var abilities = new TacticRemakeCurrentAbilities(C, owner._scenario, owner.Seed.Language,
                SimulationRuleIdentity.ForLocale(owner.Seed.Language));
            //69A770 and6A2160 have different comparators. Both preserve the
            //actual packet and query only the keys reached by a comparison.
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i]; int at = i;
                while (at > 0 && Before(row.Unit.OfficerId, rows[at - 1].Unit.OfficerId))
                { rows[at] = rows[at - 1]; at--; }
                rows[at] = row;
            }
            return rows;

            int Physical(int id) => C.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot
                ?? throw CampaignPageMissing("sort_Person");
            byte Status(int id) => personStatuses.TryGetValue(id, out byte value) ? value :
                personStatuses[id] = K(C.ReadPersonStatus(Physical(id)), "sort_E6");
            int Post(int id) => personPosts.TryGetValue(id, out int value) ? value :
                personPosts[id] = K(C.ReadMoraleWavePersonPostE7(Physical(id)), "sort_E7");
            int Dimension(int id, int dimension) => dimensions.TryGetValue((id, dimension), out int value) ? value :
                dimensions[(id, dimension)] = K(abilities.ResolveDimension(state, id, dimension,
                    (force, effect) => owner._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "sort_effective_ability");
            int Grade(int id)
            {
                int post = Post(id);
                return K(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post), "sort_Post_validity")
                    ? K(OwnedArmyMoraleWaveInputs.ReadPost37(state, post), "sort_Post37") : int.MaxValue;
            }
            bool Before(int a, int b)
            {
                byte first = Status(a), second = Status(b);
                if (handover)
                {
                    if ((first == 1) != (second == 1)) return first == 1;
                    if ((first == 3) != (second == 3)) return first == 3;
                    int firstCapacity = K(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, Post(a)), "sort_Post_capacity");
                    int secondCapacity = K(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, Post(b)), "sort_Post_capacity");
                    if (firstCapacity != secondCapacity) return firstCapacity > secondCapacity;
                }
                else
                {
                    if (first != second) return first > second;
                    int firstGrade = Grade(a), secondGrade = Grade(b);
                    if (firstGrade != secondGrade) return firstGrade < secondGrade;
                }
                int left = Dimension(a, 0), right = Dimension(b, 0);
                if (left != right) return left > right;
                if (handover)
                {
                    left = Dimension(a, 1); right = Dimension(b, 1);
                    if (left != right) return left > right;
                }
                return a < b;
            }
        }

        public CampaignDeploymentConfirmation Confirmation(IReadOnlyList<CampaignArmyDeployment> armies)
        {
            int? payerId = null, books = null, cost = null;
            try
            {
                Require();
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, C) ?? throw CampaignPageMissing("confirmation_player");
                payerId = K(ForcePrimaryDistrictSource.Resolve(C, player).PhysicalSlot, "confirmation_primary_District");
                books = state.Districts.GetValueOrDefault(payerId.Value)?.OrdersRemaining ?? throw CampaignPageMissing("confirmation_books");
                cost = ReadCampaignLaunchCommandBooks(state);
                string text = new NarrationResourceMessages(owner._repository.LoadMessages(owner.Seed.Language, MessageKind.Game).Messages)
                    .ResolveCampaignConfirmation(new CampaignConfirmationContext(books.Value));
                return new(payerId, books, cost, Math.Clamp(unchecked(books.Value - cost.Value), 0, 200), text,
                    armies.Count != 0 && books.Value >= cost.Value, null);
            }
            catch (InvalidDataException error) { return new(payerId, books, cost, null, null, false, error.Message); }
        }

        public DeploymentNarrationQuery Handover(int province, IReadOnlyList<CampaignArmyDeployment> armies,
            string locale, Func<int, int> appRandom)
        {
            try
            {
                Require();
                if (armies.Count == 0 || locale is not ("TC" or "SC")) throw CampaignPageMissing("handover_rows_or_locale");
                var ordered = SortArmies(armies, handover: true);
                int speaker = ordered[0].Unit.OfficerId;
                int physical = C.ReadPersonLookupReference(speaker)?.PhysicalSlot ?? throw CampaignPageMissing("handover_Person");
                if (!K(C.ReadPersonNativeValidity(physical), "handover_validity")) throw CampaignPageMissing("handover_invalid_speaker");
                var person = state.Officers.GetValueOrDefault(speaker) ?? throw CampaignPageMissing("handover_Person_owner");
                var force = C.ReadPersonForce(physical) ?? throw CampaignPageMissing("handover_Force");
                int ruler = C.ReadForceRecord(K(force.PhysicalSlot, "handover_Force_receiver"))?.RawRuler10
                    ?? throw CampaignPageMissing("handover_ruler");
                var abilities = new TacticRemakeCurrentAbilities(C, owner._scenario, owner.Seed.Language,
                    SimulationRuleIdentity.ForLocale(owner.Seed.Language));
                var people = new DeploymentNarrationCurrentPeople(state, C, abilities, speaker, ruler,
                    panelReader: (forceId, effect) => owner._baseCapacityInputs.ReadPanelModifier(state, forceId, effect));
                ushort appearance = K(C.ReadPersonPortraitAppearance(physical), "handover_portrait");
                var portrait = ReadNarrationPortrait(state, C, owner._definitions, speaker, appearance);
                var text = new NarrationResourceMessages(owner._repository.LoadMessages(locale, MessageKind.Game).Messages)
                    .ResolveCampaignHandover(people, appRandom, ProvinceName(province));
                return new(speaker, person.Name, appearance, text.MessageId, null, text.Text, text.Pages, null)
                    { PortraitRoute = portrait };
            }
            catch (InvalidDataException error) { return new(null, null, null, null, null, null, null, error.Message); }
        }

        public CampaignDeploymentBar Bar(int province, IReadOnlyList<CampaignArmyDeployment> armies)
        {
            try
            {
                Require();
                if (!opposing.TryGetValue(province, out var others))
                {
                    var forces = new SortedSet<int>();
                    foreach (var receiver in bases.Where(row => row.Kind == BaseKind.City && Valid(row)))
                    {
                        int force = K(C.ReadBaseForceSlot(receiver), "bar_City_Force");
                        var pointer = C.ReadForceTableReference(force <= 51 ? force : 0) ?? throw CampaignPageMissing("bar_Force_pointer");
                        if (!K(C.ReadForceNativeValidity(pointer), "bar_Force_validity")) continue;
                        int actualForce = K(C.ReadForcePhysicalSlot(pointer), "bar_Force_identity");
                        if (actualForce == ForceId) continue;
                        int city = receiver.PhysicalSlot ?? throw CampaignPageMissing("bar_City_receiver");
                        if (K(owner._growthQueries.ReadCityState(state, city), "bar_CityCE") == province) forces.Add(actualForce);
                    }
                    var totals = new List<(int Force, int Troops)>();
                    var registry = C.ReadRegistryReferences(RegistryKind.Army) ?? throw CampaignPageMissing("bar_Army_registry");
                    foreach (int force in forces)
                    {
                        if (opposingForceTotals.TryGetValue(force, out int previous))
                        { totals.Add((force, previous)); continue; }
                        int sum = 0;
                        foreach (var receiver in bases)
                            if (K(C.ReadBaseForceSlot(receiver), "bar_Base_Force") == force)
                                sum = unchecked(sum + K(C.ReadBaseResource(receiver, BaseResourceKind.Troops), "bar_Base3C"));
                        foreach (var reference in registry)
                        {
                            int physical = reference?.PhysicalSlot ?? throw CampaignPageMissing("bar_Army_pointer");
                            var army = new ArmyUnitReference(physical);
                            if (C.ReadArmyUnitRawForceSlot(army) != force ||
                                K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, C, physical), "bar_Army233560")) continue;
                            var row = state.ReadArmyLifecycle()?.ReceiverSlots()[physical];
                            UnitState? unit = row?.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : row?.PendingImage;
                            sum = unchecked(sum + (unit?.Troops ?? throw CampaignPageMissing("bar_Army16")));
                        }
                        opposingForceTotals[force] = sum;
                        totals.Add((force, sum));
                    }
                    //69B2B0 compares troop totals descending; it has no
                    //additional Force identity tie-break.
                    opposing[province] = others = totals.OrderByDescending(row => row.Troops).ToImmutableArray();
                }
                int own = armies.Aggregate(0, (sum, row) => unchecked(sum + row.Unit.Troops));
                int other = others.Aggregate(0, (sum, row) => unchecked(sum + row.Troops));
                int total = unchecked(own + other);
                var segments = others.Select(row => new CampaignTroopSegment(row.Force, row.Troops,
                    total == 0 ? 0 : (double)row.Troops / total, C.ReadForceReportColor(new(row.Force)))).ToImmutableArray();
                var commanders = ImmutableArray.CreateBuilder<CampaignCommanderPreview>();
                foreach (var army in SortArmies(armies, handover: false).Take(7))
                {
                    int id = army.Unit.OfficerId;
                    string name = state.Officers.GetValueOrDefault(id)?.Name ?? "";
                    try
                    {
                        int physical = C.ReadPersonLookupReference(id)?.PhysicalSlot ?? throw CampaignPageMissing("preview_Person");
                        if (!portraits.TryGetValue(id, out var route))
                        {
                            ushort appearance = K(C.ReadPersonPortraitAppearance(physical), "preview_A6");
                            portraits[id] = route = ReadOfficerPortraitAppearance(state, C, owner._definitions, id,
                                appearance, ArmyMapUiSources.Create());
                        }
                        commanders.Add(new(id, name, army.Unit.Troops, route, route.UnresolvedInput));
                    }
                    catch (InvalidDataException error) { commanders.Add(new(id, name, army.Unit.Troops, null, error.Message)); }
                }
                string? additional = armies.Count > 7 ? owner.ReadHistoryDataText(0, 0x3C3)
                    .Replace("%d", (armies.Count - 7).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal) : null;
                return new(own, other, segments, commanders.ToImmutable(), null)
                    { PlayerForceColor49 = C.ReadForceReportColor(new(ForceId)), AdditionalArmiesText = additional };
            }
            catch (InvalidDataException error) { return new(null, null, [], [], error.Message); }
        }
    }
}
