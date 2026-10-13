using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Decoded resource identity and both provenance layers, including explicit overrides.</summary>
public sealed record TerrainSeasonResourceIdentity(string ResourceUri, uint FileId,
    string DecodedSha256, string SourceContainerPath, SourceProvenance IndexSource,
    SourceProvenance? OverrideSource)
{
    public bool IsOverride => OverrideSource is not null;
}

public sealed record TerrainSeasonObjectInput(string Role,
    TerrainSeasonResourceIdentity Source, int SourceObjectIndex, uint ObjectId,
    uint? ClassId, DokObjectKind Kind, IReadOnlyList<DokProperty> Properties);

public sealed record TerrainSeasonTextureInput(TerrainTextureGroupKind Group,
    uint Slot, TerrainSeasonResourceIdentity TableSource,
    TerrainSeasonResourceIdentity TextureSource, int TextureIndex,
    uint ProxyObjectId, int Width, int Height, int LayerCount, int MipCount);

public sealed record TerrainSeasonUnresolvedInput(string Key, string Reason);

/// <summary>The accepted current terrain is retained separately from the unresolved
/// seasonal producer. The four named requests do not invent four material snapshots.</summary>
public sealed record TerrainSeasonResourcesResult(
    TerrainSeasonCalendarInput Calendar, MapSeasonSelection Selection,
    LoadedTerrainGbiMapResources CurrentTerrain,
    IReadOnlyList<TerrainSeasonObjectInput> SourceObjects,
    IReadOnlyList<TerrainSeasonTextureInput> TextureInputs,
    TerrainSeasonResourceIdentity ShaderSource,
    G2ShaderFeatureRecord DetailFeature, G2ShaderCombination DetailCombination,
    G2ShaderFeatureRecord CoarseFeature, G2ShaderCombination CoarseCombination,
    string CurrentControlRgbaSha256,
    IReadOnlyList<TerrainSeasonUnresolvedInput> UnresolvedInputs)
{
    public bool SeasonalTerrainProducerResolved => false;
    public bool CalendarUpdateProviderResolved => false;
    public bool WeatherProviderResolved => false;
    public bool ColdSnowBranchResolved => false;
    public bool FinalDisplayVerified => false;
}

/// <summary>Reads the actual already accepted material inputs without opening an
/// installation, executing original code, interpreting a name hash as a file ID,
/// or replacing missing seasonal sources with color grading.</summary>
public static class TerrainSeasonResources
{
    public static TerrainSeasonResourcesResult ReadCurrentInputs(
        LoadedTerrainGbiMapResources terrain, TerrainSeasonCalendarInput calendar)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(calendar);
        MapSeasonSelection selection = MapSeasonMath.SelectMonth(calendar.SourceMonth);
        ValidateCalendar(calendar);
        LoadedTerrainParameterResources parameters = terrain.Parameters;
        LoadedObjects bundle = parameters.Scene.TerrainBundle;
        DokObject[] fields = bundle.Document.Objects.Where(item =>
            item.Kind == DokObjectKind.Inline && item.ClassId == 0x468415D9).ToArray();
        if (fields.Length != 1)
            throw new InvalidDataException("Season input inspection requires the accepted terrain Field object.");
        var objects = new List<TerrainSeasonObjectInput>
        {
            SnapshotObject("LandscapeComposition", bundle.Source, parameters.TerrainRoot),
            SnapshotObject("Field", bundle.Source, fields[0]),
            SnapshotObject("TextureArrayInfo", bundle.Source, parameters.TextureArrayInfo),
            SnapshotObject("Landscape", parameters.Wrapper.Source, parameters.WrapperRoot),
            SnapshotObject("LandscapePalette", parameters.Wrapper.Source, parameters.ParameterObject)
        };
        var textures = new List<TerrainSeasonTextureInput>();
        AddGroup(TerrainTextureGroupKind.Field, terrain.Field);
        AddGroup(TerrainTextureGroupKind.Root, terrain.Root);
        if (terrain.Auxiliary is not null) AddGroup(TerrainTextureGroupKind.Auxiliary, terrain.Auxiliary);
        var unresolved = new TerrainSeasonUnresolvedInput[]
        {
            new("season-action-source", "Native context key 61AD16CF selects an action table; its physical document URI and original name/object columns remain unresolved."),
            new("season-action-application", "The named action and native active-list insertion are proved; action traversal, blending and terrain property writes remain unresolved."),
            new("season-calendar-writer", "The original season consumes world+34 month; its ordinary writer and relation to Core simulation dates remain unresolved."),
            new("season-weather-provider", "The current terrain packet does not provide an applied seasonal weather context."),
            new("season-cold-snow-branch", "Cold/snow producer, branch condition and applied material/control inputs remain unresolved; winter does not imply a fabricated snow layer."),
            new("season-effect-source", "Native effect-source context key 97485E9B and event rows 130..133 do not identify a physical resource URI.")
        };
        return new(calendar, selection, terrain, objects.AsReadOnly(), textures.AsReadOnly(),
            Identity(terrain.Shader.Shader.Source), Copy(terrain.Feature), terrain.Combination,
            Copy(terrain.CoarseFeature), terrain.CoarseCombination,
            Convert.ToHexString(SHA256.HashData(terrain.InitialControl.RgbaPixels)), Array.AsReadOnly(unresolved));

