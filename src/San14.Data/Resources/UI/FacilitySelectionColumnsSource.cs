namespace San14.Data;

/// <summary>Two actual facility selection groups and their independent
/// source display, format and sort declarations, imported offline.</summary>
internal static class FacilitySelectionColumnsSource
{
    internal sealed record Column(int Id, int Width, uint Flags, int Caption, bool Sortable);
    internal sealed record Group(int Caption, int[] Columns);
    internal static readonly Column[] Columns =
    [
        new(0,132,1,0x20A,true), new(1,132,0x20000008,0x401,true),
        new(2,132,8,0x402,true), new(12,76,2,0x403,true),
        new(13,76,0x20000002,0x20B,true), new(5,132,0x20000008,0x486,true),
        new(6,160,8,0x404,true), new(7,160,8,0x405,true),
        new(8,132,8,0x45B,true), new(11,580,1,0x412,false)
    ];
    internal static readonly Group[] Groups =
    [new(0x3CD,[0,1,2,12,13,5,6,7,8]), new(0x412,[0,11])];
}
