namespace San14.Data;

//051560's two shared bonuses. The normal parameter reader supplies both;
//a missing parameter is not an instruction to use an arbitrary zero.
internal sealed record DoctrineResearchAutomaticParameters(int PrimaryMatchBonus, int SecondaryMatchBonus);

/// <summary>Queries borrow the caller's working research draft. They do not
/// capture another world or commit a selected panel themselves.</summary>
internal sealed record DoctrineResearchAutomaticInputs(
    Func<int> ReadPrimaryDoctrineId,
    Func<int> ReadSecondaryDoctrineId,
    Func<int, int> ReadRulerCategoryPreference,
    Func<int, int> ReadDoctrineLevel,
    Func<int, bool> IsResearched,
    Func<int, bool> CanResearch);

/// <summary>A member of the current force's real registered-person iterator.
/// The two reached queries use the same captured current as that iterator.</summary>
internal sealed record DoctrineResearchSecondaryOfficer(int OfficerId, int ServiceKindId,
    int OfficeId, int FirstLocationId, int DoctrineId, bool UsesSpecialPersonRules,
    Func<int> ReadCurrentForceId, Func<bool> ReadIsStrategist);

/// <summary>The6B9530 automatic research selection session. The caller applies
/// each returned panel through the same writer as manual research, tests the
/// actual command-book limit, then calls Applied before selecting again.</summary>
internal static class DoctrineResearchAutomaticSource
{
    internal static DoctrineResearchAutomaticSession CreateSession(
        IReadOnlyList<DoctrineResearchPanelDefinition> panels, int firstPriority, int secondPriority,
        DoctrineResearchAutomaticInputs inputs, DoctrineResearchAutomaticParameters parameters,
        int? previousSecondFocusPanelId = null) =>
        new(panels, firstPriority, secondPriority, inputs, parameters, previousSecondFocusPanelId);

    //229AB0. This is independent of remaining research points and is not
    //the second-highest doctrine level. All six zero-count groups still
    //participate, so an entirely empty eligible roster returns doctrine1.
    internal static int SelectSecondaryDoctrine(int primaryDoctrineId,
        IEnumerable<DoctrineResearchSecondaryOfficer> registeredForceOfficers)
    {
        var count = new int[6]; var office = new int[6]; var strategist = new int[6];
        foreach (var person in registeredForceOfficers)
        {
            if (person.ServiceKindId is < 2 or > 4) continue;
            //2C7250's five native temporary foreign-person identity bands.
            bool foreign = InBand(person.OfficerId,4111) || InBand(person.OfficerId,4131) ||
                InBand(person.OfficerId,4151) || InBand(person.OfficerId,4171) || InBand(person.OfficerId,4191);
            if (foreign && person.FirstLocationId is >= 1 and <= 61 && person.ReadCurrentForceId() is not (>= 46 and <= 50))
                continue;
            if (person.UsesSpecialPersonRules || person.DoctrineId == primaryDoctrineId) continue;
            if (person.DoctrineId is < 1 or > 6) throw DoctrineResearchSource.Missing("secondary_person_doctrine");
            int group = person.DoctrineId - 1;
            count[group] = unchecked(count[group] + 1);
            if (person.OfficeId != 73) office[group] = unchecked(office[group] + 1);
            if (person.ReadIsStrategist()) strategist[group] = unchecked(strategist[group] + 1);
        }
        return Enumerable.Range(0,6).OrderByDescending(id => count[id]).ThenByDescending(id => office[id])
            .ThenByDescending(id => strategist[id]).ThenBy(id => id).First() + 1;
        static bool InBand(int identity, int first) => unchecked((uint)(identity - first)) <= 9;
    }
}

internal sealed class DoctrineResearchAutomaticSession
{
    private readonly IReadOnlyList<DoctrineResearchPanelDefinition> _panels;
    private readonly DoctrineResearchAutomaticInputs _inputs;
    private readonly DoctrineResearchAutomaticParameters _parameters;
    private readonly List<DoctrineResearchPanelDefinition> _first;
    private readonly List<DoctrineResearchPanelDefinition> _second;
    private DoctrineResearchPanelDefinition? _firstFocus, _secondFocus;

    internal int? FirstFocusPanelId => _firstFocus?.Id;
    internal int? SecondFocusPanelId => _secondFocus?.Id;

