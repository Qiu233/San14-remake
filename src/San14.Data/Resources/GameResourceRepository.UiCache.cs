using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    // UI source snapshots belong to the same resident owner as their original
    // bytes. A scope always reads its source first, preserving its own ledger
    // and generation observer even when parsing has already been completed.
    private (LoadedRdbAsset Source, IReadOnlyList<SpriteDefinition> Definitions) LoadUiSpriteAtlas(uint fileId)
    {
        var source = LoadRdbAsset("ScreenLayout", fileId);
        return (source, _residentSources.Ui.Atlas(source));
    }

    private sealed class ResidentUiSourceCache
    {
        private readonly object gate = new();
        private readonly Dictionary<string, (ResidentFile Source, LoadedMessages Value)> messages =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(uint File, int Index), (LoadedRdbAsset Source, LoadedTexture Value)> textures = [];
        private readonly Dictionary<uint, (LoadedRdbAsset Source, IReadOnlyList<SpriteDefinition> Value)> atlases = [];

        // One snapshot per logical key is retained. A replacement overwrites
        // this entry without mutating snapshots still borrowed by old views.
        // Parsing is serialized only at the load boundary, never per frame.
        internal LoadedMessages Messages(ResidentFile source, MessageKind kind)
        {
            lock (gate)
            {
                string key = source.Generation.Id.ToString();
                if (messages.TryGetValue(key, out var cached) && ReferenceEquals(cached.Source, source))
                    return cached.Value;
                try
                {
                    var container = Zp1Reader.Read(source.Bytes);
                    var document = MessageTableReader.Read(container.DecodedPayload, kind);
                    var value = new LoadedMessages(document, container, source.Source,
                        Convert.ToHexString(SHA256.HashData(container.DecodedPayload)));
                    messages[key] = (source, value);
                    return value;
                }
                catch (InvalidDataException error)
                {
                    throw new InvalidDataException($"Original messages failed to load: {source.Source.ResourceUri} " +
                        $"({source.Source.PhysicalPath}). {error.Message}", error);
                }
            }
        }

        internal LoadedTexture Texture(LoadedRdbAsset source, int textureIndex)
        {
            lock (gate)
            {
                var key = (source.FileId, textureIndex);
                if (textures.TryGetValue(key, out var cached) && ReferenceEquals(cached.Source, source))
                    return cached.Value;
                var value = new LoadedTexture(G1TextureReader.Decode(source.Bytes, textureIndex),
                    source.SourceContainerPath, source.FileId, source.DecodedSha256, source.IndexSource)
                { OverrideSource = source.OverrideSource, TextureIndex = textureIndex };
                textures[key] = (source, value);
                return value;
            }
        }

        internal IReadOnlyList<SpriteDefinition> Atlas(LoadedRdbAsset source)
        {
            lock (gate)
            {
                if (atlases.TryGetValue(source.FileId, out var cached) && ReferenceEquals(cached.Source, source))
                    return cached.Value;
                var value = SpriteAtlasReader.Read(source.Bytes);
                atlases[source.FileId] = (source, value);
                return value;
            }
        }
    }
}
