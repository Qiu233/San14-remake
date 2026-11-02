using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedPersonnelCreditEpisodes(S14EpisodeCatalog Catalog, SourceProvenance Source);

public sealed partial class GameResourceRepository
{
    public LoadedPersonnelCreditEpisodes LoadPersonnelCreditEpisodes(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try { return new(S14EpisodeReader.Read(S14Reader.Read(bytes, false)), source); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original personnel credit episodes failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }
}
