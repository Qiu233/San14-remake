namespace San14.Data;

public sealed record ReportReference(int? PhysicalSlot);
public sealed record ReportBlockerState(bool Present, int? RawField1B0);
public sealed record ReportCommand(int Category, int DistrictSlot, int MapCellSlot, int WorldTime,
    string? Text, string? SupplementalText, int Payload, int MarkBit0 = 0, int MarkBit3 = 0,
    int MarkBit4 = 0, int MarkBit5 = 0, int MarkBit6 = 0);

public interface IReportPoolState
{
    ReportBlockerState? ReadReportBlocker();
    ushort? ReadReportCursor();
    void WriteReportCursor(ushort value);
    ReportReference? ReadReportTableReference(int slot);
    byte? ReadReportByte(ReportReference reference, int offset);
    void WriteReportByte(ReportReference reference, int offset, byte value);
    void WriteReportWord(ReportReference reference, int offset, ushort value);
    void ClearReportBytes(ReportReference reference, int start, int length);
    DistrictReference? ReadDistrictTableReference(int slot);
    bool? ReadDistrictNativeValidity(DistrictReference reference);
    byte? ReadDistrictForceSlot(DistrictReference reference);
    ForceReference? ReadForceTableReference(int slot);
    byte? ReadForceReportColor(ForceReference reference);
    string? ReadForceReportShortName(ForceReference reference);
    bool? ReadReportUiPresent();
    void PresentReport(ReportReference reference);
}

/// <summary>935CA0 report writes and 2269C0 reset; null result can follow a committed report.</summary>
public static class ReportPool
{
    public static ReportReference? Add(ReportCommand command, IReportPoolState state)
    {
        var selected = WriteRecord(command, state);
        if (selected is null) return null;
        if (FirstNulLength(Text(command.Text, "primary text")) != 0)
        {
            if (!Known(state.ReadReportUiPresent(), "report UI presence")) return null;
            state.PresentReport(selected);
        }
        return selected;
    }

    //Domain writes finish before the independent UI service query. Normal
    //campaign callers stage notification work, never invent UI absence.
    internal static ReportReference? WriteRecord(ReportCommand command, IReportPoolState state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(state);
        ReportBlockerState blocker = state.ReadReportBlocker()
            ?? throw new InvalidDataException("Current report blocker pointer is unknown.");
        if (blocker.Present && Known(blocker.RawField1B0, "blocker1B0") != 0) return null;
        int next = (Known(state.ReadReportCursor(), "world cursor165A") + 1) % 2001;
        ReportReference selected = state.ReadReportTableReference(next)
            ?? throw new InvalidDataException("Current report table pointer is unknown.");
        if (!selected.PhysicalSlot.HasValue) return null;

        state.WriteReportByte(selected, 0x86, (byte)Math.Clamp(command.Category, 0, 6));
        state.WriteReportByte(selected, 0x12, (byte)Math.Clamp(command.DistrictSlot, 0, 51));
        state.WriteReportWord(selected, 0x84, (ushort)Math.Clamp(command.MapCellSlot, 0, 48399));
        byte flags = (byte)((Known(state.ReadReportByte(selected, 0x82), "flags82") & 0xFE) | 2);
        state.WriteReportByte(selected, 0x82, flags);
        if (command.MarkBit0 != 0) state.WriteReportByte(selected, 0x82, (byte)(flags | 1));
        state.WriteReportByte(selected, 0x82, (byte)(Known(state.ReadReportByte(selected, 0x82), "flags82 clear2/6") & 0xBB));
        flags = Known(state.ReadReportByte(selected, 0x82), "flags82 before bit6");
        if (command.MarkBit6 != 0) state.WriteReportByte(selected, 0x82, (byte)(flags | 0x40));
        state.WriteReportWord(selected, 0x10, (ushort)Math.Clamp(command.WorldTime, 0, 65535));
        string text = Text(command.Text, "primary text");
        CopyText(selected, 0x14, 55, text, state);
        CopyText(selected, 0x8A, 85, Text(command.SupplementalText, "supplemental text"), state);
        state.WriteReportWord(selected, 0x88, (ushort)Math.Clamp(command.Payload, 1, 6000));
        bool nonempty = FirstNulLength(text) != 0;
        if (nonempty)
        {
            // Use the raw argument here, not the previously clamped report byte12.
            if (command.DistrictSlot < 0) throw new InvalidDataException("Negative report district would index before the native table.");
            DistrictReference district = state.ReadDistrictTableReference(command.DistrictSlot <= 51 ? command.DistrictSlot : 0)
                ?? throw new InvalidDataException("Current report district pointer is unknown.");
            ForceReference force;
            if (Known(state.ReadDistrictNativeValidity(district), "report district validity"))
            {
                byte raw = Known(state.ReadDistrictForceSlot(district), "report district force10");
                force = Force(state, raw <= 51 ? raw : 0);
                state.WriteReportByte(selected, 0x134, Known(state.ReadForceReportColor(force), "force color49"));
            }
            else
            {
                state.WriteReportByte(selected, 0x134, 60);
                force = Force(state, 0);
            }
            CopyText(selected, 0x136, 2, Text(state.ReadForceReportShortName(force), "current force short name"), state);
        }
        UpdateFlag(selected, 0xF7, 8, command.MarkBit3, state);
        UpdateFlag(selected, 0xEF, 16, command.MarkBit4, state);
        UpdateFlag(selected, 0xDF, 32, command.MarkBit5, state);
        state.WriteReportCursor((ushort)next);
        return selected;
    }

