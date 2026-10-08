using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The original main parameter-group names, imported once from native
/// constructor directories. These are unrelated to physical shader RDEF ordinals.
/// The narrow imported query below is nMtrID; conditional lighting group lengths
/// stay unspecified. The caller supplies a successfully created fresh full table.</summary>
public sealed class LoadedOriginalModelNativeParameterDirectorySource
{
    private readonly IReadOnlyDictionary<string,IReadOnlyList<string>> directories;
    internal LoadedOriginalModelNativeParameterDirectorySource(string locale,string executableHash,
        string textHash,IReadOnlyDictionary<string,string> windows,
        IReadOnlyDictionary<string,IReadOnlyList<string>> actualDirectories)
    { Locale=locale;ExecutableSha256=executableHash;DecodedTextSha256=textHash;
      WindowSha256=windows;directories=actualDirectories; }
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public IReadOnlyDictionary<string,string> WindowSha256 { get; }
    /// <summary>Native local backing arrays. Conditional plugins may expose a
    /// prefix of these names or preserve the preceding directory. Their array
    /// lengths are not a claim about the current constructed Group54 count.</summary>
    public IReadOnlyDictionary<string,IReadOnlyList<string>> ActualLocalDirectoryNames => directories;
    public string SourceEvidence => $"SAN14CE_{Locale}.exe@{ExecutableSha256};"+
        "J30/Group40/Group54;97E1C0-source-order/97E190-inherited-append/970810-exact-name;"+
        "fresh-successful-created-row;nMtrID-only;conditional-lighting-lengths-preserved";
    public static LoadedOriginalModelNativeParameterDirectorySource Load(
        OriginalGameRepository repository,string locale)
        => OriginalDeferredSamplerSources.LoadModelNativeParameterDirectorySource(repository.SourceDirectory,locale);

    /// <summary>Every own/dependency row is retained, including explicit nulls
    /// and provider classes that differ from the owner. Loading resources is an
    /// import operation; reuse its returned lookups for normal material requests.</summary>
    public IReadOnlyList<OriginalArmyMaterialShaderParameterLookup> ReadFreshImportedFullTable(
        OriginalGameRepository repository,OriginalNamedModelShader actualFullTable)
    {
        var rows=new List<OriginalArmyMaterialShaderParameterLookup>(actualFullTable.DependencyObjectHashes.Count+1)
        { ReadFreshImportedRow(repository,actualFullTable) };
        foreach(uint id in actualFullTable.DependencyObjectHashes)
            rows.Add(id==0?NullLookup():ReadFreshImportedRow(repository,
                OriginalModelShaderResources.ReadDeclaredShader(actualFullTable.Objects,id)));
        return new ReadOnlyCollection<OriginalArmyMaterialShaderParameterLookup>(rows);
    }

    public OriginalArmyMaterialShaderParameterLookup ReadFreshImportedRow(
        OriginalGameRepository repository,OriginalNamedModelShader? actualProvider)
    {
        if(actualProvider is null)return NullLookup();
        LoadedShaderContainer shader=OriginalModelShaderResources.LoadDeclaredG1S(repository,actualProvider);
        return ReadFreshImportedRow(actualProvider,shader);
    }

    public OriginalArmyMaterialShaderParameterLookup ReadFreshImportedRow(
        OriginalNamedModelShader actualProvider,LoadedShaderContainer actualShader)
    {
        if(actualProvider.ShaderFileHash!=actualShader.Source.FileId)
            throw new InvalidDataException("Native parameter directory needs the provider's actual G1 module.");
        string? baseDirectory=actualProvider.Object.ClassId switch {
            0x18EB1E56=>"Class18Base",0x48F274C8=>"PointBase",0xE43743B2=>"OccluderBase",
            0x39E93192 or 0xCF1FF0C9 or 0x6794EA8A or 0xFB79EF73 or 0x4245A167=>null,
            _=>throw new NotSupportedException("Native parameter directory has no audited actual creator recipe.") };
        var configurations=G1ShaderConfigurationReader.Read(actualShader.Source.Bytes,actualShader.Container).Records;
        int ordinal=0;bool prefixLengthKnown=true;int? found=null;
        void Append(IReadOnlyList<string> names,bool lengthKnown) {
            int local=-1;
            for(int i=0;i<names.Count;i++)if(names[i]=="nMtrID") { local=i;break; }
            if(local>=0) {
                if(!prefixLengthKnown)throw new NotSupportedException("nMtrID follows a conditional native descriptor length.");
                // 970810 examines the most recently appended local directory first.
                found=ordinal+local;
            }
            if(lengthKnown)ordinal+=names.Count;else prefixLengthKnown=false;
        }
        if(baseDirectory is {} key)Append(directories[key],true);
        foreach(var configuration in configurations) {
            string name=configuration.Name;
            if(directories.TryGetValue(name,out var names)) {
                if(name=="GBuffer") {
                    // 9A6F90 rejects only a negative ShaderType before its cache
                    // creation; a supplied successfully created row owns its group.
                    // 96E7C0 uses signed-byte Horner27, a separate name domain
                    // from the material suffix's signed-byte powers31 hash.
                    uint shaderTypeHash=0;
                    foreach(char c in "ShaderType")shaderTypeHash=unchecked(shaderTypeHash*27+(uint)(sbyte)c);
                    var shaderType=configuration.Find(shaderTypeHash);
                    if(shaderType is not null&&unchecked((int)shaderType.RawValue)<0)
                        throw new InvalidDataException("GBuffer source layout cannot produce a successful native row.");
                }
                Append(names,name is not ("ClusterLightList" or "SubPointLightShadows" or "SubSpotLightShadows"));
            }
            else if(name is "SubShadowMaps" or "Scattering2FogAccel") {
                // Actual instance.v8 is B28380: return the preceding descriptor.
            }
            else if(name is "Instancing2" or "AlphaTest" or "Cloth" or "PSM" or "Distance") {
                // No normal source-created named factory is registered for these
                // optional modules in the audited fresh original startup scope.
                if((configuration.RawSecondWord&2)!=0)
                    throw new InvalidDataException("An absent named factory was declared required by the actual module.");
            }
            else throw new NotSupportedException($"Native names for module {name} have not been imported.");
        }
        int result=found??-1;
        return new(name=>name=="nMtrID"?result:
            throw new NotSupportedException("This imported native query currently supports nMtrID only."));
    }

    private static OriginalArmyMaterialShaderParameterLookup NullLookup()
        => new(name=>name=="nMtrID"?-1:
            throw new NotSupportedException("This imported native query currently supports nMtrID only."));
}
