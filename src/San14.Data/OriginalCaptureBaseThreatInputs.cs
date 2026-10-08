using San14.Core;

namespace San14.Data;

public sealed record OriginalCaptureBaseThreatFields(byte? Byte49, byte? Byte4B, byte? Byte62, ushort? Word64);

/// <summary>Once-created CFoothold49/4B/62/64 runtime fields. These fields
/// are omitted by static237920 and survive from the actual shared ctor.
/// Current queries and future status writers share the saved numeric bank.</summary>
public static class OriginalCaptureBaseThreatInputs
{
    public const string VersionKey = "ce-capture-base-threat-current:version";
    public static string Key(OriginalBaseReference target, int offset) =>
        $"ce-capture-base-threat-current:{(target.Kind == OriginalBaseKind.City ? "city" : "gate")}:{target.PhysicalSlot}:{offset:X}";
    public static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Fresh Base threat source parameters are missing.");
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Base threat runtime fields require an unused fresh allocation.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var target in All())
        {
            values.Add(Key(target, 0x49), 0); values.Add(Key(target, 0x4B), 0);
            values.Add(Key(target, 0x62), 255); values.Add(Key(target, 0x64), 0);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    public static OriginalCaptureBaseThreatFields? ReadCurrent(GameState state, OriginalBaseReference target)
    {
        if (state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1) return null;
        return new(Byte(0x49), Byte(0x4B), Byte(0x62), Word(0x64));
        byte? Byte(int offset) { int? value = Value(offset); return value is >= 0 and <= 255 ? (byte?)value : null; }
        ushort? Word(int offset) { int? value = Value(offset); return value is >= 0 and <= 65535 ? (ushort?)value : null; }
        int? Value(int offset)
        {
            var parameters = state.NativeArmySimulation.Parameters; string key = Key(target, offset);
            return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value) ? value : null;
        }
    }
    public static void BindCurrent(GameState state, OriginalEventInitializationContext current)
    {
        if (current.CaptureBaseThreatInputsBound) return;
        foreach (var target in All()) current.SetCaptureBaseThreatFields(target, ReadCurrent(state, target));
        //City3C is already captured from the owned City.Troops. Gates require
        //their own current3C overlay; a static scenario troop row is not current.
        foreach (var gate in state.Gates.Values)
            current.SetBaseResource(new(OriginalBaseKind.Gate, gate.Id - 51), OriginalBaseResourceKind.Troops, gate.Troops);
        current.CaptureBaseThreatInputsBound = true;
    }
    public static GameState PublishCurrent(GameState state, OriginalEventInitializationContext current)
    {
        if (!current.CaptureBaseThreatInputsBound || state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1) return state;
        var simulation = state.NativeArmySimulation; var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var target in All())
        {
            var fields = current.ReadCaptureBaseThreatFields(target);
            Put(0x49, fields?.Byte49); Put(0x4B, fields?.Byte4B); Put(0x62, fields?.Byte62); Put(0x64, fields?.Word64);
            void Put(int offset, int? value) { string key = Key(target, offset); if (value.HasValue) values[key] = value.Value; else values.Remove(key); }
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    private static IEnumerable<OriginalBaseReference> All()
    {
        for (int city = 0; city < 52; city++) yield return new(OriginalBaseKind.City, city);
        for (int gate = 0; gate < 11; gate++) yield return new(OriginalBaseKind.Gate, gate);
    }
}

public sealed partial class OriginalEventInitializationContext
{
    internal bool CaptureBaseThreatInputsBound { get; set; }
    private readonly Dictionary<OriginalBaseReference, OriginalCaptureBaseThreatFields?> _captureBaseThreatFields = [];
    public void SetCaptureBaseThreatFields(OriginalBaseReference target, OriginalCaptureBaseThreatFields? fields)
    { _ = BasePhysical(target); _captureBaseThreatFields[target] = fields; }
    public OriginalCaptureBaseThreatFields? ReadCaptureBaseThreatFields(OriginalBaseReference target)
    { _ = BasePhysical(target); return _captureBaseThreatFields.GetValueOrDefault(target); }
}
