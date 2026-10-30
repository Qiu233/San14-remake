using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus SelectTradeCountry(uint forceKey, uint output)
    {
        if (!TryEntity(forceKey, out var force) || force.Kind != WorldEntityKind.Force)
            return OriginalAiNodeStatus.Failure;
        var country = Adapter.ReadOriginalAiTradePriorityCountry(Current, InputsCurrent, force.Id);
        return country is { } selected ? SetTradeCountry(output, selected) : OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus TradeNeedFood(uint countryKey, uint troopsKey, uint output, uint membersKey)
    {
        if (!TryTradeCountry(countryKey, out var country) || country.Id is < 1 or > 4)
            return OriginalAiNodeStatus.Failure;
        //07EEE0 deliberately ignores the optional integer-read result. A
        //missing or differently typed troop value retains its initialized zero.
        int troops = TryInteger(troopsKey, out int value) ? value : 0;
        if (!TryCandidateList(membersKey, WorldEntityKind.Officer, out var members) || members.Values.Count == 0)
            return OriginalAiNodeStatus.Failure;
        //The original0240A0 is the Person-list copy constructor. Preserve the
        //actual ordered members, including duplicates, rather than seeding a roster.
        int[] ids = members.Values.Select(person => person.Id).ToArray();
        return SetInteger(output, Adapter.ReadForeignTradeRequiredFoodCurrent(Current, InputsCurrent, ids, country.Id, troops));
    }
}
