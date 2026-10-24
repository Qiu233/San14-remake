namespace San14.Core;

/// <summary>Starts an owned personnel journey. Arrival owns discovery and recruitment.</summary>
public readonly record struct TalentOrderOutcome(int? JoinedOfficerId, ulong RandomState)
{
    public GameState? State { get; init; }
    public IReadOnlyList<PeriodReportEntry> Reports { get; init; } = [];
}

public interface IPlayableTalentResolution
{
    TalentOrderOutcome Resolve(GameState current, PreparedTalentOrder task);
}

/// <summary>
/// Compatibility binding for callers that have not installed the talent source rules.
/// </summary>
public sealed class ProvisionalTalentResolution : IPlayableTalentResolution
{
    public TalentOrderOutcome Resolve(GameState current, PreparedTalentOrder task)
    {
        throw new InvalidDataException("talent.current_resolution_rules");
    }
}
