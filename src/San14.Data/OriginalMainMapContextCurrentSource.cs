using System.Buffers.Binary;

namespace San14.Data;

public enum OriginalMainMapContextCurrentPhase
{ FreshHostConstructor, DeferredPreparationReset, NativeThinSurfaceFlagWriter, ExplicitOwnedCurrent,
    NativeSpecializedGeometryFlagWriter }

/// <summary>The two actual Q words3F0/3F4. They are independent of Q3E4 and
/// CommonView flags8. This packet represents a remake-owned native source
/// recipe or an explicitly published current owned snapshot.</summary>
public sealed class OriginalMainMapContextCurrentPacket
{
    internal OriginalMainMapContextCurrentPacket(OriginalMainMapContextCurrentSource source,
        long frame, ulong flags, OriginalMainMapContextCurrentPhase phase, string writer)
    { Source=source;FrameNumber=frame;Flags3F0And3F4=flags;Phase=phase;WriterProvenance=writer; }
    public OriginalMainMapContextCurrentSource Source { get; }
    public long FrameNumber { get; }
    public ulong Flags3F0And3F4 { get; }
    public uint ContextFlags3F0 => unchecked((uint)Flags3F0And3F4);
    public uint ContextFlags3F4 => (uint)(Flags3F0And3F4>>32);
    public OriginalMainMapContextCurrentPhase Phase { get; }
    public string WriterProvenance { get; }
    public bool ThinSurfaceSourceGate => (ContextFlags3F0 & 0x80000400u) == 0x400u;
    public string SourceProvenance => Source.SourceProvenance+$";frame={FrameNumber};phase={Phase};"+WriterProvenance;
}

/// <summary>Exact Host Q constructor, deferred preparation reset and one closed
/// native OR400 writer. Native writer operands belong to their actual current
/// producer; the source seed never supplies a later query result.</summary>
public sealed class OriginalMainMapContextCurrentSource
{
    private OriginalMainMapContextCurrentSource(OriginalMainMapContextOwnerSource host, string owner)
    { Host=host;OwnedHostIdentity=owner; }
    public OriginalMainMapContextOwnerSource Host { get; }
    public string OwnedHostIdentity { get; }
    public string SourceProvenance => Host.SourceProvenance+$";owned-Q={OwnedHostIdentity};"+
        "TC-BF5AE6..BF5AFD:constructor-qword3F0=0;EA933F/EA9361:deferred-prepare-qword3F0=0";
    public static OriginalMainMapContextCurrentSource CreateFresh(OriginalGameRepository repository,
        string locale, string ownedHostIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedHostIdentity);
        return new(OriginalMainMapContextOwnerSource.LoadFreshHostDefaults(locale),ownedHostIdentity);
    }
    public OriginalMainMapContextCurrentPacket CreateFreshPacket() => new(this,0,0,
        OriginalMainMapContextCurrentPhase.FreshHostConstructor,"native Host constructor; no later current writers");

    /// <summary>EA9300's accepted nonnull execution branch writes zero to both
    /// Q words before preparing its current geometry. An absent execution skips
    /// that writer and must keep the previous packet at the caller's owner.</summary>
    public OriginalMainMapContextCurrentPacket BeginDeferredPreparation(long frameNumber,
        string actualExecutionOwnerIdentity)
    {
        RequireFrame(frameNumber,actualExecutionOwnerIdentity);
        return new(this,frameNumber,0,OriginalMainMapContextCurrentPhase.DeferredPreparationReset,
            $"actual-owned-execution={actualExecutionOwnerIdentity};TC-EA9336-nonnull/EA933F-xor/EA9361-store");
    }

    /// <summary>E4ABA2's NEG/SBB/AND400/OR recipe, after that current native
    /// preparation branch ran. E9FC4E reads selected View19B4 and passes it as
    /// argument7 (callerRsp30, calleeRbp120). Zero is not inferred from Camera.</summary>
    public OriginalMainMapContextCurrentPacket ApplyExecutedThinSurfaceFlagWriter(
        OriginalMainMapContextCurrentPacket previous, int actualViewCount19B4,
        string actualWriterOwnerIdentity, string currentWriterProvenance)
    {
        Validate(previous); RequireFrame(previous.FrameNumber,actualWriterOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWriterProvenance);
        uint low = previous.ContextFlags3F0 | (actualViewCount19B4 == 0 ? 0u : 0x400u);
        return new(this,previous.FrameNumber,((ulong)previous.ContextFlags3F4<<32)|low,
            OriginalMainMapContextCurrentPhase.NativeThinSurfaceFlagWriter,
            previous.WriterProvenance+$";executed-E9FC4E/E9FC6F/E4ABA2..E4ABB4-owner={actualWriterOwnerIdentity};View19B4={actualViewCount19B4};"+currentWriterProvenance);
    }

    /// <summary>Other current Q writers remain explicit. Publish their complete
    /// two-word owned snapshot; this scope cannot acquire constructor claims.</summary>
    public OriginalMainMapContextCurrentPacket CaptureOwnedCurrent(long frameNumber,
        ReadOnlySpan<byte> completeTwoWords3F0, string actualCurrentProducerProvenance)
    {
        RequireFrame(frameNumber,actualCurrentProducerProvenance);
        if(completeTwoWords3F0.Length!=8)throw new InvalidDataException("Current Q3F0/3F4 requires its complete8-byte owned snapshot.");
        return new(this,frameNumber,BinaryPrimitives.ReadUInt64LittleEndian(completeTwoWords3F0),
            OriginalMainMapContextCurrentPhase.ExplicitOwnedCurrent,actualCurrentProducerProvenance+
            ";complete-current-Q3F0/Q3F4");
    }
    /// <summary>D33F69 writes OR40 in the actual specialized geometry call.
    /// Preserve3F4 and the other current bits; later F103 selects from this
    /// post-geometry snapshot rather than replaying the preparation reset.</summary>
    public OriginalMainMapContextCurrentPacket ApplyExecutedSpecializedGeometryFlagWriter(
        OriginalMainMapContextCurrentPacket previous, string actualWriterProvenance)
    {
        Validate(previous); RequireFrame(previous.FrameNumber, actualWriterProvenance);
        return new(this, previous.FrameNumber, previous.Flags3F0And3F4 | 0x40ul,
            OriginalMainMapContextCurrentPhase.NativeSpecializedGeometryFlagWriter,
            previous.WriterProvenance + ";executed-D33F69-OR40;" + actualWriterProvenance);
    }
    public void Validate(OriginalMainMapContextCurrentPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if(!ReferenceEquals(packet.Source,this))throw new InvalidDataException("Current Q packet belongs to another owned Host source.");
    }
    private static void RequireFrame(long frame,string producer)
    {if(frame<1||string.IsNullOrWhiteSpace(producer))throw new InvalidDataException("Current Q writer requires its accepted frame and actual owned producer.");}
}
