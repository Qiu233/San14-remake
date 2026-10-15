using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary> source declaration, not an observation of an original
/// Camera1828 or a completed native/GPU texture. CPU+8 retains this count;
/// CPU+10 points to the ordered child Data* array allocated after its32B header.</summary>
public sealed class LoadedMapPostEffect3LutSource
{
    private readonly uint[] children;
    internal LoadedMapPostEffect3LutSource(LoadedObjects definitions,DokObject declaration,uint[] children)
    {Definitions=definitions;Declaration=declaration;this.children=children.ToArray();}
    public LoadedObjects Definitions{get;}
    public DokObject Declaration{get;}
    public uint SourceObjectId=>Declaration.ObjectId;
    public uint SourceClassId=>0x593A65D1;
    public int ChildCount=>children.Length;
    public int NativeCpuAllocationByteCount=>checked(32+children.Length*8);
    public IReadOnlyList<uint> ChildObjectKeys=>Array.AsReadOnly(children);
    public bool ProvesCurrentGameCameraBinding=>false;
    public string SourceEvidence=>$"{Definitions.Source.ResourceUri}@{Definitions.Source.DecodedSha256}#object/{SourceObjectId:X8};"+
        "TC;VT/-class593A65D1/-PostEffect3Lut;-property8D16812D/type5-array"+
        "-count24/-allocation32+8n/-ordered-keylookup/-CPU08/-CPU10"+
        "-selector=context50.v88/not-locale-or-quality;source-declaration-only";

    /// <summary>Exact source bounds/zero-key behavior. The argument must come
    /// from the caller's actual context50.v88 producer. This method does not
    /// infer that selector, resolve registry precedence or create a GPU texture.</summary>
    public uint? GetDeclaredChildKey(uint actualContextSelector)
    {
        if(actualContextSelector>=(uint)children.Length)return null;
        uint key=children[actualContextSelector];return key==0?null:key;
    }

    /// <summary>Resolve an explicitly supplied selected child's declaration to
    /// Static/G1T bytes. Registration scope and texture archive are caller
    /// inputs, never guessed from an object key or a matching texture shape.
    /// Unknown texture subclasses and RDOK property overlays remain rejected.</summary>
    public StaticTextureBinding? LoadStaticForSelector(GameResourceRepository repository,
        uint actualContextSelector,LoadedObjects actualSelectedChildDefinitions,
        string explicitDefinitionsArchive,string explicitTextureArchive)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualSelectedChildDefinitions);
        return LoadStaticForSelector(actualContextSelector,actualSelectedChildDefinitions,explicitDefinitionsArchive,
            id=>repository.LoadObjects(explicitDefinitionsArchive,id),id=>repository.LoadTextureContainer(explicitTextureArchive,id));
    }

    internal StaticTextureBinding? LoadStaticForSelector(uint actualContextSelector,
        LoadedObjects actualSelectedChildDefinitions,string explicitDefinitionsArchive,
        Func<uint,LoadedObjects> loadDefinitions,Func<uint,LoadedTextureContainer> loadTexture)
    {
        uint? selected=GetDeclaredChildKey(actualContextSelector);if(selected is not uint key)return null;
        var resolved=MapPostEffect3LutSource.ResolveDeclaration(actualSelectedChildDefinitions,
            explicitDefinitionsArchive,key,loadDefinitions);
        if(resolved.Object.ClassId!=0xFF7DBFD4)
            throw new NotSupportedException("PostEffect3Lut selected child is not a source-supported Static texture; its actual CPU.v28 producer is required.");
        return StaticTextureResources.Load(resolved.Source,[resolved.Object.ObjectId],loadTexture).Single();
    }
}

public sealed record MapPostEffect3LutSourceArchive(string Archive,string IndexSha256,
    int VisitedDokRecordCount,IReadOnlyList<LoadedMapPostEffect3LutSource> Declarations);

/// <summary>Read native PostEffect3Lut arrays without flattening reference
/// assets, reordering slots or treating zero/missing source data as a texture.</summary>
public static class MapPostEffect3LutSource
{
    public const uint NativeClassId=0x593A65D1;
    public const uint ChildArrayPropertyHash=0x8D16812D;
    public const uint LutRecordType=0x20A6A0BB;

    public static IReadOnlyList<LoadedMapPostEffect3LutSource> ReadAllDeclarations(LoadedObjects definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return Array.AsReadOnly(definitions.Document.Objects.Where(o=>o.Kind==DokObjectKind.Inline&&o.ClassId==NativeClassId)
            .Select(o=>ReadInline(definitions,o)).ToArray());
    }

