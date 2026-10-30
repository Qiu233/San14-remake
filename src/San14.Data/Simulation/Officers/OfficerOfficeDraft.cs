using San14.Core;

namespace San14.Data;

public sealed record OfficeAppointmentPerson(int Id, string Name);
public sealed record OfficeAppointmentRow(int OfficeId, string OfficeName, int CommandLimit,
    OfficerAbilities AbilityBonus, OfficeAppointmentPerson? CurrentOfficer,
    OfficeAppointmentPerson? AssignedOfficer, bool Enabled, string? DisabledReason)
{
    public OfficeAppointmentPreview Preview { get; init; } = OfficeAppointmentPreview.Unknown;
}
public sealed record OfficeAppointmentPreview(AreaAdministrationQuery<string> Loyalty,
    AreaAdministrationQuery<string> MeritStage, AreaAdministrationQuery<string> Salary,
    AreaAdministrationQuery<string> Leadership, AreaAdministrationQuery<string> Strength,
    AreaAdministrationQuery<string> Intelligence, AreaAdministrationQuery<string> Politics)
{
    internal static OfficeAppointmentPreview Unknown { get; } = new(U("loyalty"), U("merit"), U("salary"),
        U("leadership"), U("strength"), U("intelligence"), U("politics"));
    private static AreaAdministrationQuery<string> U(string field) =>
        AreaAdministrationQuery<string>.Unknown("officer-office.preview_" + field);
}
public sealed record OfficeAppointmentCandidate(int OfficerId, string Name, string CurrentOfficeName,
    AreaAdministrationQuery<OfficerAbilities> Abilities, AreaAdministrationQuery<int> CommandLimit,
    bool Enabled, string? DisabledReason);

/// <summary>An uncommitted appointment layout. The captured query belongs to
/// this open page; editing it neither writes the world nor consumes randomness.</summary>
public sealed class OfficerOfficeDraft
{
    private readonly int _forceId;
    private readonly OfficeAppointmentRow[] _rows;
    private readonly Dictionary<int, int> _original;
    private Dictionary<int, int> _assigned;
    private readonly IReadOnlyDictionary<int, OfficeAppointmentPerson> _people;
    private readonly IReadOnlyDictionary<int, int> _grades;
    private readonly Func<int, IReadOnlyDictionary<int, int>, IReadOnlyList<OfficeAppointmentCandidate>>? _candidates;
    private readonly Func<IReadOnlyDictionary<int, int>, bool, Dictionary<int, int>>? _automatic;
    private readonly Func<IReadOnlyDictionary<int, int>, (bool Promote, bool Assign)>? _availability;
    private readonly HashSet<int> _removable;
    private readonly Func<int, int?, OfficeAppointmentPreview>? _preview;
    private readonly Func<int, IReadOnlyDictionary<int, int>, AreaAdministrationQuery<OfficerOfficePeopleSelection>>? _peopleSelection;
    private readonly Dictionary<(int Office, int? Person), OfficeAppointmentPreview> _previews = [];
    private bool _availabilityCurrent;
    private (bool Promote, bool Assign) _available;
    private readonly string? _unresolved;

    internal OfficerOfficeDraft(int forceId, OfficeAppointmentRow[] rows,
        Dictionary<int, int> original, IReadOnlyDictionary<int, OfficeAppointmentPerson> people,
        IReadOnlyDictionary<int, int> grades,
        Func<int, IReadOnlyDictionary<int, int>, IReadOnlyList<OfficeAppointmentCandidate>> candidates,
        Func<IReadOnlyDictionary<int, int>, bool, Dictionary<int, int>> automatic,
        Func<IReadOnlyDictionary<int, int>, (bool Promote, bool Assign)> availability,
        IEnumerable<int> removable, Func<int, int?, OfficeAppointmentPreview> preview,
        Func<int, IReadOnlyDictionary<int, int>, AreaAdministrationQuery<OfficerOfficePeopleSelection>> peopleSelection)
    {
        _forceId = forceId; _rows = rows; _original = original; _assigned = new(original);
        _people = people; _grades = grades; _candidates = candidates; _automatic = automatic;
        _availability = availability; _removable = new(removable);
        _preview = preview; _peopleSelection = peopleSelection;
    }

    internal OfficerOfficeDraft(int forceId, string unresolved)
    {
        _forceId = forceId; _rows = []; _original = []; _assigned = [];
        _people = new Dictionary<int, OfficeAppointmentPerson>(); _grades = new Dictionary<int, int>();
        _removable = [];
        _unresolved = unresolved; Status = unresolved;
    }

    public IReadOnlyList<OfficeAppointmentRow> Rows => _rows.Select(row =>
    {
        var holder = Holder(row.OfficeId);
        var key = (row.OfficeId, holder?.Id);
        if (!_previews.TryGetValue(key, out var preview))
            _previews.Add(key, preview = _preview?.Invoke(row.OfficeId, holder?.Id) ?? OfficeAppointmentPreview.Unknown);
        return row with { AssignedOfficer = holder, Preview = preview };
    }).ToArray();
    public string Status { get; private set; } = "";
    public bool CanCommit => _unresolved is null && _original.Any(pair =>
        _assigned.TryGetValue(pair.Key, out int next) && next != pair.Value);
    public int PromoteCount => CountChanges(promoted: true);
    public int DemoteCount => CountChanges(promoted: false);
    public bool CanAutoPromote => Availability().Promote;
    public bool CanAutoAssign => Availability().Assign;
    public bool CanDismissAll => _unresolved is null;