    internal DoctrineResearchAutomaticSession(IReadOnlyList<DoctrineResearchPanelDefinition> panels,
        int firstPriority, int secondPriority, DoctrineResearchAutomaticInputs inputs,
        DoctrineResearchAutomaticParameters parameters, int? previousSecondFocusPanelId)
    {
        _panels = panels; _inputs = inputs; _parameters = parameters;
        _first = []; _second = [];
        var readings = new Readings(inputs);
        //6D7B70 and6D7D60 visit the80-slot definition table in its original
        //order. Native slot0 is invalid; panel1 is explicitly excluded.
        foreach (var panel in panels)
        {
            if (panel.Id is < 2 or >= 80 || inputs.IsResearched(panel.Id)) continue;
            bool restricted = panel.RestrictToFirstDoctrine && panel.FirstDoctrineId != 0;
            if (restricted ? panel.FirstDoctrineId == readings.Primary : panel.Category == firstPriority)
                _first.Add(panel);
            if ((!restricted || panel.FirstDoctrineId == readings.Primary) && panel.Category == secondPriority)
                _second.Add(panel);
        }
        SourceOrder(_first, (left, right) => BeforePriority(left, right, readings));
        SourceOrder(_second, (left, right) => BeforePriority(left, right, readings));
        _firstFocus = NextFocus(_first, previousSecondFocusPanelId);
        _secondFocus = NextFocus(_second, _firstFocus?.Id);
    }

    /// <summary>6CA8C0 rebuilds the current legal candidates after every
    /// successful write. It sorts only when at least two candidates remain.</summary>
    internal int? SelectNext()
    {
        var candidates = _panels.Where(panel => panel.Id is >= 2 and < 80 && _inputs.CanResearch(panel.Id)).ToList();
        if (candidates.Count > 1)
        {
            var readings = new Readings(_inputs);
            SourceOrder(candidates, (left, right) => BeforeCandidate(left, right, readings));
        }
        return candidates.Count == 0 ? null : candidates[0].Id;
    }

    //Call only after the working writer succeeded and the original book-limit
    //test permits another iteration. A rejected writer must end the loop.
    internal void Applied(int panelId)
    {
        if (_firstFocus?.Id == panelId) _firstFocus = NextFocus(_first, _secondFocus?.Id);
        if (_secondFocus?.Id == panelId) _secondFocus = NextFocus(_second, _firstFocus?.Id);
    }

    //6DDB90 removes researched and excluded heads permanently from this
    //session's priority list; an affordable-current-panel test is not involved.
    private DoctrineResearchPanelDefinition? NextFocus(List<DoctrineResearchPanelDefinition> pool, int? excluded)
    {
        while (pool.Count != 0)
        {
            var panel = pool[0];
            if (!_inputs.IsResearched(panel.Id) && panel.Id != excluded) return panel;
            pool.RemoveAt(0);
        }
        return null;
    }

    //6B0220. Its category-weight array is explicitly initialized to zero by
    //6D7B70/6D7D60, so it contributes no comparison key in this entrance.
    private static bool BeforePriority(DoctrineResearchPanelDefinition left,
        DoctrineResearchPanelDefinition right, Readings readings)
    {
        int difference = unchecked(Special(left, readings) - Special(right, readings));
        if (difference != 0) return difference > 0;
        difference = unchecked(readings.Preference(left.Category) - readings.Preference(right.Category));
        if (difference != 0) return difference > 0;
        difference = unchecked(Relevance(left, readings) - Relevance(right, readings));
        if (difference != 0) return difference > 0;
        difference = unchecked(LevelTotal(left, readings) - LevelTotal(right, readings));
        return difference != 0 ? difference > 0 : left.Id < right.Id;
    }

    private static int Special(DoctrineResearchPanelDefinition panel, Readings readings) =>
        panel.RestrictToFirstDoctrine && panel.FirstDoctrineId != 0 && panel.FirstDoctrineId == readings.Primary ? 1 : 0;

    //6CAA90 admits each doctrine channel only when its cost is nonzero.
    private static int Relevance(DoctrineResearchPanelDefinition panel, Readings readings)
    {
        int result = 0;
        if (panel.FirstCost != 0)
        {
            if (panel.FirstDoctrineId == readings.Primary) result = 2;
            else if (panel.FirstDoctrineId == readings.Secondary) result = 1;
        }
        if (panel.SecondCost != 0)
        {
            if (panel.SecondDoctrineId == readings.Primary) result = Math.Max(result, 2);
            else if (panel.SecondDoctrineId == readings.Secondary && result == 0) result = 1;
        }
        return result;
    }

