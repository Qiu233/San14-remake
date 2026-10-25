using San14.Core;

namespace San14.Data;

internal sealed record DoctrineResearchPanelDefinition(int Id, string Name, int Category,
    int FirstDoctrineId, int FirstCost, int SecondDoctrineId, int SecondCost, int Value,
    bool RestrictToFirstDoctrine, int LayoutSlot, IReadOnlyList<int> Dependencies);

/// <summary>The small research workspace belongs to one page or one command.
/// It never writes the captured current or the live force while quoting.</summary>
internal sealed class DoctrineResearchWorkspace
{
    internal DoctrineResearchWorkspace(IEnumerable<byte> points, IEnumerable<int> researched,
        IEnumerable<int> pending, IEnumerable<int> available)
    { Points = points.ToArray(); Researched = researched.ToHashSet(); Pending = pending.ToHashSet();
        Available = available.ToHashSet(); }
    internal byte[] Points { get; }
    internal HashSet<int> Researched { get; }
    internal HashSet<int> Pending { get; }
    internal HashSet<int> Available { get; }
    internal DoctrineResearchWorkspace Copy() => new(Points, Researched, Pending, Available);
}

internal static class DoctrineResearchSource
{
    internal static int CanResearch(DoctrineResearchRules rules, DoctrineResearchWorkspace working, int id)
    {
        if (id == 1) return 0;
        if (rules.ApplicationRestricted is not bool restricted) throw Missing("application_restriction");
        if (restricted) return -4;
        if (!rules.HostAvailable) return -5;
        if (working.Researched.Contains(id)) return -12;
        if (!working.Available.Contains(id)) return -8;
        if (BookCost(rules.Baseline, working, rules.BookCostPerPanel) >= rules.Books) return -6;
        if (ResearchCount(working, rules.Panels) >= rules.ResearchCapacity) return -7;
        if (id == 0 || !rules.Panels.TryGetValue(id, out var panel)) return -9;
        if (panel.RestrictToFirstDoctrine && panel.FirstDoctrineId != 0 && panel.FirstDoctrineId != rules.RulerDoctrineId) return -10;
        var cost = Costs(panel, rules.RulerDoctrineId, rules.MatchingDiscount);
        if (panel.FirstDoctrineId != 0 && (panel.FirstDoctrineId is < 1 or > 6 || working.Points[panel.FirstDoctrineId - 1] < cost.First) ||
            panel.SecondDoctrineId != 0 && (panel.SecondDoctrineId is < 1 or > 6 || working.Points[panel.SecondDoctrineId - 1] < cost.Second)) return -11;
        return 0;
    }
    //23C4D0's serialized record, shared by the two normal language resources.
    internal static DoctrineResearchPanelDefinition Decode(Formats.S14DoctrinePanelDefinition row)
    {
        var b = row.RawBytes;
        if (b.Length != 33) throw Missing("definition_record");
        return new(row.Slot, row.Name, b[18], b[19], b[20], b[21], b[22], row.RawValue,
            b[25] != 0, b[26], b[27..33].Select(value => (int)value).ToArray());
    }

    internal static (int First, int Second) Costs(DoctrineResearchPanelDefinition panel,
        int rulerDoctrineId, int matchingDiscount)
    {
        if (panel.FirstDoctrineId != 0 && panel.FirstCost == 0 ||
            panel.SecondDoctrineId != 0 && panel.SecondCost == 0) throw Missing("definition_cost");
        return (Cost(panel.FirstDoctrineId, panel.FirstCost), Cost(panel.SecondDoctrineId, panel.SecondCost));
        int Cost(int doctrine, int amount) => doctrine == 0 ? 0 : doctrine == rulerDoctrineId
            ? Math.Max(1, unchecked(amount - matchingDiscount)) : amount;
    }

    //2245C0 counts the union of both work bitmaps against the captured commit.
    internal static int BookCost(DoctrineResearchWorkspace baseline, DoctrineResearchWorkspace working, int perPanel)
        => working.Researched.Concat(working.Pending).Distinct()
            .Count(id => !baseline.Researched.Contains(id) && !baseline.Pending.Contains(id)) * perPanel;

    internal static int ResearchCount(DoctrineResearchWorkspace working,
        IReadOnlyDictionary<int, DoctrineResearchPanelDefinition> panels)
        => working.Researched.Count(id => id != 1 && id is not (>= 74 and <= 79) &&
            panels.ContainsKey(id));

    internal static void Apply(DoctrineResearchWorkspace working, DoctrineResearchWorkspace baseline,
        DoctrineResearchPanelDefinition panel, int rulerDoctrineId, int discount,
        IReadOnlyDictionary<int, int> panelByLayoutSlot)
    {
        var costs = Costs(panel, rulerDoctrineId, discount);
        Debit(panel.FirstDoctrineId, costs.First); Debit(panel.SecondDoctrineId, costs.Second);
        working.Researched.Add(panel.Id);
        if (panel.Id == 1 || baseline.Researched.Contains(panel.Id) && !baseline.Pending.Contains(panel.Id))
            working.Pending.Remove(panel.Id);
        else working.Pending.Add(panel.Id);
        foreach (int slot in panel.Dependencies)
            if (panelByLayoutSlot.TryGetValue(slot < 80 ? slot : 0, out int id) && id != 0)
                working.Available.Add(id);
        void Debit(int doctrine, int amount)
        {
            if (doctrine == 0) return;
            if (doctrine is < 1 or > 6) throw Missing("doctrine_reserve");
            working.Points[doctrine - 1] = (byte)Math.Clamp(working.Points[doctrine - 1] - amount, 0, 99);
        }
    }

    internal static void Redefine(DoctrineResearchWorkspace working, DoctrineResearchWorkspace baseline,
        IReadOnlyDictionary<int, DoctrineResearchPanelDefinition> panels, int rulerDoctrineId,
        int discount, IReadOnlyDictionary<int, int> panelByLayoutSlot)
    {
        //3365B0 refunds in panel order, capping each separate addition at99.
        for (int id = 1; id < 80; id++)
        {
            if (!working.Researched.Contains(id)) continue;
            if (!panels.TryGetValue(id, out var row)) throw Missing("refund_definition");
            var cost = Costs(row, rulerDoctrineId, discount);
            Refund(row.FirstDoctrineId, cost.First); Refund(row.SecondDoctrineId, cost.Second);
        }
        working.Researched.Clear(); working.Pending.Clear(); working.Available.Clear(); working.Available.Add(1);
        if (!panels.TryGetValue(1, out var root)) throw Missing("root_definition");
        Apply(working, baseline, root, rulerDoctrineId, discount, panelByLayoutSlot);
        void Refund(int doctrine, int amount)
        {
            if (doctrine == 0) return;
            if (doctrine is < 1 or > 6) throw Missing("refund_doctrine");
            working.Points[doctrine - 1] = (byte)Math.Min(99, working.Points[doctrine - 1] + amount);
        }
    }

    internal static InvalidDataException Missing(string owner) => new("doctrine-research." + owner);
}
