using System.Buffers.Binary;
using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapRouteTextureGroup(RenderResourceDescriptor Declaration,
    LoadedRdbAsset Table, uint TextureObjectNameHash, IReadOnlyList<StaticTextureBinding> Images);

/// <summary>The direct route-draw program and its independent TextureGroups,
/// resolved in the map's actual resource namespace. This imports source media;
/// selecting the draw context and creating GPU state remain renderer work.</summary>
public sealed class MapRouteDrawResources
{
    public ulong ContextRevision { get; }
    public RenderResourceDescriptor ProgramReference { get; }
    public RenderResourceDescriptor ProgramDeclaration { get; }
    public LoadedShaderContainer Shader { get; }
    public G1ShaderBindingTables Bindings { get; }
    public IReadOnlyDictionary<uint, MapRouteTextureGroup> TextureGroups { get; }

    private MapRouteDrawResources(ulong revision, RenderResourceDescriptor reference,
        RenderResourceDescriptor declaration, LoadedShaderContainer shader,
        Dictionary<uint, MapRouteTextureGroup> groups)
    {
        ContextRevision = revision; ProgramReference = reference; ProgramDeclaration = declaration;
        Shader = shader; Bindings = shader.ReadG1Bindings();
        TextureGroups = new ReadOnlyDictionary<uint, MapRouteTextureGroup>(groups);
    }

    public static MapRouteDrawResources Load(GameResourceRepository repository, RenderResourceContext context)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(context);
        var reference = Require(0x653D89CF, 0x0118E31A);
        var declaration = Require(Word(reference.Object, 0xD78969E0), 0x4B00DA96);
        var shader = repository.LoadShaderContainer("system", Word(declaration.Object, ModelShaderResources.G1SPropertyHash));
        var groups = new Dictionary<uint, MapRouteTextureGroup>();
        // The four roles in each actual list bank: ribbon, bend, ordinary
        // endpoint, and endpoint on native surfaces15..18.
        foreach (uint key in new uint[] { 0xD7157FCA, 0x83DC9465, 0x5A883F24, 0x3133E9E3,
            0xE57FC322, 0xF7F0860D, 0xCE9C30CC, 0xA547DB8B })
        {
            var group = Require(key, 0x3059B9C3);
            var table = repository.LoadRdbAsset("MaterialEditor", Word(group.Object, 0x7A1E1EF8));
            if (table.Bytes.Length == 0 || table.Bytes.Length % 8 != 0)
                throw new InvalidDataException("route.texture_group.table_extent");
            var images = new List<StaticTextureBinding>(table.Bytes.Length / 8);
            for (int offset = 0; offset < table.Bytes.Length; offset += 8)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(offset, 4)) != (uint)images.Count)
                    throw new InvalidDataException("route.texture_group.table_ordinal");
                uint proxyName = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(offset + 4, 4));
                // A later registered resource can replace a proxy independently
                // of its group. Retain that actual winner, not a local-file alias.
                var proxy = Require(proxyName, 0xFF7DBFD4);
                images.Add(StaticTextureResources.Load(repository, proxy.Source, "MaterialEditor", [proxyName]).Single());
            }
            groups.Add(key, new(group, table, Word(group.Object, 0x8D16812D), images.AsReadOnly()));
        }
        return new(context.Revision, reference, declaration, shader, groups);

        RenderResourceDescriptor Require(uint name, uint expectedClass)
        {
            var descriptor = context.Lookup(name) ?? throw new InvalidDataException($"route.resource.{name:X8}.missing");
            if (descriptor.Object.Kind != DokObjectKind.Inline || descriptor.Object.ClassId != expectedClass)
                throw new NotSupportedException($"route.resource.{name:X8}.class_{descriptor.Object.ClassId:X8}");
            return descriptor;
        }
    }

    private static uint Word(DokObject source, uint name)
    {
        var field = source.Properties.SingleOrDefault(p => p.NameHash == name);
        if (field is not { TypeCode: 5, ElementCount: 1 } || field.RawValue.Length != 4)
            throw new InvalidDataException($"route.resource.field_{name:X8}");
        return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
    }
}
