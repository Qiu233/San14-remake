namespace San14.Data;

// Offline-extracted ordinary Formation list profile. Only data descriptors
// are retained here; the game does not read executable or analysis files.
internal static class FormationInformationColumnsSource
{
    internal sealed record Definition(int ColumnId, int Width, int Flags, int CaptionId, int SortMode);
    internal sealed record Group(int CaptionId, int[] ColumnIds);
    internal static IReadOnlyDictionary<int, Definition> Rows { get; } = new Dictionary<int, Definition>
    {
        [0] = new(0, 76, 0x00000008, 0x22B, 0),
        [1] = new(1, 76, 0x00000001, 0x41C, 2),
        [2] = new(2, 1140, 0x20000001, 0x400, 2),
        [3] = new(3, 76, 0x20000002, 0x22C, 0),
        [4] = new(4, 76, 0x20000002, 0x22D, 0),
        [5] = new(5, 76, 0x20000002, 0x230, 0),
        [6] = new(6, 76, 0x20000002, 0x22E, 0),
        [7] = new(7, 76, 0x20000002, 0x22F, 0),
        [8] = new(8, 104, 0x20000008, 0x36A, 0),
        [9] = new(9, 76, 0x00000002, 0x403, 0),
        [10] = new(10, 132, 0x20000008, 0x396, 1),
        [11] = new(11, 132, 0x20000008, 0x397, 1),
    };
    internal static IReadOnlyList<Group> Groups { get; } = Array.AsReadOnly<Group>
    ([new(0x2DC, [0, 1, 2]), new(0x35F, [0, 1, 9, 8, 10, 11, 7, 3, 4, 5, 6])]);
}
