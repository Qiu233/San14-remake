using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>An ordered commander selection. ActiveCount describes the active
/// prefix, including cleared entries; it is not the number of occupied entries.
/// Unknown count and unknown candidate storage remain independent knowledge.</summary>
public sealed class CommanderSelectionState
{
    private readonly CommanderSelectionCandidate[]? _candidates;

    [JsonConstructor]
    public CommanderSelectionState(int? activeCount, CommanderSelectionCandidate[]? candidates)
    {
        if (activeCount < 0 || candidates?.Any(candidate => candidate is null || candidate.OfficerId < 0) == true ||
            candidates is not null && activeCount > candidates.Length)
            throw new InvalidDataException("Commander selection has an invalid active prefix.");
        ActiveCount = activeCount;
        _candidates = candidates?.ToArray();
    }

    public int? ActiveCount { get; }
    public CommanderSelectionCandidate[]? Candidates => _candidates?.ToArray();
    public CommanderSelectionCandidate CandidateAt(int ordinal) =>
        _candidates?[ordinal] ?? throw new InvalidOperationException("Commander candidate storage is unknown.");

    /// <summary>Clears only the requested entries. Their positions and the
    /// active prefix remain intact, as do all inactive and unknown entries.</summary>
    public CommanderSelectionState ClearAt(IEnumerable<int> ordinals)
    {
        if (_candidates is null || ActiveCount is not int count)
            throw new InvalidOperationException("Clearing a selection requires its active prefix and storage.");
        var next = _candidates.ToArray();
        foreach (int ordinal in ordinals)
        {
            if (ordinal < 0 || ordinal >= count)
                throw new ArgumentOutOfRangeException(nameof(ordinals));
            next[ordinal] = CommanderSelectionCandidate.Cleared;
        }
        return new(count, next);
    }
}

/// <summary>The source's signed auxiliary word. The normal selector preserves
/// it for an existing identity and writes World454 for a newly selected person.
/// Older saves may retain independent unknown knowledge.</summary>
public sealed record CommanderSelectionCandidate(int? OfficerId, int? UninterpretedValue)
{
    public static CommanderSelectionCandidate Cleared { get; } = new(0, 0);
}

public sealed partial record GameState
{
    public CommanderSelectionState? CommanderSelection { get; init; }
}
