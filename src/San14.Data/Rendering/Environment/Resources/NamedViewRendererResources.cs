using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The water construction and normal texture declarations selected by
/// a named View. This resource owner does not assert GPU or renderer readiness.</summary>
public sealed class NamedViewRendererResources
{
    private NamedViewRendererResources(LoadedObjects definitions, DokObject renderer,
        OceanGridSettings declaredGrid, uint dimensionOverride,
        uint normalVolumeGroup, uint normalNoiseGroup)
    {
        Definitions = definitions;
        DeclaredGrid = declaredGrid;
        OceanGrid = dimensionOverride == 0 ? declaredGrid : declaredGrid with
        { Columns = dimensionOverride, Rows = dimensionOverride };
        NormalVolumeGroup = normalVolumeGroup;
        NormalNoiseGroup = normalNoiseGroup;
        SourceIdentity = $"{definitions.Source.ResourceUri}@{definitions.Source.DecodedSha256}#object/{renderer.ObjectId:X8}";
    }

    internal LoadedObjects Definitions { get; }
    public OceanGridSettings DeclaredGrid { get; }
    public OceanGridSettings OceanGrid { get; }
    public uint NormalVolumeGroup { get; }
    public uint NormalNoiseGroup { get; }
    public string SourceIdentity { get; }

    /// <summary>Uses the normal map's registered definition libraries and its
    /// owned initial Common settings. No executable or saved CPU image is read.</summary>
    public static NamedViewRendererResources Load(GameResourceRepository repository)
    {
        var view = repository.LoadObjects("system", DefaultView0Source.ViewResourceId);
        var definitions = repository.LoadObjects("system", 0xDAB638B2);
        var common = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId);
        uint dimensionOverride = CommonRenderResources.ReadScalar(common, 0x27EE7E2F, 5);
        return Read(view, DefaultView0Source.ViewObjectId, definitions, dimensionOverride);
    }

    public static NamedViewRendererResources Read(LoadedObjects viewDefinitions, uint viewObjectId,
        LoadedObjects rendererDefinitions, uint dimensionOverride)
    {
        // Decode source bytes here; parsed caller metadata is not an authority.
        var view = DokReader.Read(viewDefinitions.Source.Bytes).Objects.Single(value => value.ObjectId == viewObjectId);
        if (view.Kind != DokObjectKind.Inline || view.ClassId != 0x97AF26FD)
            throw new NotSupportedException("Renderer selection requires an inline View declaration.");
        uint rendererId = Word(Field(view, 0xCA75FFCD, 5, 1), 0);
        if (rendererId == 0)
            throw new NotSupportedException("The selected View declares no renderer for Ocean preparation.");
        var definitions = new LoadedObjects(DokReader.Read(rendererDefinitions.Source.Bytes), rendererDefinitions.Source);
        var renderer = definitions.Document.Objects.Single(value => value.ObjectId == rendererId);
        if (renderer.Kind != DokObjectKind.Inline || renderer.ClassId != 0x83F5B40D)
            throw new NotSupportedException("Ocean preparation requires an inline renderer declaration in the selected library.");
        var dimensions = Field(renderer, 0x75DF5C31, 5, 2);
        var size = Field(renderer, 0xCE5ADEC6, 8, 2);
        var grid = new OceanGridSettings(Word(dimensions, 0), Word(dimensions, 1),
            new Vector2(Float(size, 0), Float(size, 1)),
            Float(Field(renderer, 0x601A5D90, 8, 1), 0),
            Word(Field(renderer, 0x9731A846, 5, 1), 0),
            Float(Field(renderer, 0x0CAE2711, 8, 1), 0));
        return new(definitions, renderer, grid, dimensionOverride,
            Word(Field(renderer, 0xD322225E, 5, 1), 0),
            Word(Field(renderer, 0x7852C9D5, 5, 1), 0));
    }

    private static DokProperty Field(DokObject owner, uint name, uint type, int count)
    {
        var field = owner.Properties.Single(value => value.NameHash == name);
        if (field.TypeCode != type || field.ElementCount != count || field.ElementSize != 4 || field.RawValue.Length != count * 4)
            throw new InvalidDataException($"Renderer property {name:X8} requires its declared type and dimensions.");
        return field;
    }

    private static uint Word(DokProperty field, int index) =>
        BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(index * 4, 4));
    private static float Float(DokProperty field, int index) =>
        BinaryPrimitives.ReadSingleLittleEndian(field.RawValue.AsSpan(index * 4, 4));
}
