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

    /// <summary>Import the selected Static volume from its already parsed declaration and texture container.</summary>
    public static SkyTextureSource ImportVolume(StaticTextureBinding source, uint nativeViewFormat)
    {
        ReadVolumeImage(source);
        return SkyTextureSource.Import(source.Resource, source.ImageIndex, nativeViewFormat);
    }

    internal static G1TextureImage ReadVolumeImage(StaticTextureBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var declarations = source.Definitions.Document.Objects.Where(o => o.ObjectId == source.Definition.ObjectId).ToArray();
        if (declarations.Length != 1 || declarations[0].Kind != DokObjectKind.Inline || declarations[0].ClassId != 0xFF7DBFD4)
            throw new InvalidDataException("LUT upload requires one inline Static declaration in its loaded definitions.");
        var declaration = declarations[0];
        uint context = BinaryPrimitives.ReadUInt32LittleEndian(Field(declaration, 0x6C7321D2, 5).RawValue);
        uint tid = BinaryPrimitives.ReadUInt32LittleEndian(Field(declaration, 0x00200571, 4).RawValue);
        if (context != source.Resource.Source.FileId || tid != source.ImageIndex ||
            (uint)source.ImageIndex >= source.Resource.Container.Textures.Count)
            throw new InvalidDataException("Static volume context/TID does not identify its supplied texture child.");
        var image = source.Resource.Container.Textures[source.ImageIndex];
        if (image.TextureSystem != 2 || image.LayerCount != 1 || image.Depth <= 1 ||
            image.SubresourceLayout != G1TextureSubresourceLayout.VolumeMipChain)
            throw new InvalidDataException("PostEffect3 Static LUT requires an encoded 3D volume, not a flattened 2D array.");
        return image;
    }

    public static IReadOnlyList<StaticTextureBinding> Load(GameResourceRepository repository,
        LoadedObjects definitions,string explicitArchive,IReadOnlyList<uint> explicitObjectIds)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(explicitObjectIds);
        var cache=new Dictionary<uint,LoadedTextureContainer>();
        return Load(definitions,explicitObjectIds,id=>
        {
            if(!cache.TryGetValue(id,out var resource))
                cache.Add(id,resource=repository.LoadTextureContainer(explicitArchive,id));
            return resource;
        });
    }

    internal static IReadOnlyList<StaticTextureBinding> Load(LoadedObjects definitions,
        IReadOnlyList<uint> explicitObjectIds,Func<uint,LoadedTextureContainer> loadTexture)
    {
        var bindings=new List<StaticTextureBinding>();
        foreach(uint id in explicitObjectIds)
        {
            var matches=definitions.Document.Objects.Where(o=>o.ObjectId==id).ToArray();
            if(matches.Length!=1 || matches[0].Kind!=DokObjectKind.Inline || matches[0].ClassId!=0xFF7DBFD4)
                throw new InvalidDataException($"{definitions.Source.ResourceUri} requires one inline Static texture {id:X8}.");
            var obj=matches[0];
            var context=Field(obj,0x6C7321D2,5);var image=Field(obj,0x00200571,4);
            uint contextId=BinaryPrimitives.ReadUInt32LittleEndian(context.RawValue!);
            uint tid=BinaryPrimitives.ReadUInt32LittleEndian(image.RawValue!);
            var resource=loadTexture(contextId);
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
