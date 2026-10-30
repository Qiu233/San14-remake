using System.Collections.Immutable;

namespace San14.Data;

/// <summary>A deployment-only definition chooser. The parent owns the draft;
/// this retained model owns row occurrence order and uncommitted checks.</summary>
public sealed class DeploymentDefinitionSelection
{
    private readonly ImmutableArray<int> ids;
    private readonly ImmutableArray<bool> eligible;
    private readonly Func<int, int, DefinitionInformationCell> cell;
    private readonly Func<int, int, string?> sortMissing;
    private readonly Func<int, int, int, bool, bool> before;
    private readonly Dictionary<(int Id, int Column), DefinitionInformationCell> cells = [];
    private readonly HashSet<int> selected = [];
    private int[] order;
    public int TitleMessageId { get; }
    public ImmutableArray<DefinitionInformationGroup> Groups { get; }
    public bool Multiple { get; }
    public int MaximumSelected => Multiple ? 3 : 1;
    public int MinimumSelected => Multiple ? 0 : 1;
    public IReadOnlyList<int> OrderedSourceIndices => order;
    public IReadOnlyList<int> SelectedDefinitionIds => order.Where(selected.Contains).Select(slot => ids[slot]).ToArray();
    public int SelectedCount => selected.Count;

    internal DeploymentDefinitionSelection(int title, ImmutableArray<DefinitionInformationGroup> groups,
        IReadOnlyList<int> ids, IReadOnlyList<bool> eligible, IReadOnlyList<int> initialSourceIndices,
        bool multiple, Func<int, int, DefinitionInformationCell> cell,
        Func<int, int, string?> sortMissing, Func<int, int, int, bool, bool> before)
    {
        TitleMessageId = title; Groups = groups; this.ids = ids.ToImmutableArray();
        this.eligible = eligible.ToImmutableArray(); Multiple = multiple;
        if (ids.Count != eligible.Count) throw new ArgumentException("Definition eligibility must match occurrences.");
        order = Enumerable.Range(0, ids.Count).ToArray(); this.cell = cell; this.sortMissing = sortMissing; this.before = before;
        foreach (int slot in initialSourceIndices)
        {
            // The source installs preselection independently from its
            // disabled occurrence list. A disabled existing check survives
            // opening and an unchanged accept, but cannot be toggled.
            if ((uint)slot >= (uint)ids.Count || selected.Count >= MaximumSelected)
                throw new InvalidDataException("deployment.definition_selection.initial_selection");
            selected.Add(slot);
        }
    }

    public bool IsSelected(int sourceIndex) => selected.Contains(sourceIndex);
    public bool IsEligible(int sourceIndex) => (uint)sourceIndex < (uint)eligible.Length && eligible[sourceIndex];
    public bool CanToggle(int sourceIndex) => IsEligible(sourceIndex) &&
        (!Multiple || selected.Contains(sourceIndex) || selected.Count < MaximumSelected);
    public bool Toggle(int sourceIndex)
    {
        if (!CanToggle(sourceIndex)) return false;
        if (!Multiple) { selected.Clear(); selected.Add(sourceIndex); return true; }
        if (!selected.Remove(sourceIndex)) selected.Add(sourceIndex);
        return true;
    }

    public void SelectAllOrClear()
    {
        if (!Multiple) return;
        if (selected.Count >= MaximumSelected || order.Where(IsEligible).All(selected.Contains)) { selected.Clear(); return; }
        foreach (int slot in order) if (selected.Count < MaximumSelected && IsEligible(slot)) selected.Add(slot);
    }

    public DefinitionInformationPage ReadPage(int group)
    {
        if ((uint)group >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(group));
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var rows = ImmutableArray.CreateBuilder<DefinitionInformationRow>(order.Length);
        foreach (int slot in order)
        {
            var values = ImmutableArray.CreateBuilder<DefinitionInformationCell>(Groups[group].Columns.Length);
            foreach (var column in Groups[group].Columns)
            {
                if (!cells.TryGetValue((ids[slot], column.Id), out var value))
                    cells.Add((ids[slot], column.Id), value = cell(ids[slot], column.Id));
                if (value.UnresolvedOwner is { } missing) issues.Add(missing);
                if (value.PresetUnresolvedOwner is { } palette) issues.Add(palette);
                values.Add(value);
            }
            rows.Add(new(slot, ids[slot], values.MoveToImmutable()));
        }
        return new(group, rows.MoveToImmutable(), issues.Count == 0 ? null : string.Join(";", issues));
    }

    public AreaAdministrationQuery<DefinitionInformationPage> Sort(int group, int columnId, bool ascending)
    {
        if ((uint)group >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(group));
        if (columnId == -1 && Multiple)
            order = order.OrderBy(slot => selected.Contains(slot) ? ascending ? 1 : 0 : ascending ? 0 : 1).ToArray();
        else
        {
            var descriptor = Groups[group].Columns.SingleOrDefault(column => column.Id == columnId);
            if (descriptor is not { CanSort: true })
                return AreaAdministrationQuery<DefinitionInformationPage>.Unknown("deployment.definition_selection.sort_column");
            if (!descriptor.ComparesRows) return AreaAdministrationQuery<DefinitionInformationPage>.Known(ReadPage(group));
            try
            {
                foreach (int slot in order)
                    if (sortMissing(ids[slot], columnId) is { } missing) throw new InvalidDataException(missing);
                order = order.OrderBy(slot => slot, Comparer<int>.Create((left, right) =>
                    before(ids[left], ids[right], columnId, ascending) ? -1 :
                    before(ids[right], ids[left], columnId, ascending) ? 1 : 0)).ToArray();
            }
            catch (InvalidDataException error) { return AreaAdministrationQuery<DefinitionInformationPage>.Unknown(error.Message); }
        }
        return AreaAdministrationQuery<DefinitionInformationPage>.Known(ReadPage(group));
    }
}
