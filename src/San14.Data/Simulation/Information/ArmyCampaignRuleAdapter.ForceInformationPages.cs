using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public ForceInformationPageSet PrepareForceInformationPages(GameState state, int playerForceId,
        IReadOnlyList<int> actualForceIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actualForceIds);
        return new ForceInformationCurrent(this, state, playerForceId).Pages(actualForceIds);
    }

    public AreaAdministrationQuery<ForceInformationPageSet> PrepareAllForceInformationPages(
        GameState state, int playerForceId, bool ownForceOnly)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var source = new ForceInformationCurrent(this, state, playerForceId);
            return AreaAdministrationQuery<ForceInformationPageSet>.Known(source.Pages(source.OrdinaryCandidates(ownForceOnly)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<ForceInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class ForceInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly Lazy<SourceWorldContext> captured;
        private SourceWorldContext Current => captured.Value;
        private readonly Dictionary<(int ForceId, int Column), PreparedCell> prepared = [];

        internal ForceInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, int playerForceId)
        {
            this.rules = rules; this.state = state; this.playerForceId = playerForceId;
            captured = new(() =>
            {
                var current = rules.CaptureOwnedArmyReturnWorld(state).Context;
                BaseReturnCaptivesInputs.BindCurrent(state, current);
                current.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
                return current;
            });
        }

        internal ForceInformationPageSet Pages(IReadOnlyList<int> ids)
        {
            var groups = ForceInformationColumnsSource.Groups.Select((group, index) =>
                new ForceInformationGroup(index, group.CaptionId, Data(1, group.CaptionId),
                    group.ColumnIds.Select(id =>
                    {
                        var row = ForceInformationColumnsSource.Rows[id];
                        int format = (row.Flags & 1) != 0 ? 3 : 4;
                        if ((row.Flags & 8) != 0) format = 4;
                        if ((row.Flags & 2) != 0) format = 5;
                        return new ForceInformationColumn(id, row.CaptionId, Data(1, row.CaptionId), row.Width,
                            format, (row.Flags & 0x20000000) == 0, true, row.Visible);
                    }).ToImmutableArray())).ToImmutableArray();
            return new(Data(1, 0x36E), groups, ids,
                (id, column) => Cell(id, column).Display with { SortUnresolvedOwner = Cell(id, column).SortUnresolvedOwner },
                (id, column) => Cell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => Before(Cell(left, column).Key ?? throw Missing("sort_key"),
                    Cell(right, column).Key ?? throw Missing("sort_key"), column, ascending));
        }

        internal int[] OrdinaryCandidates(bool own)
        {
            var normal = new List<int>(); var special = new List<int>();
            //Own opens the detail selector with the real player first; it is
            //not a filter that drops other Forces from that caller list.
            int player = own ? Need(OwnedCurrentPlayerForce.ReadCurrentReference(state, Current)?.PhysicalSlot,
                "candidate_player") : -1;
            if (own) normal.Add(player);
            for (int slot = 0; slot < 52; slot++)
            {
                var force = Force(slot);
                if (!Need(Current.ReadForceNativeValidity(force), "candidate_Force_validity")) continue;
                int actual = Need(Current.ReadForcePhysicalSlot(force), "candidate_Force_receiver");
                if (own && actual == player) continue;
                int ruler = Need(Current.ReadForceRecord(actual)?.RawRuler10, "candidate_Force_ruler10");
                if (ruler is >= 3001 and <= 3150 or >= 2001 and <= 2088 or >= 5101 and <= 5200) special.Add(actual);
                else normal.Add(actual);
            }
            normal.AddRange(special); return normal.ToArray();
        }

        private PreparedCell Cell(int forceId, int column)
        {
            if (prepared.TryGetValue((forceId, column), out var saved)) return saved;
            try
            {
                _ = Force(forceId);
                saved = column switch
                {
                    >= 12 and <= 14 or >= 21 and <= 27 or 37 or 38 or 41 or 43 => DiplomacyCell(forceId, column),
                    15 or 16 or >= 29 and <= 34 or >= 44 and <= 49 => GovernanceCell(forceId, column),
                    _ => BasicCell(forceId, column)
                };
            }
            catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
            return prepared[(forceId, column)] = saved;
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

        private ForceReference Force(int id) => id is >= 0 and < 52
            ? Current.ReadForceTableReference(id) ?? throw Missing("Force_pointer") : throw Missing("Force_identity");
        private bool Valid(int id) => Need(Current.ReadForceNativeValidity(Force(id)), "Force_validity");
        private static bool Special(int id) => id is >= 46 and <= 50;
        private string Data(int section, int id) => rules.ReadHistoryDataText(section, id);
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("force-information." + field);
        private sealed record PreparedCell(ForceInformationCell Display, SortKey? Key, string? SortUnresolvedOwner);
        private abstract record SortKey
        {
            internal virtual bool IsEmpty => false;
            internal abstract bool Before(SortKey other, bool ascending);
        }
        private static bool Before(SortKey left, SortKey right, int column, bool ascending)
        {
            //Common mode1 fixes typed empties last; mode0/4 reverse exchanges
            //the actual keys, including their empty tag2 priority.
            if (ForceInformationColumnsSource.Rows[column].SortMode == 1 && left.IsEmpty != right.IsEmpty)
                return !left.IsEmpty;
            return left.Before(right, ascending);
        }
        private sealed record NumberKey(long Value, bool Empty = false) : SortKey
        {
            internal override bool IsEmpty => Empty;
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (NumberKey)other;
                if (Empty != right.Empty) return ascending ? !Empty : Empty;
                return !Empty && (ascending ? Value < right.Value : Value > right.Value);
            }
        }
        private sealed record NameKey(string Value, bool Empty = false) : SortKey
        {
            internal override bool IsEmpty => Empty;
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (NameKey)other;
                if (Empty != right.Empty) return ascending ? !Empty : Empty;
                return !Empty && (ascending ? PersonNameOrder.Compare(Value, right.Value) < 0 : PersonNameOrder.Compare(right.Value, Value) < 0);
            }
        }
    }
}