    //6CA850 uses current remaining doctrine levels, rather than definition
    //costs or the force's lifetime accumulated doctrine experience.
    private static int LevelTotal(DoctrineResearchPanelDefinition panel, Readings readings)
    {
        int first = panel.FirstDoctrineId != 0 && panel.FirstCost != 0 ? readings.Level(panel.FirstDoctrineId) : 0;
        int second = panel.SecondDoctrineId != 0 && panel.SecondCost != 0 ? readings.Level(panel.SecondDoctrineId) : 0;
        return unchecked(first + second);
    }

    //050090: special panel, total score, exact-focus bonus, dependency bonus,
    //then panel identity. Comparator subtraction preserves source Int32 rules.
    private bool BeforeCandidate(DoctrineResearchPanelDefinition left,
        DoctrineResearchPanelDefinition right, Readings readings)
    {
        int difference = unchecked(Special(left, readings) - Special(right, readings));
        if (difference != 0) return difference > 0;
        var a = Score(left, readings); var b = Score(right, readings);
        difference = unchecked(a.Total - b.Total);
        if (difference == 0) difference = unchecked(a.Direct - b.Direct);
        if (difference == 0) difference = unchecked(a.Dependency - b.Dependency);
        return difference != 0 ? difference > 0 : left.Id < right.Id;
    }

    private (int Total, int Direct, int Dependency) Score(DoctrineResearchPanelDefinition panel, Readings readings)
    {
        int total = panel.FirstDoctrineId == 0 ? 0 : panel.FirstCost;
        if (panel.SecondDoctrineId != 0) total = unchecked(total + panel.SecondCost);
        //051560 intentionally does not require nonzero costs or doctrine IDs
        //for these equality checks. In particular, a known secondary0 remains0.
        if (panel.FirstDoctrineId == readings.Primary || panel.SecondDoctrineId == readings.Primary)
            total = unchecked(total + _parameters.PrimaryMatchBonus);
        if (panel.FirstDoctrineId == readings.Secondary || panel.SecondDoctrineId == readings.Secondary)
            total = unchecked(total + _parameters.SecondaryMatchBonus);
        int direct = 0, dependency = 0;
        AddFocus(_firstFocus); AddFocus(_secondFocus);
        return (unchecked(total + direct + dependency), direct, dependency);

        void AddFocus(DoctrineResearchPanelDefinition? focus)
        {
            if (focus is null) return;
            if (panel.Id == focus.Id) direct = unchecked(direct + _parameters.PrimaryMatchBonus);
            else if (focus.Dependencies.Contains(panel.LayoutSlot))
                dependency = unchecked(dependency + _parameters.SecondaryMatchBonus);
        }
    }

    //04F590/69D310 insert a node before its predecessors while the comparator
    //returns true. Keeping that traversal also preserves lazy query reachability.
    private static void SourceOrder(List<DoctrineResearchPanelDefinition> panels,
        Func<DoctrineResearchPanelDefinition, DoctrineResearchPanelDefinition, bool> before)
    {
        for (int index = 1; index < panels.Count; index++)
        {
            var selected = panels[index];
            if (!before(selected, panels[index - 1])) continue;
            int position = index - 1;
            while (position > 0 && before(selected, panels[position - 1])) position--;
            panels.RemoveAt(index); panels.Insert(position, selected);
        }
    }

    //A selection pass has no intervening world writes. Memoize only reached
    //queries within this pass, so secondary-doctrine calculation is not repeated
    //for every pair and a singleton does not read unrelated scoring owners.
    private sealed class Readings(DoctrineResearchAutomaticInputs inputs)
    {
        private int? _primary, _secondary;
        private readonly Dictionary<int, int> _preferences = [], _levels = [];
        internal int Primary => _primary ??= inputs.ReadPrimaryDoctrineId();
        internal int Secondary => _secondary ??= inputs.ReadSecondaryDoctrineId();
        internal int Preference(int category) => Read(_preferences, category, inputs.ReadRulerCategoryPreference);
        internal int Level(int doctrine) => Read(_levels, doctrine, inputs.ReadDoctrineLevel);
        private static int Read(Dictionary<int, int> cache, int id, Func<int, int> query)
        {
            if (!cache.TryGetValue(id, out int value)) cache.Add(id, value = query(id));
            return value;
        }
    }
}
