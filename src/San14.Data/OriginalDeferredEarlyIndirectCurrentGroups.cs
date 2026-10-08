using System.Buffers.Binary;

namespace San14.Data;

/// <summary>One actual source pattern group. Mask is the OR of its current
/// inner World records, not a texture format or an RGBA component mask.</summary>
public sealed class OriginalDeferredEarlyIndirectCurrentGroup
{
    internal OriginalDeferredEarlyIndirectCurrentGroup(OriginalMapSourceCreatedLightingPacket? modifier,
        uint mask, IReadOnlyList<OriginalMapWorldKind1CurrentRecord> members)
        => (ModifierLighting, Mask78, Members) = (modifier, mask, members);
    public OriginalMapSourceCreatedLightingPacket? ModifierLighting { get; }
    public uint Mask78 { get; }
    public IReadOnlyList<OriginalMapWorldKind1CurrentRecord> Members { get; }
    public ReadOnlyMemory<byte> ModifierParameter => ModifierLighting is { } light &&
        OriginalDeferredF103Kind1Modifier.HasBuiltinModifier(light.LocalLightData.Span)
        ? light.LocalLightData.Slice(0x1B0) : ReadOnlyMemory<byte>.Empty;
    public uint StencilReference => 0;
    // The shared zero-based compare enum:5=NotEqual,2=Equal (7=Always).
    public uint NativeStencilCompareCode => Mask78 == 0 ? 5u : 2u;
    public uint StencilReadMask => Mask78 == 0 ? 255u : ~Mask78 & 15;
    public bool KeepsEveryStencilOutcome => true;
}

public sealed class OriginalDeferredEarlyIndirectCurrentRecord
{
    internal OriginalDeferredEarlyIndirectCurrentRecord(OriginalMapSourceCreatedF103CurrentFrame frame,
        OriginalMapWorldKind1CurrentRecord record, OriginalMapSourceCreatedLightingPacket lighting,
        IReadOnlyList<OriginalDeferredEarlyIndirectCurrentGroup> groups)
        => (Frame, Record, Lighting, Groups) = (frame, record, lighting, groups);
    public OriginalMapSourceCreatedF103CurrentFrame Frame { get; }
    public OriginalMapWorldKind1CurrentRecord Record { get; }
    public OriginalMapSourceCreatedLightingPacket Lighting { get; }
    public IReadOnlyList<OriginalDeferredEarlyIndirectCurrentGroup> Groups { get; }
    public string SourceEvidence => "EBCD70/current-D58E10-kind1/source-CameraA00-exclusion;" +
        "EBD320-kind1-active-inner/D59210;CE0B30-seven-float-pattern-grouping;" +
        "EBD429-max1/defaultmaskF;EBD4D8-Reference0/complement-low4/Keep;CPU-current-groups-only";
}