    public static LoadedMapPostEffect3LutSource Load(GameResourceRepository repository,
        LoadedObjects explicitDefinitions,string explicitDefinitionsArchive,uint explicitObjectKey)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Load(explicitDefinitions,explicitDefinitionsArchive,explicitObjectKey,
            id=>repository.LoadObjects(explicitDefinitionsArchive,id));
    }

    internal static LoadedMapPostEffect3LutSource Load(LoadedObjects explicitDefinitions,
        string explicitDefinitionsArchive,uint explicitObjectKey,Func<uint,LoadedObjects> loadDefinitions)
    {
        var resolved=ResolveDeclaration(explicitDefinitions,explicitDefinitionsArchive,explicitObjectKey,loadDefinitions);
        if(resolved.Object.ClassId!=NativeClassId)throw new InvalidDataException("Selected source object is not native class593A65D1/PostEffect3Lut.");
        return ReadInline(resolved.Source,resolved.Object);
    }

    /// <summary>Explicit installed archive scan of every original DOK record.
    /// Retains each declaration's own file identity. It does not choose among
    /// duplicate object keys or reproduce native registry registration order.</summary>
    public static MapPostEffect3LutSourceArchive LoadAllSourceDeclarations(
        GameResourceRepository repository,string explicitArchive)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _=GameResourceId.Rdb(explicitArchive,0);
        string path=Path.Combine(repository.SourceDirectory,"0002",explicitArchive+".rdb");
        byte[] indexBytes=File.ReadAllBytes(path);var index=RdbIndexReader.Read(indexBytes);
        int visited=0;var declarations=new List<LoadedMapPostEffect3LutSource>();
        foreach(var record in index.Records.Where(r=>r.TypeId==LutRecordType))
        {
            var definitions=repository.LoadObjects(explicitArchive,record.FileId);
            if(definitions.Source.OverrideSource is not null)
                throw new NotSupportedException("Original PostEffect3Lut source inventory cannot silently include override definitions.");
            declarations.AddRange(ReadAllDeclarations(definitions));visited++;
        }
        return new(explicitArchive,Convert.ToHexString(SHA256.HashData(indexBytes)),visited,declarations.AsReadOnly());
    }

    private static LoadedMapPostEffect3LutSource ReadInline(LoadedObjects definitions,DokObject obj)
    {
        if(obj.Kind!=DokObjectKind.Inline||obj.ClassId!=NativeClassId||obj.Properties.Count!=1)
            throw new InvalidDataException("PostEffect3Lut source requires its native one-property inline declaration.");
        var p=obj.Properties[0];
        if(p.Index!=0||p.NameHash!=ChildArrayPropertyHash||p.TypeCode!=5||p.ElementSize!=4||
            p.ElementCount<0||p.ElementCount>0xFFFFFF||p.RawValue.Length!=checked(p.ElementCount*4))
            throw new InvalidDataException("PostEffect3Lut source8D16812D must retain every native type5/u32 array reference.");
        var values=new uint[p.ElementCount];
        for(int i=0;i<values.Length;i++)values[i]=BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue.AsSpan(i*4,4));
        return new(definitions,obj,values);
    }

    internal static (LoadedObjects Source,DokObject Object) ResolveDeclaration(
        LoadedObjects definitions,string archive,uint key,Func<uint,LoadedObjects> loadDefinitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if(key==0)throw new InvalidDataException("Zero lookup key has the native null behavior and is not a resource declaration.");
        _=GameResourceId.Rdb(archive,0);var visited=new HashSet<(string,uint)>();
        for(int depth=0;depth<32;depth++)
        {
            if(!visited.Add((definitions.Source.ResourceUri,key)))throw new InvalidDataException("PostEffect3Lut source RDOK reference cycle.");
            var matches=definitions.Document.Objects.Where(o=>o.ObjectId==key).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("PostEffect3Lut source lookup requires one explicit registered object declaration.");
            var obj=matches[0];if(obj.Kind==DokObjectKind.Inline)return(definitions,obj);
            if(obj.Reference is not DokReference reference||obj.Properties.Count!=0)
                throw new NotSupportedException("PostEffect3Lut RDOK property inheritance requires its source merge implementation; it is not flattened here.");
            definitions=loadDefinitions(reference.ResourceId);key=reference.ObjectId;
        }
        throw new NotSupportedException("PostEffect3Lut reference depth exceeds the bounded source reader.");
    }

}
