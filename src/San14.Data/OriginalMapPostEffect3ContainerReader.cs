using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapPostEffect3Chunk(int Index,uint Kind,uint Version,int Offset,int Length);
public sealed record OriginalMapPostEffect3Table(uint ChunkKind,int RowSize,int Offset,
    IReadOnlyList<ReadOnlyMemory<byte>> Rows);
public sealed record OriginalMapPostEffect3Program(uint StageCode,int StageIndex,int Offset,
    ReadOnlyMemory<byte> Bytes,string Sha256,DxbcContainer Container);
public sealed record OriginalMapPostEffect3ProgramReference(uint StageCode,int? StageIndex);
public sealed record OriginalMapPostEffect3Pass(int Index,uint NameHash,
    IReadOnlyList<OriginalMapPostEffect3ProgramReference> ProgramReferences,ReadOnlyMemory<byte> RawBytes);
public sealed record OriginalMapPostEffect3Functor(int Index,uint NameHash,int FirstPass,int PassCount,
    uint RawValue16,ReadOnlyMemory<byte> RawBytes);

/// <summary>Closed structural subset of 3SPK1000. Retains every byte and exposes
/// programs only through the native counted directory, never a magic scan.
/// GPU execution, parameter semantics and allocator recipes remain unsupported.</summary>
public sealed class OriginalMapPostEffect3Container
{
    internal OriginalMapPostEffect3Container(byte[] bytes,uint version,IReadOnlyList<OriginalMapPostEffect3Chunk> chunks,
        IReadOnlyList<OriginalMapPostEffect3Table> tables,IReadOnlyList<OriginalMapPostEffect3Program> programs,
        IReadOnlyList<OriginalMapPostEffect3Pass> passes,IReadOnlyList<OriginalMapPostEffect3Functor> functors)
    {Bytes=bytes;Version=version;Chunks=chunks;Tables=tables;Programs=programs;Passes=passes;Functors=functors;
     Sha256=Convert.ToHexString(SHA256.HashData(bytes));}
    public ReadOnlyMemory<byte> Bytes{get;}
    public string Sha256{get;}
    public uint Version{get;}
    public IReadOnlyList<OriginalMapPostEffect3Chunk> Chunks{get;}
    public IReadOnlyList<OriginalMapPostEffect3Table> Tables{get;}
    public IReadOnlyList<OriginalMapPostEffect3Program> Programs{get;}
    public IReadOnlyList<OriginalMapPostEffect3Pass> Passes{get;}
    public IReadOnlyList<OriginalMapPostEffect3Functor> Functors{get;}
    public IReadOnlyList<string> UnsupportedSections{get;}=Array.AsReadOnly(new[]{
        "Header fields outside native chunk count/version and allocation capacities.",
        "Texture/buffer/sampler allocation row semantics and runtime extent evaluation.",
        "Parameter names, binding rows, pass render states and type13 row semantics.",
        "Native external factories, pass scheduling and GPU execution."});
    public OriginalMapPostEffect3Program GetProgram(uint stageCode,int stageIndex)
        =>Programs.SingleOrDefault(p=>p.StageCode==stageCode&&p.StageIndex==stageIndex)
          ??throw new InvalidDataException("3SPK selected stage program is absent.");
    public IReadOnlyList<OriginalMapPostEffect3Pass> ReadFunctorPasses(int index)
    {
        if((uint)index>=Functors.Count)throw new ArgumentOutOfRangeException(nameof(index));
        var f=Functors[index];return Array.AsReadOnly(Passes.Skip(f.FirstPass).Take(f.PassCount).ToArray());
    }
}

public static class OriginalMapPostEffect3ContainerReader
{
    // maps these six counted groups to metadata A0/A8/B0/B8/C0/C8.
    // D3D creators // confirm CS/VS/PS, not VS/PS/GS.
    private static readonly uint[] Stages=[5,1,0,2,3,4];
    // Kind -> header capacity offset, wire stride and supported chunk version.
    // allocation + dispatch + the individual stream readers.
    private static readonly (uint Kind,int Capacity,int Stride,uint Version)[] Tables=
    [(2,0x48,36,1),(3,0x4C,36,0),(4,0x50,16,0),(5,0x54,16,0),
     (6,0x58,12,1),(7,0x5C,8,0),(8,0x60,64,1),(9,0x64,20,0),(13,0x84,8,0)];

    public static OriginalMapPostEffect3Container Read(OriginalMapFinalOutputPostEffect3Source source)
    {ArgumentNullException.ThrowIfNull(source);return Read(source.Payload.Span);}

