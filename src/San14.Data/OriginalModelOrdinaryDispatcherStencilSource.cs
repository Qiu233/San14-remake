namespace San14.Data;

/// <summary>Exact current record selected by the native late ordinary
/// dispatcher, before any later shader hook. Record identity/count is a CPU
/// publication, independent of the static112-byte geometry descriptor.</summary>
public sealed record OriginalModelOrdinaryDispatcherRecord(long FrameNumber, string ViewIdentity,
    uint RecordMask78, bool? TypedKind1Record, int? CurrentSelectedCounterCount,
    bool? AcceptedLateOrdinaryBranch, string CurrentPublicationProvenance);

public sealed record OriginalModelOrdinaryDispatcherStencilPacket(long FrameNumber,
    int QueueNodeIndex, uint RecordMask78, bool RecordSelected, bool CounterNonempty,
    bool StencilEnabled, uint NativeCompareCode, byte Reference, byte ReadMask, byte WriteMask,
    uint? NativeFailOperation, uint? NativeDepthFailOperation, uint? NativePassOperation,
    string SourceProvenance)
{
    public bool HasPrimitiveSubmission => RecordSelected && CounterNonempty;
}

/// <summary>D43190 late ordinary record state. This recipe describes the
/// proven state immediately before the shader/primitive pass; it does not
/// certify that later shader hooks retain it. EA76A2 inverts packed bit19
/// before passing the record sign selector to this dispatcher.</summary>
public static class OriginalModelOrdinaryDispatcherStencilSource
{
    public static OriginalModelOrdinaryDispatcherStencilPacket Prepare(
        OriginalModelQueueStateSource sourceQueue, OriginalModelOrdinaryDispatcherRecord currentRecord,
        OriginalMapFreshCommonResourceSnapshot currentCommon, uint actualContextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(sourceQueue); ArgumentNullException.ThrowIfNull(currentRecord);
        ArgumentNullException.ThrowIfNull(currentCommon);
        if (currentRecord.FrameNumber <= 0 || currentRecord.FrameNumber != currentCommon.FrameNumber ||
            currentRecord.ViewIdentity != currentCommon.SelectedViewOwnerIdentity ||
            currentRecord.TypedKind1Record != true || currentRecord.CurrentSelectedCounterCount is not int count || count < 0 ||
            currentRecord.AcceptedLateOrdinaryBranch != true || string.IsNullOrWhiteSpace(currentRecord.CurrentPublicationProvenance) ||
            sourceQueue.RenderType != 0 || (sourceQueue.Flag & 2) == 0 || currentCommon.CurrentModelGroupSelector400 is not byte selector)
            throw new InvalidDataException("Ordinary stencil needs the actual accepted kind1 record, nonnegative current counter, source late branch and current Common selector.");
        bool positive = (currentRecord.RecordMask78 & 0x80000000u) == 0;
        bool selected = positive == ((sourceQueue.Flag & 0x20) == 0);
        uint selectedGroup = selector == 0 ? 0 : 1u << ((selector - 1) & 31);
        bool prefix = (sourceQueue.Flag & 0x40) != 0;
        uint reference = currentRecord.RecordMask78;
        if (selectedGroup != 0)
        {
            if (!prefix && currentRecord.RecordMask78 == selectedGroup) selected = false;
            if (prefix) reference |= (actualContextFlags3E4 & 3) == 1 ? 0x60u : 0x80u;
        }
        else if (prefix) reference |= (actualContextFlags3E4 & 3) == 1 ? 0x60u : 0x80u;
        else if ((sourceQueue.Flag2 & 4) != 0 && (actualContextFlags3E4 & 3) == 1) reference |= 0xD0;
        bool enabled = sourceQueue.ShaderStateOffset != 15 && (sourceQueue.Flag2 & 1) == 0;
        return new(currentRecord.FrameNumber, sourceQueue.Node.Index, currentRecord.RecordMask78,
            selected, count != 0, enabled, 7, unchecked((byte)reference), 255, 255,
            sourceQueue.ShaderStateOffset == 15 ? null : 0u,
            sourceQueue.ShaderStateOffset == 15 ? null : 0u,
            sourceQueue.ShaderStateOffset == 15 ? null : 2u,
            currentRecord.CurrentPublicationProvenance + ";" + currentCommon.ProducerProvenance +
            $";queue={sourceQueue.Node.Index};Flag={sourceQueue.Flag:X4};Flag2={sourceQueue.Flag2:X8};" +
            "TC:E80C27/E6C730/EA7700/D46472-late;E80C11/E6C6F8/EA765A/EA76A2-NOT/EA7820/D48600-record-sign;" +
            "D484D5-enable/D4851A-keep-keep-replace/D486AE-always-ref-readFF/D486C9-writeFF;before-shader-hooks");
    }
}
