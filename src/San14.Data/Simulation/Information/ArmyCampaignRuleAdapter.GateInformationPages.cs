using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public GateInformationPageSet PrepareGateInformationPages(GameState state, int playerForceId,
        IReadOnlyList<int> actualGateIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actualGateIds);
        return new GateInformationCurrent(this, state, playerForceId).Pages(actualGateIds);
    }

    public AreaAdministrationQuery<GateInformationPageSet> PrepareAllGateInformationPages(
        GameState state, int playerForceId, bool ownForceOnly)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var source = new GateInformationCurrent(this, state, playerForceId);
            return AreaAdministrationQuery<GateInformationPageSet>.Known(source.Pages(source.OrdinaryCandidates(ownForceOnly)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<GateInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class GateInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly Lazy<SourceWorldContext> captured;
        private SourceWorldContext Current => captured.Value;
        private readonly Dictionary<int, Gate> gates = [];
        private readonly Dictionary<(int GateId, int Column), PreparedCell> prepared = [];

        internal GateInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, int playerForceId)
        {
            this.rules = rules; this.state = state; this.playerForceId = playerForceId;
            captured = new(() =>
            {
                var current = rules.CaptureOwnedArmyReturnWorld(state).Context;
                BaseReturnCaptivesInputs.BindCurrent(state, current);
                return current;
            });
        }

        internal GateInformationPageSet Pages(IReadOnlyList<int> ids)
        {
            var groups = GateInformationColumnsSource.Groups.Select((group, index) =>
                new GateInformationGroup(index, group.CaptionId, Data(1, group.CaptionId),
                    group.ColumnIds.Select(id =>
                    {
                        var row = GateInformationColumnsSource.Rows[id];
                        int format = (row.Flags & 1) != 0 ? 3 : 4;
                        if ((row.Flags & 8) != 0) format = 4;
                        if ((row.Flags & 2) != 0) format = 5;
                        return new GateInformationColumn(id, row.CaptionId, Data(1, row.CaptionId), row.Width,
                            format, (row.Flags & 0x20000000) == 0, true, row.Visible);
                    }).ToImmutableArray())).ToImmutableArray();
            return new(Data(1, 0x5F9), groups, ids,
                (id, column) => Cell(id, column).Display with { SortUnresolvedOwner = Cell(id, column).SortUnresolvedOwner },
                (id, column) => Cell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Cell(left, column).Key ?? throw Missing("sort_key"))
                    .Before(Cell(right, column).Key ?? throw Missing("sort_key"), ascending));
        }

        internal int[] OrdinaryCandidates(bool own)
        {
            int player = own ? Need(OwnedCurrentPlayerForce.ReadCurrentReference(state, Current)?.PhysicalSlot,
                "candidate_player_force") : -1;
            var ids = new List<int>();
            //8E84B0 case6 uses the eleven fixed Gate pointers. Own compares
            //the actual map-derived Force receiver, not Gate's raw header.
            for (int lookup = 0; lookup < 11; lookup++)
            {
                var receiver = Current.ReadGateTableReference(lookup) ?? throw Missing("candidate_Gate_lookup");
                if (!Need(Current.ReadBaseNativeValidity(receiver), "candidate_Gate_validity")) continue;
                if (own && Need(Force(receiver).PhysicalSlot, "candidate_Force_receiver") != player) continue;
                int physical = Need(receiver.PhysicalSlot, "candidate_Gate_receiver");
                if (physical is < 1 or > 10) throw Missing("candidate_Gate_domain_identity");
                int id = physical + 51;
                var owner = state.Gates.GetValueOrDefault(id) ?? throw Missing("candidate_Gate_owner");
                gates[id] = new(id, physical, receiver, owner); ids.Add(id);
            }
            return ids.ToArray();
        }

        private Gate ReadGate(int id)
        {
            if (gates.TryGetValue(id, out var saved)) return saved;
            if (id is < 52 or > 61) throw Missing("Gate_identity");
            var receiver = Current.ReadGateTableReference(id - 51) ?? throw Missing("Gate_lookup");
            int physical = Need(receiver.PhysicalSlot, "Gate_receiver");
            var owner = state.Gates.GetValueOrDefault(physical + 51) ?? throw Missing("Gate_owner");
            return gates[id] = new(id, physical, receiver, owner);
        }

        private PreparedCell Cell(int id, int column)
        {
            if (prepared.TryGetValue((id, column), out var saved)) return saved;
            try
            {
                var gate = ReadGate(id);
                saved = BasicCell(gate, column);
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
        private ForceReference Force(BaseReference gate)
        {
            int raw = Need(Current.ReadBaseForceSlot(gate), "Gate_Force");
            return Current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("Force_lookup");
        }
        private string Data(int section, int id) => rules.ReadHistoryDataText(section, id);
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("gate-information-list." + field);
        private sealed record Gate(int Id,int PhysicalSlot,BaseReference Receiver,GateState Owner);
        private sealed record PreparedCell(GateInformationCell Display,SortKey? Key,string? SortUnresolvedOwner);
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
