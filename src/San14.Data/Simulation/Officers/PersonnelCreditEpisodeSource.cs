using San14.Data.Formats;

namespace San14.Data;

/// <summary>The ordinary promotion caller's immutable Episode selection.
/// Current person validity, numerical commit and presentation belong to its
/// caller; a genuine no-match remains distinct from a missing catalog.</summary>
internal static class PersonnelCreditEpisodeSource
{
    internal static S14EpisodeDefinition? Find(S14EpisodeCatalog catalog,
        ushort promotedPersonId, ushort rulerPersonId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        // The source scans physical slots1..30, excluding the native null
        // receiver0. The reader publishes that exact physical slot order.
        foreach (var episode in catalog.Episodes)
            if (episode.Slot != 0 && episode.PersonId == promotedPersonId &&
                (episode.RulerId == 0 || episode.RulerId == rulerPersonId))
                return episode;
        return null;
    }

    internal static int MessageRoot(int rawIndex) => unchecked(0x5C8A + rawIndex);
}
