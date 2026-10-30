using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnMonthlyDisastersCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnDisastersSource(this, state, current, pool).Run();

    //429420 owns this complete operation. The phase and mode are local to
    //2978D0; only Area flags, cooldowns and security survive the call.
    private sealed partial class NormalTurnDisastersSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            if (package.Seed.Language is not ("TC" or "SC")) throw Missing("locale_rules");
            AreaFlagsSourceAdapter.BindMutation(current, () => state, value => state = value);
            package._personAssignmentPrecallInputs.BindAreaAdministrationScalars(state, current);
            package._baseCapacityInputs.BindCurrent(state, current);
            for (int mode = 0; mode < 2; mode++)
            {
                int phase = ClearSeason(mode) ? 3 : ResolveExisting(mode) ? 2 : 3;
                if (phase == 2) Spread(mode);
                ActivatePending(mode);
                SelectNew(mode);
            }
            SelectHarvest();
            MatureHarvest();
            ClearHarvest();
            return state;
        }

        //29A1B0 clears active flags on the four season boundaries, reports
        //every affected native City in pointer order, then skips phases1/2.
        private bool ClearSeason(int mode)
        {
            if (!SeasonBoundary) return false;
            var cities = new SortedSet<int>();
            foreach (int area in Registry(RegistryKind.Area))
            {
                if ((Flags(area) & Active(mode)) == 0) continue;
                SetFlags(area, (byte)(Flags(area) & ~Active(mode)));
                var city = City(area);
                if (Valid(city)) cities.Add(Physical(city));
            }
            ReportEnded(cities.Select(CityAt).ToArray(), mode);
            return true;
        }

        //297940: all original affected City counts are retained, so only a
        //City whose remaining count reaches zero enters the end report.
        private bool ResolveExisting(int mode)
        {
            var counts = new SortedDictionary<int, int>();
            int remaining = 0;
            foreach (int area in Registry(RegistryKind.Area))
            {
                if ((Flags(area) & Active(mode)) == 0) continue;
                var city = City(area);
                bool valid = Valid(city);
                if (valid) Increment(counts, Physical(city));
                int probability = RecoveryProbability(area, mode);
                if (AreaSample(area, 100, DateSalt) < probability)
                {
                    SetFlags(area, (byte)(Flags(area) & ~Active(mode)));
                    if (Valid(city)) counts[Physical(city)]--;
                }
                else remaining++;
            }
            var ended = Temporary();
            foreach (var pair in counts) if (pair.Value == 0) Add(ended, RegistryKind.City, pair.Key);
            ReportEnded(Values(ended).Select(value => CityAt(value.Physical)).ToArray(), mode);
            Release(ended);
            return remaining != 0;
        }

        //298DE0: neighbors are collected on the real pool even when a later
        //admission rejects the receiver. City ranking is counts descending,
        //then native City ordinal ascending (24BF50).
        private void Spread(int mode)
        {
            var areas = Temporary();
            foreach (int area in Registry(RegistryKind.Area))
            {
                var neighbors = Temporary();
                FillNeighbors(area, neighbors);
                int affected = 0;
                foreach (var neighbor in Values(neighbors)) if ((Flags(neighbor.Physical) & Active(mode)) != 0) affected++;
                if (affected != 0 && !HasEvent(area) && Timer(area, mode) < 6)
                {
                    var city = City(area);
                    bool immune = false;
                    int force = ForceId(city);
                    if (force != 0 && Valid(city) && ForceId(city) != 0) immune = CityHasImmuneOfficer(city);
                    if (!immune && !ForceFlag(city, 8) && !Special(city))
                    {
                        int probability = SpreadProbability(area, mode);
                        if (AreaSample(area, 100, DateSalt) < probability) Add(areas, RegistryKind.Area, area);
                    }
                }
                Release(neighbors);
            }
            int existing = 0;
            foreach (var city in Cities()) if (CityAreas(city).Any(area => (Flags(area) & Active(mode)) != 0)) existing++;
            int limit = Math.Min(P(mode == 0 ? "spread-city-limit" : "second-spread-city-limit") - existing,
                P(mode == 0 ? "city-choice-limit" : "second-city-choice-limit"));
            if (Count(areas) == 0 || limit == 0) { Release(areas); return; }
            var cities = Temporary();
            var counts = new Dictionary<int, int>();
            foreach (var entry in Values(areas))
            {
                var city = City(entry.Physical);
                if (!Valid(city)) continue;
                int id = Physical(city);
                if (!Contains(cities, RegistryKind.City, id)) Add(cities, RegistryKind.City, id);
                Increment(counts, id);
            }
            Sort(cities, (a, b) => counts.GetValueOrDefault(a) != counts.GetValueOrDefault(b)
                ? counts.GetValueOrDefault(a) > counts.GetValueOrDefault(b) : a < b);
            var selected = Temporary();
            bool playerAffected = false;
            //The native do/while reaches its first City even for a negative
            //remaining limit. Preserve that source order rather than Take().
            int visited = 0;
            foreach (var entry in Values(cities))
            {
                var city = CityAt(entry.Physical);
                if (!CityAreas(city).Any(area => (Flags(area) & Active(mode)) != 0))
                {
                    Add(selected, RegistryKind.City, entry.Physical);
                    if (Player(Force(city))) playerAffected = true;
                }
                if (++visited >= limit) break;
            }
            if (Count(selected) != 0)
            {
                foreach (var entry in Values(areas))
                {
                    int area = entry.Physical;
                    if (!Contains(selected, RegistryKind.City, Physical(City(area)))) continue;
                    SetFlags(area, (byte)(Flags(area) | Active(mode)));
                    SetTimer(area, mode, 12);
                }
                var locations = Values(selected).Select(value => CityAt(value.Physical)).ToArray();
                if (playerAffected) Show(locations, (mode == 0 ? 0x5D9A : 0x5DA8) + locations.Length - 1, mode == 0 ? 16 : 15, 7);
                History(locations, (mode == 0 ? 0x5080 : 0x508D) + locations.Length - 1);
            }
            Release(selected); Release(cities); Release(areas);
        }

        //29A8F0: clear every pending flag before qualification. The count
        //tree and real candidate lists are independent of allocation success.
        private void ActivatePending(int mode)
        {
            if (!SeasonBoundary) return;
            var counts = new SortedDictionary<int, int>();
            var areas = Temporary();
            foreach (int area in Registry(RegistryKind.Area))
            {
                if ((Flags(area) & Pending(mode)) == 0) continue;
                SetFlags(area, (byte)(Flags(area) & ~Pending(mode)));
                var city = City(area);
                if (!Valid(city) || ForceId(city) != 0 && BaseTrait(city, 0x43) != 0 ||
                    ForceFlag(city, 8) || Special(city)) continue;
                Increment(counts, Physical(city));
                Add(areas, RegistryKind.Area, area);
            }
            if (counts.Count == 0) { Release(areas); return; }
            var cities = Temporary();
            foreach (int city in counts.Keys) Add(cities, RegistryKind.City, city);
            Shuffle(cities);
            var order = Values(cities).Select((value, index) => (value.Physical, index)).ToDictionary(value => value.Physical, value => value.index);
            Sort(cities, (a, b) => counts.GetValueOrDefault(a) != counts.GetValueOrDefault(b)
                ? counts.GetValueOrDefault(a) > counts.GetValueOrDefault(b) : order[a] < order[b]);
            var selected = Values(cities).Take(4).Select(value => CityAt(value.Physical)).ToArray();
            if (Count(cities) != 0)
            {
                //The actual presentation enter precedes the numerical stores.
                Sound(7); state = package.AppendNormalTurnDisasterEventCurrent(state, mode == 0 ? 16 : 15);
                foreach (var city in selected)
                {
                    int cityOrdinal = Physical(city); //22DC80, independently of Base4E.
                    foreach (var entry in Values(areas))
                    {
                        int area = entry.Physical;
                        if (N(current.ReadAreaCitySlot(new(area)), "active_Area35") != cityOrdinal) continue;
                        SetFlags(area, (byte)(Flags(area) | Active(mode)));
                        SetTimer(area, mode, 12);
                        int after = Math.Clamp(unchecked(Security(area) - Sample(10,
                            N(current.ReadAreaCitySlot(new(area)), "security_Area35"), mode) - 5), 0, 100);
                        int force = N(current.ReadAreaCurrentForceSlot(new(area)), "security_Force");
                        if (force is < 46 or > 50) SetSecurity(area, after);
                    }
                }
                Narration(Format((mode == 0 ? 0x5DA0 : 0x5DAE) + selected.Length - 1, Operands(selected, false)));
                state = package.AppendNormalTurnDisasterEventCurrent(state, null);
            }
            //Current count is re-read after all actual stores/presentation.
            selected = Values(cities).Take(4).Select(value => CityAt(value.Physical)).ToArray();
            History(selected, (mode == 0 ? 0x5077 : 0x5084) + selected.Length - 1);
            Release(cities); Release(areas);
        }

        //29B5C0 creates next season's pending states. It does not apply the
        //subsequent action16's population/development/Army damage.
        private void SelectNew(int mode)
        {
            int selector = Selector();
            if (selector != 0 && Setting(0x84) > 2) return;
            var world = state.CampaignEventWorld ?? throw Missing("World_owner");
            if (unchecked(world.ElapsedPeriodOffset + (int)(state.NativeMapSeason ?? throw Missing("calendar")).AdvancedPeriods) < 37 ||
                !SeasonBoundary || world.ScenarioMode == 2) return;
            var eligible = Temporary();
            foreach (var city in Cities())
            {
                if (CityAreas(city).Any(HasEvent)) continue;
                if (ForceId(city) != 0 && BaseTrait(city, 0x43) != 0 || ForceFlag(city, 8)) continue;
                Add(eligible, RegistryKind.City, Physical(city));
            }
            if (Count(eligible) < 3) { Release(eligible); return; }
            var counts = new Dictionary<int, int>();
            var areas = Temporary(); var cities = Temporary();
            foreach (var entry in Values(eligible))
            {
                var city = CityAt(entry.Physical);
                if (ForceId(city) != 0 && BaseTrait(city, 0x43) != 0) continue;
                int probability = OccurrenceProbability(city, mode);
                foreach (int area in CityAreas(city))
                {
                    if (HasEvent(area) || Timer(area, mode) != 0 || AreaSample(area, 100, DateSalt) >= probability) continue;
                    Add(areas, RegistryKind.Area, area);
                    if (!Contains(cities, RegistryKind.City, entry.Physical)) Add(cities, RegistryKind.City, entry.Physical);
                    Increment(counts, entry.Physical);
                }
            }
            int number = Draw(Math.Min(Count(cities), P(mode == 0 ? "city-choice-limit" : "second-city-choice-limit")));
            if (number != 0)
            {
                Shuffle(cities);
                var order = Values(cities).Select((value, index) => (value.Physical, index)).ToDictionary(value => value.Physical, value => value.index);
                Sort(cities, (a, b) => counts.GetValueOrDefault(a) != counts.GetValueOrDefault(b)
                    ? counts.GetValueOrDefault(a) > counts.GetValueOrDefault(b) : order[a] < order[b]);
                var selected = Temporary();
                int copied = 0;
                foreach (var entry in Values(cities))
                { Add(selected, RegistryKind.City, entry.Physical); if (++copied >= number) break; }
                Shuffle(areas);
                int target = Draw(Math.Min(Count(areas), P(mode == 0 ? "new-area-limit" : "second-new-area-limit"))) + 1;
                int actual = 0;
                foreach (var entry in Values(areas))
                {
                    int area = entry.Physical;
                    if (!Contains(selected, RegistryKind.City, Physical(City(area)))) continue;
                    SetFlags(area, (byte)(Flags(area) | Pending(mode)));
                    if (++actual >= target) break;
                }
                Release(selected);
            }
            Release(cities); Release(areas); Release(eligible);
        }

        private void SelectHarvest()
        {
            int setting = Setting(0x84), selector = Selector();
            if (selector != 0 ? setting > 2 : Advanced() == 0) return;
            if (Date.Month != 4) return;
            var eligible = Temporary();
            foreach (var city in Cities())
                if (Valid(city) && !Special(city) && !ForceFlag(city, 16) && !CityAreas(city).Any(HasEvent))
                    Add(eligible, RegistryKind.City, Physical(city));
            if (Count(eligible) == 0) { Release(eligible); return; }
            var candidates = Temporary(); var preferred = Temporary();
            Shuffle(eligible);
            foreach (var entry in Values(eligible))
            {
                var city = CityAt(entry.Physical);
                bool hasEvent = false; int count = 0, marked = 0;
                foreach (int area in CityAreas(city))
                {
                    count++;
                    if (HasEvent(area)) { hasEvent = true; break; }
                    if (AreaHasMarkedArmy(area)) marked++;
                }
                if (hasEvent || marked * 2 > count || CitySample(city, 100) >= P("harvest-probability")) continue;
                bool immune = Valid(city) && ForceId(city) != 0 && CityHasImmuneOfficer(city);
                if (immune || ForceFlag(city, 8)) Add(preferred, RegistryKind.City, entry.Physical);
                Add(candidates, RegistryKind.City, entry.Physical);
            }
            int total = Count(candidates);
            if (total != 0)
            {
                int offset = total > 2 ? 1 : 0;
                int cap = selector == 0 ? P("harvest-middle-count") : setting == 1 ? P("harvest-middle-count") :
                    setting == 2 ? P("harvest-high-count") : P("harvest-low-count");
                int number = Draw(Math.Min(total, cap) - offset) + offset;
                if (selector == 0) number = Div(unchecked(number * Advanced()), 100);
                number = Math.Clamp(number, 0, P("harvest-high-count"));
                int half = (int)MathF.Ceiling(number * 0.5f), visited = 0;
                foreach (var entry in Values(preferred))
                {
                    visited++;
                    var city = CityAt(entry.Physical);
                    if (CitySample(city, 100) >= P("harvest-preference-probability")) continue;
                    MarkHarvest(city);
                    RemoveAll(candidates, RegistryKind.City, entry.Physical);
                    number--;
                    if (visited >= half) break;
                }
                if (number != 0)
                {
                    int done = 0;
                    foreach (var entry in Values(candidates))
                    { MarkHarvest(CityAt(entry.Physical)); if (++done >= number) break; }
                }
            }
            Release(preferred); Release(candidates); Release(eligible);
        }

        private void MarkHarvest(BaseReference city)
        { foreach (int area in CityAreas(city)) SetFlags(area, (byte)(Flags(area) | 0x20)); }
        private void MatureHarvest()
        {
            if (Date.Month != 7) return;
            var areas = Temporary();
            foreach (int area in Registry(RegistryKind.Area))
                if ((Flags(area) & 0x20) != 0 && Timer(area, 0) < 9 && Timer(area, 1) < 9) Add(areas, RegistryKind.Area, area);
            if (Count(areas) != 0)
            {
                var counts = new SortedDictionary<int, int>();
                foreach (var entry in Values(areas))
                {
                    int area = entry.Physical;
                    var city = City(area);
                    SetFlags(area, (byte)(Flags(area) & ~0x20));
                    if (!Valid(city)) continue;
                    SetFlags(area, (byte)(Flags(area) | 0x10)); Increment(counts, Physical(city));
                }
                if (counts.Count != 0)
                {
                    var cities = counts.Keys.Select(CityAt).ToArray();
                    Sound(20); state = package.AppendNormalTurnDisasterEventCurrent(state, 14);
                    for (int at = 0; at < cities.Length; at += 4)
                    {
                        var group = cities.Skip(at).Take(4).ToArray();
                        Narration(Format(0x5DB4 + group.Length - 1, Operands(group, false)));
                    }
                    state = package.AppendNormalTurnDisasterEventCurrent(state, null);
                    for (int at = 0; at < cities.Length; at += 4)
                    {
                        var group = cities.Skip(at).Take(4).ToArray();
                        History(group, new[] { 0x5091, 0x5094, 0x5095, 0x5096 }[group.Length - 1]);
                    }
                }
            }
            Release(areas);
        }
        private void ClearHarvest()
        {
            if (Date.Month == 10)
                foreach (int area in Registry(RegistryKind.Area)) SetFlags(area, (byte)(Flags(area) & 0xCF));
        }
    }
}
