using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record StaticTextureBinding(LoadedObjects Definitions,DokObject Definition,
    DokProperty ContextReference,DokProperty ImageReference,LoadedTextureContainer Resource,int ImageIndex)
{
    public G1TextureImage Image=>Resource.Container.Textures[ImageIndex];
}

/// <summary>Resolve explicit original Static objects through their context resource
/// hash and TID. Object IDs are never treated as file IDs; namespace stays explicit.</summary>
public static class StaticTextureResources
{
    public const uint TextureInterfaceClassId = 0xB672FAA2;

    /// <summary>Detach an imported planar image before creating its device
    /// owner. Default view and sampler come from the selected image.</summary>
    public static SkyTextureSource Import2D(StaticTextureBinding source, uint? nativeViewFormat = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var image = SkyTextureSource.Import(source.Resource, source.ImageIndex,
            nativeViewFormat ?? ModelStageTextures.ReadNativeViewFormat(source.Image));
        if (image.SourceDimension != 4)
            throw new NotSupportedException("Registered planar textures require a two-dimensional image.");
        return image;
    }

    public static IReadOnlyList<StaticTextureBinding> Load(GameResourceRepository repository,
        LoadedObjects definitions,string explicitArchive,IReadOnlyList<uint> explicitObjectIds)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(explicitObjectIds);
        var cache=new Dictionary<uint,LoadedTextureContainer>();var bindings=new List<StaticTextureBinding>();
        foreach(uint id in explicitObjectIds)
        {
            var matches=definitions.Document.Objects.Where(o=>o.ObjectId==id).ToArray();
            if(matches.Length!=1 || matches[0].Kind!=DokObjectKind.Inline || matches[0].ClassId!=0xFF7DBFD4)
                throw new InvalidDataException($"{definitions.Source.ResourceUri} requires one inline Static texture {id:X8}.");
            var obj=matches[0];
            var context=Field(obj,0x6C7321D2,5);var image=Field(obj,0x00200571,4);
            uint contextId=BinaryPrimitives.ReadUInt32LittleEndian(context.RawValue!);
            uint tid=BinaryPrimitives.ReadUInt32LittleEndian(image.RawValue!);
            if(!cache.TryGetValue(contextId,out var resource))
                cache.Add(contextId,resource=repository.LoadTextureContainer(explicitArchive,contextId));
            if(tid>=resource.Container.Textures.Count)
                throw new InvalidDataException($"Static {id:X8} TID{tid} lies outside {resource.Source.ResourceUri}.");
            bindings.Add(new(definitions,obj,context,image,resource,(int)tid));
        }
        return bindings.AsReadOnly();
    }

    private static DokProperty Field(DokObject obj,uint hash,ushort type)
    {
        var fields=obj.Properties.Where(p=>p.NameHash==hash).ToArray();
        if(fields.Length!=1 || fields[0].TypeCode!=type || fields[0].ElementCount!=1 ||
            fields[0].ElementSize!=4 || fields[0].RawValue is not {Length:4})
            throw new InvalidDataException($"Static texture {obj.ObjectId:X8} requires one type{type} scalar {hash:X8}.");
        return fields[0];
    }
}
