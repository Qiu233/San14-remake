namespace San14.Data;

/// <summary>The scenario-description resource ranges, extracted offline from
/// both TC and SC. The normal message text still belongs to Data section0.</summary>
internal static class ScenarioAnnotationMessageIndex
{
    private readonly record struct Range(int Start, int End, int Base, int Flag);

    private static readonly Range[] Ranges =
    [
        new(1, 19, 2418, 0),
        new(21, 25, 3406, 0),
        new(26, 29, 3666, 0),
        new(30, 35, 3874, 0),
        new(55, 57, 4186, 0),
        new(58, 62, 4342, 0),
        new(63, 63, 4602, 0),
        new(100, 199, 4943, 1)
    ];

    //30C440: first inclusive range, signed force/stride comparison and
    //unchecked UInt32 index bound. Null is the proved original empty result.
    internal static int? Read(int scenarioId, int forcePhysicalSlot)
    {
        foreach (var row in Ranges)
        {
            if (scenarioId < row.Start || scenarioId > row.End) continue;
            int stride = row.Flag != 0 ? 3 : 52;
            uint index = forcePhysicalSlot < stride
                ? unchecked((uint)(((scenarioId >= 150 ? scenarioId - 45 : scenarioId) - row.Start)
                    * stride + row.Base + forcePhysicalSlot))
                : unchecked((uint)row.Base);
            return index <= 0x1808 ? (int)index : null;
        }
        return null;
    }
}
