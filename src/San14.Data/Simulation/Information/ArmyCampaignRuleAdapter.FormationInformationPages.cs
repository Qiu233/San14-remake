using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary information menu has no deployment Person
    /// context. Its definition coefficients and sort keys share one saved
    /// current owner, including edited formation parameters.</summary>
    public AreaAdministrationQuery<FormationInformationPageSet> PrepareAllFormationInformationPages(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try { return AreaAdministrationQuery<FormationInformationPageSet>.Known(new FormationInformationCurrent(this, state).Pages()); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<FormationInformationPageSet>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<OfficerInformationPageSet> PrepareFormationOfficerInformationPages(
        GameState state, int playerForceId, int formationId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            // Actual Formation footer14 passes the palette enable argument0.
            var source = new OfficerInformationCurrent(this, state, playerForceId, rowPaletteEnabled: false);
            var current = source.current;
            var ids = new List<int>();
            // Ordinary footer14: registered Person100 order, validity,
            // non-bandit Force, status1..6 and actual capability predicate.
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw FormationInformationCurrent.Missing("Person_registry"))
            {
                int physical = FormationInformationCurrent.Need(entry, "Person_payload");
                byte status = FormationInformationCurrent.Need(current.ReadPersonStatus(physical), "Person_status");
                if (!FormationInformationCurrent.Need(current.ReadPersonNativeValidity(physical), "Person_validity")) continue;
                int force = FormationInformationCurrent.Need(current.ReadPersonForce(physical)?.PhysicalSlot, "Person_force");
                if (force == 51 || status is < 1 or > 6) continue;
                int id = FormationInformationCurrent.Need(current.ReadPersonIdentity(physical), "Person_identity");
                uint mask = FormationInformationCurrent.Need(current.ReadPersonFormationMask164(physical), "Person_formations");
                if (DeploymentFormationEligibility.Allows(mask, formationId, id)) ids.Add(id);
            }
            return AreaAdministrationQuery<OfficerInformationPageSet>.Known(source.Pages(ids));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<OfficerInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class FormationInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state)
    {
        private readonly Dictionary<(int Id, int Column), PreparedCell> prepared = [];
        internal FormationInformationPageSet Pages()
        {
            var current = rules.CapturePersonPresentationCurrent(state);
            var ids = new List<int>();
            for (int slot = 0; slot < 21; slot++)
            {
                var pointer = current.ReadStaticTableReference(0x77428, slot) ?? throw Missing("candidate_pointer");
                int id = pointer.PhysicalSlot ?? 0;
                if (id == 0 || id is 17 or 18 || Need(TacticRemakeCurrentFormations.ReadRawByte(state, id, 0x7E), "category") == 0) continue;
                if (!state.Formations.ContainsKey(id)) throw Missing("current_definition");
                ids.Add(id);
            }
            var groups = FormationInformationColumnsSource.Groups.Select((group, index) =>
                new FormationInformationGroup(index, group.CaptionId, Data(group.CaptionId), group.ColumnIds.Select(id =>
                {
                    var row = FormationInformationColumnsSource.Rows[id];
                    int format = (row.Flags & 1) != 0 ? 3 : (row.Flags & 2) != 0 ? 5 : 4;
                    return new FormationInformationColumn(id, row.CaptionId, Data(row.CaptionId), row.Width,
                        format, (row.Flags & 0x20000000) == 0, true, true, row.SortMode != 2);
                }).ToImmutableArray())).ToImmutableArray();
            var pages = new FormationInformationPageSet(Data(0x5FC), groups, ids,
                (id, column) => Cell(id, column).Display,
                (id, column) => Cell(id, column).Missing,
                (left, right, column, ascending) => Before(Cell(left, column), Cell(right, column), ascending))
            {
                OfficersCaption = Data(0x211),
                ReadCanBrowseOfficers = id => ids.Contains(id) && id != 19 &&
                    Need(TacticRemakeCurrentFormations.ReadRawByte(state, id, 0x7E), "category") != 2
            };
            if (ids.Count != 0) pages.SelectSourceIndex(0);
            return pages;
        }

        private PreparedCell Cell(int id, int column)
        {
            if (prepared.TryGetValue((id, column), out var cached)) return cached;
            PreparedCell value;
            try
            {
                var row = state.Formations.GetValueOrDefault(id) ?? throw Missing("current_definition");
                string text; int key = 0; bool empty = false;
                switch (column)
                {
                    case 0: text = row.Name; key = id; break;
                    case 1: text = rules._skills.Formations.Formations[id].ShortText; break;
                    case 2: text = row.Description; break;
                    case 3: key = Coefficient(id, 0x83, 45); text = Number(key); break;
                    case 4: key = Coefficient(id, 0x84, 70); text = Number(key); break;
                    case 5: key = Coefficient(id, 0x85, 60); text = Number(key); break;
                    case 6: key = Coefficient(id, 0x86, 40); text = Number(key); break;
                    case 7: key = Coefficient(id, 0x7F, 30); text = Number(key); break;
                    case 8: key = Need(TacticRemakeCurrentFormations.ReadParameter(state, id, 0xA6), "occupation") & 1;
                        text = Data(key == 0 ? 0x339 : 0x338); break;
                    case 9: key = Need(TacticRemakeCurrentFormations.ReadRawWord(state, id, 0x9A), "minimum_troops"); text = Number(key); break;
                    case 10: case 11:
                        key = Need(TacticRemakeCurrentFormations.ReadParameter(state, id, column == 10 ? 0x80 : 0x81), "coefficient") * 100;
                        empty = key == 0; text = empty ? "" : Number(key); break;
                    default: throw Missing("column");
                }
                value = new(new(text, null, null), key, empty, null);
            }
            catch (InvalidDataException error) { value = new(new(null, null, error.Message), 0, false, error.Message); }
            return prepared[(id, column)] = value;
        }
        private int Coefficient(int id, int offset, int maximum)
        {
            int bytes = Need(TacticRemakeCurrentFormations.ReadVectorByteCount(state, id), "vector");
            int value = Need(TacticRemakeCurrentFormations.ReadParameter(state, id, offset), "coefficient");
            return (bytes & ~3) == 32 ? Math.Clamp(value, 1, maximum) : value;
        }
        private static bool Before(PreparedCell left, PreparedCell right, bool ascending)
        {
            if (left.Missing is { } missing) throw new InvalidDataException(missing);
            if (right.Missing is { } otherMissing) throw new InvalidDataException(otherMissing);
            if (left.Empty != right.Empty) return !left.Empty;
            return !left.Empty && (ascending ? left.Key < right.Key : left.Key > right.Key);
        }
        private string Data(int id) => rules.ReadHistoryDataText(1, id);
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        internal static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        internal static InvalidDataException Missing(string field) => new("formation-information." + field);
        private sealed record PreparedCell(FormationInformationCell Display, int Key, bool Empty, string? Missing);
    }
}
