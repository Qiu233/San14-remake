namespace San14.Data;

/// <summary>Owns the parsed postprocessing resources of one renderer. The caller serializes
/// access; camera frames and dynamic selectors are resolved separately.</summary>
public sealed class MapPostEffect3Resources
{
    private readonly GameResourceRepository repository;
    private readonly string definitionsArchive,textureArchive;
    private readonly uint rootFileId;
    private readonly Dictionary<(string Archive,uint FileId),LoadedObjects> definitions=new();
    private readonly Dictionary<uint,LoadedTextureContainer> textures=new();
    private readonly Dictionary<uint,LoadedMapPostEffect3LutSource> declarations=new();
    private readonly Dictionary<uint,StaticTextureBinding> children=new();
    private readonly Dictionary<(LoadedMapCameraConstructionSource Camera,string Locale),LoadedMapPostEffect3LensPresetSource> lensPresets=new();
    private readonly Dictionary<(string Archive,uint FileId),OverrideStamp> overrideStamps=new();

    public MapPostEffect3Resources(GameResourceRepository repository,string definitionsArchive,
        uint rootFileId,string textureArchive)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionsArchive);
        ArgumentException.ThrowIfNullOrWhiteSpace(textureArchive);
        _=GameResourceId.Rdb(definitionsArchive,rootFileId);
        _=GameResourceId.Rdb(textureArchive,0);
        this.repository=repository;this.definitionsArchive=definitionsArchive;
        this.rootFileId=rootFileId;this.textureArchive=textureArchive;
    }

    public (LoadedMapPostEffect3LutSource Declaration,StaticTextureBinding? SelectedStaticTexture)
        ResolveLut(uint requestedKey,uint selector)
    {
        RefreshChangedOverrides();
        if(!declarations.TryGetValue(requestedKey,out var declaration))
        {
            declaration=MapPostEffect3LutSource.Load(LoadLutDefinitions(rootFileId),definitionsArchive,requestedKey,LoadLutDefinitions);
            declarations.Add(requestedKey,declaration);
        }
        if(declaration.GetDeclaredChildKey(selector) is not uint childKey)return(declaration,null);
        if(!children.TryGetValue(childKey,out var child))
        {
            child=declaration.LoadStaticForSelector(selector,LoadLutDefinitions(rootFileId),definitionsArchive,LoadLutDefinitions,LoadTexture)!;
            children.Add(childKey,child);
        }
        return(declaration,child);
    }

    public LoadedMapPostEffect3LensPresetSource ResolveLensPreset(LoadedMapCameraConstructionSource camera,string locale)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if(locale is not ("TC" or "SC"))throw new ArgumentOutOfRangeException(nameof(locale));
        RefreshChangedOverrides();
        var key=(camera,locale);
        if(!lensPresets.TryGetValue(key,out var preset))
        {
            preset=MapPostEffect3LensPresetSource.LoadForCamera(
                ()=>LoadDefinitions("system",MapPostEffect3LensPresetSource.SourceFileId),camera,locale);
            lensPresets.Add(key,preset);
        }
        return preset;
    }

    /// <summary>Drop loaded and derived resources, including changes that preserve
    /// both file length and timestamp. Already returned owners remain unchanged.</summary>
    public void Invalidate()
    {
        definitions.Clear();textures.Clear();declarations.Clear();children.Clear();lensPresets.Clear();overrideStamps.Clear();
    }

    private LoadedObjects LoadLutDefinitions(uint fileId)=>LoadDefinitions(definitionsArchive,fileId);

    private LoadedObjects LoadDefinitions(string archive,uint fileId)
    {
        var key=(archive,fileId);
        if(!definitions.TryGetValue(key,out var loaded))
        {
            overrideStamps[key]=ReadOverrideStamp(key);
            loaded=repository.LoadObjects(archive,fileId);
            definitions.Add(key,loaded);
        }
        return loaded;
    }

    private LoadedTextureContainer LoadTexture(uint fileId)
    {
        if(!textures.TryGetValue(fileId,out var loaded))
        {
            var key=(textureArchive,fileId);
            overrideStamps[key]=ReadOverrideStamp(key);
            loaded=repository.LoadTextureContainer(textureArchive,fileId);
            textures.Add(fileId,loaded);
        }
        return loaded;
    }

    private void RefreshChangedOverrides()
    {
        List<((string Archive,uint FileId) Key,OverrideStamp Stamp)>? changes=null;
        foreach(var pair in overrideStamps)
        {
            var current=ReadOverrideStamp(pair.Key);
            if(current!=pair.Value)(changes??=new()).Add((pair.Key,current));
        }
        if(changes is null)return;
        declarations.Clear();children.Clear();lensPresets.Clear();
        foreach(var change in changes)
        {
            definitions.Remove(change.Key);
            if(change.Key.Archive==textureArchive)textures.Remove(change.Key.FileId);
            overrideStamps[change.Key]=change.Stamp;
        }
    }

    private OverrideStamp ReadOverrideStamp((string Archive,uint FileId) key)
    {
        if(repository.OverrideDirectory is not{} directory)return default;
        var id=GameResourceId.Rdb(key.Archive,key.FileId);
        var file=new FileInfo(Path.Combine(directory,id.OverrideRelativePath.Replace('/',Path.DirectorySeparatorChar)));
        return file.Exists?new(file.FullName,true,file.Length,file.LastWriteTimeUtc):new(file.FullName,false,0,default);
    }

    private readonly record struct OverrideStamp(string? Path,bool Exists,long Length,DateTime LastWriteTimeUtc);
}
