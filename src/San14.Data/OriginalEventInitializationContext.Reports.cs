using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalAssociationReportState
{
    private readonly Dictionary<int, int?> _reportPointers = [];
    private readonly Dictionary<int, byte?[]> _reportFields = [];
    private readonly Dictionary<int, byte?> _forceReportColors = [];
    public ushort? RawReportCursor165A { get; set; }
    public int? RawWorldField450 { get; set; }
    public OriginalReportBlockerState? ReportBlockerState { get; set; }
    public bool? ReportUiPresent { get; set; }
    public bool? ReportManagerPresent { get; set; }
    public Action<OriginalReportReference>? ReportPresenter { get; set; }
    public Func<OriginalForceReference, string?>? ForceReportShortNameReader { get; set; }
    public Func<OriginalAssociationRemovalReport, string?>? AssociationReportFormatter { get; set; }
    public string? AssociationSupplementalText { get; set; }

    public OriginalReportBlockerState? ReadReportBlocker() => ReportBlockerState;
    public ushort? ReadReportCursor() => RawReportCursor165A;
    public void WriteReportCursor(ushort value) => RawReportCursor165A = value;
    public void ResetReportCursor() => RawReportCursor165A = 0;
    public void SetReportTablePointer(int slot, int? physicalSlot) => SetPointer(_reportPointers, slot, 2001, physicalSlot, 2001);
    public OriginalReportReference? ReadReportTableReference(int slot)
    { Check(slot, 2001, "report lookup slot"); return _reportPointers.TryGetValue(slot, out int? physical) ? new(physical) : null; }
    public byte? ReadReportRawByte(int physicalSlot, int offset)
    {
        Check(physicalSlot, 2001, "report physical slot"); Check(offset - 0x10, 0x12A, "report field offset");
        return _reportFields.TryGetValue(physicalSlot, out byte?[]? fields) ? fields[offset - 0x10] : null;
    }
    public void SetReportRawByte(int physicalSlot, int offset, byte? value)
    {
        Check(physicalSlot, 2001, "report physical slot"); Check(offset - 0x10, 0x12A, "report field offset");
        if (!_reportFields.TryGetValue(physicalSlot, out byte?[]? fields)) _reportFields[physicalSlot] = fields = new byte?[0x12A];
        fields[offset - 0x10] = value;
    }
    public byte? ReadReportByte(OriginalReportReference reference, int offset) => ReadReportRawByte(ReportPhysical(reference), offset);
    public void WriteReportByte(OriginalReportReference reference, int offset, byte value) => SetReportRawByte(ReportPhysical(reference), offset, value);
    public void WriteReportWord(OriginalReportReference reference, int offset, ushort value)
    {
        int physical = ReportPhysical(reference);
        Check(offset - 0x10, 0x129, "report word offset");
        SetReportRawByte(physical, offset, (byte)value); SetReportRawByte(physical, offset + 1, (byte)(value >> 8));
    }
    public void ClearReportBytes(OriginalReportReference reference, int start, int length)
    {
        int physical = ReportPhysical(reference);
        if (length < 0 || start < 0x10 || (long)start + length > 0x13A)
            throw new InvalidDataException("Report clear window is outside the inspected record.");
        for (int offset = start; offset < start + length; offset++) SetReportRawByte(physical, offset, 0);
    }
    public void ResetReportRecord(int physicalSlot) => OriginalReportPool.Reset(new(physicalSlot), this);
    public OriginalDistrictReference? ReadDistrictTableReference(int slot)
    { Check(slot, 52, "district lookup slot"); return _districtPointers.TryGetValue(slot, out int? physical) ? new(physical) : null; }
    public bool? ReadDistrictNativeValidity(OriginalDistrictReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.PhysicalSlot.HasValue ? ReadDistrictNativeValidity(reference.PhysicalSlot.Value) : false;
    }
    public void SetForceReportColor(int physicalSlot, byte? value)
    { Check(physicalSlot, 52, "force physical slot"); _forceReportColors[physicalSlot] = value; }
    public byte? ReadForceReportColor(OriginalForceReference reference) =>
        _forceReportColors.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 52, "force"));
    public string? ReadForceReportShortName(OriginalForceReference reference)
    {
        bool? valid = ReadForceNativeValidity(reference);
        if (!valid.HasValue) return null;
        if (!valid.Value) return string.Empty;
        return ForceReportShortNameReader?.Invoke(reference);
    }
    public bool? ReadReportUiPresent() => ReportUiPresent;
    public void PresentReport(OriginalReportReference reference)
    {
        if (ReportPresenter is null) throw new InvalidDataException("Current report UI presentation effects are unknown.");
        ReportPresenter(reference);
    }
    public string? FormatAssociationRemovalReport(OriginalAssociationRemovalReport report) => AssociationReportFormatter?.Invoke(report);
    public int? ReadWorldReportTime() => RawWorldField450;
    public void AcquireReportManager()
    {
        if (ReportManagerPresent != true) throw new InvalidDataException("Current report manager is unknown or has not been initialized.");
    }
    public OriginalHexReference? ReadReportBaseHex(OriginalBaseReference reference) => OriginalCurrentGateMapQueries.FindHex(reference, this);
    public int? ReadReportMapCellPhysicalSlot(OriginalHexReference reference) => MapPhysical(reference.PhysicalSlot, 48400, "hex");
    public string? ReadAssociationSupplementalText() => AssociationSupplementalText;
    private static int ReportPhysical(OriginalReportReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return MapPhysical(reference.PhysicalSlot, 2001, "report");
    }
    private static int ReadStaticReportWorldTime(S14ScenarioWorldDocument source)
    {
        if (source.RawBytes.Length < S14ScenarioWorldReader.WorldBytes ||
            Convert.ToHexString(SHA256.HashData(source.RawBytes)) != source.WorldSha256)
            throw new InvalidDataException("Static report world450 requires the complete verified CE world block.");
        return BinaryPrimitives.ReadInt32LittleEndian(source.RawBytes.AsSpan(80, 4));
    }
}
