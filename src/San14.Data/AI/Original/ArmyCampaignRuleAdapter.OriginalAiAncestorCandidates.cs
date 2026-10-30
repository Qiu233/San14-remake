using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal IReadOnlyList<OriginalAiEntity>? ReadOriginalAiFootholdPeopleCurrent(
        SourceWorldContext current, int baseKey, bool officerCandidates)
    {
        var source = BaseLookup.Find(baseKey, current);
        if (!AncestorKnown(current.ReadBaseNativeValidity(source), "Foothold_validity")) return null;
        if (officerCandidates)
        {
            int city = AncestorKnown(CurrentGateMapQueries.FindCity(source, current).PhysicalSlot, "candidate_related_City");
            // The ordinary appointment selector already owns the same6D8600
            // query: City then linked Gates, source Person registration order,
            // serving statuses and233240 exclusion. Reuse its actual list.
            return AreaAdministrationExecution.OrderedCandidates(current, city)
                .Select(person => new OriginalAiEntity(WorldEntityKind.Officer, person.Identity)).ToArray();
        }
        var primary = source;
        int home = AncestorKnown(current.ReadBaseObjectLink(primary), "Foothold4E");
        int district = AncestorKnown(current.ReadBaseDistrictSlot(source), "Foothold_District");
        var result = new List<OriginalAiEntity>();
        AddHome(primary);
        //089D40(42) includes linked Gates only when the original input is a
        //City. 6D8600 always receives the resolved City and excludes233240.
        if (home < 52)
        {
            var relatedCity = CurrentGateMapQueries.FindCity(source, current);
            foreach (var reference in current.ReadRegistryReferences(RegistryKind.Gate)
                ?? throw AncestorSourceMissing("GateA0_registry"))
            {
                if (reference is not { Kind: RegistryKind.Gate }) throw AncestorSourceMissing("Gate_pointer");
                var gate = new BaseReference(BaseKind.Gate, reference.PhysicalSlot);
                if (CurrentGateMapQueries.FindCity(gate, current) == relatedCity) AddHome(gate);
            }
        }
        return result;

        void AddHome(BaseReference at)
        {
            int location = AncestorKnown(current.ReadBaseObjectLink(at), "iterator_Base4E");
            if (location is < 1 or > 61) return;
            foreach (var entry in current.ReadRegisteredPersonPhysicalSlots()
                ?? throw AncestorSourceMissing("Person100_registry"))
            {
                int person = entry ?? throw AncestorSourceMissing("Person_pointer");
                if (AncestorKnown(current.ReadPersonFirstLocation(person), "PersonE2") != location ||
                    AncestorKnown(current.ReadPersonStatus(person), "PersonE6") is not (>= 1 and <= 4)) continue;
                if (AncestorKnown(current.ReadPersonDistrictSlot(person), "PersonE0") != district) continue;
                result.Add(new(WorldEntityKind.Officer, AncestorKnown(current.ReadPersonIdentity(person), "Person10")));
            }
        }
    }

    internal bool ReadOriginalAiDistrictPurposeCurrent(SourceWorldContext current, int district, int purpose) =>
        AncestorKnown(current.ReadDistrictNativeValidity(district), "purpose_District_validity") &&
        unchecked((sbyte)AncestorKnown(current.ReadDistrictRawByte(district, 0x15), "District_purpose15")) == purpose;

    internal bool ReadOriginalAiDistrictIsCurrentPlayer(GameState state, SourceWorldContext current, int district)
    {
        if (!AncestorKnown(current.ReadDistrictNativeValidity(district), "player_District_validity")) return false;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw AncestorSourceMissing("current_player_Force");
        if (!AncestorKnown(current.ReadForceNativeValidity(player), "player_Force_validity")) return false;
        return AncestorKnown(current.ReadDistrictForceId(district), "District10") ==
            AncestorKnown(current.ReadForcePhysicalSlot(player), "player_Force_identity");
    }

    internal bool ReadOriginalAiDistrictUpdateEligible(GameState state, SourceWorldContext current,
        int district, int percentage)
    {
        if (!AncestorKnown(current.ReadDistrictNativeValidity(district), "schedule_District_validity")) return false;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw AncestorSourceMissing("schedule_current_player_Force");
        int playerIdentity = AncestorKnown(current.ReadForcePhysicalSlot(player), "schedule_player_Force_identity");
        if (AncestorKnown(current.ReadDistrictForceId(district), "schedule_District10") == playerIdentity) return true;
        var bases = current.ReadRegisteredBases() ?? throw AncestorSourceMissing("schedule_Base80");
        int count = 0;
        if (playerIdentity is >= 1 and <= 51)
            foreach (var entry in bases)
            {
                var at = entry ?? throw AncestorSourceMissing("schedule_Base_pointer");
                if (AncestorKnown(current.ReadBaseForceSlot(at), "schedule_Base_Force") == playerIdentity) count++;
            }
        //Source divides by the actual registered Foothold list count; it is
        //neither the City count nor the count of the current actor's Areas.
        if (bases.Count == 0) throw AncestorSourceMissing("schedule_empty_Base80_divisor");
        if (unchecked(count * 100) / bases.Count >= percentage) return true;
        int selector = AncestorKnown(BattleRemakeCurrentSettings.ReadSelectorE58(state), "schedule_settings_selector");
        int difficulty = AncestorKnown(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "schedule_difficulty");
        difficulty = Math.Min(difficulty, 2);
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw AncestorSourceMissing("schedule_World454_45C");
        int periods = ReadOriginalAiGameInformation(state, 3);
        int residue = unchecked(district + periods + sampler.Salt45C) % 3;
        return residue <= difficulty;
    }

    private static T AncestorKnown<T>(T? value, string field) where T : struct =>
        value ?? throw AncestorSourceMissing(field);
    private static InvalidDataException AncestorSourceMissing(string field) => new("original-ai.ancestor-current." + field);
}
