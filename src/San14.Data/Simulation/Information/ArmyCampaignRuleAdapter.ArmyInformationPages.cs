using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public ArmyInformationPageSet PrepareArmyInformationPages(GameState state, int playerForceId,
        IReadOnlyList<int> actualUnitIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actualUnitIds);
        return new ArmyInformationCurrent(this, state, playerForceId).Pages(actualUnitIds);
    }

    public AreaAdministrationQuery<ArmyInformationPageSet> PrepareAllArmyInformationPages(
        GameState state, int playerForceId, bool ownForceOnly)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var source = new ArmyInformationCurrent(this, state, playerForceId);
            return AreaAdministrationQuery<ArmyInformationPageSet>.Known(source.Pages(source.OrdinaryCandidates(ownForceOnly)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<ArmyInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class ArmyInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly Lazy<(SourceWorldContext Current, TacticRemakeCurrentContext Rules)> captured;
        private SourceWorldContext Current => captured.Value.Current;
        private TacticRemakeCurrentContext Rules => captured.Value.Rules;
        private readonly Dictionary<int, Unit> units = [];
        private readonly Dictionary<(int UnitId, int Column), PreparedCell> prepared = [];

        internal ArmyInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, int playerForceId)
        {
            this.rules = rules; this.state = state; this.playerForceId = playerForceId;
            captured = new(() =>
            {
                var result = rules.CaptureUnitDetailsCurrent(state);
                return (result.Current ?? throw new InvalidDataException(result.Missing ?? Missing("current").Message),
                    result.Rules ?? throw new InvalidDataException(result.Missing ?? Missing("rules").Message));
            });
        }

        internal ArmyInformationPageSet Pages(IReadOnlyList<int> ids)
        {
            var groups = ArmyInformationColumnsSource.Groups.Select((group, index) =>
                new ArmyInformationGroup(index, group.CaptionId, Data(1, group.CaptionId),
                    group.ColumnIds.Select(id =>
                    {
                        var row = ArmyInformationColumnsSource.Rows[id];
                        int format = (row.Flags & 1) != 0 ? 3 : 4;
                        if ((row.Flags & 8) != 0) format = 4;
                        if ((row.Flags & 2) != 0) format = 5;
                        return new ArmyInformationColumn(id, row.CaptionId, Data(1, row.CaptionId), row.Width,
                            format, (row.Flags & 0x20000000) == 0, true, row.Visible);
                    }).ToImmutableArray())).ToImmutableArray();
            return new(Data(1, 0x5F2), groups, ids,
                (id, column) => Cell(id, column).Display with { SortUnresolvedOwner = Cell(id, column).SortUnresolvedOwner },
                (id, column) => Cell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Cell(left, column).Key ?? throw Missing("sort_key"))
                    .Before(Cell(right, column).Key ?? throw Missing("sort_key"), ascending));
        }

        internal int[] OrdinaryCandidates(bool own)
        {
            var arena = state.ReadArmyLifecycle() ?? throw Missing("candidate_lifecycle");
            var slots = arena.ReceiverSlots();
            int player = Need(Current.ReadForceTableReference(playerForceId is >= 0 and < 52 ? playerForceId : 0)?.PhysicalSlot,
                "candidate_player_force");
            var ids = new List<int>();
            //8E84B0 scans the fixed Army pointer table, not the World60 registry.
            for (int physical = 0; physical <= 500; physical++)
            {
                var reference = new ArmyUnitReference(physical);
                if (!Need(Current.ReadArmyUnitNativeValidity(reference), "candidate_validity")) continue;
                int force = Need(Current.ReadArmyUnitForceReference(reference)?.PhysicalSlot, "candidate_force");
                if (own && force != player) continue;
                var slot = slots[physical];
                var owner = slot.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : slot.PendingImage;
                if (owner is null) throw Missing("candidate_logical_unit");
                units[owner.Id] = new(owner.Id, physical, owner); ids.Add(owner.Id);
            }
            //The source initial selected-membership sort ties for an empty
            //selection. Its stable sorter consequently retains this order.
            return ids.ToArray();
        }

        private Unit ReadUnit(int id)
        {
            if (units.TryGetValue(id, out var saved)) return saved;
            var owner = state.Units.GetValueOrDefault(id);
            if (owner is null)
                owner = state.ReadArmyLifecycle()?.ReceiverSlots().FirstOrDefault(slot => slot.PendingImage?.Id == id)?.PendingImage;
            if (owner is null) throw Missing("Unit_owner");
            int physical = Need(owner.NativeArmyOrigin?.NativeSlotIndex, "Army_receiver");
            if (physical is < 0 or > 500) throw Missing("Army_receiver_range");
            return units[id] = new(id, physical, owner);
        }

        private PreparedCell Cell(int id, int column)
        {
            if (prepared.TryGetValue((id, column), out var saved)) return saved;
            try
            {
                var unit = ReadUnit(id);
                saved = column switch
                {
                    >= 15 and <= 30 => FormationCell(unit, column),
                    >= 31 and <= 33 => TacticCell(unit, column),
                    >= 34 and <= 41 => CommandCell(unit, column),
                    _ => BasicCell(unit, column)
                };
            }
            catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
            return prepared[(id, column)] = saved;
        }

        private PreparedCell MakeCell(Func<string?> display, Func<SortKey>? sort = null, Func<int?>? palette = null)
        {
            string? text = null, issue = null, sortIssue = null, presetIssue = null;
            int? preset = null; SortKey? key = null;
            try { text = display() ?? ""; } catch (InvalidDataException error) { issue = error.Message; }
            if (palette is not null)
                try { preset = palette(); } catch (InvalidDataException error) { presetIssue = error.Message; }
            if (sort is not null)
                try { key = sort(); } catch (InvalidDataException error) { sortIssue = error.Message; }
            return new(new(text, preset, issue) { PresetUnresolvedOwner = presetIssue }, key, sortIssue);
        }

        private string Data(int section, int id) => rules.ReadHistoryDataText(section, id);
        private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("army-information." + field);
        private sealed record Unit(int Id, int PhysicalSlot, UnitState Owner);
        private sealed record PreparedCell(ArmyInformationCell Display, SortKey? Key, string? SortUnresolvedOwner);
        private abstract record SortKey { internal abstract bool Before(SortKey other, bool ascending); }
        private sealed record NumberKey(long Value, bool Empty = false) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (NumberKey)other;
                if (Empty != right.Empty) return ascending ? !Empty : Empty;
                return !Empty && (ascending ? Value < right.Value : Value > right.Value);
            }
        }
        private sealed record NameKey(string Value, bool Empty = false) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (NameKey)other;
                if (Empty != right.Empty) return ascending ? !Empty : Empty;
                return !Empty && (ascending ? PersonNameOrder.Compare(Value, right.Value) < 0 : PersonNameOrder.Compare(right.Value, Value) < 0);
            }
        }
    }
}
