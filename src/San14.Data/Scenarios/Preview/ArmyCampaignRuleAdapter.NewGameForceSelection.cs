using System.Collections.ObjectModel;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Publishes the complete ordinary force-selection page once.
    /// Selection/hover consume this publication without recapturing current state.</summary>
    public NewGameForceSelectionPreview CaptureNewGameForceSelection(GameState state,
        LoadedArmyFlagPaneNames names, NewGameForcePageApplication? application = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(names);
        bool preview = ArmyOperationContext.IsNewGamePreview(state);
        var current = preview ? CapturePersonPresentationCurrent(state) : CaptureDetailForceInputs(state);
        if (current is null) return new([], new ReadOnlyDictionary<int, NewGameForcePreview>(
            new Dictionary<int, NewGameForcePreview>()), [], null, ["new-game.force.current-definition-owner"]);
        //Reuse existing current Person/Base/registry owners. No opening-summary
        //fallback, world publication or correctness-proof pass is introduced.
        if (!preview) current.OverlayOwnedArmyReturnCurrentFields(state);
        _moraleWaveInputs.BindCurrent(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        if (state.ReadArmyLifecycle() is { } arena)
            _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        return new NewGameForcePageReader(this, state, current, names, application ?? new(null, null, null), preview).Read();
    }

    private sealed class NewGameForcePageReader(ArmyCampaignRuleAdapter owner, GameState state,
        SourceWorldContext current, LoadedArmyFlagPaneNames names, NewGameForcePageApplication application, bool preview)
    {
        private readonly List<string> issues = [];
        private readonly MapSpritePalettes palettes = MapSpritePalettes.Create();
        private readonly List<BaseSample> bases = [];
        private readonly List<PersonSample> people = [];
        private readonly Dictionary<(int Force, int Province), int> provinceCities = [];
        private bool basesKnown, peopleKnown, provinceCitiesKnown;
        private IReadOnlyList<MessageRecord>? dataText;
        private sealed record BaseSample(BaseReference Receiver, int? Owner, bool? Valid,
            ushort? ObjectLink, int? Gold, int? Food, int? Troops, NewGameForceBaseMarker Marker);
        private sealed record PersonSample(int? Owner, byte? Status);
        private sealed record Candidate(ForceReference Force, TransferPersonReference Ruler, bool? RulerValid, int? Key);

        internal NewGameForceSelectionPreview Read()
        {
            try { dataText = owner._repository.LoadMessages(owner._scenario.Snapshot.Locale, MessageKind.Data)
                .Messages.Sections[1].Records; }
            catch (InvalidDataException error) { issues.Add("new-game.force.messages: " + error.Message); }
            ReadBaseSamples(); ReadPersonSamples(); ReadProvinceCitySamples();
            var candidates = ReadCandidates();
            //472AB0 is stable strict-less insertion, using actual Person10.
            for (int at = 1; at < candidates.Count; at++)
            {
                var item = candidates[at]; int before = at;
                while (before > 0 && item.Key is int key && candidates[before - 1].Key is int previous && key < previous)
                { candidates[before] = candidates[before - 1]; before--; }
                candidates[before] = item;
            }
            //504A40 filters ruler validity only after472AB0 has sorted the
            //full ordinary list; invalid/null rulers can affect comparisons.
            var eligible = candidates.Where(x => x.RulerValid == true).ToArray();
            var ordered = eligible.Where(x => !Custom(x.Key)).Concat(eligible.Where(x => Custom(x.Key))).ToArray();
            var rows = ordered.Select(ReadRow).ToArray();
            var index = new Dictionary<int, NewGameForcePreview>();
            foreach (var row in rows) index.TryAdd(row.ForceId, row);
            //Avatar events select22CB40(Person), while map markers can reach
            //Forces absent from the portrait list. Publish those same owners
            //without adding synthetic avatars or recapturing on hover.
            var reached = rows.Select(x => x.RulerForceId).Concat(bases.Select(x => x.Marker.ForceId))
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
            foreach (int force in reached.Where(id => !index.ContainsKey(id)))
            {
                var reference = new ForceReference(force);
                bool? valid = current.ReadForceNativeValidity(reference);
                if (valid != true)
                { index[force] = new(force) { ForceValid = valid }; continue; }
                ushort? raw = current.ReadForceRecord(force)?.RawRuler10;
                var person = raw is ushort r ? r <= 6000 ? current.ReadPersonLookupReference(r)
                    : current.ReadTransferPersonTableReference(0) : null;
                if (person is null)
                {
                    index[force] = new(force) { ForceValid = true, UnresolvedInputs = new ReadOnlyDictionary<string, string>(
                        new Dictionary<string, string> { [nameof(NewGameForcePreview.RulerId)] = "new-game.force.reached-ruler-lookup" }) };
                    continue;
                }
                bool? personValid = person.PhysicalSlot is int p ? current.ReadPersonNativeValidity(p) : false;
                int? identity = person.PhysicalSlot is int physical ? current.ReadPersonIdentity(physical) : null;
                index[force] = ReadRow(new(reference, person, personValid, identity));
            }
            int? first = rows.Length == 0 ? null : rows[0].RulerForceId;
            return new(Array.AsReadOnly(rows), new ReadOnlyDictionary<int, NewGameForcePreview>(index),
                Array.AsReadOnly(bases.Where(x => x.Valid == true).Select(x => x.Marker).ToArray()),
                first, issues.AsReadOnly());
        }
        private static bool Custom(int? id) => id is >= 3001 and <= 3150 or >= 2001 and <= 2088 or >= 5101 and <= 5200;
        private List<Candidate> ReadCandidates()
        {
            var result = new List<Candidate>();
            try
            {
                var roster = current.ReadRegistryReferences(RegistryKind.Force)
                    ?? throw Missing("current-Force-chain");
                foreach (var entry in roster)
                {
                    if (entry?.Kind != RegistryKind.Force || entry.PhysicalSlot is not int physical)
                        throw Missing("current-Force-chain-payload");
                    var force = new ForceReference(physical);
                    ushort ruler = current.ReadMoraleWaveRuler22EAC0(force) ?? throw Missing("current-Force-ruler-filter");
                    if (ruler == 0 || physical is >= 46 and <= 50) continue;
                    //564C30's filter above is not Force NativeValidity.
                    ushort rawRuler = current.ReadForceRecord(physical)?.RawRuler10 ?? throw Missing("current-Force10");
                    var person = rawRuler <= 6000 ? current.ReadPersonLookupReference(rawRuler)
                        : current.ReadTransferPersonTableReference(0);
                    if (person is null) throw Missing("ruler-Person-lookup");
                    if (person.PhysicalSlot is not int p) { result.Add(new(force, person, false, null)); continue; }
                    bool? valid = current.ReadPersonNativeValidity(p);
                    if (valid is null) issues.Add("new-game.force.roster-ruler-validity:" + physical);
                    int? id = current.ReadPersonIdentity(p);
                    if (id is null) issues.Add("new-game.force.order-Person10:" + physical);
                    result.Add(new(force, person, valid, id));
                }
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
            return result;
        }
        private void ReadBaseSamples()
        {
            try
            {
                foreach (var target in current.ReadRegisteredBases() ?? throw Missing("current-Base-chain"))
                {
                    if (target?.PhysicalSlot is not int physical) throw Missing("current-Base-chain-payload");
                    int? force = current.ReadBaseForceSlot(target);
                    int id = target.Kind == BaseKind.City ? physical : physical + 51;
                    //523BD0 compares22CAD0's actual Force pointer. Aggregates
                    //use the iterator's independent raw virtualB0 identity.
                    int? mapForce = force is int raw ? current.ReadForceTableReference((uint)raw < 52 ? raw : 0)?.PhysicalSlot : null;
                    int? hex = null;
                    try { hex = CurrentGateMapQueries.FindHex(target, current).PhysicalSlot; }
                    catch (InvalidDataException error) { issues.Add("new-game.force.base-position:" + id + ":" + error.Message); }
                    bases.Add(new(target, force, current.ReadBaseNativeValidity(target), current.ReadBaseObjectLink(target),
                        current.ReadBaseResource(target, BaseResourceKind.Gold), current.ReadBaseResource(target, BaseResourceKind.Supplies),
                        current.ReadBaseResource(target, BaseResourceKind.Troops), new(target.Kind, id, mapForce, hex)));
                }
                basesKnown = true;
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
        }
        private void ReadPersonSamples()
        {
            try
            {
                foreach (var person in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("current-Person-chain"))
                {
                    if (person is not int physical) throw Missing("current-Person-chain-payload");
                    people.Add(new(current.ReadPersonOldForceSlot(physical), current.ReadPersonStatus(physical)));
                }
                peopleKnown = true;
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
        }
        private void ReadProvinceCitySamples()
        {
            try
            {
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("current-City-chain"))
                {
                    if (entry?.Kind != RegistryKind.City || entry.PhysicalSlot is not int city) throw Missing("current-City-chain-payload");
                    bool valid = current.ReadBaseNativeValidity(new(BaseKind.City, city)) ?? throw Missing("current-City-validity");
                    if (!valid) continue;
                    int province = (preview ? owner._scenario.Snapshot.Cities[city].GroupCode : owner._growthQueries.ReadCityState(state, city)) ?? throw Missing("current-CityCE");
                    int force = current.ReadBaseForceSlot(new(BaseKind.City, city)) ?? throw Missing("current-City-owner");
                    var key = (force, province);
                    provinceCities[key] = unchecked(provinceCities.GetValueOrDefault(key) + 1);
                }
                provinceCitiesKnown = true;
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
        }
        private NewGameForcePreview ReadRow(Candidate candidate)
        {
            int forceId = candidate.Force.PhysicalSlot!.Value;
            var missing = new Dictionary<string, string>();
            T? Value<T>(string field, Func<T?> read) where T : struct
            {
                try { T? value = read(); if (!value.HasValue) missing[field] = "new-game.force." + field + ".owner"; return value; }
                catch (InvalidDataException error) { missing[field] = error.Message; return null; }
            }
            string? Text(string field, Func<string?> read)
            {
                try { string? value = read(); if (value is null) missing[field] = "new-game.force." + field + ".owner"; return value; }
                catch (InvalidDataException error) { missing[field] = error.Message; return null; }
            }
            bool? valid = Value<bool>(nameof(NewGameForcePreview.ForceValid), () => current.ReadForceNativeValidity(candidate.Force));
            if (candidate.RulerValid is null) missing[nameof(NewGameForcePreview.RulerValid)] = "new-game.force.ruler-validity-owner";
            int? p = candidate.Ruler.PhysicalSlot;
            int? rulerId = Value<int>(nameof(NewGameForcePreview.RulerId), () => p is int person ? current.ReadPersonIdentity(person) : null);
            int? rulerForce = Value<int>(nameof(NewGameForcePreview.RulerForceId), () => p is int person ? current.ReadPersonForce(person)?.PhysicalSlot : null);
            string? rulerName = candidate.RulerValid == false ? "" : Text(nameof(NewGameForcePreview.RulerName), () => rulerId is int id ? state.Officers.GetValueOrDefault(id)?.Name : null);
            int? portrait = candidate.RulerValid == false ? null : Value<int>(nameof(NewGameForcePreview.PortraitId), () => p is int person ? current.ReadPersonPortraitAppearance(person) : null);
            int? color = Value<int>(nameof(NewGameForcePreview.ReportColorIndex), () => current.ReadForceReportColor(candidate.Force));
            DetailForceAppearance? appearance = null;
            try { if (valid is bool known) appearance = ReadDetailForceAppearance(state, current, names, candidate.Force, known, palettes); }
            catch (InvalidDataException error) { missing[nameof(NewGameForcePreview.FlagAppearance)] = error.Message; }
            if (appearance is null) missing.TryAdd(nameof(NewGameForcePreview.FlagAppearance), "new-game.force.flag-owner");
            string? displayName = Text(nameof(NewGameForcePreview.ForceDisplayName), () => names.Resolve(current, candidate.Force).NativeShortName);
            int? rank = Value<int>(nameof(NewGameForcePreview.RankId), () => current.ReadMoraleWaveForceRank47(forceId));
            string? rankName = Text(nameof(NewGameForcePreview.RankName), () => rank is int r
                ? r is 0 or > 12 ? "" : state.ForceRanks.GetValueOrDefault(r)?.Name : null);
            int? doctrine = Value<int>(nameof(NewGameForcePreview.DoctrineId), () => rulerId is int id && state.Officers.TryGetValue(id, out var person)
                ? OfficerDoctrineRules.ReadDoctrineId(person.DoctrineId, person.AbilityState) : null);
            string? doctrineName = Text(nameof(NewGameForcePreview.DoctrineName), () => doctrine is int d
                ? d is 0 or > 6 ? "" : state.Doctrines.GetValueOrDefault(d)?.Name : null);
            int? cityCount = Value<int>(nameof(NewGameForcePreview.CityCount), () => PersonArmyCaptureSource.ReadOwnedCityCount(current, candidate.Force));
            int? officers = Value<int>(nameof(NewGameForcePreview.ActiveOfficerCount), () => ActiveOfficers(forceId));
            int? gold = Value<int>(nameof(NewGameForcePreview.Gold), () => Total(forceId, BaseResourceKind.Gold));
            int? food = Value<int>(nameof(NewGameForcePreview.Food), () => Total(forceId, BaseResourceKind.Supplies));
            int? troops = Value<int>(nameof(NewGameForcePreview.Troops), () => Total(forceId, BaseResourceKind.Troops));
            string? annotation = Text(nameof(NewGameForcePreview.ScenarioAnnotation), () => Annotation());
            string? heaven = Text(nameof(NewGameForcePreview.HeavenTiming), () => Heaven(forceId));
            IReadOnlyList<NewGameForceGeographicAdvantage>? geographical = null;
            try { geographical = Geography(forceId); }
            catch (InvalidDataException error) { missing[nameof(NewGameForcePreview.GeographicAdvantages)] = error.Message; }
            if (geographical is null) missing.TryAdd(nameof(NewGameForcePreview.GeographicAdvantageCount), "new-game.force.geographic-owner");
            IReadOnlyList<NewGameForceBaseMarker>? ownBases = basesKnown && bases.All(x => x.Marker.ForceId.HasValue && (x.Marker.ForceId != forceId || x.Valid.HasValue))
                ? Array.AsReadOnly(bases.Where(x => x.Marker.ForceId == forceId && x.Valid == true).Select(x => x.Marker).ToArray()) : null;
            if (ownBases is null) missing[nameof(NewGameForcePreview.Bases)] = "new-game.force.current-Base-chain-owner";
            return new(forceId) { ForceValid = valid, RulerId = rulerId, RulerForceId = rulerForce, RulerValid = candidate.RulerValid, RulerName = rulerName,
                PortraitId = portrait, ReportColorIndex = color, FlagAppearance = appearance, ForceDisplayName = displayName,
                RankId = rank, RankName = rankName, DoctrineId = doctrine, DoctrineName = doctrineName, CityCount = cityCount,
                ActiveOfficerCount = officers, Gold = gold, Food = food, Troops = troops, ScenarioAnnotation = annotation,
                HeavenTiming = heaven, GeographicAdvantages = geographical, GeographicAdvantageCount = geographical?.Count,
                Bases = ownBases, UnresolvedInputs = new ReadOnlyDictionary<string, string>(missing) };
        }
        private int? ActiveOfficers(int force)
        {
            if (force is < 1 or > 51) return 0;
            if (!peopleKnown || people.Any(x => x.Owner is null)) return null;
            int count = 0;
            foreach (var person in people.Where(x => x.Owner == force))
            { if (person.Status is not byte status) return null; if (status is >= 1 and <= 4) count = unchecked(count + 1); }
            return count;
        }
        private int? Total(int force, BaseResourceKind kind)
        {
            if (force is < 1 or > 51) return 0; //ForceFootholdIterator3347B0.
            if (!basesKnown || bases.Any(x => x.Owner is null)) return null;
            int value = 0;
            foreach (var target in bases.Where(x => x.Owner == force))
            {
                if (target.Valid is not bool valid) return null;
                if (!valid) continue;
                if (kind != BaseResourceKind.Troops)
                { if (target.ObjectLink is not ushort link) return null; if (link >= 52) continue; }
                int? quantity = kind switch { BaseResourceKind.Gold => target.Gold, BaseResourceKind.Supplies => target.Food, _ => target.Troops };
                if (quantity is not int amount) return null;
                value = unchecked(value + amount);
            }
            return value;
        }
        private string? Annotation()
        {
            uint? flags = OwnedArmyReturnServiceInputs.ReadCurrent(state);
            if (flags is null) throw Missing("ScenarioAnnotation.World16A8");
            if ((flags.Value & 0x40000) != 0) return "";
            //30C440's normal Data0 consumer is known, but its8x16-byte
            //range table is absent from existing caches. No guessed row/index.
            throw Missing("ScenarioAnnotation.range-table-start-end-base-flag");
        }
        private string? Heaven(int force)
        {
            bool task = application.TaskScenePresent ?? throw Missing("HeavenTiming.application-TaskScene");
            if (task && (application.TaskIndex ?? throw Missing("HeavenTiming.TaskScene-index")) != -1)
                return DataText(0x353);
            int mode = current.ReadScenarioMode() ?? throw Missing("HeavenTiming.World44");
            if (mode == 3 && (application.CccPersonSelectStatePresent ?? throw Missing("HeavenTiming.application-CCC-state")))
                return DataText(0x353);
            bool valid = current.ReadForceNativeValidity(new(force)) ?? throw Missing("HeavenTiming.Force-validity");
            if (!valid) return "";
            if (force is >= 46 and <= 50) return DataText(0x353);
            byte? flags = state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true
                ? reset[0x12] : state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags12;
            if (flags is not byte value) throw Missing("HeavenTiming.current-Force12");
            return (value & 8) != 0 ? DataText(0x338) : (value & 0x10) != 0 ? DataText(0x339) : "";
        }
        private string? DataText(int index) => dataText is { } rows && (uint)index < rows.Count ? rows[index].Text : null;
        private IReadOnlyList<NewGameForceGeographicAdvantage>? Geography(int force)
        {
            if (!provinceCitiesKnown) return null;
            var result = new List<NewGameForceGeographicAdvantage>();
            for (int slot = 0; slot < 23; slot++)
            {
                var province = current.ReadStaticTableReference(0xDC80, slot) ?? throw Missing("geographic-State-pointer");
                if (province.PhysicalSlot is not int p) continue;
                bool valid = current.ReadConditionStateValidity(new(p)) ?? throw Missing("geographic-State-validity");
                if (!valid) continue;
                int anchor = current.ReadStaticTableReference(0xDC80, 0)?.PhysicalSlot ?? throw Missing("geographic-State-anchor");
                int identity = p - anchor; //22E1C0, not State30.
                if (identity >= 18) continue;
                int raw = (preview ? owner._growthQueries.Definitions.Catalog.States[p].RawAdvantage30 : owner._growthQueries.ReadStateAdvantage(state, p)) ?? throw Missing("geographic-State30");
                var advantage = current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0) ?? throw Missing("geographic-Advantage-pointer");
                int physical = advantage.PhysicalSlot ?? throw Missing("geographic-Advantage-receiver");
                int required = GeographicRequirement(physical);
                int count = provinceCities.GetValueOrDefault((force, identity));
                if (count < required) continue;
                string name = S14ForceNameDefinitionReader.ReadLiteralUnits(owner._growthQueries.Definitions.Catalog.States[p].RawBytes.AsSpan(24, 6));
                string advantageName = S14ForceNameDefinitionReader.ReadLiteralUnits(owner._growthQueries.Definitions.Catalog.Advantages[physical].RawBytes.AsSpan(0, 14));
                result.Add(new(identity, name, physical, advantageName, required, count));
            }
            return result.AsReadOnly();
        }
        private int GeographicRequirement(int advantage)
        {
            //23B780's normal read appends encoded B3 as the first of5
            //entries;521110 decodes it with the same physical identity.
            //The existing saved growth owner retains that decoded threshold,
            //not a claim that the vector is empty or raw executable bytes.
            return (preview ? owner._growthQueries.Definitions.Catalog.Advantages[advantage].RequiredCitiesB3
                : owner._growthQueries.ReadAdvantageRequirement(state, advantage))
                ?? throw Missing("geographic-current-decoded-threshold");
        }
        private static InvalidDataException Missing(string input) => new("new-game.force." + input);
    }
}
