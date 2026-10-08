using System.Globalization;

namespace San14.Data;

public sealed record EffectiveScenario(string Locale, int Number, string Edition,
    string SourcePath);

/// <summary>Selects the highest installed scenario revision for each locale and number.</summary>
public static class ScenarioResolver
{
    public static IReadOnlyList<EffectiveScenario> Resolve(IEnumerable<S14Entry> documents)
    {
        var candidates = new List<(string Locale, int Number, int Rank, string Edition, string Path)>();
        foreach (S14Entry document in documents)
        {
            string[] parts = document.SourcePath.Split('/');
            if (parts.Length != 2 || !parts[0].StartsWith("0010_", StringComparison.OrdinalIgnoreCase))
                continue;
            string locale = parts[0][5..].ToUpperInvariant();
            if (locale is not ("SC" or "TC")) continue;
            string stem = Path.GetFileNameWithoutExtension(parts[1]);
            int rank;
            string edition;
            string digits;
            uint marker;
            if (stem.StartsWith("scedaexce", StringComparison.OrdinalIgnoreCase))
            {
                (rank, edition, digits, marker) = (3, "CE", stem[9..], 0x66);
            }
            else if (stem.StartsWith("scedaex", StringComparison.OrdinalIgnoreCase))
            {
                (rank, edition, digits, marker) = (2, "PK", stem[7..], 0x5c);
            }
            else if (stem.StartsWith("sceda", StringComparison.OrdinalIgnoreCase))
            {
                (rank, edition, digits, marker) = (1, "Base", stem[5..], 0x33);
            }
            else continue;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture,
                    out int number) || number < 1 || document.FormatMarker != marker ||
                document.Title is null || document.StartYear is null || document.StartMonth is null)
                throw new InvalidDataException($"Scenario revision is invalid: {document.SourcePath}");
            candidates.Add((locale, number, rank, edition, document.SourcePath));
        }
        var result = new List<EffectiveScenario>();
        foreach (var group in candidates.GroupBy(item => (item.Locale, item.Number)))
        {
            int rank = group.Max(item => item.Rank);
            var selected = group.Where(item => item.Rank == rank).ToArray();
            if (selected.Length != 1)
                throw new InvalidDataException($"Duplicate scenario revision: {group.Key}");
            result.Add(new EffectiveScenario(group.Key.Locale, group.Key.Number,
                selected[0].Edition, selected[0].Path));
        }
        return result.OrderBy(item => item.Locale, StringComparer.Ordinal)
            .ThenBy(item => item.Number).ToArray();
    }
}
