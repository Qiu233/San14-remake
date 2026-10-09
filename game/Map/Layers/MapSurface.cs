using System.Globalization;
using System.Text.Json;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>
/// Source-texture backdrop sampled at fitted map cells. This is an overview
/// adapter, not a claim that the original terrain shader or projection is solved.
/// </summary>
public sealed class MapSurface
{
    private readonly byte[] _rgb;
    public string SourceUri { get; }
    public string SourceSha256 { get; }
    public int CellCount => _rgb.Length / 3;

    private MapSurface(byte[] rgb, string uri, string hash)
        => (_rgb, SourceUri, SourceSha256) = (rgb, uri, hash);

    public Color ColorAt(int hexId)
    {
        if ((uint)hexId >= (uint)CellCount) throw new ArgumentOutOfRangeException(nameof(hexId));
        int offset = hexId * 3;
        return new Color(_rgb[offset] / 255f, _rgb[offset + 1] / 255f,
            _rgb[offset + 2] / 255f);
    }

    public static MapSurface Build(GameResourceRepository repository, MapDocument map)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(map);
        using Godot.FileAccess file = Godot.FileAccess.Open("res://ui/map-projection.json",
            Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidDataException("Map projection configuration is missing.");
        using JsonDocument config = JsonDocument.Parse(file.GetAsText());
        if (config.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
            config.RootElement.GetProperty("originalWorldMappingVerified").GetBoolean())
            throw new InvalidDataException("Map overview has no supported fitted projection.");
        JsonElement[] profiles = config.RootElement.GetProperty("profiles").EnumerateArray()
            .Where(item => item.GetProperty("sceneFileId").GetString() == "0449AC47")
            .ToArray();
        if (profiles.Length != 1)
            throw new InvalidDataException("Map overview requires one configured field scene.");
        JsonElement visual = profiles[0].GetProperty("overviewTexture");
        int imageIndex = visual.GetProperty("image").GetInt32();
        if (visual.GetProperty("layer").GetInt32() != 0)
            throw new InvalidDataException("Map overview currently supports source layer zero only.");
        double extentX = visual.GetProperty("extentX").GetDouble();
        double extentZ = visual.GetProperty("extentZ").GetDouble();
        if (!double.IsFinite(extentX) || extentX <= 0 ||
            !double.IsFinite(extentZ) || extentZ <= 0)
            throw new InvalidDataException("Map overview texture extent is invalid.");
        MapWorldProjectionPolicy policy = JsonSerializer.Deserialize<MapWorldProjectionPolicy>(
            profiles[0].GetProperty("projection").GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                RespectRequiredConstructorParameters = true,
                RespectNullableAnnotations = true
            }) ?? throw new InvalidDataException("Map overview projection is missing.");
        MapWorldProjection projection = MapWorldProjection.Build(map, policy);
        GameResourceId resource = GameResourceId.Parse(policy.FieldResourceUri);
        string[] key = resource.Key.Split('/');
        if (resource.Kind != "rdb" || key.Length != 2 ||
            !uint.TryParse(key[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out uint fileId))
            throw new InvalidDataException("Map overview field texture URI is invalid.");
        LoadedRdbAsset source = repository.LoadRdbAsset(key[0], fileId);
        if (source.ResourceUri != policy.FieldResourceUri ||
            !source.DecodedSha256.Equals(policy.FieldDecodedSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Map overview source texture differs from its projection profile.");
        // The configured Field image/layer is only a visual preview candidate.
        // The full container remains validated by the decoder.
        RgbaTexture texture = G1TextureReader.Decode(source.Bytes, imageIndex);
        byte[] rgb = new byte[checked(map.Cells.Length * 3)];
        foreach (MapCell cell in map.Cells)
        {
            ProjectedMapPoint point = projection.Project(cell.Id);
            double u = point.X / extentX, v = point.Z / extentZ;
            if (!double.IsFinite(u) || !double.IsFinite(v) ||
                u < 0 || u >= 1 || v < 0 || v >= 1)
                throw new InvalidDataException($"Map overview cell {cell.Id} lies outside the source texture extent.");
            int x = (int)(u * texture.Width), y = (int)(v * texture.Height);
            int pixel = checked((y * texture.Width + x) * 4);
            int target = cell.Id * 3;
            rgb[target] = texture.Pixels[pixel];
            rgb[target + 1] = texture.Pixels[pixel + 1];
            rgb[target + 2] = texture.Pixels[pixel + 2];
        }
        return new MapSurface(rgb, source.ResourceUri, source.DecodedSha256);
    }
}