    public IReadOnlyList<OfficeAppointmentCandidate> QueryCandidates(int officeId)
    {
        if (_candidates is null || !_rows.Any(row => row.OfficeId == officeId)) return [];
        try { return _candidates(officeId, _assigned); }
        catch (InvalidDataException error) { Status = error.Message; return []; }
    }

    /// <summary>Captures the actual draft appointments for the source single-person
    /// selector. Reading the page does not hypothetically assign any candidate.</summary>
    public AreaAdministrationQuery<OfficerOfficePeopleSelection> ReadPeopleSelection(int officeId)
    {
        if (_peopleSelection is null || !_rows.Any(row => row.OfficeId == officeId))
            return AreaAdministrationQuery<OfficerOfficePeopleSelection>.Unknown(
                _unresolved ?? "officer-office.people.target_slot");
        return _peopleSelection(officeId, _assigned);
    }

    public bool Assign(int officeId, int? personId)
    {
        var row = _rows.FirstOrDefault(row => row.OfficeId == officeId);
        if (_unresolved is not null || row is null || !row.Enabled)
        { Status = _unresolved ?? row?.DisabledReason ?? "office.draft.slot"; return false; }
        if (personId is int id)
        {
            var candidate = QueryCandidates(officeId).FirstOrDefault(person => person.OfficerId == id);
            if (candidate is null || !candidate.Enabled)
            { Status = candidate?.DisabledReason ?? (Status.Length != 0 ? Status : "office.draft.candidate"); return false; }
        }
        var holder = Holder(officeId);
        if (holder is not null) _assigned[holder.Id] = 0x49;
        if (personId is int selected) _assigned[selected] = officeId;
        _availabilityCurrent = false;
        Status = "";
        return true;
    }

    public bool AutoPromote() => Automatic(promote: true);
    public bool AutoAssign() => Automatic(promote: false);

    public bool DismissAll()
    {
        if (!CanDismissAll) { Status = _unresolved ?? "office.draft.dismiss"; return false; }
        foreach (int person in _removable) _assigned[person] = 0x49;
        _availabilityCurrent = false;
        Status = ""; return true;
    }

    /// <summary>The original clear action discards pending changes.</summary>
    public bool Clear()
    {
        if (_unresolved is not null) { Status = _unresolved; return false; }
        _assigned = new(_original); _availabilityCurrent = false; Status = ""; return true;
    }

    public SetOfficerOfficesOrder? BuildOrder()
    {
        if (!CanCommit) return null;
        var assignments = new List<OfficerOfficeAssignment>();
        var manual = _rows.Select(row => row.OfficeId).ToHashSet();
        // A force can still hold an office which its current title no longer
        // permits assigning. The original dismiss action can release it.
        foreach (var (person, previous) in _original)
            if (previous != 0x49 && !manual.Contains(previous) && _assigned[person] == 0x49 &&
                assignments.All(row => row.OfficeId != previous))
                assignments.Add(new(previous, null));
        foreach (var row in _rows)
        {
            int? next = Holder(row.OfficeId)?.Id;
            if (next != row.CurrentOfficer?.Id) assignments.Add(new(row.OfficeId, next));
        }
        if (assignments.Count == 0) { Status = "office.draft.unrepresentable_changes"; return null; }
        return new(_forceId, assignments);
    }

    private bool Automatic(bool promote)
    {
        if (_automatic is null) { Status = _unresolved ?? "office.draft.automatic"; return false; }
        var available = Availability();
        if (!(promote ? available.Promote : available.Assign)) return false;
        try { _assigned = _automatic(_assigned, promote); _availabilityCurrent = false; Status = ""; return true; }
        catch (InvalidDataException error) { Status = error.Message; return false; }
    }

    private (bool Promote, bool Assign) Availability()
    {
        if (_availability is null) return (false, false);
        if (_availabilityCurrent) return _available;
        try { _available = _availability(_assigned); }
        catch (InvalidDataException error) { Status = error.Message; _available = (false, false); }
        _availabilityCurrent = true; return _available;
    }

    private OfficeAppointmentPerson? Holder(int officeId)
    {
        // Dictionary insertion order is the captured native Person order.
        foreach (var (person, office) in _assigned)
            if (office == officeId && _people.TryGetValue(person, out var found)) return found;
        return null;
    }

    private int CountChanges(bool promoted)
    {
        int count = 0;
        foreach (var (person, previous) in _original)
        {
            if (!_assigned.TryGetValue(person, out int next) || previous == next) continue;
            if (!_grades.TryGetValue(previous, out int oldGrade) || !_grades.TryGetValue(next, out int newGrade)) continue;
            if (promoted ? newGrade < oldGrade : newGrade > oldGrade) count++;
        }
        return count;
    }
}
