using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ForceInformationCurrent
    {
        private readonly Dictionary<int, ForceDetailEconomyPublication> economies = [];
        private readonly Dictionary<int, CityDomesticPersonForceName?> forceNames = [];
        private Dictionary<int, int>? cityCounts, districtCounts, displayedPersonCounts, sortingPersonCounts;

        private PreparedCell BasicCell(int forceId, int column) => column switch
        {
            0 => BasicForceNameCell(forceId),
            1 => MakeCell(() => Country(forceId).NativeValidity ? Country(forceId).ShortName : "",
                () => new NameKey(Country(forceId).Text18, !Country(forceId).NativeValidity)),
            2 => MakeCell(() => Rank(forceId) is int rank && rank != 0 ? rank == 9 ? Data(1, 0x53F)
                    : (rules._scenario.Snapshot.RankParameters ?? throw Missing("Rank_catalog")).Ranks[rank].Name : "",
                () => new NumberKey(Rank(forceId), Rank(forceId) == 0)),
            3 or 4 or 5 or 10 or >= 17 and <= 20 => EconomyCell(forceId, column),
            6 => MakeCell(() => Number(Count(forceId, 6, false)), () => new NumberKey(Count(forceId, 6, true))),
            7 => MakeCell(() => Number(Count(forceId, 7, false)), () => new NumberKey(Count(forceId, 7, true))),
            8 => MakeCell(() => EmperorHere(forceId) ? Data(1, 0x338) : "",
                () => new NumberKey(1, !EmperorHere(forceId))),
            9 => MakeCell(() => Number(Count(forceId, 9, false)), () => new NumberKey(Count(forceId, 9, true))),
            11 => MakeCell(() => DefaultFormation(forceId) is int formation && formation != 0
                    ? rules._skills.Formations.Formations[formation].Name : "",
                () => new NumberKey(DefaultFormation(forceId), DefaultFormation(forceId) == 0)),
            _ => throw Missing("basic.column." + column)
        };

        private PreparedCell EconomyCell(int forceId, int column)
        {
            int node = column switch { 3 => 69, 4 => 90, 5 => 111, 10 => 121,
                17 => 74, 18 => 95, 19 => 79, 20 => 100, _ => throw Missing("economy.column") };
            bool empty = Special(forceId) && column is not (5 or 10);
            ForceDetailEconomyPublication Read()
            {
                if (!economies.TryGetValue(forceId, out var value))
                    economies.Add(forceId, value = rules.ReadForceDetailEconomyCurrent(state, Current, forceId));
                return value;
            }
            return MakeCell(() =>
            {
                var value = Read();
                if (value.UnresolvedFields.TryGetValue(node, out var error)) throw new InvalidDataException(error);
                return value.Text.GetValueOrDefault(node) ?? throw Missing("economy.text." + node);
            }, () =>
            {
                if (empty) return new NumberKey(int.MaxValue, true);
                var value = Read();
                if (value.UnresolvedFields.TryGetValue(node, out var error)) throw new InvalidDataException(error);
                return new NumberKey(value.NumericValues.TryGetValue(node, out int number)
                    ? number : throw Missing("economy.number." + node));
            });
        }

        private int Person(int raw) => Need((raw <= 6000 ? Current.ReadPersonLookupReference(raw)
            : Current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_pointer");
        private int Ruler(int forceId) => Person(Need(Current.ReadForceRecord(forceId)?.RawRuler10, "Force_ruler10"));
        private string PersonDisplay(int physical)
        {
            int identity = Need(Current.ReadPersonIdentity(physical), "Person_identity");
            if (state.Officers.TryGetValue(identity, out var person)) return person.Name;
            if (physical == 0 && rules._scenario.Snapshot.Characters is { Count: > 0 } rows && rows[0].Slot == 0)
                return rows[0].Name;
            throw Missing("Person_current_name12_20");
        }
        private string PersonSort(int physical, bool surnameOnly = false)
        {
            int identity = Need(Current.ReadPersonIdentity(physical), "sort_Person_identity");
            if (state.Officers.TryGetValue(identity, out var person))
                return (surnameOnly ? person.CurrentSurnameSortKey : person.CurrentNameSortKey)
                    ?? throw Missing("Person_current_sort_text");
            if (physical == 0 && rules._scenario.Snapshot.Characters is { Count: > 0 } rows && rows[0].Slot == 0)
                return (surnameOnly ? rows[0].SurnameSortKey : rows[0].NameSortKey)
                    ?? throw Missing("Person0_sort_text");
            throw Missing("Person_current_sort_owner");
        }
        private SortKey ForceSort(int id)
        {
            //230FB0 first checks Force validity, then uses the actual5014
            //placeholder for Force0. The returned empty text is tag1, not2.
            if (!Valid(id)) return new NameKey("");
            int person = id == 0 ? Person(5014) : Ruler(id);
            if (!Need(Current.ReadPersonNativeValidity(person), "sort_ruler_validity")) return new NameKey("");
            int identity = Need(Current.ReadPersonIdentity(person), "sort_ruler_identity");
            bool surname = Special(id) && identity is not (>= 5101 and <= 5200) && identity is not (>= 2062 and <= 2066);
            return new NameKey(PersonSort(person, surname));
        }
        private PreparedCell BasicForceNameCell(int id)
        {
            CityDomesticPersonForceName? Name()
            {
                if (forceNames.TryGetValue(id, out var known)) return known;
                if (!Need(Current.ReadPersonNativeValidity(Ruler(id)), "display_ruler_validity")) return null;
                if (!Valid(id)) { forceNames[id] = null; return null; }
                string body = Special(id) ? rules.UnitDetailBaseName(state, Current, BaseKind.City, id + 1) : PersonDisplay(Ruler(id));
                byte colour = Need(Current.ReadForceReportColor(Force(id)), "Force49_colour");
                var result = new CityDomesticPersonForceName(Data(1, 0x571) + body, MapSpritePalettes.ForceFill(colour));
                forceNames[id] = result; return result;
            }
            var cell = MakeCell(() => Need(Current.ReadPersonNativeValidity(Ruler(id)), "display_ruler_validity")
                ? Name()?.Text ?? "" : "NO DATA", () => ForceSort(id));
            try
            {
                bool rulerValid = Need(Current.ReadPersonNativeValidity(Ruler(id)), "marker_ruler_validity");
                return cell with { Display = cell.Display with { MarkerColour = rulerValid ? Name()?.MarkerColour : null } };
            }
            catch (InvalidDataException error)
            { return cell with { Display = cell.Display with { UnresolvedOwner = cell.Display.UnresolvedOwner ?? error.Message } }; }
        }

        private Formats.S14ForceNameDefinition Country(int id)
        {
            int raw = Need(Current.ReadForceKingdomNameIndex(id), "Force48_country");
            return rules.ReadEmpireNameResourcesCurrent().CustomNames.Definitions[raw <= 100 ? raw : 0];
        }
        private int Rank(int id)
        {
            int raw = Need(Current.ReadMoraleWaveForceRank47(id), "Force47_rank");
            return raw <= 12 ? raw : 0;
        }
        private int DefaultFormation(int id)
        {
            string key = BattleRemakeCurrentForcePolicies.ForceFieldKey(id, 0x15E);
            if (state.NativeArmySimulation?.Parameters is not { } values || values.Float32Values.ContainsKey(key) ||
                !values.Int32Values.TryGetValue(key, out int raw) || raw is < 0 or > 255)
                throw Missing("Force15E_formation");
            return raw <= 20 ? raw : 0;
        }
        private bool EmperorHere(int id)
        {
            int person = Person(Need(Current.ReadEmperorId(), "World1654_emperor"));
            int raw = Need(Current.ReadPersonNativeValidity(person), "emperor_validity")
                ? Need(Current.ReadPersonFirstLocation(person), "emperor_HomeE2") : 0;
            if (raw is >= 52 and <= 61) return false;
            int cityId = raw is >= 1 and <= 51 ? raw : 0;
            var city = Current.ReadCityTableReference(cityId) ?? throw Missing("emperor_City_pointer");
            if(Need(Current.ReadBaseForceSlot(city), "emperor_City_Force")!=id)return false;
            //3481A0 suppresses the matched Emperor only in the real title
            //selector, for actual Force47 ranks1/10. Live readers keep false.
            return !titleForcePicker||Rank(id) is not (1 or 10);
        }

        private int Count(int id, int column, bool sort)
        {
            if (column == 6)
            {
                if (cityCounts is null)
                {
                    cityCounts = [];
                    for (int physical = 0; physical < 52; physical++)
                    {
                        var city = Current.ReadCityTableReference(physical) ?? throw Missing("count_City_pointer");
                        int owner = Need(Current.ReadBaseForceSlot(city), "count_City_Force");
                        cityCounts[owner] = cityCounts.GetValueOrDefault(owner) + 1;
                    }
                }
                return cityCounts.GetValueOrDefault(id);
            }
            if (column == 9)
            {
                if (districtCounts is null)
                {
                    districtCounts = [];
                    for (int physical = 0; physical < 52; physical++)
                    {
                        int owner = Need(Current.ReadDistrictForceId(physical), "count_District10");
                        districtCounts[owner] = districtCounts.GetValueOrDefault(owner) + 1;
                    }
                }
                return districtCounts.GetValueOrDefault(id);
            }
            if (sortingPersonCounts is null || displayedPersonCounts is null)
            {
                var shown = new Dictionary<int, int>(); var ordered = new Dictionary<int, int>();
                for (int slot = 0; slot < rules._scenario.Snapshot.Characters.Count; slot++)
                {
                    int physical = Need(Current.ReadTransferPersonTableReference(slot)?.PhysicalSlot, "count_Person_pointer");
                    int rawDistrict = Need(Current.ReadPersonDistrictSlot(physical), "count_PersonE0");
                    var district = Current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0) ?? throw Missing("count_District_pointer");
                    int owner = Need(Current.ReadDistrictForceId(Need(district.PhysicalSlot, "count_District_receiver")), "count_District10");
                    //34D240 sorts all physical rows;35F880's display alone adds
                    //native-validity plus232530/232500 exclusion.
                    ordered[owner] = ordered.GetValueOrDefault(owner) + 1;
                    if (!Need(Current.ReadPersonNativeValidity(physical), "count_Person_validity") || owner == 51) continue;
                    int home = Need(Current.ReadPersonFirstLocation(physical), "count_PersonE2");
                    if (Special(owner) && home is not (>= 1 and <= 61)) continue;
                    shown[owner] = shown.GetValueOrDefault(owner) + 1;
                }
                sortingPersonCounts = ordered; displayedPersonCounts = shown;
            }
            return (sort ? sortingPersonCounts : displayedPersonCounts).GetValueOrDefault(id);
        }
    }
}
