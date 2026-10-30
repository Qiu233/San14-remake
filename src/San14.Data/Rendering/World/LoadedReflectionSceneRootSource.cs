using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One actual child name in the selected Scene Octree declaration.
/// This retains the individual instance and its definition, including RDOK
/// instance overrides; two clones of one definition remain distinct rows.
/// Declaration membership does not grant current World activation.</summary>
public sealed record ReflectionSceneRootReference(int Index,uint ObjectId,int Flags,
    DokObject Instance,LoadedObjects InstanceDocument,
    DokObject ResolvedDefinition,LoadedObjects DefinitionDocument,int? VisibilitySetIndex)
{
    public string SourceIdentity=>InstanceDocument.Source.ResourceUri+"#object/"+ObjectId.ToString("X8");
    public SceneInstance AsSceneInstance()=>new(Index,Instance,InstanceDocument,ResolvedDefinition,DefinitionDocument);
}

/// <summary>The actual Scene descriptor's selected class2C012245 Octree and
/// all its typed dependency references. Load once per actual resource revision;
/// current registration, spatial activation and traversal are separate owners.</summary>
public sealed class LoadedReflectionSceneRootSource
{
    private LoadedReflectionSceneRootSource(LoadedSceneResources scene,LoadedObjects rootDocument,
        DokObject root,uint capacity,Vector3 position,Vector3 scale,uint external,
        ReflectionSceneRootReference[] references)
    {
        Scene=scene;RootDocument=rootDocument;RootDeclaration=root;Capacity=capacity;
        Position=position;Scale=scale;ExternalObjectId=external;
        SourceReferences=Array.AsReadOnly(references);
    }
    public LoadedSceneResources Scene {get;}
    public LoadedObjects RootDocument {get;}
    public DokObject RootDeclaration {get;}
    public uint ObjectId=>RootDeclaration.ObjectId;
    public uint Capacity {get;}
    public Vector3 Position {get;}
    public Vector3 Scale {get;}
    public uint ExternalObjectId {get;}
    public IReadOnlyList<ReflectionSceneRootReference> SourceReferences {get;}

    /// <summary>Complete selected Root Model/Effect inventory. Existing
    /// VisibilitySet indices retain their source identities; Root-only children
    /// receive separate identity indices, never VisibilitySet array positions.</summary>
    public ScenePlacements CreateModelAndEffectPlacements()
    {
        int nextIndex=Scene.Instances.Count;
        var selected=SourceReferences.Where(row=>row.ResolvedDefinition.ClassId is
            ScenePlacementReader.ModelWorldPqClass or ScenePlacementReader.EffectWorldPqClass)
            .Select(row=>new SceneInstance(row.VisibilitySetIndex??nextIndex++,row.Instance,row.InstanceDocument,
                row.ResolvedDefinition,row.DefinitionDocument)).ToArray();
        return ScenePlacementReader.ReadSelected(Scene,selected);
    }

