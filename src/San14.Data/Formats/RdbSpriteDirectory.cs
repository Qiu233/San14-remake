namespace San14.Data.Formats;

public sealed record RdbSpriteBinding(uint AtlasId, uint? TextureId, SpriteDefinition Sprite);

/// <summary>
/// Indexes names from the original atlas metadata. A texture association is accepted
/// only when the original container places its complete entry directly before the atlas.
/// Other layouts remain unresolved until their source relationships are understood.
/// </summary>
public sealed class RdbSpriteDirectory
{
    private readonly Dictionary<string, List<RdbSpriteBinding>> _names = new(StringComparer.Ordinal);
    public int AtlasCount { get; private set; }
    public int SpriteCount { get; private set; }
    public int UnresolvedAtlasCount { get; private set; }

    public static RdbSpriteDirectory Read(string rdbPath, RdbIndex index,
        Func<RdbRecord, byte[]> readPayload)
    {
        var directory = new RdbSpriteDirectory();
        var textureEnds = index.Records.Where(record => record.TypeId == 0xAFBEC60C &&
                record.Address is not null)
            .GroupBy(record => (Container: RdbArchiveReader.ContainerPath(rdbPath, index, record),
                End: checked(record.Address!.Offset + record.Address.Length)))
            .ToDictionary(group => group.Key, group => group.ToArray());
        foreach (RdbRecord atlas in index.Records.Where(record => record.TypeId == SpriteAtlasReader.RdbTypeId))
        {
            IReadOnlyList<SpriteDefinition> sprites = SpriteAtlasReader.Read(readPayload(atlas));
            uint? textureId = null;
            if (atlas.Address is RdbAddress address && textureEnds.TryGetValue(
                (RdbArchiveReader.ContainerPath(rdbPath, index, atlas), address.Offset), out RdbRecord[]? candidates))
            {
                if (candidates.Length != 1)
                    throw new InvalidDataException($"Sprite atlas {atlas.FileId:X8} has ambiguous preceding textures.");
                textureId = candidates[0].FileId;
            }
            directory.AtlasCount++;
            directory.SpriteCount += sprites.Count;
            if (textureId is null) directory.UnresolvedAtlasCount++;
            foreach (SpriteDefinition sprite in sprites)
            {
                if (!directory._names.TryGetValue(sprite.Name, out List<RdbSpriteBinding>? bindings))
                    directory._names.Add(sprite.Name, bindings = []);
                bindings.Add(new RdbSpriteBinding(atlas.FileId, textureId, sprite));
            }
        }
        return directory;
    }

    public RdbSpriteBinding Find(string name)
    {
        if (!_names.TryGetValue(name, out List<RdbSpriteBinding>? entries))
            throw new KeyNotFoundException($"Original sprite {name} is missing.");
        if (entries.Count != 1)
            throw new InvalidDataException($"Original sprite {name} occurs in {entries.Count} atlases; select an explicit atlas.");
        if (entries[0].TextureId is null)
            throw new InvalidDataException($"Original sprite {name} has an unresolved texture reference.");
        return entries[0];
    }
}
