using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Godot;
using San14.Data;

internal sealed record MapSceneDefinition(uint FileId, string LabelKey);

/// <summary>Read-only scene choices and rendering configuration, loaded before scene selection.</summary>
internal sealed class MapSceneConfiguration
{
    private readonly IReadOnlyDictionary<uint, MapWorldProjectionPolicy> _projections;
    public string Archive { get; }
    public TexturePreviewPolicy ModelTextures { get; }
    public TerrainTexturePreviewPolicy TerrainTextures { get; }
    public int TerrainDepth { get; }
    public IReadOnlyList<MapSceneDefinition> Scenes { get; }

    private MapSceneConfiguration(string archive, TexturePreviewPolicy modelTextures,
        TerrainTexturePreviewPolicy terrainTextures, int terrainDepth,
        List<MapSceneDefinition> scenes, Dictionary<uint, MapWorldProjectionPolicy> projections)
    {
        (Archive, ModelTextures, TerrainTextures, TerrainDepth) = (archive, modelTextures, terrainTextures, terrainDepth);
        Scenes = scenes.AsReadOnly();
        _projections = new ReadOnlyDictionary<uint, MapWorldProjectionPolicy>(projections);
    }

    public bool TryGetProjection(uint scene, [NotNullWhen(true)] out MapWorldProjectionPolicy? policy) =>
        _projections.TryGetValue(scene, out policy);

    public int InitialSceneIndex(bool playable)
    {
        if (playable)
            for (int i = 0; i < Scenes.Count; i++)
                if (_projections.ContainsKey(Scenes[i].FileId)) return i;
        return 0;
    }

    public static MapSceneConfiguration Parse(string sceneJson, string projectionJson, bool embedded)
    {
        using JsonDocument config = JsonDocument.Parse(sceneJson);
        string archive = config.RootElement.GetProperty("archive").GetString()
            ?? throw new InvalidDataException("Scene preview archive is missing.");
        JsonElement texture = config.RootElement.GetProperty("texturePreview");
        TexturePreviewPolicy modelTextures = new(texture.GetProperty("kind").GetUInt16(), texture.GetProperty("uvLayer").GetUInt16());
        int depth = config.RootElement.GetProperty("terrainPreviewDepth").GetInt32();
        if (embedded) depth = Math.Max(depth, 4);
        JsonElement terrain = config.RootElement.GetProperty("terrainTexturePreview");
        TerrainTextureGroupKind group = terrain.GetProperty("group").GetString() switch
        {
            "field" => TerrainTextureGroupKind.Field,
            "root" => TerrainTextureGroupKind.Root,
            "auxiliary" => TerrainTextureGroupKind.Auxiliary,
            _ => throw new InvalidDataException("Scene preview terrain texture group is unknown.")
        };
        TerrainTexturePreviewPolicy terrainTextures = new(group, terrain.GetProperty("slot").GetUInt32(),
            terrain.GetProperty("layer").GetInt32(), ReadVector2(terrain.GetProperty("uvScale")),
            ReadVector2(terrain.GetProperty("uvOffset")));
        var scenes = new List<MapSceneDefinition>();
        foreach (JsonElement entry in config.RootElement.GetProperty("scenes").EnumerateArray())
        {
            uint id = uint.Parse(entry.GetProperty("fileId").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (scenes.Any(scene => scene.FileId == id))
                throw new InvalidDataException("Scene preview configuration contains a duplicate source.");
            scenes.Add(new(id, entry.GetProperty("labelKey").GetString()
                ?? throw new InvalidDataException("Scene preview label key is missing.")));
        }
        if (scenes.Count == 0) throw new InvalidDataException("Scene preview has no configured sources.");
        using JsonDocument projectionConfig = JsonDocument.Parse(projectionJson);
        if (projectionConfig.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
            projectionConfig.RootElement.GetProperty("originalWorldMappingVerified").GetBoolean())
            throw new InvalidDataException("Unsupported fitted map projection configuration.");
        var projections = new Dictionary<uint, MapWorldProjectionPolicy>();
        foreach (JsonElement entry in projectionConfig.RootElement.GetProperty("profiles").EnumerateArray())
        {
            uint scene = uint.Parse(entry.GetProperty("sceneFileId").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var policy = JsonSerializer.Deserialize<MapWorldProjectionPolicy>(entry.GetProperty("projection").GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true, RespectRequiredConstructorParameters = true,
                    RespectNullableAnnotations = true
                }) ?? throw new InvalidDataException("Map projection policy is missing.");
            if (!scenes.Any(row => row.FileId == scene) || !projections.TryAdd(scene, policy))
                throw new InvalidDataException("Map projection policy has a missing or duplicate scene source.");
        }
        return new(archive, modelTextures, terrainTextures, depth, scenes, projections);
    }

    private static Vector2 ReadVector2(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2)
            throw new InvalidDataException("Scene preview UV vector requires exactly two values.");
        Vector2 vector = new(value[0].GetSingle(), value[1].GetSingle());
        if (!vector.IsFinite()) throw new InvalidDataException("Scene preview UV vector must be finite.");
        return vector;
    }
}