        void AddGroup(TerrainTextureGroupKind kind, LoadedTerrainTextureGroup group)
        {
            objects.Add(SnapshotObject(kind + "TextureGroup", bundle.Source, group.Definition));
            TerrainSeasonResourceIdentity table = Identity(group.TableSource);
            // Table and binding orders are retained; slots are not grouped into seasons.
            foreach (TerrainTextureBinding binding in group.Bindings)
            {
                var image = binding.Image;
                textures.Add(new(kind, binding.Slot, table, Identity(binding.Resource.Source),
                    binding.TextureIndex, binding.ProxyObject.ObjectId, image.Width, image.Height,
                    image.LayerCount, image.MipCount));
            }
        }
    }

    public static TerrainSeasonResourceIdentity Identity(LoadedRdbAsset source)
    {
        ArgumentNullException.ThrowIfNull(source);
        GameResourceId id = GameResourceId.Parse(source.ResourceUri);
        if (id.Kind != "rdb" ||
            !id.Key.EndsWith("/" + source.FileId.ToString("X8"), StringComparison.OrdinalIgnoreCase) ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
            throw new InvalidDataException("Season source identity differs from its complete decoded resource bytes.");
        if (source.OverrideSource is not null &&
            (!source.OverrideSource.IsOverride || source.OverrideSource.ResourceUri != source.ResourceUri ||
             source.OverrideSource.Sha256 != source.DecodedSha256))
            throw new InvalidDataException("Season source override identity differs from its resource.");
        return new(source.ResourceUri, source.FileId, source.DecodedSha256, source.SourceContainerPath,
            source.IndexSource, source.OverrideSource);
    }

    /// <summary>Retain descriptor and value bytes, offsets and source orders for later
    /// seasonal property resolution. This does not assign seasonal meaning to any property.</summary>
    public static TerrainSeasonObjectInput SnapshotObject(string role,
        LoadedRdbAsset source, DokObject item)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Season input role is empty.", nameof(role));
        int headerWidth = item.Kind == DokObjectKind.Inline ? 24 : 28;
        if (item.Offset < 28 || item.Length < headerWidth || item.Offset > source.Bytes.Length - item.Length ||
            item.RawHeader.Length != headerWidth ||
            !source.Bytes.AsSpan(item.Offset, headerWidth).SequenceEqual(item.RawHeader) ||
            BinaryPrimitives.ReadUInt32LittleEndian(item.RawHeader.AsSpan(8)) != item.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(item.RawHeader.AsSpan(12)) != item.ObjectId ||
            item.Kind == DokObjectKind.Inline &&
                BinaryPrimitives.ReadUInt32LittleEndian(item.RawHeader.AsSpan(16)) != item.ClassId ||
            BinaryPrimitives.ReadUInt32LittleEndian(item.RawHeader.AsSpan(headerWidth - 4)) != item.Properties.Count)
            throw new InvalidDataException("Season source object differs from its decoded document.");
        for (int i = 0; i < item.Properties.Count; i++)
        {
            DokProperty property = item.Properties[i];
            if (property.Index != i || property.DescriptorOffset != item.Offset + headerWidth + i * 12 ||
                property.RawDescriptor.Length != 12 || property.DescriptorOffset > item.Offset + item.Length - 12 ||
                !source.Bytes.AsSpan(property.DescriptorOffset, 12).SequenceEqual(property.RawDescriptor) ||
                BinaryPrimitives.ReadUInt32LittleEndian(property.RawDescriptor) != property.TypeCode ||
                BinaryPrimitives.ReadUInt32LittleEndian(property.RawDescriptor.AsSpan(4)) != property.ElementCount ||
                BinaryPrimitives.ReadUInt32LittleEndian(property.RawDescriptor.AsSpan(8)) != property.NameHash ||
                property.ValueOffset < item.Offset + headerWidth + item.Properties.Count * 12 ||
                property.ValueOffset > item.Offset + item.Length - property.RawValue.Length ||
                !source.Bytes.AsSpan(property.ValueOffset, property.RawValue.Length).SequenceEqual(property.RawValue))
                throw new InvalidDataException("Season source property differs from its decoded object.");
        }
        var properties = item.Properties.Select(property => property with
        {
            RawDescriptor = (byte[])property.RawDescriptor.Clone(),
            RawValue = (byte[])property.RawValue.Clone()
        }).ToArray();
        return new(role, Identity(source), item.Index, item.ObjectId, item.ClassId, item.Kind,
            Array.AsReadOnly(properties));
    }

    private static void ValidateCalendar(TerrainSeasonCalendarInput calendar)
    {
        if (calendar.Origin == TerrainSeasonCalendarOrigin.ExplicitSourceMonth)
        {
            if (calendar.Source is not null || calendar.SourceDate is not null || calendar.SourceWorldSha256 is not null)
                throw new InvalidDataException("An explicit source month cannot masquerade as a captured scenario date.");
        }
        else if (calendar.Origin != TerrainSeasonCalendarOrigin.ScenarioWorldThirdDate ||
                 calendar.Source is null || calendar.SourceDate is null ||
                 calendar.SourceDate.SourceOffset != 38 || calendar.SourceDate.RawMonth != calendar.SourceMonth ||
                 calendar.SourceWorldSha256 is null)
            throw new InvalidDataException("Season scenario input must retain the separate world third date at source offset 38.");
    }

    private static G2ShaderFeatureRecord Copy(G2ShaderFeatureRecord item) =>
        item with { RawBytes = (byte[])item.RawBytes.Clone() };

}
