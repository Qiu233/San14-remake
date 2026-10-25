using San14.Data.Formats;

namespace San14.Data;

internal sealed record LoadedForeignCountryNames(IReadOnlyList<string> Names, SourceProvenance Source);

public sealed partial class GameResourceRepository
{
    internal LoadedForeignCountryNames LoadForeignCountryNames(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        return new(S14ForeignCountryReader.ReadNames(S14Reader.Read(bytes, false)), source);
    }
}
