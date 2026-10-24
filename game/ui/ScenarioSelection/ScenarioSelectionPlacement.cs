using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The extracted ordinary scenario row order and installed-content
/// portrait centres. Unread table entries remain unsupported rather than guessed.</summary>
internal static class ScenarioSelectionPlacement
{
    internal static IReadOnlyList<int> Mounts { get; } = Array.AsReadOnly(new[] { 3, 6, 9, 12, 15, 18, 21, 24, 27 });
    private static readonly IReadOnlyDictionary<byte, Vector2I> centers = new Dictionary<byte, Vector2I>
    {
        [0] = new(0, 0),
        [2] = new(1452, 144),
        [5] = new(1415, 144),
        [6] = new(1332, 199),
        [7] = new(1412, 196),
        [8] = new(1521, 179),
        [9] = new(1503, 287),
        [10] = new(1438, 273),
        [11] = new(1532, 347),
        [13] = new(1452, 439),
        [14] = new(1381, 240),
        [15] = new(1335, 286),
        [16] = new(1315, 343),
        [17] = new(1355, 403),
        [18] = new(1258, 261),
        [19] = new(1247, 340),
        [20] = new(1170, 250),
        [21] = new(1184, 376),
        [22] = new(1119, 177),
        [23] = new(1039, 236),
        [25] = new(1521, 422),
        [26] = new(1578, 468),
        [27] = new(1563, 552),
        [28] = new(1418, 508),
        [31] = new(1187, 616),
        [32] = new(1364, 457),
        [33] = new(1281, 419),
        [34] = new(1255, 460),
        [35] = new(1272, 519),
        [40] = new(1173, 481),
        [41] = new(1113, 329),
        [44] = new(1007, 492),
        [45] = new(1056, 615),
        [46] = new(1007, 612),
    };

    internal static ScenarioSelectionPreview Read(S14Entry scenario, Func<S14Entry, short, LoadedSprite> resolvePortrait)
    {
        ArgumentNullException.ThrowIfNull(resolvePortrait);
        var metadata = scenario.SelectionMetadata
            ?? throw new InvalidDataException("Scenario preview metadata is unknown.");
        if (metadata.PreviewPeople.Count != 3)
            throw new InvalidDataException("Scenario preview requires three person slots.");
        var portraits = new ScenarioSelectionPreviewPortrait?[3];
        for (int slot = 0; slot < portraits.Length; slot++)
        {
            var person = metadata.PreviewPeople[slot];
            if (person.PersonId is < 1 or > 5999) continue;
            if (!centers.TryGetValue(person.PositionIndex, out var center))
                throw new InvalidDataException("Scenario preview position is not extracted: " + person.PositionIndex);
            // The resource owner receives the actual row, including native ID63,
            // to resolve its bank10 branch. This table does not choose artwork.
            LoadedSprite sprite = resolvePortrait(scenario, person.PersonId)
                ?? throw new InvalidDataException("Scenario preview portrait is unknown.");
            portraits[slot] = new(sprite, center);
        }
        return new(scenario.Description, Array.AsReadOnly(portraits));
    }
}
