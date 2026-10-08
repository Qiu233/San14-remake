using System.Collections.ObjectModel;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>An actual query of this imported source directory. It is not a
/// read of the original game's live registry. Missing declared source objects
/// cannot synthesize a registered resource in B3B6F0.</summary>
public sealed class OriginalArmyContextDataLookup
{
    internal OriginalArmyContextDataLookup(uint id,string evidence)
    {ActualContextObjectId=id;SourceEvidence=evidence;}
    public uint ActualContextObjectId { get; }
    public bool HasActualRegisteredDefinition => false;
    public string SourceEvidence { get; }
}

public sealed record OriginalArmyContextDataImportedSource(
    string ResourceUri,string DecodedSha256,int ObjectCount);
public sealed record OriginalArmyContextDataImportedIndex(
    string SourcePath,string Sha256,int RecordCount);

/// <summary>The complete installed RDB/DOK candidate namespace, imported once
/// into this program's owned directory. This includes all seven installed RDB
/// indexes and every source file with the actual DOK archive type; a texture
/// directory or a single material DOK cannot substitute for it. Queries only
/// inspect retained IDs and never reopen or hash source files.</summary>
public sealed class LoadedOriginalArmyContextDataSourceDirectory
{
    private sealed record Definition(string ResourceUri,uint? ClassId,DokObjectKind Kind);
    private readonly Dictionary<uint,List<Definition>> definitions;
    private readonly Dictionary<uint,List<string>> fileIds;
    private LoadedOriginalArmyContextDataSourceDirectory(string nativeEvidence,
        Dictionary<uint,List<Definition>> actualDefinitions,Dictionary<uint,List<string>> actualFileIds,
        IReadOnlyList<OriginalArmyContextDataImportedIndex> indices,
        IReadOnlyList<OriginalArmyContextDataImportedSource> sources)
    {
        definitions=actualDefinitions;fileIds=actualFileIds;ImportedIndices=indices;ImportedSources=sources;
        SourceEvidence=nativeEvidence+$";owned-complete-installed-source-directory;RDB={indices.Count};"+
            $"records={indices.Sum(x=>x.RecordCount)};DOK={sources.Count};objects={sources.Sum(x=>x.ObjectCount)}";
    }
    public string SourceEvidence { get; }
    public int ImportedDokCount => ImportedSources.Count;
    public IReadOnlyList<OriginalArmyContextDataImportedIndex> ImportedIndices { get; }
    public IReadOnlyList<OriginalArmyContextDataImportedSource> ImportedSources { get; }
    /// <summary>D9B636 clears EAX; D9B69B explicitly writes MaterialCPU+C8=0.
    /// The imported native source verifies the complete constructor once.</summary>
    public bool OriginalMaterialConstructorClearsContextC8 => true;

    public static LoadedOriginalArmyContextDataSourceDirectory Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string native=OriginalDeferredSamplerSources.LoadArmyContextDataNativeSource(repository.SourceDirectory,locale);
        var indices=new List<OriginalArmyContextDataImportedIndex>();
        var sources=new List<OriginalArmyContextDataImportedSource>();
        var definitions=new Dictionary<uint,List<Definition>>();
        var fileIds=new Dictionary<uint,List<string>>();
        var overrides=new ResourceOverrides(repository.OverrideDirectory);
        const uint actualDokType=0x20A6A0BB;
        foreach(string path in Directory.EnumerateFiles(Path.Combine(repository.SourceDirectory,"0002"),"*.rdb").Order(StringComparer.Ordinal))
        {
            string archive=Path.GetFileNameWithoutExtension(path);
            string relative=Path.GetRelativePath(repository.SourceDirectory,path).Replace('\\','/');
            byte[] raw=File.ReadAllBytes(overrides.Find(GameResourceId.File(relative))??path);var index=RdbIndexReader.Read(raw);
            indices.Add(new(relative,
                Convert.ToHexString(SHA256.HashData(raw)),index.Records.Count));
            foreach(var entry in index.Records)
            {
                string uri=GameResourceId.Rdb(archive,entry.FileId).ToString();
                if(!fileIds.TryGetValue(entry.FileId,out var files))fileIds.Add(entry.FileId,files=[]);
                files.Add(uri);
                if(entry.TypeId!=actualDokType)continue;
                // Use the retained index and configured logical URI override.
                // Each source index/payload is read once for this import.
                string? replacement=overrides.Find(GameResourceId.Rdb(archive,entry.FileId));
                byte[] bytes=replacement is null?RdbArchiveReader.ReadPayload(path,index,entry):File.ReadAllBytes(replacement);
                var dok=DokReader.Read(bytes);
                sources.Add(new(uri,Convert.ToHexString(SHA256.HashData(bytes)),dok.Objects.Count));
                foreach(var obj in dok.Objects)
                {
                    if(!definitions.TryGetValue(obj.ObjectId,out var objects))definitions.Add(obj.ObjectId,objects=[]);
                    objects.Add(new(uri,obj.ClassId,obj.Kind));
                }
            }
        }
        if(!sources.Any(x=>x.ResourceUri==GameResourceId.Rdb("KIDSSystemResource",OriginalArmyFlagMaterialVariations.MaterialDictionaryId).ToString()))
            throw new InvalidDataException("Complete Context Data source import did not contain its actual material dictionary.");
        return new(native,definitions,fileIds,new ReadOnlyCollection<OriginalArmyContextDataImportedIndex>(indices),
            new ReadOnlyCollection<OriginalArmyContextDataImportedSource>(sources));
    }

    /// <summary>True source-directory MISS follows B3B6F0's short path. A
    /// registered candidate HIT requires its real class/type13 producer and is
    /// rejected explicitly while unsupported. Absence is never supplied by a
    /// caller's null adapter and is not a claim about the original live process.</summary>
    public OriginalArmyContextDataLookup ReadCurrent(uint actualContextObjectId)
    {
        if(actualContextObjectId==0)return new(0,SourceEvidence+";B3B6F0-zero-key-null");
        if(definitions.TryGetValue(actualContextObjectId,out var objects))
            throw new NotSupportedException($"Context Data {actualContextObjectId:X8} has actual source definitions: "+
                string.Join(",",objects.Select(x=>$"{x.ResourceUri}[{x.ClassId?.ToString("X8")??x.Kind.ToString()}]"))+
                "; its registered class/context type13 producer must be supported before publishing C8.");
        if(fileIds.TryGetValue(actualContextObjectId,out var files))
            throw new NotSupportedException($"Context Data {actualContextObjectId:X8} has actual RDB file candidates: "+
                string.Join(",",files)+"; its actual context producer must be supported before publishing C8.");
        return new(actualContextObjectId,SourceEvidence+";B3B6F0-owned-directory-MISS-no-factory");
    }
    public OriginalArmyContextDataLookup ReadActualContextDataLookup(LoadedOriginalArmyMaterialParameterSource actualMaterial)
    {
        ArgumentNullException.ThrowIfNull(actualMaterial);
        return ReadCurrent(actualMaterial.RegisteredContextResourceObjectId);
    }
}
