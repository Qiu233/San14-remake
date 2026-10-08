namespace San14.Data;

/// <summary>
/// Current area queries and the summary receiver captured by the caller.
/// Writes must target that same receiver throughout one Apply invocation.
/// </summary>
public interface IOriginalAreaMapStatisticsSummaryState
{
    OriginalAreaReference? ReadAreaTableReference(int slot);
    bool? ReadAreaNativeValidity(OriginalAreaReference area);
    ushort? ReadAreaRawField68(OriginalAreaReference area);
    int? ReadAreaPhysicalSlot(OriginalAreaReference area);
    void WriteAreaMapStatisticsSummaryWord(int physicalSlot, ushort value);
}

/// <summary>
/// Complete 928490: copy current valid areas' field68 words into the separate
/// summary cache, in pointer-table order. This operation does not reset the
/// cache or its dirty bits and does not infer values from a static scenario.
/// </summary>
public static class OriginalAreaMapStatisticsSummary
{
    public static void Apply(IOriginalAreaMapStatisticsSummaryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        for (int slot = 0; slot < 501; slot++)
        {
            OriginalAreaReference area = state.ReadAreaTableReference(slot)
                ?? throw new InvalidDataException($"Current summary area pointer {slot} is unknown.");
            if (!area.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadAreaNativeValidity(area), "area native validity")) continue;

            // Native code saves this word before calling the physical getter.
            ushort value = Known(state.ReadAreaRawField68(area), "area field68");
            int physical = Known(state.ReadAreaPhysicalSlot(area), "area physical identity");
            if (physical is < 0 or > 500)
                throw new InvalidDataException("Summary area identity is outside the supported native allocation.");
            state.WriteAreaMapStatisticsSummaryWord(physical, value);
        }
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area map statistics summary {field} is unknown.");
}
