namespace San14.Core;

/// <summary>The selected application's accumulated command-book usage and grants.
/// Null counters retain unknown knowledge until their actual writer supplies it.</summary>
public sealed record ApplicationCommandAccountingState(uint? CommandBooksUsed, uint? CommandBooksGranted);

public sealed partial record GameState
{
    public ApplicationCommandAccountingState? ApplicationCommandAccounting { get; init; }
}
