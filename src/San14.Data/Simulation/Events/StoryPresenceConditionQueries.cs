using San14.Data.Formats;

namespace San14.Data;

public interface IS14StoryPresenceConditionQueries
{
    bool? ReadConditionPersonDisappearing(S14EventConditionPersonReference person);
    int? ReadConditionFootholdEndurance(BaseReference receiver);
    CampaignEventExecutionInputs ReadStoryPresenceInvocationInputs();
}

/// <summary>Reached predicates shared by ordinary eligibility and Story.
/// Resolving an operand remains the responsibility of its actual invocation.</summary>
internal static class StoryPresenceConditions
{
    internal static bool PersonDisappearing(IS14PlayerEventConditionQueries queries,
        S14EventConditionPersonReference? person) => Valid(queries, person) &&
        K((queries as IS14StoryPresenceConditionQueries)?.ReadConditionPersonDisappearing(person!), "Person_disappearing");

    internal static bool SameForce(IS14PlayerEventConditionQueries queries,
        S14EventConditionPersonReference? first, S14EventConditionPersonReference? second)
    {
        if (!Valid(queries, first) || !Valid(queries, second)) return false;
        //3A5B20 resolves both Persons before testing them; the two22CB40
        //getters are then consumed in second/first order. Force validity is
        //not tested, and two observed null Force pointers compare equal.
        var secondForce = queries.ReadConditionPersonForce(second!) ?? throw Missing("second_Person_Force_pointer");
        var firstForce = queries.ReadConditionPersonForce(first!) ?? throw Missing("first_Person_Force_pointer");
        return firstForce.Value == secondForce.Value;
    }

    internal static bool Likes(IS14PlayerEventConditionQueries queries,
        S14EventConditionPersonReference? first, S14EventConditionPersonReference? second, int expected)
    {
        if (!Valid(queries, first) || !Valid(queries, second)) return false;
        ushort target = K(queries.ReadConditionPersonCharacterId(second!), "like_target_identity");
        bool found = false;
        //233010 is the directional eight-word C0 list, not friendship,
        //sworn-family, spouse or a symmetric relationship calculation.
        for (int ordinal = 0; ordinal < 8; ordinal++)
            if (K(queries.ReadConditionPersonRelationListIdentity(first!, ordinal, true), "like_list_word") == target)
            { found = true; break; }
        return (found ? 1 : 0) == expected;
    }

    internal static RegistryObjectReference? ValidItem(IS14PlayerEventConditionQueries queries, int selector)
    {
        if (selector < 0) throw Missing("negative_Item_table_index");
        var item = queries.ReadConditionItemTableSlot(selector < 202 ? selector : 0) ?? throw Missing("Item_pointer");
        return K(queries.ReadConditionItemValidity(item), "Item_validity") ? item : null;
    }

    internal static bool ItemOwner(IS14PlayerEventConditionQueries queries, RegistryObjectReference item,
        S14EventConditionPersonReference? person, int expected)
    {
        if (!Valid(queries, person)) return false;
        ushort raw = K(queries.ReadConditionItemOwnerId(item), "Item_owner4A");
        var owner = (raw < 6001 ? queries.ReadConditionPersonTableSlot(raw) : queries.ReadConditionBasePerson())
            ?? throw Missing("Item_owner_Person_pointer");
        return (owner.Value == person ? 1 : 0) == expected;
    }

    internal static bool Endurance(IS14PlayerEventConditionQueries queries, BaseReference receiver, int mode, int threshold)
    {
        if (!K(queries.ReadConditionBaseValidity(receiver), "Foothold_validity")) return false;
        return Compare(K((queries as IS14StoryPresenceConditionQueries)?.ReadConditionFootholdEndurance(receiver),
            "Foothold_endurance"), mode, threshold);
    }

    internal static bool SpeakerCount(IS14PlayerEventConditionQueries queries,
        IEnumerable<S14EventConditionPersonReference?> speakers, int mode, int threshold)
    {
        int count = 0;
        foreach (var speaker in speakers) if (Valid(queries, speaker)) count++;
        return Compare(count, mode, threshold);
    }

    internal static bool Compare(int value, int mode, int threshold) => mode switch
    {
        1 => value != threshold, 2 => value == threshold, 3 => value > threshold,
        4 => value >= threshold, 5 => value < threshold, 6 => value <= threshold, _ => false
    };

    private static bool Valid(IS14PlayerEventConditionQueries queries, S14EventConditionPersonReference? person) =>
        person is not null && K(queries.ReadConditionPersonValidity(person), "Person_validity");
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("story.presence_condition." + field);
}
