using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int ReadOriginalAiForceInformationScalar(GameState state, SourceWorldContext current, int force, int mode)
    {
        if (mode == 0) return ReadOriginalAiForceIdentity(current, force);
        if (mode == 11)
        {
            int person = AiForceInformationAdvisor(current, force);
            return AiForceInformationKnown(current.ReadPersonNativeValidity(person), "advisor_validity") ? 1 : 0;
        }
        if (mode is >= 12 and <= 16)
        {
            int group = mode - 12;
            var ruler = ReadOriginalAiForceRuler(current, force) ?? throw AiForceInformationMissing("ruler_pointer");
            var owner = state.Officers.GetValueOrDefault(ruler.Id) ?? throw AiForceInformationMissing("ruler_current");
            int raw = AiForceInformationKnown(OfficerDoctrineRules.ReadDoctrineId(owner.DoctrineId, owner.AbilityState), "ruler_doctrine14C");
            int doctrine = AiForceInformationKnown((current.ReadStaticTableReference(0x78CF8, raw < 7 ? raw : 0)
                ?? throw AiForceInformationMissing("doctrine_lookup")).PhysicalSlot, "doctrine_pointer");
            var definition = _scenario.Snapshot.DoctrineParameters?.Doctrines.FirstOrDefault(row => row.Slot == doctrine)
                ?? throw AiForceInformationMissing("doctrine_definition");
            if (definition.RawBytes.Length != 102) throw AiForceInformationMissing("doctrine_format");
            var values = state.NativeArmySimulation?.Parameters.Int32Values ?? throw AiForceInformationMissing("policy_current");
            int expansion = Byte(BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(force, group));
            int occupied = 0;
            for (int slot = 0; slot < 7; slot++)
            {
                string key = BattleRemakeCurrentForcePolicies.DepartmentPersonKey(force, group, slot);
                if (!values.TryGetValue(key, out int person) || person is < 0 or > ushort.MaxValue)
                    throw AiForceInformationMissing("department_person");
                //22D480 counts only1..6000, without a person-validity test.
                if (person is >= 1 and <= 6000) occupied++;
            }
            return unchecked(definition.RawBytes[91 + group] + expansion - occupied);
            int Byte(string key) => values.TryGetValue(key, out int value) && value is >= 0 and <= byte.MaxValue
                ? value : throw AiForceInformationMissing("department_expansion");
        }
        if (mode == 32)
        {
            int rank = AiForceInformationKnown(current.ReadMoraleWaveForceRank47(force), "Force47_rank");
            int physical = AiForceInformationKnown((current.ReadStaticTableReference(0x78490, rank < 13 ? rank : 0)
                ?? throw AiForceInformationMissing("rank_lookup")).PhysicalSlot, "rank_pointer");
            return AiForceInformationKnown(OwnedArmyMoraleWaveInputs.ReadRankAC(state, physical), "RankAC");
        }
        throw AiForceInformationMissing("scalar_mode_" + mode);
    }

    internal OriginalAiEntity? ReadOriginalAiForceInformationEntity(GameState state, SourceWorldContext current, int force, int mode)
    {
        if (mode == 2) return ReadOriginalAiForceRuler(current, force);
        if (mode == 18)
        {
            var district = ForcePrimaryDistrictSource.Resolve(current, new(force));
            return district.PhysicalSlot is int id ? new(WorldEntityKind.District, id) : null;
        }
        if (mode == 24)
        {
            int physical = AiForceInformationAdvisor(current, force);
            return new(WorldEntityKind.Officer, AiForceInformationKnown(current.ReadPersonIdentity(physical), "advisor_identity"));
        }
        var objective = state.Forces.GetValueOrDefault(force)?.CombatObjective ?? throw AiForceInformationMissing("combat_objective");
        if (mode == 29)
        {
            int raw = AiForceInformationKnown(objective.RequestingForceId, "requesting_Force15F");
            if ((uint)raw > byte.MaxValue) throw AiForceInformationMissing("requesting_Force15F_format");
            var at = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw AiForceInformationMissing("requesting_Force_lookup");
            return at.PhysicalSlot is int id ? new(WorldEntityKind.Force, id) : null;
        }
        if (mode == 30)
        {
            int raw = AiForceInformationKnown(objective.TargetLocationId, "request_target160");
            if ((uint)raw > byte.MaxValue) throw AiForceInformationMissing("request_target160_format");
            //Both source callers sign-extend this byte before the registered
            //Base lookup. Preserve its real fallback instead of clamping it.
            var at = BaseLookup.Find(unchecked((sbyte)raw), current);
            return AiForceInformationBaseEntity(current, at);
        }
        throw AiForceInformationMissing("entity_mode_" + mode);
    }

    internal IReadOnlyList<OriginalAiEntity> ReadOriginalAiForceInformationList(GameState state,
        SourceWorldContext current, int force, int mode)
    {
        var result = new List<OriginalAiEntity>();
        if (mode == 17)
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw AiForceInformationMissing("World60"))
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw AiForceInformationMissing("Army_pointer");
                if (AiForceInformationKnown(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(new(physical))), "Army_Force") != force) continue;
                if (AiForceInformationKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army233560")) continue;
                int id = ArmyDestructionQueue.ResolveUnitId(state, physical) ?? throw AiForceInformationMissing("Army_domain_identity");
                Add(new(WorldEntityKind.Army, id), unique: true);
            }
            return result;
        }
        if (mode == 25)
        {
            foreach (int? raw in current.ReadRegisteredPersonPhysicalSlots() ?? throw AiForceInformationMissing("World100"))
            {
                int physical = AiForceInformationKnown(raw, "Person_pointer");
                if (AiForceInformationKnown(current.ReadPersonOldForceSlot(physical), "Person_Force") != force) continue;
                if (AiForceInformationKnown(current.ReadPersonStatus(physical), "PersonE6") is < 1 or > 4) continue;
                Add(Person(physical), unique: false);
            }
            return result;
        }
        if (mode == 28)
        {
            var saved = state.OriginalAiStrategyMemory?.Forces.GetValueOrDefault(force)
                ?? throw AiForceInformationMissing("saved_frontier_owner");
            if (saved.FrontierLocationIds.IsDefault) throw AiForceInformationMissing("saved_frontier_list");
            foreach (int location in saved.FrontierLocationIds)
                foreach (var at in AiForceInformationNeighbors(current, BaseLookup.Find(location, current)))
                    if (AiForceInformationKnown(current.ReadBaseForceSlot(at), "neighbor_Force") != force)
                        Add(AiForceInformationBaseEntity(current, at), unique: true);
            return result;
        }
        foreach (var entry in current.ReadRegisteredBases() ?? throw AiForceInformationMissing("World80"))
        {
            var at = entry ?? throw AiForceInformationMissing("Base_pointer");
            if (force is < 1 or > 51 || AiForceInformationKnown(current.ReadBaseForceSlot(at), "Base_Force") != force) continue;
            if (mode == 10) Add(AiForceInformationBaseEntity(current, at), unique: true);
            else if (mode == 9)
                foreach (var neighbor in AiForceInformationNeighbors(current, at))
                {
                    int raw = AiForceInformationKnown(current.ReadBaseForceSlot(neighbor), "neighbor_Force");
                    var other = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw AiForceInformationMissing("neighbor_Force_lookup");
                    int id = AiForceInformationKnown(other.PhysicalSlot, "neighbor_Force_pointer");
                    if (id != force && AiForceInformationKnown(current.ReadForceNativeValidity(other), "neighbor_Force_validity"))
                        Add(new(WorldEntityKind.Force, id), unique: true);
                }
            else if (mode == 35)
            {
                int location = AiForceInformationKnown(current.ReadBaseObjectLink(at), "captive_Base4E");
                foreach (int? raw in current.ReadRegisteredPersonPhysicalSlots() ?? throw AiForceInformationMissing("World100"))
                {
                    int physical = AiForceInformationKnown(raw, "captive_Person_pointer");
                    if (AiForceInformationKnown(current.ReadPersonStatus(physical), "captive_PersonE6") != 6) continue;
                    if (AiForceInformationKnown(current.ReadPersonSecondLocation(physical), "captive_PersonE4") == location)
                        Add(Person(physical), unique: false);
                }
            }
            else throw AiForceInformationMissing("list_mode_" + mode);
        }
        return result;

        OriginalAiEntity Person(int physical) => new(WorldEntityKind.Officer,
            AiForceInformationKnown(current.ReadPersonIdentity(physical), "Person_identity"));
        void Add(OriginalAiEntity value, bool unique)
        { if (!unique || !result.Contains(value)) result.Add(value); }
    }

    internal bool ReadOriginalAiForceIsCurrentPlayer(GameState state, SourceWorldContext current, int force)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw AiForceInformationMissing("current_player_Force");
        int id = AiForceInformationKnown(player.PhysicalSlot, "current_player_Force_identity");
        return id != 0 && id == force;
    }

    private IEnumerable<BaseReference> AiForceInformationNeighbors(SourceWorldContext current, BaseReference source)
    {
        int physical = AiForceInformationKnown(source.PhysicalSlot, "neighbor_source_pointer");
        IReadOnlyList<byte> links = source.Kind == BaseKind.City ? _scenario.Snapshot.Cities[physical].LinkedLocationSlots
            : _scenario.Snapshot.Gates[physical].LinkedLocationSlots;
        if (links.Count < 10) throw AiForceInformationMissing("Base58_ten_links");
        int own = AiForceInformationKnown(current.ReadBaseObjectLink(source), "neighbor_source4E");
        for (int index = 0; index < 10; index++)
        {
            int link = links[index];
            if (link == 0) continue;
            var at = BaseLookup.Find(link, current);
            if (!AiForceInformationKnown(current.ReadBaseNativeValidity(at), "neighbor_validity") || link == own) continue;
            yield return at;
        }
    }

    private static int AiForceInformationAdvisor(SourceWorldContext current, int force)
    {
        ushort raw = AiForceInformationKnown(current.ReadAssignmentForceAdvisor52(force), "Force52_advisor");
        var at = raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0);
        return at?.PhysicalSlot ?? throw AiForceInformationMissing("advisor_lookup");
    }
    private static OriginalAiEntity AiForceInformationBaseEntity(SourceWorldContext current, BaseReference at) =>
        new(at.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate,
            AiForceInformationKnown(current.ReadBaseObjectLink(at), "Base4E_identity"));
    private static T AiForceInformationKnown<T>(T? value, string field) where T : struct => value ?? throw AiForceInformationMissing(field);
    private static InvalidDataException AiForceInformationMissing(string field) => new("original_ai.force_information." + field);
}
