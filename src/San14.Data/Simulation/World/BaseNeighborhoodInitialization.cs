namespace San14.Data;

public interface IBaseNeighborhoodState : IHexReferenceListState
{
    IReadOnlyList<BaseReference?>? ReadRegisteredBases();
    bool? ReadBaseNativeValidity(BaseReference reference);
    HexReference? FindBaseHex(BaseReference reference);
    int? ReadBaseNativeKind(BaseReference reference);
    int? ReadHexPhysicalSlot(HexReference reference);
    HexReference? ReadHexTableReference(int slot);
    byte? ReadHexSurfaceCode(HexReference reference);
}

public sealed record BaseNeighborhoodResult(BaseReference Base,
    HexReference Center, int SuccessfulAppends);

/// <summary>3306E0 after the static area-city loop, using the current shared native lists.</summary>
public static class BaseNeighborhoodInitialization
{
    public static IReadOnlyList<BaseNeighborhoodResult> Apply(IBaseNeighborhoodState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IReadOnlyList<BaseReference?> bases = state.ReadRegisteredBases()
            ?? throw new InvalidDataException("Current registered base sequence is unknown.");
        var results = new List<BaseNeighborhoodResult>();
        foreach (BaseReference? item in bases)
        {
            BaseReference reference = Pointer(item, "registered base payload");
            if (!reference.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadBaseNativeValidity(reference), "base validity")) continue;
            state.ClearBaseHexReferences(reference);
            HexReference center = Pointer(state.FindBaseHex(reference), "base representative hex");
            int appended = state.AppendBaseHexReference(reference, center) ? 1 : 0;
            bool city = Known(state.ReadBaseNativeKind(reference), "base slot2 first read") == 5;
            // Native code repeats slot2 after a non-city result rather than caching it.
            bool gate = !city && Known(state.ReadBaseNativeKind(reference), "base slot2 second read") == 6;
            if (city || gate)
            {
                HexListHandle temporary = Pointer(state.CreateTemporaryHexList(), "temporary descriptor allocation");
                _ = PopulateRadiusOne(center, temporary, state);
                HexListNodeReference node = Pointer(state.ReadHexListHead(temporary), "temporary head");
                while (node.Identity.HasValue)
                {
                    HexReference hex = Pointer(state.ReadHexListNodePayload(node), "temporary node payload");
                    if (city || Known(state.ReadHexSurfaceCode(hex), "gate neighboring hex surface10") == 17)
                        if (state.AppendBaseHexReference(reference, hex)) appended++;
                    node = Pointer(state.ReadHexListNodeNext(node), "temporary node next");
                }
                // The original success path clears nodes before releasing the descriptor.
                // A preceding failure retains effects; there is no synthetic finally rollback.
                state.ClearTemporaryHexList(temporary);
                state.ReleaseTemporaryHexList(temporary);
            }
            results.Add(new(reference, center, appended));
        }
        return results.AsReadOnly();
    }

    /// <summary>The actual radius1 invocation of 2A6F40, including its ignored count read.</summary>
    public static int PopulateRadiusOne(HexReference center, HexListHandle temporary,
        IBaseNeighborhoodState state)
        => PopulateRadiusOne(center, temporary, state, () => state.ClearTemporaryHexList(temporary));

    /// <summary>Allows the caller's actual list wrapper to own its clear/lock
    /// operation while retaining the same original radius walk.</summary>
    public static int PopulateRadiusOne(HexReference center, HexListHandle temporary,
        IBaseNeighborhoodState state, Action clear)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(temporary);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(clear);
        clear();
        int physical = Known(state.ReadHexPhysicalSlot(center), "center physical identity");
        foreach (int key in HexNeighborhood.RadiusOneCandidates(physical))
        {
            HexReference hex = Pointer(state.ReadHexTableReference(key), $"candidate hex {key}");
            // Native32F0B0(null) is false; every nonnull CHex has slot3=true, including0.
            if (!hex.PhysicalSlot.HasValue) continue;
            _ = state.AppendTemporaryHexReference(temporary, hex);
        }
        return Known(state.ReadTemporaryHexListCount(temporary), "temporary current count");
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current base neighborhood input {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current base neighborhood {field} is unknown.");
}
