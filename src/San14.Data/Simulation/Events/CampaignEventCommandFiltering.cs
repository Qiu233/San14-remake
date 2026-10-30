using San14.Data.Formats;

namespace San14.Data;

/// <summary>Mode0's 3DECB0 metadata filter and 3E28C0 comparator families.
/// Operates on the captured catalogue; UI controls never rescan the world.</summary>
public static class CampaignEventCommandFiltering
{
    public static IReadOnlyList<CampaignEventCommandRow> ReadRows(this CampaignEventCommandCatalog catalog,
        int category, byte filterMask, int sortColumn, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if ((uint)category > 4) throw new ArgumentOutOfRangeException(nameof(category));
        if ((uint)sortColumn > 1) throw new ArgumentOutOfRangeException(nameof(sortColumn));
        var rows = catalog.Rows.Where(row => Included(row, category, filterMask)).ToList();
        rows.Sort((first, second) => Compare(first, second, sortColumn, ascending));
        return rows.AsReadOnly();
    }

    private static bool Included(CampaignEventCommandRow row, int category, byte mask)
    {
        //3DECB0 bounds the original type before replacing ending IDs by5.
        if ((uint)row.Kind >= 6 || (uint)row.TriggerKind >= 3) return false;
        int kind = row.EventId > 949 ? 5 : row.Kind;
        bool included = category switch
        {
            0 => kind is 1 or 2 or 3 or 4,
            1 => kind == 1,
            2 => kind == 4,
            3 => kind == 2,
            4 => kind == 3,
            _ => false
        };
        if (!included) return false;
        int statusMask = mask & 15;
        //An unresolved query row remains visibly Unknown rather than being
        //forged into a native status. A selected status filter cannot admit it.
        if (row.Status is null) { if (statusMask != 0) return false; }
        else if ((uint)row.Status.Value >= 4 || statusMask != 0 && (statusMask & (1 << (int)row.Status.Value)) == 0) return false;
        int triggerMask = (mask >> 4) & 3;
        if (triggerMask != 0 && (row.TriggerKind switch { 2 => (triggerMask & 1) != 0, 1 => (triggerMask & 2) != 0, _ => false }) == false)
            return false;
        int repeatMask = (mask >> 6) & 3;
        return repeatMask == 0 || (repeatMask & (row.Repeatable ? 2 : 1)) != 0;
    }

    private static int Compare(CampaignEventCommandRow first, CampaignEventCommandRow second, int column, bool ascending)
    {
        //Unknown is a client publication state, outside the native 0..3
        //status range. Keep such visible rows after known source decisions.
        if (first.Status is null || second.Status is null)
            return first.Status is null ? second.Status is null ? 0 : 1 : -1;
        if (column == 0)
        {
            bool firstSpecial = first.EventId is >= 900 and < 950;
            bool secondSpecial = second.EventId is >= 900 and < 950;
            int special = firstSpecial.CompareTo(secondSpecial);
            if (special != 0) return ascending ? special : -special;
        }
        bool firstActive = (int)first.Status.Value < 2;
        bool secondActive = (int)second.Status.Value < 2;
        int active = secondActive.CompareTo(firstActive);
        if (active != 0) return active;
        if (column == 1)
        {
            bool? firstFresh = Fresh(first), secondFresh = Fresh(second);
            if (firstFresh is null || secondFresh is null)
                return firstFresh is null ? secondFresh is null ? 0 : 1 : -1;
            int fresh = firstFresh.Value.CompareTo(secondFresh.Value);
            if (fresh != 0) return ascending ? fresh : -fresh;
            int ratio = Ratio(first).CompareTo(Ratio(second));
            if (ratio != 0) return ascending ? ratio : -ratio;
            //Both criterion1 directions retain the source order key ascending.
            return first.SourceOrder.CompareTo(second.SourceOrder);
        }
        int order = first.SourceOrder.CompareTo(second.SourceOrder);
        return ascending ? order : -order;
    }

    private static bool? Fresh(CampaignEventCommandRow row)
        => row.Status != S14EventEligibilityStatus.Satisfied ? false : row.Acknowledged is bool acknowledged ? !acknowledged : null;
    private static int Ratio(CampaignEventCommandRow row)
        => row.EvaluatedConditions == 0 ? 0 : unchecked(row.SuccessfulConditions * 1000) / row.EvaluatedConditions;
}
