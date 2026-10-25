namespace San14.Core;

/// <summary>The selected application's accumulated command-book usage and grants.
/// Null counters retain unknown knowledge until their actual writer supplies it.</summary>
public sealed record ApplicationCommandAccountingState(uint? CommandBooksUsed, uint? CommandBooksGranted)
{
    public uint? SingleLinkedTacticCount { get; init; }
    public uint? DoubleLinkedTacticCount { get; init; }
    public uint? TripleLinkedTacticCount { get; init; }
    public uint? LargeLinkedTacticCount { get; init; }
    public uint? ManualTacticUseCount { get; init; }
}

public sealed partial record GameState
{
    public ApplicationCommandAccountingState? ApplicationCommandAccounting { get; init; }
}
