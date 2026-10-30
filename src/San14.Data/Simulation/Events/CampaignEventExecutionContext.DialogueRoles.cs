using San14.Data.Formats;

namespace San14.Data;

/// <summary>Invocation-local source references. Null cast means the producer
/// has not run; null fields are known cleared pointers, not unknown inputs.
/// Saved selection scope owns whether this snapshot is known at all.</summary>
public sealed record CampaignEventDialogueCast(S14EventConditionForceReference? CachedForce,
    S14EventConditionPersonReference? Adviser,
    S14EventConditionPersonReference? MilitaryOfficer,
    S14EventConditionPersonReference? CivilOfficer,
    S14EventConditionPersonReference? SameFamilyOfficer,
    S14EventConditionPersonReference? OtherFamilyOfficer,
    S14EventConditionPersonReference? RemainingOfficer);

internal sealed partial class CampaignEventExecutionContext
{
    private CampaignEventDialogueCast? _dialogueCast = inputs.InitialDialogueCast;

    internal void RestoreDialogueCast(CampaignEventDialogueCast? cast) => _dialogueCast = cast;
    internal CampaignEventDialogueCast? ExportDialogueCast() => _dialogueCast;

    private S14EventConditionPersonReference? ReadDialogueRole(int selector)
    {
        //The source latch is the actual adviser pointer. A known null adviser
        //does not latch even when the other roles have already been selected.
        if (_dialogueCast?.Adviser is null) PrepareDialogueCast();
        var cast = _dialogueCast ?? throw Missing("dialogue cast");
        return selector switch
        {
            -3 => cast.Adviser, -2 => cast.MilitaryOfficer, -1 => cast.CivilOfficer,
            -17 => cast.SameFamilyOfficer, -16 => cast.OtherFamilyOfficer, -15 => cast.RemainingOfficer,
            _ => throw new ArgumentOutOfRangeException(nameof(selector))
        };
    }

    private void PrepareDialogueCast()
    {
        var force = _dialogueCast?.CachedForce ??
            (Queries.ReadConditionCurrentPlayerForce() ?? throw Missing("dialogue current Force lookup")).Value
            ?? throw Missing("dialogue current Force pointer");
        var ruler = ReadPersonPointer(Known(Queries.ReadConditionForceRulerPersonId(force), "dialogue Force ruler"))
            ?? throw Missing("dialogue ruler pointer");
        var originalAdviser = ReadPersonPointer(Known(Queries.ReadConditionForceAdvisorId(force), "dialogue Force adviser"));
        ushort family = Known(Queries.ReadConditionPersonBloodlineId(ruler), "dialogue ruler bloodline");
        var remaining = new List<S14EventConditionPersonReference>();
        foreach (int? entry in Queries.ReadRegisteredConditionPersonPhysicalSlots() ?? throw Missing("dialogue registered Persons"))
        {
            var person = new S14EventConditionPersonReference(entry ?? throw Missing("dialogue registered Person pointer"));
            //The normal Force iterator has no additional native Person-valid
            //gate. Preserve its registration order and status1..4 family.
            if (force.PhysicalSlot is < 1 or > 51 ||
                Known(Queries.ReadConditionPersonRawForceSlot(person), "dialogue Person Force byte") != force.PhysicalSlot ||
                Known(Queries.ReadConditionPersonStatus(person), "dialogue Person status") is < 1 or > 4) continue;
            if (person == ruler || person == originalAdviser) continue;
            ushort id = Known(Queries.ReadConditionPersonCharacterId(person), "dialogue candidate identity");
            if (unchecked((ushort)(id - 4101)) > 99) remaining.Add(person);
        }

        var adviser = originalAdviser;
        if (adviser is null || !Known(Queries.ReadConditionPersonValidity(adviser), "dialogue adviser validity"))
            adviser = TakeDialogueRole(remaining, (a, b) => BeforeDialogueAbility(a, b, 2), 5014);
        var military = TakeDialogueRole(remaining, (a, b) => BeforeDialogueAbility(a, b, 1), 5015);
        var civil = TakeDialogueRole(remaining, (a, b) => BeforeDialogueAbility(a, b, 2), 5014);

        var sameFamily = new List<S14EventConditionPersonReference>();
        var otherFamily = new List<S14EventConditionPersonReference>();
        foreach (var person in remaining)
            (Known(Queries.ReadConditionPersonBloodlineId(person), "dialogue candidate bloodline") == family
                ? sameFamily : otherFamily).Add(person);
        var same = TakeDialogueRole(sameFamily, (a, b) => DialogueFamilyKey(a, family) > DialogueFamilyKey(b, family), 5015);
        var other = TakeDialogueRole(otherFamily, (a, b) => DialogueSeniorityKey(a) > DialogueSeniorityKey(b), 5014);
        remaining.Clear(); remaining.AddRange(sameFamily); remaining.AddRange(otherFamily);
        var last = TakeDialogueRole(remaining, (a, b) => DialogueLoyaltyKey(a) > DialogueLoyaltyKey(b), 5016);
        _dialogueCast = new(force, adviser, military, civil, same, other, last);
    }

