using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public CityInformationPageSet PrepareCityInformationPages(GameState state, int playerForceId,
        IReadOnlyList<int> actualCityIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actualCityIds);
        return new CityInformationCurrent(this, state, playerForceId).Pages(actualCityIds);
    }

    public AreaAdministrationQuery<CityInformationPageSet> PrepareAllCityInformationPages(
        GameState state, int playerForceId, bool ownForceOnly)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var source = new CityInformationCurrent(this, state, playerForceId);
            return AreaAdministrationQuery<CityInformationPageSet>.Known(source.Pages(source.OrdinaryCandidates(ownForceOnly)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CityInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class CityInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly Lazy<SourceWorldContext> captured;
        private SourceWorldContext Current => captured.Value;
        private readonly Dictionary<int, City> cities = [];
        private readonly Dictionary<(int CityId, int Column), PreparedCell> prepared = [];

        internal CityInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, int playerForceId)
        {
            this.rules = rules; this.state = state; this.playerForceId = playerForceId;
            captured = new(() =>
            {
                var current = rules.CaptureOwnedArmyReturnWorld(state).Context;
                BaseReturnCaptivesInputs.BindCurrent(state, current);
                return current;
            });
        }

        internal CityInformationPageSet Pages(IReadOnlyList<int> ids)
        {
            var groups = CityInformationColumnsSource.Groups.Select((group, index) =>
                new CityInformationGroup(index, group.CaptionId, Data(1, group.CaptionId),
                    group.ColumnIds.Select(id =>
                    {
                        var row = CityInformationColumnsSource.Rows[id];
                        int format = (row.Flags & 1) != 0 ? 3 : 4;
                        if ((row.Flags & 8) != 0) format = 4;
                        if ((row.Flags & 2) != 0) format = 5;
                        return new CityInformationColumn(id, row.CaptionId, Data(1, row.CaptionId), row.Width,
                            format, (row.Flags & 0x20000000) == 0, true, row.Visible);
                    }).ToImmutableArray())).ToImmutableArray();
            return new(Data(1, 0x5F3), groups, ids,
                (id, column) => Cell(id, column).Display with { SortUnresolvedOwner = Cell(id, column).SortUnresolvedOwner },
                (id, column) => Cell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Cell(left, column).Key ?? throw Missing("sort_key"))
                    .Before(Cell(right, column).Key ?? throw Missing("sort_key"), ascending));
        }

        internal int[] OrdinaryCandidates(bool own)
        {
            int player = Need(Current.ReadForceTableReference(playerForceId is >= 0 and < 52 ? playerForceId : 0)?.PhysicalSlot,
                "candidate_player_force");
            var ids = new List<int>();
            //8E84B0 scans52 City pointers, not dictionary order. The actual
            //empty selected-membership default sort keeps this caller order.
            for (int lookup = 0; lookup < 52; lookup++)
            {
                var receiver = Current.ReadCityTableReference(lookup) ?? throw Missing("candidate_City_lookup");
                if (!Need(Current.ReadBaseNativeValidity(receiver), "candidate_City_validity")) continue;
                if (own && Need(Force(receiver).PhysicalSlot, "candidate_Force_receiver") != player) continue;
                int physical = Need(receiver.PhysicalSlot, "candidate_City_receiver");
                var owner = state.Cities.GetValueOrDefault(physical) ?? throw Missing("candidate_City_owner");
                cities[physical] = new(physical, physical, receiver, owner); ids.Add(physical);
            }
            return ids.ToArray();
        }

        private City ReadCity(int id)
        {
            if (cities.TryGetValue(id, out var saved)) return saved;
            if (id is < 0 or > 51) throw Missing("City_index");
            var receiver = Current.ReadCityTableReference(id) ?? throw Missing("City_lookup");
            int physical = Need(receiver.PhysicalSlot, "City_receiver");
            var owner = state.Cities.GetValueOrDefault(physical) ?? throw Missing("City_owner");
            return cities[id] = new(id, physical, receiver, owner);
        }

        private PreparedCell Cell(int id, int column)
        {
            if (prepared.TryGetValue((id, column), out var saved)) return saved;
            try
            {
                var city = ReadCity(id);
                saved = column switch
                {
                    >= 19 and <= 21 or >= 24 and <= 29 => EconomyCell(city, column),
                    >= 30 and <= 32 => GeographyCell(city, column),
                    _ => BasicCell(city, column)
                };
            }
            catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
            return prepared[(id, column)] = saved;
        }

        private PreparedCell MakeCell(Func<string?> display, Func<SortKey>? sort = null)
        {
            string? text = null, issue = null, sortIssue = null; SortKey? key = null;
            try { text = display() ?? ""; } catch (InvalidDataException error) { issue = error.Message; }
            if (sort is not null)
                try { key = sort(); } catch (InvalidDataException error) { sortIssue = error.Message; }
            return new(new(text, null, issue), key, sortIssue);
        }
        private ForceReference Force(BaseReference city)
        {
            int raw = Need(Current.ReadBaseForceSlot(city), "City_Force");
            return Current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("Force_lookup");
        }
        private bool Special(City city) => Need(Force(city.Receiver).PhysicalSlot, "Force_receiver") is >= 46 and <= 50;
        private string Data(int section, int id) => rules.ReadHistoryDataText(section, id);
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("city-information-list." + field);
        private sealed record City(int Id,int PhysicalSlot,BaseReference Receiver,CityState Owner);
        private sealed record PreparedCell(CityInformationCell Display,SortKey? Key,string? SortUnresolvedOwner);
        private abstract record SortKey { internal abstract bool Before(SortKey other,bool ascending); }
        private sealed record NumberKey(long Value,bool Empty=false) : SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right = (NumberKey)other;
                if (Empty != right.Empty) return ascending ? !Empty : Empty;
                return !Empty && (ascending ? Value < right.Value : Value > right.Value);
            }
        }
        private sealed record NameKey(string Value,bool Empty=false) : SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right = (NameKey)other;
                if (Empty != right.Empty) return ascending ? !Empty : Empty;
                return !Empty && (ascending ? PersonNameOrder.Compare(Value,right.Value) < 0 : PersonNameOrder.Compare(right.Value,Value) < 0);
            }
        }
    }
}
