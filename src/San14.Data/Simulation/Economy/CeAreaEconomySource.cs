using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>Static CE serializer inputs consumed by the income calculation.
/// These fields are coefficients and an opaque source amount, not development progress.</summary>
public static class CeAreaEconomySource
{
    public static AreaEconomySourceInputs Project(AreaPreviewRecord area, LoadedScenario loaded)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(loaded);
        if (loaded.Metadata.FormatMarker != 0x66)
            throw new InvalidDataException("Area economy input projection requires the inspected CE source profile.");
        if (area.RawField44 is not ushort raw44 || area.RawField46 is not ushort raw46)
            throw new InvalidDataException($"Area {area.Slot} has no verified source income words.");
        byte[] fixedBytes = Convert.FromHexString(area.RemainingBytesHex);
        byte[] scenarioBytes = Convert.FromHexString(area.StateRemainingBytesHex);
        if (fixedBytes.Length != 26 || scenarioBytes.Length != 4 ||
            BinaryPrimitives.ReadUInt16LittleEndian(fixedBytes.AsSpan(2, 2)) != raw44 ||
            BinaryPrimitives.ReadUInt16LittleEndian(fixedBytes.AsSpan(4, 2)) != raw46)
            throw new InvalidDataException($"Area {area.Slot} source income words differ from retained source bytes.");
        ValidateHash(loaded.FixedDataSource.Sha256);
        ValidateHash(loaded.ScenarioSource.Sha256);
        var sourceRow = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(sourceRow, area.Amount0);
        sourceRow[2] = area.Value2;
        sourceRow[3] = area.ForceSlot;
        scenarioBytes.CopyTo(sourceRow, 4);
        return new(raw44, raw46, area.Amount0,
            Convert.ToHexString(fixedBytes), Convert.ToHexString(scenarioBytes),
            loaded.FixedDataSource.Sha256, loaded.ScenarioSource.Sha256)
        { ScenarioSourceBytesHex = Convert.ToHexString(sourceRow) };
    }

    public static GameState ApplyStaticSource(GameState state, LoadedScenario loaded)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(loaded);
        if (state.Phase != TurnPhase.Initializing)
            throw new InvalidOperationException("Static economy sources may only be applied during initialization.");
        var source = loaded.Snapshot.Areas.ToDictionary(area => area.Slot);
        var projected = new Dictionary<int, AreaState>();
        foreach ((int id, AreaState area) in state.Areas)
        {
            if (!source.TryGetValue(id, out AreaPreviewRecord? record))
                throw new InvalidDataException($"Current area {id} has no source economy record.");
            projected.Add(id, area with { EconomySource = Project(record, loaded) });
        }
        return state with { Areas = projected };
    }

    private static void ValidateHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(value => !Uri.IsHexDigit(value)))
            throw new InvalidDataException("Area economy source SHA-256 is invalid.");
    }
}
