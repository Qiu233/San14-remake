using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

public enum OriginalMapPostEffect3ParameterResourceTable { Texture=2, Buffer=3, ConstantBuffer=4 }
public enum OriginalMapPostEffect3ParameterAliasMode { UnknownNativeLocale, SourceAsciiZeroLocale }

public sealed record OriginalMapPostEffect3ParameterDeclaration(int Index,uint NameHash,uint NativeValueType,
    byte ElementCount,int ValueByteCount,int NativeRecordOffset,int NativeRecordByteCount);

/// <summary>Scalar values require an explicit source allocator or actual writer.
/// Unknown allocator storage remains uninitialized; the admitted native default
/// allocator virtual30 separately proves fresh allocation zeroing.</summary>
public sealed class OriginalMapPostEffect3ParameterState
{
    private readonly byte[][] values;
    private readonly bool[] written;
    private readonly string[] evidence;
    private readonly OriginalMapPostEffect3Container source;
    private readonly ParameterDirectory directory;
    private OriginalMapPostEffect3AllocationPlan? ownedAllocation;
    private OriginalMapPostEffect3ParameterState(OriginalMapPostEffect3Container source,long frame,string owner,
        OriginalMapPostEffect3ParameterDeclaration[] declarations,int nativeExtent,ParameterDirectory? directory=null)
    {
        this.source=source;FrameNumber=frame;OwnerIdentity=owner;Declarations=Array.AsReadOnly(declarations);
        NativeRecordExtent=nativeExtent;values=declarations.Select(d=>new byte[d.ValueByteCount]).ToArray();
        written=new bool[values.Length];evidence=new string[values.Length];
        this.directory=directory??new(source,declarations);
    }
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public string SourceSha256=>source.Sha256;
    public IReadOnlyList<OriginalMapPostEffect3ParameterDeclaration> Declarations{get;}
    public int NativeRecordExtent{get;}
    public int WrittenCount=>written.Count(x=>x);

    /// <summary>Retain native scalar storage across an invocation. Unwritten
    /// declarations remain unwritten; current Apply and pointer writers must
    /// still run in the new frame. No allocator or factory initializer runs.</summary>
    public OriginalMapPostEffect3ParameterState CloneForNewFrame(long actualFrameNumber)
    {
        if(actualFrameNumber<=FrameNumber)
            throw new InvalidDataException("Retained scalar storage requires a strictly later actual frame.");
        var clone=new OriginalMapPostEffect3ParameterState(source,actualFrameNumber,OwnerIdentity,
            Declarations.ToArray(),NativeRecordExtent,directory);
        clone.ownedAllocation=ownedAllocation;
        for(int i=0;i<values.Length;i++)
        {
            values[i].CopyTo(clone.values[i],0);
            clone.written[i]=written[i];clone.evidence[i]=evidence[i];
        }
        return clone;
    }

    // Only an actual owned Camera frame can bind scalar storage to an
    // allocation. Matching shape/hash/owner alone is not the same module.
    internal void BindOwnedAllocation(OriginalMapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        if(!ReferenceEquals(source,allocation.Source)||OwnerIdentity!=allocation.ContextOwnerIdentity||
            ownedAllocation is not null&&!ReferenceEquals(ownedAllocation,allocation))
            throw new InvalidDataException("Owned scalar storage belongs to another source allocation or owner.");
        ownedAllocation=allocation;
    }

    internal void RequireRetainedOwnedAllocation(OriginalMapPostEffect3AllocationPlan allocation,long actualFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        if(!ReferenceEquals(ownedAllocation,allocation)||!ReferenceEquals(source,allocation.Source)||
            OwnerIdentity!=allocation.ContextOwnerIdentity||actualFrameNumber<=FrameNumber)
            throw new InvalidDataException("Retained scalars require the same owned source allocation and a prior frame.");
    }

    public static OriginalMapPostEffect3ParameterState CreateFreshWithSourceZeroAllocator(
        OriginalMapPostEffect3Container source,long frameNumber,string ownerIdentity,
        LoadedOriginalMapPostEffect3ScalarZeroAllocationSource actualDefaultAllocator)
    {
        ArgumentNullException.ThrowIfNull(actualDefaultAllocator);
        return actualDefaultAllocator.CreateFreshState(source,frameNumber,ownerIdentity);
    }

