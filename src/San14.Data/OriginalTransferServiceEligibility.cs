namespace San14.Data;

public sealed record OriginalTransferModeState(bool Present, int? Value);
public interface IOriginalTransferServiceEligibilityQueries
{
    uint? ReadWorldTransferServiceFlags();
    OriginalTransferModeState? ReadTransferModeState();
}

/// <summary>3337D0, including the repeated external-mode lookup and second world16A8 read.</summary>
public static class OriginalTransferServiceEligibility
{
    private static readonly int[] NativeBlockedBits = [2, 8, 3, 4, 5, 6, 21, 22, 18];
    public static bool IsEligible(IOriginalTransferServiceEligibilityQueries queries)
    {
        ArgumentNullException.ThrowIfNull(queries);
        uint first = Known(queries.ReadWorldTransferServiceFlags(), "initial world16A8");
        if ((first & 0x100) != 0) return false;
        OriginalTransferModeState mode = Pointer(queries.ReadTransferModeState());
        if (mode.Present)
        {
            mode = Pointer(queries.ReadTransferModeState());
            if (!mode.Present) throw new InvalidDataException("Current external mode pointer became null before its native dereference.");
            if (Known(mode.Value, "external mode value") != 0) return false;
        }
        uint current = Known(queries.ReadWorldTransferServiceFlags(), "current world16A8");
        if ((current & 0x78) != 0) return false;
        foreach (int bit in NativeBlockedBits)
            if ((current & (1u << bit)) != 0) return false;
        return true;
    }
    private static OriginalTransferModeState Pointer(OriginalTransferModeState? value) => value
        ?? throw new InvalidDataException("Current external transfer mode pointer is unknown.");
    private static T Known<T>(T? value, string name) where T : struct => value
        ?? throw new InvalidDataException($"Current transfer service eligibility input {name} is unknown.");
}
