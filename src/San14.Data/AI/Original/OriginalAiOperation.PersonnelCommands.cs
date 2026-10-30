using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus PersonnelSearch(uint hexKey, uint officerKey)
    {
        if (!TryHex(hexKey, out var hex) || !TryEntity(officerKey, out var person) || person.Kind != WorldEntityKind.Officer)
            return OriginalAiNodeStatus.Failure;
        return ApplyPersonnelPreparation(Adapter.PrepareOriginalAiSearchCurrent(Current, InputsCurrent, hex.Id, person.Id));
    }

    internal OriginalAiNodeStatus PersonnelCredit(uint peopleKey, bool rankup)
    {
        var district = Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor);
        if (district is not { } at || !Adapter.ReadOriginalAiDistrictValidity(InputsCurrent, at.Id) ||
            !TryTypedCandidates(peopleKey, CandidateListKind.Officer, out var people)) return OriginalAiNodeStatus.Failure;
        return ApplyPersonnelPreparation(Adapter.PrepareOriginalAiCreditCurrent(Current, InputsCurrent,
            at.Id, people.Values, rankup));
    }

    internal OriginalAiNodeStatus PersonnelPrize(uint peopleKey, uint budgetKey)
    {
        if (!TryInteger(budgetKey, out int budget) || !TryTypedCandidates(peopleKey, CandidateListKind.Officer, out var people))
            return OriginalAiNodeStatus.Failure;
        var district = Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor);
        if (district is not { } at) throw new InvalidDataException("original_ai.personnel.prize_actor_district_contract");
        return ApplyPersonnelPreparation(Adapter.PrepareOriginalAiPrizeCurrent(Current, InputsCurrent,
            at.Id, people.Values, budget));
    }

    internal OriginalAiNodeStatus PersonnelItem(uint recipientKey, uint itemKey)
    {
        if (!TryEntity(recipientKey, out var person) || person.Kind != WorldEntityKind.Officer ||
            !TryEntity(itemKey, out var item) || item.Kind != WorldEntityKind.Item) return OriginalAiNodeStatus.Failure;
        return ApplyPersonnelPreparation(Adapter.PrepareOriginalAiItemCurrent(Current, InputsCurrent, person.Id, item.Id));
    }

    private OriginalAiNodeStatus ApplyPersonnelPreparation(OriginalAiCommandPreparation prepared) => !prepared.Success
        ? OriginalAiNodeStatus.Failure : prepared.Order is { } order ? ApplyNativeCommand(order) : OriginalAiNodeStatus.Success;
}