public static class OriginalDeferredEarlyIndirectCurrentGroups
{
    /// <summary>The same complete accepted World population supplies all
    /// records, including ones absent from F103's selected draw subset.</summary>
    public static IReadOnlyList<OriginalDeferredEarlyIndirectCurrentRecord> Prepare(
        OriginalMapSourceCreatedF103CurrentFrame actualGeometry,
        OriginalMapSourceCreatedWorldF103Frames actualWorld,
        OriginalDeferredEarlyIndirectPatternCameraTail actualCamera)
    {
        actualCamera.RequireCurrent(actualGeometry);
        if (actualCamera.CameraKind3Ac != 5)
            throw new InvalidDataException("Early68 grouping requires the actual Pattern resource gate for this prepared Camera kind.");
        return PrepareCore(actualGeometry, actualWorld, actualCamera, false);
    }
    public static IReadOnlyList<OriginalDeferredEarlyIndirectCurrentRecord> Prepare(
        OriginalMapSourceCreatedF103CurrentFrame actualGeometry,
        OriginalMapSourceCreatedWorldF103Frames actualWorld,
        OriginalDeferredEarlyIndirectPatternCameraTail actualCamera,
        IOriginalDeferredEarlyIndirectCurrentPatternResources actualResources)
    {
        bool special = OriginalDeferredEarlyIndirectPatternSpecialSource.UsesSpecialBranch(actualCamera, actualResources);
        if (special) OriginalDeferredEarlyIndirectPatternSpecialSource.RequireActualSta(actualGeometry, actualResources);
        return PrepareCore(actualGeometry, actualWorld, actualCamera, special);
    }
    private static IReadOnlyList<OriginalDeferredEarlyIndirectCurrentRecord> PrepareCore(
        OriginalMapSourceCreatedF103CurrentFrame actualGeometry,
        OriginalMapSourceCreatedWorldF103Frames actualWorld,
        OriginalDeferredEarlyIndirectPatternCameraTail actualCamera, bool special)
    {
        ArgumentNullException.ThrowIfNull(actualGeometry); ArgumentNullException.ThrowIfNull(actualWorld);
        actualCamera.RequireCurrent(actualGeometry);
        if (!ReferenceEquals(actualWorld.View, actualGeometry.ViewFields) || actualWorld.View.CurrentWorldQueue is not { } queue ||
            queue.FrameNumber != actualGeometry.FrameNumber)
            throw new InvalidDataException("Early68 grouping requires this actual complete World/View queue, not F103's selected pass list.");
        uint exclusion = ~(1u << ((actualCamera.CameraA00 - 1) & 31));
        var result = new List<OriginalDeferredEarlyIndirectCurrentRecord>();
        uint active = actualGeometry.ViewFields.ViewFlags19AC & 0x9FFFFFFF;
        foreach (var outer in queue.Records.Where(record => (record.Mask78 & exclusion) == 0))
        {
            var groups = new List<OriginalDeferredEarlyIndirectCurrentGroup>();
            // EBD2F6..306 jumps directly to EBD429 on this branch. It
            // retains the actual outer records but skips the inner grouping.
            if (special)
            {
                groups.Add(new(null, 15, Array.Empty<OriginalMapWorldKind1CurrentRecord>()));
                result.Add(new(actualGeometry, outer, actualWorld.BorrowAcceptedRecordLighting(outer), groups.AsReadOnly()));
                continue;
            }
            foreach (var inner in queue.Records.Where(record => !ReferenceEquals(record, outer) && (record.Mask78 & active) != 0))
            {
                var lighting = actualWorld.BorrowAcceptedRecordLighting(inner);
                byte[] key = ReadComparisonKey(lighting);
                int group = groups.FindIndex(value => SameComparisonKey(ReadComparisonKey(value.ModifierLighting), key));
                if (group >= 0)
                {
                    var previous = groups[group];
                    groups[group] = new(previous.ModifierLighting, previous.Mask78 | inner.Mask78,
                        Array.AsReadOnly(previous.Members.Append(inner).ToArray()));
                }
                else
                {
                    if (groups.Count == 4) throw new NotSupportedException("Current early68 patterns exceed the source four-group local array.");
                    groups.Add(new(lighting, inner.Mask78, Array.AsReadOnly(new[] { inner })));
                }
            }
            if (groups.Count == 0) groups.Add(new(null, 15, Array.Empty<OriginalMapWorldKind1CurrentRecord>()));
            result.Add(new(actualGeometry, outer, actualWorld.BorrowAcceptedRecordLighting(outer), groups.AsReadOnly()));
        }
        return result.AsReadOnly();
    }
    private static byte[] ReadComparisonKey(OriginalMapSourceCreatedLightingPacket? light)
    {
        if (light is { } actual && OriginalDeferredF103Kind1Modifier.HasBuiltinModifier(actual.LocalLightData.Span))
            return actual.LocalLightData.Slice(0x1B0 + 0xE0, 40).ToArray();
        // The actual CB6C90 local constructor, not a null modifier's buffer.
        byte[] value = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(value.AsSpan(16), 0x46FA0000);
        BinaryPrimitives.WriteUInt32LittleEndian(value.AsSpan(20), 0x46FA0000);
        return value;
    }
    internal static bool SameComparisonKey(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != 40 || right.Length != 40) throw new InvalidDataException("Native pattern grouping requires its40B local key.");
        // CE0B30 deliberately does not compare E4/E8/EC.
        foreach (int offset in new[] { 0, 24, 28, 16, 20, 32, 36 })
            if (BinaryPrimitives.ReadSingleLittleEndian(left.Slice(offset, 4)) !=
                BinaryPrimitives.ReadSingleLittleEndian(right.Slice(offset, 4))) return false;
        return true;
    }
}
