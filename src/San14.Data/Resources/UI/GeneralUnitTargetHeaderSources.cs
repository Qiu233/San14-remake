namespace San14.Data;

/// <summary>The ordinary CDispatchHeader resource contract. Aggregates are
/// supplied by the current multi-source deployment draft.</summary>
public static class GeneralUnitTargetHeaderSources
{
    public const string LayoutName="06_01_11_00_shuppei";
    public const string ContentLayoutName="06_01_11_00_shuppei_offset_00";
    public const int ContentMountNode=1;
    public static IReadOnlyList<(int Node,int Message)> Captions {get;} =
        Array.AsReadOnly(new (int,int)[] {(3,0x32D),(5,0x35B),(7,0x54E),
            (9,0x39F),(11,0x35C),(14,0x292)});
}
