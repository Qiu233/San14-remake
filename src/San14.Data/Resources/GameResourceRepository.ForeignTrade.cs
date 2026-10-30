using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    internal IReadOnlyList<ForeignTradeCountryDefinition> LoadForeignTradeCountries(string relativePath)
    {
        var (bytes, _) = ReadSource(relativePath);
        var document = S14Reader.Read(bytes, false);
        var names = S14ForeignCountryReader.ReadNames(document);
        const int start = 355084, stride = 25;
        int[] days = [0, 60, 60, 90, 120]; //22F100 data, extracted offline.
        return Enumerable.Range(0, 5).Select(id =>
        {
            int at = start + id * stride + 20;
            var source = document.DecodedPayload;
            return new ForeignTradeCountryDefinition(id, names[id], source[at], source[at + 1],
                source[at + 2], source[at + 3], source[at + 4], days[id])
            { NameSortKey = S14ForceNameDefinitionReader.ReadLiteralUnits(source.AsSpan(start + id * stride + 8, 12)) };
        }).ToArray();
    }
}
