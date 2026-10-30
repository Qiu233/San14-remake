using System.Collections.Immutable;

namespace San14.Data;

/// <summary>Authored nine-sample graph node identities. Position and size
/// nodes are separate arrays even though each source table is consecutive.</summary>
public static class IncomeReportGraphNodeTables
{
    public static ImmutableArray<int> PositionNodes { get; } = [11, 12, 13, 14, 15, 16, 17, 18, 19];
    public static ImmutableArray<int> SizeNodes { get; } = [2, 3, 4, 5, 6, 7, 8, 9, 10];
}
