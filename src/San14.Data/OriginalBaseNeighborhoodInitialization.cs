namespace San14.Data;

public interface IOriginalBaseNeighborhoodState : IOriginalHexReferenceListState
{
    IReadOnlyList<OriginalBaseReference?>? ReadRegisteredBases();
    bool? ReadBaseNativeValidity(OriginalBaseReference reference);
    OriginalHexReference? FindBaseHex(OriginalBaseReference reference);
    int? ReadBaseNativeKind(OriginalBaseReference reference);
    int? ReadHexPhysicalSlot(OriginalHexReference reference);
    OriginalHexReference? ReadHexTableReference(int slot);
    byte? ReadHexSurfaceCode(OriginalHexReference reference);
}

public sealed record OriginalBaseNeighborhoodResult(OriginalBaseReference Base,
    OriginalHexReference Center, int SuccessfulAppends);

/// <summary>3306E0 after the static area-city loop, using the current shared native lists.</summary>
public static class OriginalBaseNeighborhoodInitialization
{
    public static IReadOnlyList<OriginalBaseNeighborhoodResult> Apply(IOriginalBaseNeighborhoodState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IReadOnlyList<OriginalBaseReference?> bases = state.ReadRegisteredBases()
            ?? throw new InvalidDataException("Current registered base sequence is unknown.");
        var results = new List<OriginalBaseNeighborhoodResult>();
        foreach (OriginalBaseReference? item in bases)
        {
            OriginalBaseReference reference = Pointer(item, "registered base payload");
            if (!reference.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadBaseNativeValidity(reference), "base validity")) continue;
            state.ClearBaseHexReferences(reference);
            OriginalHexReference center = Pointer(state.FindBaseHex(reference), "base representative hex");
            int appended = state.AppendBaseHexReference(reference, center) ? 1 : 0;
            bool city = Known(state.ReadBaseNativeKind(reference), "base slot2 first read") == 5;
            // Native code repeats slot2 after a non-city result rather than caching it.
            bool gate = !city && Known(state.ReadBaseNativeKind(reference), "base slot2 second read") == 6;
            if (city || gate)
            {
                OriginalHexListHandle temporary = Pointer(state.CreateTemporaryHexList(), "temporary descriptor allocation");
                _ = PopulateRadiusOne(center, temporary, state);
                OriginalHexListNodeReference node = Pointer(state.ReadHexListHead(temporary), "temporary head");
                while (node.Identity.HasValue)
                {
                    OriginalHexReference hex = Pointer(state.ReadHexListNodePayload(node), "temporary node payload");
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
    public static int PopulateRadiusOne(OriginalHexReference center, OriginalHexListHandle temporary,
        IOriginalBaseNeighborhoodState state)
        => PopulateRadiusOne(center, temporary, state, () => state.ClearTemporaryHexList(temporary));

    /// <summary>Allows the caller's actual list wrapper to own its clear/lock
    /// operation while retaining the same original radius walk.</summary>
    public static int PopulateRadiusOne(OriginalHexReference center, OriginalHexListHandle temporary,
        IOriginalBaseNeighborhoodState state, Action clear)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(temporary);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(clear);
        clear();
        int physical = Known(state.ReadHexPhysicalSlot(center), "center physical identity");
        foreach (int key in OriginalHexNeighborhood.RadiusOneCandidates(physical))
        {
            OriginalHexReference hex = Pointer(state.ReadHexTableReference(key), $"candidate hex {key}");
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
