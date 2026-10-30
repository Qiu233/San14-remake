using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    private readonly object _uiResourceCacheGate = new();
    private readonly Dictionary<string, (UiResourceRevision Revision, LoadedMessages Messages)> _uiMessages = [];
    private readonly Dictionary<(uint FileId, int TextureIndex), (UiTextureSourceRevision Revision, LoadedTexture Texture)> _uiTextures = [];

    private readonly record struct UiResourceRevision(string Path, long Length, long LastWriteUtcTicks);
    private readonly record struct UiTextureSourceRevision(string IndexGeneration, UiResourceRevision Texture);

    // A cache hit examines source identity, never the content or its hash. The
    // override resolver is consulted each time so adding/removing an override
    // selects a different owner even if its size and timestamp match.
    private UiResourceRevision UiRevision(GameResourceId id, string fallback)
    {
        string path = _overrides.Find(id) ?? fallback;
        var file = new FileInfo(path);
        return new(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks);
    }

    private UiTextureSourceRevision UiTextureRevision(string archive, uint fileId)
    {
        string relative = $"0002/{archive}.rdb";
        string indexPath = SourcePath(relative);
        // RDB indices already belong to the repository lifetime. An index
        // replacement uses a fresh repository, as do its sprite directories.
        // Reuse that loaded generation rather than detecting a newer index
        // while accidentally retaining its old record offsets.
        var index = LoadRdbIndex(archive);
        string indexGeneration = _readFiles[GameResourceId.File(relative).ToString()].Sha256;
        if (!_assetRecords[relative].TryGetValue(fileId, out var record))
            throw new KeyNotFoundException($"RDB asset {archive}/{fileId:X8} is missing.");
        string container = RdbArchiveReader.ContainerPath(indexPath, index, record);
        _ = SourcePath(Path.GetRelativePath(SourceDirectory, container).Replace('\\', '/'));
        return new(indexGeneration, UiRevision(GameResourceId.Rdb(archive, fileId), container));
    }

    // Public resource results historically own mutable byte arrays. Keep that
    // ownership: callers receive copies; only the decode/parse work is shared.
    private static LoadedTexture CopyUiTexture(LoadedTexture source) => source with
    { Texture = source.Texture with { Pixels = source.Texture.Pixels.ToArray() } };

    private static LoadedMessages CopyUiMessages(LoadedMessages source)
    {
        var messages = source.Messages with
        {
            DecodedPayload = source.Messages.DecodedPayload.ToArray(),
            Sections = source.Messages.Sections.Select(section => section with
            {
                HeaderBytes = section.HeaderBytes.ToArray(),
                RecordOffsets = section.RecordOffsets.ToArray(),
                Records = section.Records.Select(record => record with
                {
                    RawBytes = record.RawBytes.ToArray(),
                    Literals = record.Literals.ToArray(),
                    UninterpretedRanges = record.UninterpretedRanges.Select(range => range with
                    { RawBytes = range.RawBytes.ToArray() }).ToArray()
                }).ToArray()
            }).ToArray()
        };
        var container = source.Container with
        {
            HeaderBytes = source.Container.HeaderBytes.ToArray(),
            Chunks = source.Container.Chunks.Select(chunk => chunk with
            { PaddingBytes = chunk.PaddingBytes.ToArray() }).ToArray(),
            DecodedPayload = source.Container.DecodedPayload.ToArray()
        };
        return source with { Messages = messages, Container = container };
    }
}
