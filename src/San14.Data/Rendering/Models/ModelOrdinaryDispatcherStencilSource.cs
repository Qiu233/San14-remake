namespace San14.Data;

/// <summary>Exact current record selected by the native late ordinary
/// dispatcher, before any later shader hook. Record identity/count is a CPU
/// publication, independent of the static112-byte geometry descriptor.</summary>
public sealed record ModelOrdinaryDispatcherRecord(long FrameNumber, string ViewIdentity,
    uint RecordMask78, bool? TypedKind1Record, int? CurrentSelectedCounterCount,
    bool? AcceptedLateOrdinaryBranch, string CurrentPublicationProvenance);

public sealed record ModelOrdinaryDispatcherStencilPacket(long FrameNumber,
    int QueueNodeIndex, uint RecordMask78, bool RecordSelected, bool CounterNonempty,
    bool StencilEnabled, uint NativeCompareCode, byte Reference, byte ReadMask, byte WriteMask,
    uint? NativeFailOperation, uint? NativeDepthFailOperation, uint? NativePassOperation,
    string SourceProvenance)
{
    public bool HasPrimitiveSubmission => RecordSelected && CounterNonempty;
}

/// <summary>Declared routing and stencil state without a live View or counter.</summary>
public sealed record ModelOrdinaryStaticStencilRule(bool RecordSelected, bool StencilEnabled,
    byte Reference, byte ReadMask, byte WriteMask, uint NativeCompareCode,
    uint? NativeFailOperation, uint? NativeDepthFailOperation, uint? NativePassOperation);

/// <summary>D43190 late ordinary record state before shader hooks. EA76A2
/// inverts packed bit19 before passing the sign selector to this dispatcher.</summary>
public static class ModelOrdinaryDispatcherStencilSource
{
    public static ModelOrdinaryDispatcherStencilPacket Prepare(
        ModelQueueStateSource sourceQueue, ModelOrdinaryDispatcherRecord currentRecord,
        MapCommonRenderState currentCommon, uint actualContextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(sourceQueue); ArgumentNullException.ThrowIfNull(currentRecord);
        ArgumentNullException.ThrowIfNull(currentCommon);
        if (currentRecord.FrameNumber <= 0 || currentRecord.FrameNumber != currentCommon.FrameNumber ||
            currentRecord.ViewIdentity != currentCommon.SelectedViewOwnerIdentity ||
            currentRecord.TypedKind1Record != true || currentRecord.CurrentSelectedCounterCount is not int count || count < 0 ||
            currentRecord.AcceptedLateOrdinaryBranch != true || string.IsNullOrWhiteSpace(currentRecord.CurrentPublicationProvenance) ||
            sourceQueue.RenderType != 0 || (sourceQueue.Flag & 2) == 0 || currentCommon.ModelGroupSelector is not byte selector)
            throw new InvalidDataException("Ordinary stencil needs the actual accepted kind1 record, nonnegative current counter, source late branch and current Common selector.");
        var rule = PrepareResourceRoute(sourceQueue, currentRecord.RecordMask78, selector, actualContextFlags3E4);
        return new(currentRecord.FrameNumber, sourceQueue.Node.Index, currentRecord.RecordMask78,
            rule.RecordSelected, count != 0, rule.StencilEnabled, rule.NativeCompareCode, rule.Reference, rule.ReadMask, rule.WriteMask,
            rule.NativeFailOperation, rule.NativeDepthFailOperation, rule.NativePassOperation,
            currentRecord.CurrentPublicationProvenance + ";" + currentCommon.ProducerProvenance +
            $";queue={sourceQueue.Node.Index};Flag={sourceQueue.Flag:X4};Flag2={sourceQueue.Flag2:X8};" +
            "TC:E80C27/E6C730/EA7700/D46472-late;E80C11/E6C6F8/EA765A/EA76A2-NOT/EA7820/D48600-record-sign;" +
            "D484D5-enable/D4851A-keep-keep-replace/D486AE-always-ref-readFF/D486C9-writeFF;before-shader-hooks");
    }

    /// <summary>Pure load-bound routing and state for a declared record mask.
    /// It does not supply a frame, live counter, renderer receipt or draw.</summary>
    public static ModelOrdinaryStaticStencilRule PrepareResourceRoute(
        ModelQueueStateSource sourceQueue, uint recordMask, byte selector, uint actualContextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(sourceQueue);
        if (sourceQueue.RenderType != 0 || (sourceQueue.Flag & 2) == 0)
            throw new NotSupportedException("Static ordinary routing requires its declared late ordinary queue.");
        bool positive = (recordMask & 0x80000000u) == 0;
        bool selected = positive == ((sourceQueue.Flag & 0x20) == 0);
        uint selectedGroup = selector == 0 ? 0 : 1u << ((selector - 1) & 31);
        bool prefix = (sourceQueue.Flag & 0x40) != 0;
        uint reference = recordMask;
        if (selectedGroup != 0)
        {
            if (!prefix && recordMask == selectedGroup) selected = false;
            if (prefix) reference |= (actualContextFlags3E4 & 3) == 1 ? 0x60u : 0x80u;
        }
        else if (prefix) reference |= (actualContextFlags3E4 & 3) == 1 ? 0x60u : 0x80u;
        else if ((sourceQueue.Flag2 & 4) != 0 && (actualContextFlags3E4 & 3) == 1) reference |= 0xD0;
        bool enabled = sourceQueue.ShaderStateOffset != 15 && (sourceQueue.Flag2 & 1) == 0;
        return new(selected, enabled, unchecked((byte)reference), 255, 255, 7,
            sourceQueue.ShaderStateOffset == 15 ? null : 0u,
            sourceQueue.ShaderStateOffset == 15 ? null : 0u,
            sourceQueue.ShaderStateOffset == 15 ? null : 2u);
    }
}
