using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    /// <summary>Reads the eight timed opening captions from the selected normal
    /// scenario resource. These captions belong to the movie presentation and
    /// require no campaign or event execution.</summary>
    public LoadedCollectionMovieSubtitles LoadCollectionMovieSubtitles(
        string scenarioPath, string locale)
    {
        if (locale is not ("TC" or "SC"))
            throw new ArgumentException("Collection locale must be TC or SC.", nameof(locale));
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioPath);
        var (bytes, source) = ReadSource(scenarioPath);
        var document = S14Reader.Read(bytes, true);
        var boundary = S14ScenarioTailReader.Read(document, locale);
        var table = boundary.StateTables.SingleOrDefault(value => value.Section == 31)
            ?? throw new InvalidDataException("collection.movie.subtitles.scenario_table_31");
        // The scenario serializer writes count 8, then each history row, and
        // finally marker 32. Its byte flag precedes two signed millisecond
        // values and two fixed 51-code-unit UTF-16 strings.
        const int rowBytes = 1 + sizeof(int) * 2 + 51 * sizeof(char) * 2;
        if (table.RawCount != 8 || table.RecordBytes != rowBytes ||
            table.RawBytes.Length != 8 * rowBytes)
            throw new InvalidDataException("collection.movie.subtitles.history_rows");

        var subtitles = new CollectionMovieSubtitle[8];
        for (int index = 0; index < subtitles.Length; index++)
        {
            ReadOnlySpan<byte> row = table.RawBytes.AsSpan(index * rowBytes, rowBytes);
            int start = BinaryPrimitives.ReadInt32LittleEndian(row.Slice(1, sizeof(int)));
            int end = BinaryPrimitives.ReadInt32LittleEndian(row.Slice(5, sizeof(int)));
            string prefix = ReadMovieSubtitleText(row.Slice(9, 102));
            string body = ReadMovieSubtitleText(row.Slice(111, 102));
            // The original caption consumer copies these stored strings and
            // inserts its literal newline. The first string is already text,
            // rather than a date requiring localization or formatting.
            subtitles[index] = new(index, start, end, prefix + "\n" + body);
        }
        return new(Array.AsReadOnly(subtitles), source);
    }

    private static string ReadMovieSubtitleText(ReadOnlySpan<byte> text)
    {
        int length = 0;
        while (length < text.Length && (text[length] != 0 || text[length + 1] != 0))
            length += sizeof(char);
        return Encoding.Unicode.GetString(text[..length]);
    }
}
