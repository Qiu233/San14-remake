using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeferredEarlyShadowFrameArenaLayout(uint TotalFrameBudgetBytes,
    uint RegisteredPointerCapacity, uint ModelCacheCapacity, uint First32ByteCapacity,
    uint Second32ByteCapacity, uint ReservedPrefixBytes, uint Arena18CapacityBytes)
{
    public int FreshCursor20 => 0;
    public int SourceCommonFrameCount => 4;
}

internal sealed record DeferredEarlyShadowFrameArenaNativeFields(string Locale,
    int NormalHostBudgetOverride720, uint First32ByteCapacity, uint Second32ByteCapacity,
    string SourceEvidence);

/// <summary>Load-once default View declaration and actual normal Host startup
/// memory override. It retains the exact four Frame+18 capacity recipe;
/// current allocation cursors are owned independently for each live Frame.</summary>
public sealed class LoadedDeferredEarlyShadowFrameArenaSource
{
    internal LoadedDeferredEarlyShadowFrameArenaSource(
        DeferredEarlyShadowFrameArenaNativeFields native, uint budget,
        uint registered, uint models, string viewSource)
    {
        Locale = native.Locale; NormalHostBudgetOverride720 = native.NormalHostBudgetOverride720;
        DeclaredFrameBudget = budget;
        uint selected = native.NormalHostBudgetOverride720 >= 0 ? (uint)native.NormalHostBudgetOverride720 : budget;
        Layout = DeferredEarlyShadowFrameArenaSource.PrepareNumericLayout(selected,registered,
            models,native.First32ByteCapacity,native.Second32ByteCapacity);
        SourceEvidence = viewSource + ";" + native.SourceEvidence +
            ";defaultView-p6/7/14;D21BB6-signed-Q720-override;D21CA0-align16;D5FB60-fourFrames;D5FA89..D5FB3C-Frame18/20/24";
    }
    public string Locale { get; }
    public uint DeclaredFrameBudget { get; }
    public int NormalHostBudgetOverride720 { get; }
    public DeferredEarlyShadowFrameArenaLayout Layout { get; }
    public string SourceEvidence { get; }

    /// <summary>Actual fresh normal Common lifetime. The finite numerical
    /// arenas represent the real capacity/cursor service; no unused memory
    /// contents or full Common Frame fields are invented.</summary>
    public DeferredEarlyShadowCurrentCommonFrameArenaOwner ConstructFreshNormalCommonFrames(
        object actualCommonOwner, MainMapContextOwnerSource actualFreshHost)
    {
        ArgumentNullException.ThrowIfNull(actualCommonOwner); ArgumentNullException.ThrowIfNull(actualFreshHost);
        if (actualFreshHost.Locale != Locale)
            throw new ArgumentException("Common arena startup and fresh actual Main Host use different source locales.",nameof(actualFreshHost));
        return new(actualCommonOwner,this);
    }
}

/// <summary>The acquired actual Frame slot. Its bank/tuple allocator is shared
/// with all represented current Frame18 producers. The lease is retired after
/// the real Frame reset/release event, rather than by a numbered request.</summary>
public sealed class DeferredEarlyShadowCurrentCommonFrameArena
{
    internal DeferredEarlyShadowCurrentCommonFrameArena(
        DeferredEarlyShadowCurrentCommonFrameArenaOwner owner,int side,long frame,
        DeferredEarlyShadowCurrentTupleArena arena)
        => (Owner,CommonFrameSide,FrameNumber,Arena)=(owner,side,frame,arena);
    public DeferredEarlyShadowCurrentCommonFrameArenaOwner Owner { get; }
    public int CommonFrameSide { get; }
    public long FrameNumber { get; }
    public DeferredEarlyShadowCurrentTupleArena Arena { get; }
    public bool IsCurrent => Owner.IsCurrent(this);
    public void RequireCurrent()
    { if(!IsCurrent)throw new InvalidOperationException("The actual Common Frame arena was retired or belongs to another owner."); }
}

/// <summary>Finite D61AC0 first-free slot selection and D523F0/D608F0
/// current Frame retirement. Four source capacities persist with the Common
/// owner. This is a CPU allocator service, not a full original Frame object or
/// an implementation of the native wait/renderer callback when all are busy.</summary>
public sealed class DeferredEarlyShadowCurrentCommonFrameArenaOwner
{
    private readonly DeferredEarlyShadowCurrentTupleArena[] arenas;
    private readonly DeferredEarlyShadowCurrentCommonFrameArena?[] active=new DeferredEarlyShadowCurrentCommonFrameArena?[4];
    internal DeferredEarlyShadowCurrentCommonFrameArenaOwner(object common,
        LoadedDeferredEarlyShadowFrameArenaSource source)
    {
        ActualCommonOwner=common;Source=source;
        arenas=Enumerable.Range(0,4).Select(NewArena).ToArray();
    }
    public object ActualCommonOwner { get; }
    public LoadedDeferredEarlyShadowFrameArenaSource Source { get; }
    public int CurrentActiveFrameCount=>active.Count(slot=>slot is not null);

