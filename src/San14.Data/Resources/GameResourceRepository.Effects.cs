using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedG1Effect(G1EffectAsset Effect, LoadedRdbAsset Source);

public sealed partial class GameResourceRepository
{
    public LoadedG1Effect LoadEffect(string archiveName, uint fileId)
    {
        var source = LoadRdbAsset(archiveName, fileId);
        try { return new(G1EffectReader.Read(source.Bytes), source); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Effect decode failed: {source.ResourceUri}. {error.Message}", error); }
    }
}
