using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedG1EffectModel(G1EffectModel Model, LoadedRdbAsset Source);

public sealed partial class GameResourceRepository
{
    public LoadedG1EffectModel LoadEffectModel(string archiveName, uint fileId)
    {
        var source = LoadRdbAsset(archiveName, fileId);
        try { return new(G1EffectModelReader.Read(source.Bytes), source); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Effect model decode failed: {source.ResourceUri}. {error.Message}", error); }
    }
}
