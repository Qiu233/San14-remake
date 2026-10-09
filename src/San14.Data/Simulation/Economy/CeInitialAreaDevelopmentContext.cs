namespace San14.Data;

/// <summary>Current area fields for a continuing source replay. Null fields are
/// unresolved; these values are independent of the reset-prefix source record.</summary>
public sealed record CeAreaDevelopmentCurrentFields(ushort? Progress3C,
    ushort? Progress3E, ushort? Progress40, ushort? Officer36, byte? Bonus6D);

/// <summary>Shares the native map/force queries with the monthly context and
/// applies audited development writes to that same income input. It starts with
/// unknown current fields and never promotes a reset checkpoint to final state.</summary>
public sealed class CeInitialAreaDevelopmentContext : ICeAreaDevelopmentWriteContext
{
    private readonly CeEconomyRuntimeContext _runtime;
    private readonly Dictionary<int, InitialEconomySourceRow> _sources;
    private readonly Dictionary<int, CeAreaDevelopmentCurrentFields> _areas;
    private readonly Dictionary<int, short?> _forceWords = [];
    private int? _levelChangeCoefficient;

    public CeInitialAreaDevelopment Rules { get; }

    public CeInitialAreaDevelopmentContext(CeInitialAreaDevelopment rules,
        LoadedScenario source, CeEconomyRuntimeContext runtime)
    {
        ArgumentNullException.ThrowIfNull(rules); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(runtime);
        if (source.Snapshot.Locale != rules.Locale || source.Metadata.FormatMarker != 0x66 ||
            source.ScenarioSource.Sha256 != runtime.ScenarioSourceSha256 ||
            source.FixedDataSource.Sha256 != runtime.FixedSourceSha256)
            throw new InvalidDataException("Area development writer and economy context have contradictory current source bindings.");
        Rules = rules; _runtime = runtime;
        _sources = runtime.SourceRows.ToDictionary(row => row.AreaSlot);
        if (_sources.Count != 501 || !Enumerable.Range(0, 501).All(_sources.ContainsKey))
            throw new InvalidDataException("Area development replay requires all501 source slots.");
        _areas = _sources.Keys.ToDictionary(slot => slot,
            _ => new CeAreaDevelopmentCurrentFields(null, null, null, null, null));
    }

    public CeAreaDevelopmentCurrentFields ReadCurrentAreaFields(int physicalSlot) =>
        _areas.TryGetValue(physicalSlot, out var value) ? value :
        throw new ArgumentOutOfRangeException(nameof(physicalSlot));

    /// <summary>Applies actual current replay output, with all three progress
    /// fields retained. Supplying null keeps a consumed value unresolved.</summary>
    public void SetCurrentAreaFields(int physicalSlot, CeAreaDevelopmentCurrentFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        InitialEconomySourceRow source = _sources.TryGetValue(physicalSlot, out var value) ? value :
            throw new ArgumentOutOfRangeException(nameof(physicalSlot));
        _areas[physicalSlot] = fields;
        _runtime.SetAreaCurrentFields(physicalSlot, source.Raw44AfterFixedLoad, source.Raw46AfterFixedLoad,
            source.Raw52AfterScenarioLoad, fields.Progress3C, fields.Progress3E, fields.Officer36, fields.Bonus6D);
    }

    public CeAreaDevelopmentWriteResult WriteCurrentProgress(int physicalSlot,
        CeAreaDevelopmentField field, int requestedValue, int notificationFlag) =>
        Rules.WriteCurrentProgress(new(physicalSlot), field, requestedValue, notificationFlag, this);

    public void SetCurrentForceDevelopmentWord1BE(int physicalSlot, short? value)
    {
        CheckForce(physicalSlot); _forceWords[physicalSlot] = value;
    }

    public void SetCurrentLevelChangeCoefficient(int? value) => _levelChangeCoefficient = value;

    public ushort? ReadAreaProgress(AreaReference area, CeAreaDevelopmentField field)
    {
        var current = ReadCurrentAreaFields(AreaSlot(area));
        return field switch
        {
            CeAreaDevelopmentField.Commerce => current.Progress3C,
            CeAreaDevelopmentField.Agriculture => current.Progress3E,
            CeAreaDevelopmentField.Barracks => current.Progress40,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
    }

    public void WriteAreaProgress(AreaReference area, CeAreaDevelopmentField field, ushort value)
    {
        int slot = AreaSlot(area);
        var current = ReadCurrentAreaFields(slot);
        SetCurrentAreaFields(slot, field switch
        {
            CeAreaDevelopmentField.Commerce => current with { Progress3C = value },
            CeAreaDevelopmentField.Agriculture => current with { Progress3E = value },
            CeAreaDevelopmentField.Barracks => current with { Progress40 = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        });
    }

    public ushort? ReadAreaRepresentativeHexIndex(AreaReference area) => _runtime.Native.ReadAreaHexIndex(area);
    public HexReference? ReadHexTableReference(int physicalSlot) => _runtime.Native.ReadHexTableReference(physicalSlot);
    public byte? ReadHexForceSlot(HexReference hex) => _runtime.Native.ReadHexForceSlot(hex);
    public ForceReference? ReadForceTableReference(int physicalSlot) => _runtime.Native.ReadForceTableReference(physicalSlot);
    public bool? ReadForceNativeValidity(ForceReference force) => _runtime.Native.ReadForceNativeValidity(force);
    public int? ReadForcePhysicalIdentity(ForceReference force) => _runtime.Native.ReadForcePhysicalSlot(force);
    public short? ReadForceDevelopmentWord1BE(ForceReference force) => _forceWords.GetValueOrDefault(ForceSlot(force));
    public int? ReadCurrentDevelopmentLevelChangeCoefficient() => _levelChangeCoefficient;
    public void WriteForceDevelopmentWord1BE(ForceReference force, short value) =>
        SetCurrentForceDevelopmentWord1BE(ForceSlot(force), value);

    private static int AreaSlot(AreaReference area) => area.PhysicalSlot is int slot && slot is >= 0 and <= 500
        ? slot : throw new InvalidDataException("Area development receiver is null or outside the native table.");
    private static int ForceSlot(ForceReference force) => force.PhysicalSlot is int slot && slot is >= 0 and <= 51
        ? slot : throw new InvalidDataException("Area development force receiver is null or outside the native table.");
    private static void CheckForce(int slot)
    { if (slot is < 0 or > 51) throw new ArgumentOutOfRangeException(nameof(slot)); }
}