    /// <summary> consumes the low BYTE of type5+0C (not its full u32).
    /// Runtime record+10 is the value; its occupied stride is minimum32,
    /// otherwise align4(value bytes+20). No initializer is implicitly run.</summary>
    public static OriginalMapPostEffect3ParameterState CreateUninitialized(
        OriginalMapPostEffect3Container source,long frameNumber,string ownerIdentity)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity);
        if(frameNumber<0)throw new ArgumentOutOfRangeException(nameof(frameNumber));
        ValidateSource(source);var table=source.Tables.Single(t=>t.ChunkKind==5);
        var declarations=new List<OriginalMapPostEffect3ParameterDeclaration>();int offset=0;
        for(int i=0;i<table.Rows.Count;i++)
        {
            var row=table.Rows[i].Span;uint type=U(row,8);byte count=row[12];
            // Unsupported native types have zero size and unsigned-underflow
            // stride behavior. They cannot produce a useful owned GPU value.
            int n=ValueByteCount(type,count);
            if(count==0)throw new NotSupportedException("3SPK zero-element scalar declaration is outside the closed source profile.");
            int stride=Math.Max(32,checked((n+23)&~3));
            declarations.Add(new(i,U(row,4),type,count,n,offset,stride));offset=checked(offset+stride);
        }
        return new(source,frameNumber,ownerIdentity,declarations.ToArray(),offset);
    }

    public static int ValueByteCount(uint nativeValueType,int elementCount)
    {
        if(elementCount<0||elementCount>ushort.MaxValue)throw new ArgumentOutOfRangeException(nameof(elementCount));
        int stride=nativeValueType switch{1 or 6=>4,2 or 7=>8,3=>12,4=>16,5=>64,
            _=>throw new NotSupportedException($"3SPK scalar type {nativeValueType} is unsupported.")};
        return checked(stride*elementCount);
    }

    public int? FindScalarIndex(string name)=>FindScalarIndex(PrimaryNameHash(name));
    public int? FindScalarIndex(uint primaryHash)
    {
        return directory.Scalars.TryGetValue(primaryHash,out int index)?index:null;
    }

    /// <summary>/: primary lookup only, exact byte size, false
    /// without mutation on absence or size mismatch. Type13 is not consulted.</summary>
    public bool SetByName(string name,ReadOnlySpan<byte> bytes,string writerEvidence)
        =>SetByHash(PrimaryNameHash(name),bytes,writerEvidence);
    public bool SetByHash(uint primaryHash,ReadOnlySpan<byte> bytes,string writerEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(writerEvidence);int? found=FindScalarIndex(primaryHash);
        if(found is not int i||bytes.Length!=Declarations[i].ValueByteCount)return false;
        bytes.CopyTo(values[i]);written[i]=true;evidence[i]=writerEvidence;return true;
    }
    /// <summary>The native controller initializer additionally compares type,
    /// count and byte size. This entry retains those checks for recovered defaults.</summary>
    public bool SetTypedByHash(uint primaryHash,uint nativeValueType,int elementCount,
        ReadOnlySpan<byte> bytes,string writerEvidence)
    {
        int? found=FindScalarIndex(primaryHash);
        if(found is not int i||Declarations[i].NativeValueType!=nativeValueType||
           Declarations[i].ElementCount!=elementCount)return false;
        return SetByHash(primaryHash,bytes,writerEvidence);
    }
    public bool IsWritten(int index)
    {if((uint)index>=written.Length)throw new ArgumentOutOfRangeException(nameof(index));return written[index];}
    public ReadOnlyMemory<byte> RequireValue(string name)
    {
        int i=FindScalarIndex(name)??throw new InvalidDataException($"3SPK scalar {name} is absent.");
        if(!written[i])throw new NotSupportedException($"3SPK scalar {name} has no recovered initializer/current write.");
        return values[i].ToArray();
    }
    public string RequireWriterEvidence(string name)
    {int i=FindScalarIndex(name)??throw new InvalidDataException("3SPK scalar is absent.");
     if(!written[i])throw new NotSupportedException("3SPK scalar has not been written.");return evidence[i];}

    public void ApplyKnownCameraWrites(OriginalMapPostEffect3ApplyKnownWrites writes)
    {
        ArgumentNullException.ThrowIfNull(writes);
        if(writes.FrameNumber!=FrameNumber)throw new InvalidDataException("3SPK scalar writes belong to another frame.");
        // Validate the complete transaction before touching any current value.
        foreach(var w in writes.Writes)
        {
            int i=FindScalarIndex(w.Name)??throw new InvalidDataException($"3SPK Apply key {w.Name} is absent.");
            if(w.Bytes.Length!=Declarations[i].ValueByteCount||string.IsNullOrWhiteSpace(w.SourceEvidence))
                throw new InvalidDataException($"3SPK Apply key {w.Name} has an invalid native size/evidence.");
        }
        foreach(var w in writes.Writes)SetByName(w.Name,w.Bytes.Span,w.SourceEvidence);
    }

    public int? FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable table,string name,
        OriginalMapPostEffect3ParameterAliasMode aliasMode=OriginalMapPostEffect3ParameterAliasMode.UnknownNativeLocale)
    {
        uint primary=PrimaryNameHash(name);
        int? first=FindResourceIndexByHashes(table,primary,null,false);
        if(first is not null)return first;
        if(aliasMode!=OriginalMapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
            throw new NotSupportedException("3SPK alias lookup requires the actual native locale branch; no ASCII default is inferred.");
        return FindResourceIndexByHashes(table,primary,PrimaryNameHash(AsciiBytes(name).Select(b=>
            b>=(byte)'A'&&b<=(byte)'Z'?(byte)(b+32):b).ToArray()),true);
    }
    public int? FindResourceIndexByHashes(OriginalMapPostEffect3ParameterResourceTable table,
        uint primaryHash,uint? actualAliasHash)
        =>FindResourceIndexByHashes(table,primaryHash,actualAliasHash,true);
    private int? FindResourceIndexByHashes(OriginalMapPostEffect3ParameterResourceTable table,
        uint primaryHash,uint? aliasHash,bool aliasRequired)
    {
        uint kind=(uint)table;
        if(kind is <2 or >4)throw new ArgumentOutOfRangeException(nameof(table));
        var rows=directory.Resources[kind];
        if(rows.TryGetValue(primaryHash,out int index))return index;
        if(!aliasRequired)return null;
        if(aliasHash is not uint h)throw new NotSupportedException("3SPK native alias hash is unknown.");
        if(!directory.Aliases.TryGetValue(h,out uint target)||target==0x7FFFFFFF)return null;
        return rows.TryGetValue(target,out index)?index:null;
    }

    // The verified source directory is static for this loaded module. Values,
    // writer state and texture aliases remain per invocation. TryAdd preserves
    // the native scan's first matching row, including the first alias hit.
    private sealed class ParameterDirectory
    {
        internal readonly Dictionary<uint,int> Scalars=new();
        internal readonly Dictionary<uint,Dictionary<uint,int>> Resources=new();
        internal readonly Dictionary<uint,uint> Aliases=new();
        internal ParameterDirectory(OriginalMapPostEffect3Container source,
            IReadOnlyList<OriginalMapPostEffect3ParameterDeclaration> declarations)
        {
            foreach(var declaration in declarations)Scalars.TryAdd(declaration.NameHash,declaration.Index);
            foreach(uint kind in new uint[]{2,3,4})
            {
                var map=new Dictionary<uint,int>();var rows=source.Tables.Single(t=>t.ChunkKind==kind).Rows;
                for(int i=0;i<rows.Count;i++)map.TryAdd(U(rows[i].Span,4),i);
                Resources.Add(kind,map);
            }
            foreach(var row in source.Tables.Single(t=>t.ChunkKind==13).Rows)
                Aliases.TryAdd(U(row.Span,0),U(row.Span,4));
        }
    }

    /// <summary> initializes with the first SIGNED byte, then adds that
    /// byte again with weight31. This is intentionally not ResourceNameHash.</summary>
    public static uint PrimaryNameHash(string name)=>PrimaryNameHash(AsciiBytes(name));
    public static uint PrimaryNameHash(ReadOnlySpan<byte> name)
    {
        if(name.IsEmpty)return 0;uint result=unchecked((uint)(sbyte)name[0]),weight=1;
        for(int i=0;i<name.Length&&i<512&&name[i]!=0;i++)
        {weight=unchecked(weight*31);result=unchecked(result+unchecked((uint)(sbyte)name[i])*weight);}
        return result;
    }
    private static byte[] AsciiBytes(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if(name.Any(c=>c>127))throw new NotSupportedException("Native parameter string encoding outside the proven ASCII names is unsupported; use the byte hash entry.");
        return Encoding.ASCII.GetBytes(name);
    }
    internal static void ValidateSource(OriginalMapPostEffect3Container source)
    {
        // ContainerReader owns and hashes the complete payload at import. A
        // current scalar/CB operation borrows that loaded declaration; it must
        // not hash the entire shader container again for every pass or lookup.
        if(source.Sha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256)
            throw new InvalidDataException("3SPK parameter state requires the loaded complete 6CA7130C source payload.");
    }
    private static uint U(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
