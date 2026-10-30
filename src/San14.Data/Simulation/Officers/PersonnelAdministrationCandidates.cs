using San14.Core;

namespace San14.Data;

internal sealed record PersonnelRewardCandidate(int OfficerId, int PhysicalSlot, byte Loyalty);

/// <summary>6D9E60 and1EFAB0's read-only reward candidates. Page and command
/// preparation borrow the same current owner; no campaign capture occurs here.</summary>
internal static class PersonnelAdministrationCandidates
{
    internal static IReadOnlyList<PersonnelRewardCandidate> ReadReward(GameState state,
        SourceWorldContext current, int districtPhysical, Func<int, byte?> readCurrentLoyalty)
    {
        byte rawForce = K(current.ReadDistrictForceId(districtPhysical), "District10");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
            ?? throw Missing("District_Force_lookup");
        if (!K(current.ReadForceNativeValidity(force), "Force_validity")) return [];
        int forceId = K(current.ReadForcePhysicalSlot(force), "Force_identity");
        var player = current.ReadCurrentPlayerForce() ?? throw Missing("current_player_Force");
        int playerId = K(current.ReadForcePhysicalSlot(player), "current_player_identity");
        bool wholeForce = playerId != 0 && playerId == forceId &&
            ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot == districtPhysical;
        int districtIdentity = wholeForce ? 0 : districtPhysical - K((current.ReadDistrictTableReference(0)
            ?? throw Missing("District_anchor_lookup")).PhysicalSlot, "District_anchor_receiver");
        var result = new List<PersonnelRewardCandidate>();
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
        {
            int physical = K(entry, "Person_payload");
            //334810 uses raw22CF60;3345D0 uses rawE0 and the District's
            //relative identity. Both mode0 iterators then admit status1..4.
            if (wholeForce)
            {
                if (forceId is < 1 or > 51 || K(current.ReadPersonOldForceSlot(physical), "iterator_Person_Force") != forceId) continue;
            }
            else if (districtIdentity is < 1 or > 51 ||
                K(current.ReadPersonDistrictSlot(physical), "iterator_PersonE0") != districtIdentity) continue;
            if (K(current.ReadPersonStatus(physical), "iterator_PersonE6") is not (>= 1 and <= 4)) continue;
            if (!IsRewardEligibleCurrent(state, current, physical, readCurrentLoyalty)) continue;
            result.Add(new(K(current.ReadPersonIdentity(physical), "Person10"), physical,
                K(readCurrentLoyalty(physical), "PersonE8")));
        }
        return result.AsReadOnly();
    }

    internal static IReadOnlyList<PersonnelRewardCandidate> SelectAutomaticReward(
        IReadOnlyList<PersonnelRewardCandidate> candidates, int availableGold)
    {
        if (availableGold < 0) throw Missing("available_Gold");
        //69FE80's strict insertion comparison is a stable E8 ascending sort.
        //75E2F0 appends while the next count*100 fits the actual capital Gold.
        return Array.AsReadOnly(candidates.OrderBy(person => person.Loyalty)
            .Take(availableGold / 100).ToArray());
    }

    internal static bool IsRewardEligibleCurrent(GameState state, SourceWorldContext current,
        int physical, Func<int, byte?> readCurrentLoyalty)
    {
        if (!K(current.ReadPersonNativeValidity(physical), "Person_validity")) return false;
        if (K(current.ReadPersonStatus(physical), "PersonE6") == 1) return false;
        if ((K(current.ReadArmyReturnPersonFlags196(physical), "Person15E") & 2) != 0) return false;
        if (K(readCurrentLoyalty(physical), "PersonE8") >= 100) return false;
        int id = K(current.ReadPersonIdentity(physical), "special_Person10");
        bool classified = false;
        for (int range = 0; range < 5; range++)
            if (id >= 4111 + range * 20 && id <= 4120 + range * 20) { classified = true; break; }
        if (classified && K(current.ReadPersonFirstLocation(physical), "special_PersonE2") is >= 1 and <= 61)
        {
            var force = current.ReadPersonForce(physical) ?? throw Missing("special_Person_Force");
            if (K(current.ReadForcePhysicalSlot(force), "special_Force_identity") is not (>= 46 and <= 50)) return false;
        }
        var linked = current.ReadPersonLinkedEntity(physical) ?? throw Missing("Person22AC00");
        if (K(current.ReadLinkedEntityNativeValidity(linked), "linked_validity") &&
            ReadAssociatedElapsed(state, current, K(linked.Identity, "linked_receiver")) != 0) return false;
        var army = PersonArmyLocationSource.ReadCommanderArmy(current, physical) ?? throw Missing("Person22A200");
        if (K(current.ReadArmyUnitNativeValidity(army), "Army_validity"))
        {
            int armyPhysical = K(army.PhysicalSlot, "Army_receiver");
            var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("Army_lifecycle");
            var receiver = lifecycle.Receivers.Find(lifecycle.Slots[armyPhysical].ReceiverId)
                ?? throw Missing("Army_current_receiver");
            var image = receiver.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : receiver.PendingImage;
            if (K(ArmyConfigurationSourceAdapter.ReadAction(state, image), "Army37") != 0) return false;
        }
        return true;
    }

    private static int ReadAssociatedElapsed(GameState state, SourceWorldContext current, int identity)
    {
        var commands = current.ReadOwnedPersonCommandManager() ?? throw Missing("linked_current_manager");
        NativeMapSeasonDate start; bool clampNegative;
        if (commands.Returns.TryGetValue(identity, out var returning))
        { start = returning.StartedOn; clampNegative = true; } //22E270.
        else if (commands.Talents is {} talents && talents.TryGetValue(identity, out var talent))
        { start = talent.StartDate; clampNegative = false; }
        else if (commands.Diplomacies is {} diplomacies && diplomacies.TryGetValue(identity, out var diplomacy))
        { start = diplomacy.StartDate; clampNegative = false; }
        else if (commands.Caravans?.GetValueOrDefault(identity) is { } caravan)
        { start = caravan.StartDate; clampNegative = false; }
        else if (commands.Disappearances is {} disappearances && disappearances.TryGetValue(identity, out var disappearance))
        { start = disappearance.StartedOn; clampNegative = false; }
        else throw Missing("linked_virtualB0_start_date");
        var today = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34");
        int elapsed = unchecked(PersonReturnCommands.CalendarOrdinal(today) - PersonReturnCommands.CalendarOrdinal(start));
        return clampNegative ? Math.Max(0, elapsed) : elapsed;
    }

    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new("personnel.reward.candidates." + owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal IReadOnlyList<PersonnelRewardCandidate> ReadRewardCandidatesCurrent(GameState state,
        SourceWorldContext current, int districtPhysical) => PersonnelAdministrationCandidates.ReadReward(
            state, current, districtPhysical, physical => ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, physical, _governorInputs));

    internal bool IsRewardEligibleCurrent(GameState state, SourceWorldContext current, int physical) =>
        PersonnelAdministrationCandidates.IsRewardEligibleCurrent(state, current, physical,
            person => ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs));
}
