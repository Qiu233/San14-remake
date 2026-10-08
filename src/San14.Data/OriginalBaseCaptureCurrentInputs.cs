using San14.Core;

namespace San14.Data;

/// <summary>Explicit fresh CHex2213B0/City220B70/World326230 allocation,
/// followed by City236E80's actual static C9 load. Bind never repeats it.</summary>
public static class OriginalBaseCaptureCurrentInputs
{
    public const string Revision = "ce-base-capture-current-fields-20261008";

    public static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        if (state.NativeBaseCaptureCurrent is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation?.Parameters.Language != source.Snapshot.Locale ||
            source.Metadata.FormatMarker != 0x66 || state.MapWidth != 220 || state.MapHeight != 220 ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            source.Snapshot.Cities.Count != 52)
            throw new InvalidDataException("Base capture fields need their unused fresh CE campaign.");
        var cities = new Dictionary<int, NativeCityCaptureFields>();
        for (int slot = 0; slot < 52; slot++)
        {
            var row = source.Snapshot.Cities[slot];
            if (row.Slot != slot) throw new InvalidDataException("Base capture City source identity is inconsistent.");
            byte[] tail = Convert.FromHexString(row.RemainingBytesHex);
            if (tail.Length != 8) throw new InvalidDataException("Base capture C9 needs the complete CE23-byte City row.");
            //CE row22 is physical C9. Static236E80 reads it even with8C=1;
            //CF is skipped in that mode and retains ctor qwordC8=10's zero.
            cities.Add(slot, new(tail[7], 0));
        }
        //2213B0's unaligned qword+16=0 contains word16. Map/CE static
        //serializers do not overwrite it.326230 clears1560..1627, including
        //the full word1562. These are explicit allocation writes, not defaults.
        var hexes = new ushort?[48400];
        Array.Fill(hexes, (ushort)0);
        var result = state with { NativeBaseCaptureCurrent = new(Revision, hexes, cities, 0) };
        NativeBaseCaptureCurrentStateValidation.Validate(result);
        return result;
    }

    public static void BindCurrent(GameState state, OriginalEventInitializationContext current)
    {
        NativeBaseCaptureCurrentStateValidation.Validate(state);
        current.BindBaseCaptureCurrent(state.NativeBaseCaptureCurrent);
    }

    /// <summary>Merge only fields written by this operation. A baseline
    /// capture never overwrites newer unrelated state supplied by its caller.</summary>
    public static GameState PublishCurrent(GameState state, OriginalEventInitializationContext current)
    {
        if (!current.HasBaseCaptureCurrentWrites) return state;
        var owner = state.NativeBaseCaptureCurrent
            ?? throw new InvalidDataException("Reached Base capture writer has no saved current owner.");
        NativeBaseCaptureCurrentStateValidation.Validate(state);
        var hexes = current.CaptureHex16Writes.Count == 0 ? owner.HexWords16 : owner.HexWords16.ToArray();
        foreach (int slot in current.CaptureHex16Writes) hexes[slot] = current.ReadCaptureHexWord16(slot);
        var cities = current.CaptureCityC9Writes.Count == 0 && current.CaptureCityCFWrites.Count == 0
            ? owner.Cities : new Dictionary<int, NativeCityCaptureFields>(owner.Cities);
        foreach (int slot in current.CaptureCityC9Writes)
            cities[slot] = cities[slot] with { C9 = current.ReadCaptureCityByteC9(slot) };
        foreach (int slot in current.CaptureCityCFWrites)
            cities[slot] = cities[slot] with { CF = current.ReadCaptureCityByteCF(slot) };
        var result = state with { NativeBaseCaptureCurrent = owner with
        {
            HexWords16 = hexes, Cities = cities,
            World1562 = current.CaptureWorld1562Written ? current.ReadCaptureWorld1562() : owner.World1562
        } };
        current.ClearBaseCaptureCurrentWrites();
        return result;
    }

    public static ushort? ReadWorld1562(GameState state) => state.NativeBaseCaptureCurrent?.World1562;

    public static GameState WithWorld1562(GameState state, ushort? value)
    {
        var owner = state.NativeBaseCaptureCurrent
            ?? throw new InvalidDataException("World1562 writer has no saved current Base capture owner.");
        return state with { NativeBaseCaptureCurrent = owner with { World1562 = value } };
    }
}