    public static OriginalMapPostEffect3Container Read(ReadOnlySpan<byte> source)
    {
        Require(source,0,32);
        if(!source[..8].SequenceEqual("3SPK1000"u8)||U(source,8)!=source.Length||U(source,12)!=20)
            throw Error("unsupported 20-byte native header or declared length");
        uint version=U(source,24);
        // Native supports 0 and 1; this closed subset intentionally implements
        // the installed version1's 104-byte remainder, including 64-byte pass rows.
        if(version!=1||U(source,28)!=116)throw new NotSupportedException("3SPK directory version/profile is unsupported (only installed version1).");
        Require(source,32,104);
        int chunkCount=N(source,16);if(chunkCount!=10)throw new NotSupportedException("3SPK chunk population is outside the closed installed subset.");
        byte[] owned=source.ToArray();var chunks=new List<OriginalMapPostEffect3Chunk>();
        var tables=new List<OriginalMapPostEffect3Table>();var programs=new List<OriginalMapPostEffect3Program>();
        int cursor=136;
        for(int i=0;i<chunkCount;i++)
        {
            Require(source,cursor,12);uint kind=U(source,cursor),v=U(source,cursor+4);int length=N(source,cursor+8);
            if(length<12)throw Error("chunk length is smaller than its native header");Require(source,cursor,length);
            if(chunks.Any(c=>c.Kind==kind))throw Error("duplicate chunk would replace a native table");
            chunks.Add(new(i,kind,v,cursor,length));int end=checked(cursor+length),at=cursor+12;
            if(kind==10)
            {
                if(v!=0)throw new NotSupportedException("3SPK program chunk version is unsupported.");
                RequireChunk(source,at,24,end);var counts=new int[6];
                for(int g=0;g<6;g++)
                {counts[g]=N(source,at+g*4);if(counts[g]>N(source,0x68+g*4))throw Error("program population exceeds metadata allocation capacity");}
                at+=24;
                for(int g=0;g<6;g++)for(int j=0;j<counts[g];j++)
                {
                    RequireChunk(source,at,4,end);int n=N(source,at);at+=4;RequireChunk(source,at,n,end);
                    if(n<32||n>N(source,0x80))throw Error("program size exceeds the native scratch allocation");
                    var dxbc=DxbcContainerReader.Read(source.Slice(at,n));
                    var code=dxbc.Parts.Where(p=>p.TokenHeader is not null).ToArray();
                    if(code.Length!=1||(code[0].TokenHeader!.RawVersionToken>>16)!=Stages[g])
                        throw Error("program token stage disagrees with its native stage group");
                    var bytes=owned.AsMemory(at,n);programs.Add(new(Stages[g],j,at,bytes,Convert.ToHexString(SHA256.HashData(bytes.Span)),dxbc));at+=n;
                }
                if(at!=end)throw Error("program chunk has unconsumed bytes");
            }
            else
            {
                var spec=Tables.SingleOrDefault(t=>t.Kind==kind);
                if(spec.Kind==0||v!=spec.Version)throw new NotSupportedException($"3SPK chunk {kind} version {v} is unsupported.");
                RequireChunk(source,at,4,end);int count=N(source,at);at+=4;
                if(count>N(source,spec.Capacity)||(long)count*spec.Stride!=end-at)
                    throw Error("counted table exceeds allocation capacity or leaves unconsumed bytes");
                var rows=new ReadOnlyMemory<byte>[count];for(int j=0;j<count;j++)rows[j]=owned.AsMemory(at+j*spec.Stride,spec.Stride);
                tables.Add(new(kind,spec.Stride,at,Array.AsReadOnly(rows)));
            }
            cursor=end;
        }
        if(cursor!=source.Length||Tables.Any(t=>tables.All(r=>r.ChunkKind!=t.Kind))||chunks.All(c=>c.Kind!=10))
            throw Error("directory does not consume the entire source or lacks a required table");
        var passTable=tables.Single(t=>t.ChunkKind==8);var passes=new List<OriginalMapPostEffect3Pass>();
        for(int i=0;i<passTable.Rows.Count;i++)
        {
            var raw=passTable.Rows[i];var refs=new List<OriginalMapPostEffect3ProgramReference>();
            for(int g=0;g<6;g++)
            {
                short n=BinaryPrimitives.ReadInt16LittleEndian(raw.Span.Slice(0x2C+g*2,2));
                if(n< -1||(n>=0&&programs.All(p=>p.StageCode!=Stages[g]||p.StageIndex!=n)))
                    throw Error("pass stage reference exceeds its counted program table");
                refs.Add(new(Stages[g],n==-1?null:n));
            }
            passes.Add(new(i,U(raw.Span,4),refs.AsReadOnly(),raw));
        }
        var functors=new List<OriginalMapPostEffect3Functor>();var functorTable=tables.Single(t=>t.ChunkKind==9);
        for(int i=0;i<functorTable.Rows.Count;i++)
        {
            var raw=functorTable.Rows[i];int first=N(raw.Span,8),count=N(raw.Span,12);
            if((long)first+count>passes.Count)throw Error("functor pass span exceeds its native pass table");
            functors.Add(new(i,U(raw.Span,4),first,count,U(raw.Span,16),raw));
        }
        return new(owned,version,chunks.AsReadOnly(),tables.AsReadOnly(),programs.AsReadOnly(),passes.AsReadOnly(),functors.AsReadOnly());
    }
    private static void RequireChunk(ReadOnlySpan<byte> source,int at,int count,int end)
    {Require(source,at,count);if((long)at+count>end)throw Error("field crosses its owning chunk boundary");}
    private static void Require(ReadOnlySpan<byte> source,int at,int count)
    {if(at<0||count<0||(long)at+count>source.Length)throw Error("field exceeds source boundaries");}
    private static uint U(ReadOnlySpan<byte> source,int at){Require(source,at,4);return BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(at,4));}
    private static int N(ReadOnlySpan<byte> source,int at){uint n=U(source,at);return n<=int.MaxValue?(int)n:throw Error("integer exceeds supported bounds");}
    private static InvalidDataException Error(string message)=>new($"3SPK: {message}.");
}