    public static LoadedReflectionSceneRootSource Load(GameResourceRepository repository,LoadedSceneResources actualScene)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualScene);
        uint rootName=Word(actualScene.RootObject,0x72548CCA);
        // B3B6F0 resolves names within this actual Scene's declared context.
        // Unrelated installed libraries cannot satisfy missing/ambiguous names.
        var resources=actualScene.Dependencies.Select(d=>d.Resource).Prepend(actualScene.Scene).ToArray();
        var candidates=resources.SelectMany(r=>r.Document.Objects.Select(o=>(Resource:r,Object:o)))
            .GroupBy(p=>p.Object.ObjectId).ToDictionary(g=>g.Key,g=>g.ToArray());
        var selected=Unique(rootName);
        if(selected.Object.Kind!=DokObjectKind.Inline||selected.Object.ClassId!=0x2C012245)
            throw new NotSupportedException("Selected Scene Octree requires its actual inline class2C012245 declaration.");
        var root=selected.Object;
        var names=Property(root,0x51FEA812,5);
        var flags=Property(root,0xCA014F47,4);
        if(names.ElementCount!=flags.ElementCount)
            throw new InvalidDataException("Scene Octree child names and signed flag arrays differ in length.");
        uint capacity=Word(root,0xD1E088AA);
        if(capacity==0||capacity>=0xFFFE||names.ElementCount>capacity)
            throw new NotSupportedException("Scene Octree requires its actual supported finite spatial capacity.");
        var loaded=resources.GroupBy(r=>r.Source.FileId).ToDictionary(g=>g.Key,g=>g.First());
        // A definition is loaded only through the instance's explicit RDOK
        // reference, in the same archive as this selected normal Scene.
        string uri=actualScene.Scene.Source.ResourceUri;
        const string prefix="san14://rdb/";
        if(!uri.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidDataException("Scene has no RDB archive identity.");
        string archive=uri[prefix.Length..].Split('/')[0];
        var visibility=actualScene.Instances.ToDictionary(i=>i.Object.ObjectId,i=>i.Index);
        var rows=new ReflectionSceneRootReference[names.ElementCount];
        for(int index=0;index<rows.Length;index++)
        {
            uint name=BinaryPrimitives.ReadUInt32LittleEndian(names.RawValue.AsSpan(index*4));
            var source=Unique(name);var definition=source.Object;var definitionDocument=source.Resource;
            if(source.Object.Kind==DokObjectKind.Reference)
            {
                var reference=source.Object.Reference??throw new InvalidDataException("Scene child RDOK reference is absent.");
                if(!loaded.TryGetValue(reference.ResourceId,out definitionDocument))
                    loaded.Add(reference.ResourceId,definitionDocument=repository.LoadObjects(archive,reference.ResourceId));
                var matches=definitionDocument.Document.Objects.Where(o=>o.ObjectId==reference.ObjectId).ToArray();
                if(matches.Length!=1||matches[0].Kind!=DokObjectKind.Inline)
                    throw new NotSupportedException("Scene child requires its unique actual inline definition; a further RDOK chain needs its own inheritance owner.");
                definition=matches[0];
            }
            rows[index]=new(index,name,BinaryPrimitives.ReadInt32LittleEndian(flags.RawValue.AsSpan(index*4)),
                source.Object,source.Resource,definition,definitionDocument,visibility.TryGetValue(name,out int v)?v:null);
        }
        return new(actualScene,selected.Resource,root,capacity,Vector(root,0x27D889E5),
            Vector(root,0xFEA9E4F8),Word(root,0x1BE08F85),rows);

        (LoadedObjects Resource,DokObject Object) Unique(uint name)
        {
            if(!candidates.TryGetValue(name,out var matches)||matches.Length!=1)
                throw new InvalidDataException("Scene Octree name "+name.ToString("X8")+" is missing or ambiguous in its declared context.");
            return matches[0];
        }
    }
    private static DokProperty Property(DokObject source,uint hash,uint type,int? count=null)
    {
        var values=source.Properties.Where(p=>p.NameHash==hash).ToArray();
        if(values.Length!=1||values[0].TypeCode!=type||values[0].ElementSize!=4||
            values[0].RawValue.Length!=checked(values[0].ElementCount*4)||
            count is {} n&&values[0].ElementCount!=n)
            throw new InvalidDataException("Scene Octree typed resource property differs: "+hash.ToString("X8"));
        return values[0];
    }
    private static uint Word(DokObject source,uint hash)=>BinaryPrimitives.ReadUInt32LittleEndian(Property(source,hash,5,1).RawValue);
    private static Vector3 Vector(DokObject source,uint hash)
    {
        var bytes=Property(source,hash,8,3).RawValue;
        return new(BinaryPrimitives.ReadSingleLittleEndian(bytes),BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(4)),
            BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(8)));
    }
}
