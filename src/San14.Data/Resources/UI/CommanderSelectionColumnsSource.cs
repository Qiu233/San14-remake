using System.Collections.Immutable;

namespace San14.Data;

/// <summary>The commander's five ordinary Person-list pages. Column identities
/// and widths are resource-format descriptors, shared with the mature readers.</summary>
internal static class CommanderSelectionColumnsSource
{
    internal static ImmutableArray<OfficerInformationGroup> Create(Func<int, int, string> text)
    {
        var columns = OfficerInformationSources.Create(text).SelectMany(group => group.Columns)
            .DistinctBy(column => column.Id).ToDictionary(column => column.Id);
        columns.Add(36, new(36, 0x223, text(1, 0x223), 76, 4, false, true, true));
        return [
            Page(0, 0x3CD, [0, 5, 36, 4, 8, 9, 10, 11, 12, 18]),
            Page(1, 0x3DD, [0, 51, 52, 53, 54, 55]),
            Page(2, 0x3DE, [0, 56, 57, 58, 59, 60]),
            Page(3, 0x23D, [0, 61, 62, 63, 64, 65]),
            Page(4, 0x451, [0, 29, 28, 24, 30, 31, 32, 33])
        ];

        OfficerInformationGroup Page(int index, int caption, int[] ids) =>
            new(index, caption, text(1, caption), ids.Select(id => columns[id]).ToImmutableArray());
    }
}