    private S14EventConditionPersonReference? TakeDialogueRole(List<S14EventConditionPersonReference> people,
        Func<S14EventConditionPersonReference, S14EventConditionPersonReference, bool> before, int fallbackSlot)
    {
        //Source fallbacks are real Person-table entries:0x158+5014*8=0x9E08,
        //0x158+5015*8=0x9E10,0x158+5016*8=0x9E18. Do not substitute Person0 here.
        if (people.Count == 0) return ReadPersonPointer(fallbackSlot);
        for (int index = 1; index < people.Count; index++)
        {
            var value = people[index]; int prior = index - 1;
            while (prior >= 0 && before(value, people[prior]))
            { people[prior + 1] = people[prior]; prior--; }
            people[prior + 1] = value;
        }
        var selected = people[0]; people.RemoveAt(0); return selected;
    }

    private bool BeforeDialogueAbility(S14EventConditionPersonReference first,
        S14EventConditionPersonReference second, int dimension)
    {
        int a = DialogueAbility(first, dimension), b = DialogueAbility(second, dimension);
        if (a != b) return a > b;
        byte firstLoyalty = DialogueLoyalty(first), secondLoyalty = DialogueLoyalty(second);
        if (firstLoyalty != secondLoyalty) return firstLoyalty > secondLoyalty;
        return DialogueIdentity(first) < DialogueIdentity(second);
    }

    private int DialogueAbility(S14EventConditionPersonReference person, int dimension) =>
        inputs.ReadCurrentAbility(person, dimension) ?? throw Missing("dialogue current effective ability");

    private ushort DialogueAbilitySum(S14EventConditionPersonReference person)
    {
        int sum = 0;
        for (int dimension = 0; dimension < 5; dimension++) sum = unchecked(sum + DialogueAbility(person, dimension));
        return unchecked((ushort)sum);
    }

    private ushort DialogueSeniority(S14EventConditionPersonReference person)
    {
        var date = (Queries as IS14BasicEventConditionQueries)?.ReadConditionDate()
            ?? throw Missing("dialogue current date");
        ushort joined = inputs.ReadServiceYear?.Invoke(person) ?? throw Missing("dialogue current service year");
        return unchecked((ushort)(date.Year - joined));
    }

    private ushort DialogueIdentity(S14EventConditionPersonReference person) =>
        Known(Queries.ReadConditionPersonCharacterId(person), "dialogue Person identity");
    private byte DialogueLoyalty(S14EventConditionPersonReference person) =>
        Known(Queries.ReadConditionPersonRawLoyalty(person), "dialogue current raw loyalty");
    private ulong DialogueIdentityKey(S14EventConditionPersonReference person) =>
        unchecked((ushort)(6000 - DialogueIdentity(person)));
    private ulong DialogueSeniorityKey(S14EventConditionPersonReference person) =>
        ((ulong)DialogueSeniority(person) << 32) | ((ulong)DialogueAbilitySum(person) << 16) | DialogueIdentityKey(person);
    private ulong DialogueFamilyKey(S14EventConditionPersonReference person, ushort family) =>
        (((uint)Known(Queries.ReadConditionPersonBloodlineId(person), "dialogue sort bloodline") == unchecked((uint)(short)family) ? 1UL : 0UL) << 48) |
        DialogueSeniorityKey(person);
    private ulong DialogueLoyaltyKey(S14EventConditionPersonReference person) =>
        ((ulong)DialogueSeniority(person) << 32) | ((ulong)DialogueLoyalty(person) << 16) | DialogueIdentityKey(person);
}
