using San14.Core;

namespace San14.Data;

public sealed record CaptureBaseThreatFields(byte? Byte49, byte? Byte4B, byte? Byte62, ushort? Word64)
{
    public byte? Byte4A { get; init; }
    public byte? Byte48 { get; init; }
    public ushort? Word4C { get; init; }
}

/// <summary>Fresh Base threat fields and the resource-owned automatic-tactics
/// setting. Runtime-only fields survive the shared constructor; byte48 comes
/// from the CE scenario's static prefix. Restored worlds are never filled.</summary>
public static class CaptureBaseThreatInputs
{
    public const string VersionKey = "ce-capture-base-threat-current:version";
    public static string Key(BaseReference target, int offset) =>
        $"ce-capture-base-threat-current:{(target.Kind == BaseKind.City ? "city" : "gate")}:{target.PhysicalSlot}:{offset:X}";
    public static GameState InstallFresh(GameState state, LoadedScenario? source = null)
    {
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Fresh Base threat source parameters are missing.");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Base threat runtime fields require an unused fresh allocation.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var target in All())
        {
            values.Add(Key(target, 0x49), 0); values.Add(Key(target, 0x4B), 0);
            values.Add(Key(target, 0x4A), 0); values.Add(Key(target, 0x4C), 0);
            values.Add(Key(target, 0x62), 255); values.Add(Key(target, 0x64), 0);
            //237920 serializes48 in the static prefix immediately after46.
            //Unlike49/4B, its live initial value comes from the resource row,
            //not the constructor's earlier zero. Never fill restored worlds.
            if (source?.Metadata.FormatMarker == 0x66)
            {
                int slot = target.PhysicalSlot!.Value;
                byte? tactics = target.Kind == BaseKind.City
                    ? source.Snapshot.Cities[slot].RawAutomaticTactics48
                    : source.Snapshot.Gates[slot].RawAutomaticTactics48;
                if (tactics.HasValue) values.Add(Key(target, 0x48), tactics.Value);
            }
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    public static CaptureBaseThreatFields? ReadCurrent(GameState state, BaseReference target)
    {
        if (state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1) return null;
        return new(Byte(0x49), Byte(0x4B), Byte(0x62), Word(0x64))
            { Byte48 = Byte(0x48), Byte4A = Byte(0x4A), Word4C = Word(0x4C) };
        byte? Byte(int offset) { int? value = Value(offset); return value is >= 0 and <= 255 ? (byte?)value : null; }
        ushort? Word(int offset) { int? value = Value(offset); return value is >= 0 and <= 65535 ? (ushort?)value : null; }
        int? Value(int offset)
        {
            var parameters = state.NativeArmySimulation.Parameters; string key = Key(target, offset);
            return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value) ? value : null;
        }
    }
    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        if (current.CaptureBaseThreatInputsBound) return;
        foreach (var target in All()) current.SetCaptureBaseThreatFields(target, ReadCurrent(state, target));
        //City3C is already captured from the owned City.Troops. Gates require
        //their own current3C overlay; a static scenario troop row is not current.
        foreach (var gate in state.Gates.Values)
            current.SetBaseResource(new(BaseKind.Gate, gate.Id - 51), BaseResourceKind.Troops, gate.Troops);
        current.CaptureBaseThreatInputsBound = true;
    }
    public static GameState PublishCurrent(GameState state, SourceWorldContext current)
    {
        if (!current.CaptureBaseThreatInputsBound || state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1) return state;
        var simulation = state.NativeArmySimulation; var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var target in All())
        {
            var fields = current.ReadCaptureBaseThreatFields(target);
            Put(0x49, fields?.Byte49); Put(0x4B, fields?.Byte4B); Put(0x62, fields?.Byte62); Put(0x64, fields?.Word64);
            Put(0x4A, fields?.Byte4A); Put(0x4C, fields?.Word4C);
            Put(0x48, fields?.Byte48);
            void Put(int offset, int? value) { string key = Key(target, offset); if (value.HasValue) values[key] = value.Value; else values.Remove(key); }
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    private static IEnumerable<BaseReference> All()
    {
        for (int city = 0; city < 52; city++) yield return new(BaseKind.City, city);
        for (int gate = 0; gate < 11; gate++) yield return new(BaseKind.Gate, gate);
    }
}

public sealed partial class SourceWorldContext
{
    internal bool CaptureBaseThreatInputsBound { get; set; }
    private readonly Dictionary<BaseReference, CaptureBaseThreatFields?> _captureBaseThreatFields = [];
    public void SetCaptureBaseThreatFields(BaseReference target, CaptureBaseThreatFields? fields)
    { _ = BasePhysical(target); _captureBaseThreatFields[target] = fields; }
    public CaptureBaseThreatFields? ReadCaptureBaseThreatFields(BaseReference target)
    { _ = BasePhysical(target); return _captureBaseThreatFields.GetValueOrDefault(target); }
}