    /// <summary>D61AC0 scans side0..3 for a free current Frame. Null means
    /// the actual caller must wait for a real release; it is not a completed
    /// native all-busy wait or a fabricated fifth Frame.</summary>
    public DeferredEarlyShadowCurrentCommonFrameArena? TryAcquireCurrent(long actualFrameNumber)
    {
        if(actualFrameNumber<=0)throw new ArgumentOutOfRangeException(nameof(actualFrameNumber));
        for(int side=0;side<active.Length;side++)
            if(active[side] is null)return active[side]=new(this,side,actualFrameNumber,arenas[side]);
        return null;
    }

    /// <summary>Share the same finite cursor for an actual current producer,
    /// including already constructed Main tuple spans before borrowing them.
    /// The producer supplies its source-computed aligned byte request.</summary>
    public bool TryReserveCurrentSourceBytes(DeferredEarlyShadowCurrentCommonFrameArena current,
        int actualSourceByteCount,out int arenaRelativeOffset)
    {
        RequireOwnedCurrent(current);
        if(actualSourceByteCount<0)throw new ArgumentOutOfRangeException(nameof(actualSourceByteCount));
        return current.Arena.TryReserve(actualSourceByteCount,out arenaRelativeOffset);
    }

    /// <summary>After actual current prepared View/tuple borrowers have been
    /// retired: D528CD resets Frame20, then D608F0 releases Frame210. Resetting
    /// happens only at this real lifetime event, never on cache lookup, frame
    /// number arithmetic or the next Acquire call.</summary>
    public void RetireCurrentAfterSourceResetAndRelease(DeferredEarlyShadowCurrentCommonFrameArena current)
    {
        RequireOwnedCurrent(current);int side=current.CommonFrameSide;
        arenas[side]=NewArena(side);active[side]=null;
    }

    internal bool IsCurrent(DeferredEarlyShadowCurrentCommonFrameArena current)
        => ReferenceEquals(current.Owner,this)&&(uint)current.CommonFrameSide<4&&
            ReferenceEquals(active[current.CommonFrameSide],current);
    private void RequireOwnedCurrent(DeferredEarlyShadowCurrentCommonFrameArena current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if(!IsCurrent(current))throw new InvalidOperationException("This is not the owner's acquired current Common Frame arena.");
    }
    private DeferredEarlyShadowCurrentTupleArena NewArena(int side)
        =>new(checked((int)Source.Layout.Arena18CapacityBytes),0,
            Source.SourceEvidence+";owned-CommonFrame-side"+side+";source-D5F170-initialize/D523F0-actual-retirement-reset20");
}

public static class DeferredEarlyShadowFrameArenaSource
{
    public static LoadedDeferredEarlyShadowFrameArenaSource Load(GameResourceRepository repository,
        string locale,LoadedDefaultView0Source alreadyLoadedDefaultView0)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(alreadyLoadedDefaultView0);
        var native=RenderSamplerSourceAdapters.LoadEarlyShadowFrameArenaNativeSource(repository.SourceDirectory,locale);
        var definitions=alreadyLoadedDefaultView0.CopyViewDefinitions();
        var view=definitions.Document.Objects.Single(value=>value.ObjectId==DefaultView0Source.ViewObjectId&&
            value.Kind==DokObjectKind.Inline&&value.ClassId==0x97AF26FD);
        uint Word(int ordinal,uint hash)
        {
            var property=view.Properties[ordinal];
            if(property.Index!=ordinal||property.NameHash!=hash||property.TypeCode!=5||property.ElementCount!=1||property.RawValue.Length!=4)
                throw new InvalidDataException("Default View memory capacity source descriptor differs at import.");
            return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
        }
        return new(native,Word(6,0x339CE875),Word(7,0x21B50B62),Word(14,0xD6339D20),alreadyLoadedDefaultView0.SourceIdentity);
    }

    public static DeferredEarlyShadowFrameArenaLayout PrepareNumericLayout(uint actualTotalBudget,
        uint actualRegisteredCapacity,uint actualModelCacheCapacity,uint actualFirst32ByteCapacity,
        uint actualSecond32ByteCapacity)
    {
        uint total=unchecked((actualTotalBudget+15)&~15u);
        ulong prefix=(((ulong)actualRegisteredCapacity*8+15)&~15UL)+
            (ulong)actualModelCacheCapacity*0x330+(ulong)actualFirst32ByteCapacity*32+(ulong)actualSecond32ByteCapacity*32;
        // These are physical capacity inputs. An unsupported over-budget source
        // layout cannot publish a synthetic huge managed scratch arena.
        if(prefix>total||total>int.MaxValue)
            throw new NotSupportedException("Current owned Frame18 needs its actual finite addressable source budget and reserved regions.");
        return new(total,actualRegisteredCapacity,actualModelCacheCapacity,actualFirst32ByteCapacity,
            actualSecond32ByteCapacity,(uint)prefix,total-(uint)prefix);
    }
}
