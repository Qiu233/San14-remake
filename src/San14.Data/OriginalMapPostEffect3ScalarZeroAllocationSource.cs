using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The explicit original default allocator route used by a fresh
/// RTScalar bank. Virtual30 allocates and then clears the entire requested
/// block; virtual28 alone is not this contract. Custom allocators and retained
/// banks require their own producer.</summary>
public sealed class LoadedOriginalMapPostEffect3ScalarZeroAllocationSource
{
    internal LoadedOriginalMapPostEffect3ScalarZeroAllocationSource(string locale,string evidence)
        =>(Locale,SourceEvidence)=(locale,evidence);
    public string Locale{get;}
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3ParameterState CreateFreshState(
        OriginalMapPostEffect3Container module,long frameNumber,string ownerIdentity)
    {
        if(frameNumber<=0)throw new InvalidDataException("A fresh default-allocator scalar bank requires an owned frame.");
        var state=OriginalMapPostEffect3ParameterState.CreateUninitialized(module,frameNumber,ownerIdentity);
        if(state.Declarations.Select(d=>d.NameHash).Distinct().Count()!=state.Declarations.Count)
            throw new NotSupportedException("Fresh scalar zero allocation requires the actual unique source primary hashes.");
        foreach(var declaration in state.Declarations)
            if(!state.SetTypedByHash(declaration.NameHash,declaration.NativeValueType,declaration.ElementCount,
                new byte[declaration.ValueByteCount],SourceEvidence+";explicit-fresh-default-allocator-owner="+ownerIdentity+
                ";whole-allocation-cleared-before-record-metadata-and-handler-initializers"))
                throw new InvalidDataException("Original zero-allocation scalar declaration was not uniquely writable.");
        return state;
    }
}

public static class OriginalMapPostEffect3ScalarZeroAllocationSource
{
    public static LoadedOriginalMapPostEffect3ScalarZeroAllocationSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        (string hash,int outer,int forward,int inner,int clear,int memset)=locale switch
        {
            "TC"=>("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
                0x14BBA40,0x939320,0x14BB978,0x9387B0,0x101CD70),
            "SC"=>("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
                0x14BC920,0x93A3D0,0x14BC858,0x939870,0x101DC70),
            _=>throw new NotSupportedException("Original scalar zero allocation supports the pinned TC/SC default allocator only.")
        };
        string path=Path.Combine(repository.SourceDirectory,$"SAN14CE_{locale}.exe");
        byte[] exe=File.ReadAllBytes(path);
        if(Convert.ToHexString(SHA256.HashData(exe))!=hash)
            throw new InvalidDataException("Original scalar zero-allocation executable differs from its source revision.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        if(pe.PEHeaders.PEHeader?.ImageBase!=0x140000000UL)
            throw new InvalidDataException("Original scalar allocation expects its pinned native image base.");
        ulong Slot(int at)
        {
            var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".rdata"&&at>=s.VirtualAddress&&
                (long)at+8<=(long)s.VirtualAddress+s.SizeOfRawData);
            return BinaryPrimitives.ReadUInt64LittleEndian(exe.AsSpan(section.PointerToRawData+at-section.VirtualAddress,8));
        }
        if(Slot(outer+0x30)!=0x140000000UL+(uint)forward||Slot(inner+0x30)!=0x140000000UL+(uint)clear)
            throw new InvalidDataException("Original default allocator virtual30 source link differs.");
        return new(locale,$"san14://file/SAN14CE_{locale}.exe@{hash};RTScalar114AC40-allocation-v30;"+
            $"default-allocator-VT{outer:X8}/v30={forward:X8}->inner-VT{inner:X8}/v30={clear:X8};"+
            $"allocate-v28-success;memset{memset:X8}(result,0,original-request-size);custom-allocator-not-admitted");
    }
}
