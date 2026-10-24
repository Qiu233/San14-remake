namespace San14.Data;

/// <summary>472980's stable strict-before insertion, using48DA90's actual
/// comparison order. It never substitutes base abilities for effective values,
/// or a default deployment sort for an unavailable original comparison.</summary>
internal static class NewGameForceOfficerSort
{
    internal static IReadOnlyList<NewGameForceOfficerPreview>? Read(
        IReadOnlyList<NewGameForceOfficerPreview> source, out string? unresolved)
    {
        var sorted = source.ToList();
        for (int at = 1; at < sorted.Count; at++)
        {
            var value = sorted[at]; int before = at;
            while (before > 0)
            {
                bool? precedes = Before(value, sorted[before - 1], out unresolved);
                if (precedes is null) return null;
                if (precedes == false) break;
                sorted[before] = sorted[before - 1]; before--;
            }
            sorted[before] = value;
        }
        unresolved = null;
        return sorted.AsReadOnly();
    }

    private static bool? Before(NewGameForceOfficerPreview a, NewGameForceOfficerPreview b,
        out string? unresolved)
    {
        unresolved = null;
        if (a.ServiceStatus is not byte sa || b.ServiceStatus is not byte sb)
            return Missing("status", out unresolved);
        for (int preferred = 1; preferred <= 3; preferred++)
        {
            int comparison = (sa == preferred ? 1 : 0) - (sb == preferred ? 1 : 0);
            if (comparison != 0) return comparison > 0;
        }
        if (a.OfficeCommandLimit is not int oa || b.OfficeCommandLimit is not int ob)
            return Missing("office_command_limit", out unresolved);
        if (oa != ob) return oa > ob;
        if (a.EffectiveAbilities.Leadership is not int la || b.EffectiveAbilities.Leadership is not int lb)
            return Missing("effective_leadership", out unresolved);
        if (la != lb) return la > lb;
        if (a.EffectiveAbilities.Strength is not int wa || b.EffectiveAbilities.Strength is not int wb)
            return Missing("effective_strength", out unresolved);
        if (wa != wb) return wa > wb;
        if (a.StoredMeritWord is not ushort ma || b.StoredMeritWord is not ushort mb)
            return Missing("stored_merit_word", out unresolved);
        if (ma != mb) return ma > mb;
        if (a.OfficerId is not int ia || b.OfficerId is not int ib)
            return Missing("person_identity", out unresolved);
        return ia < ib;
    }
    private static bool? Missing(string field, out string? unresolved)
    { unresolved = "new-game.officers.sort." + field; return null; }
}