    public static void Reset(ReportReference reference, IReportPoolState state)
    {
        ArgumentNullException.ThrowIfNull(reference); ArgumentNullException.ThrowIfNull(state);
        state.ClearReportBytes(reference, 0x14, 110);
        state.WriteReportByte(reference, 0x82, 0);
        state.WriteReportWord(reference, 0x10, 0);
        state.WriteReportByte(reference, 0x12, 0);
        state.WriteReportWord(reference, 0x88, 0);
        state.WriteReportWord(reference, 0x84, 48400);
        state.WriteReportByte(reference, 0x86, 255);
        state.ClearReportBytes(reference, 0x8A, 170);
        state.WriteReportByte(reference, 0x134, 0);
        state.ClearReportBytes(reference, 0x136, 4);
    }

    private static void UpdateFlag(ReportReference reference, byte mask, byte bit, int setting, IReportPoolState state)
    {
        state.WriteReportByte(reference, 0x82, (byte)(Known(state.ReadReportByte(reference, 0x82), "flags82 memory clear") & mask));
        byte current = Known(state.ReadReportByte(reference, 0x82), "flags82 conditional set");
        if (setting != 0) state.WriteReportByte(reference, 0x82, (byte)(current | bit));
    }
    private static void CopyText(ReportReference reference, int offset, int capacity, string text, IReportPoolState state)
    {
        int length = FirstNulLength(text), count = Math.Min(length + 1, capacity);
        for (int index = 0; index < count; index++)
            state.WriteReportWord(reference, offset + index * 2, index < length ? text[index] : (ushort)0);
    }
    private static int FirstNulLength(string text) { int zero = text.IndexOf('\0'); return zero < 0 ? text.Length : zero; }
    private static ForceReference Force(IReportPoolState state, int slot) => state.ReadForceTableReference(slot)
        ?? throw new InvalidDataException("Current report force pointer is unknown.");
    private static string Text(string? value, string name) => value
        ?? throw new InvalidDataException($"Current report {name} is unknown or unsafe to read.");
    private static T Known<T>(T? value, string name) where T : struct => value
        ?? throw new InvalidDataException($"Current report input {name} is unknown.");
}

public interface IAssociationReportState : IReportPoolState, IBaseLookupState
{
    string? FormatAssociationRemovalReport(AssociationRemovalReport report);
    int? ReadWorldReportTime();
    ushort? ReadPersonFirstLocation(int physicalSlot);
    byte? ReadPersonDistrictSlot(int physicalSlot);
    void AcquireReportManager();
    HexReference? ReadReportBaseHex(BaseReference reference);
    int? ReadReportMapCellPhysicalSlot(HexReference reference);
    int? ReadDistrictPhysicalSlot(DistrictReference reference);
    string? ReadAssociationSupplementalText();
}

/// <summary>27769C..27777F report preparation after association cleanup; formatting is an explicit dependency.</summary>
public static class AssociationReportWriter
{
    public const int FormatKey = 0x3452;
    public static ReportReference? Write(AssociationRemovalReport report, IAssociationReportState state)
    {
        ArgumentNullException.ThrowIfNull(report); ArgumentNullException.ThrowIfNull(state);
        string text = state.FormatAssociationRemovalReport(report)
            ?? throw new InvalidDataException("Current association report formatting is unknown.");
        ushort location = Known(state.ReadPersonFirstLocation(report.PersonPhysicalSlot), "person11A");
        int time = Known(state.ReadWorldReportTime(), "world450");
        BaseReference selected = BaseLookup.Find(location, state);
        byte rawDistrict = Known(state.ReadPersonDistrictSlot(report.PersonPhysicalSlot), "person118");
        DistrictReference district = state.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
            ?? throw new InvalidDataException("Current association report district pointer is unknown.");
        state.AcquireReportManager();
        HexReference hex = state.ReadReportBaseHex(selected)
            ?? throw new InvalidDataException("Current association report Hex pointer lookup is unknown.");
        int mapCell = Known(state.ReadReportMapCellPhysicalSlot(hex), "map cell physical slot");
        int districtSlot = Known(state.ReadDistrictPhysicalSlot(district), "district physical slot");
        return ReportPool.Add(new(0, districtSlot, mapCell, time, text,
            state.ReadAssociationSupplementalText(), 0x1394), state);
    }
    private static T Known<T>(T? value, string name) where T : struct => value
        ?? throw new InvalidDataException($"Current association report input {name} is unknown.");
}
