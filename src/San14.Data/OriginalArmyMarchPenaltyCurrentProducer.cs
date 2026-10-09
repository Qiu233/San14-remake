using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyMarchPenaltyCurrentArmyFields(int UnitId, int NativePoolSlot,
    long OwnedUpdateOrdinal, byte Flag140, string SourceProvenance);
/// <summary>Current actual Army140 is not present in Core yet. Providers must
/// return an owned field capture, not infer it from a gameplay action or trait.</summary>
public interface IOriginalArmyMarchPenaltyCurrentOwnerQueries
{
    OriginalArmyMarchPenaltyCurrentArmyFields? ReadArmyFields(GameState state, UnitState army);
}
public sealed record OriginalArmyMarchPenaltyCurrentReceipt(int Value, int? CenterHex,
    bool CommanderEffect51Exemption, int RadiusOneEnemies, int RadiusTwoEnemies, int RadiusThreeEnemies,
    int RadiusThreeHostileHexes, string SourceProvenance);
public sealed record OriginalArmyMarchPenaltyCurrentResolution(OriginalArmyMarchPenaltyCurrentReceipt? Receipt, string? UnresolvedOwner)
{ public bool Complete => Receipt is not null; }

/// <summary>TC2B5C10 as reached by ordinary2695D0 with null Hex override.
/// Current Army/Person/Force/map owners produce actual neighborhood counts.
/// Special Army140 and non-Base generic Objects remain explicit source gaps.</summary>
public sealed class OriginalArmyMarchPenaltyCurrentProducer
{
    public const string Revision = "san14:ce-ordinary-current-march-penalty-20261003";
    private readonly OriginalOwnedArmyCampaignRules package;
    private readonly LoadedMap map;
    private readonly LoadedTerrainTypes terrain;
    private readonly OriginalTacticSourceCatalog catalog;
    private readonly OriginalArmyMarchPenaltyParameterSeed source;
    private readonly IOriginalArmyMarchPenaltyCurrentOwnerQueries? owners;
    public OriginalArmyMarchPenaltyCurrentProducer(OriginalGameRepository repository,
        OriginalOwnedArmyCampaignRules ownedPackage, LoadedMap ownedMap,
        OriginalArmyMarchPenaltyParameterSeed nativeSource, IOriginalArmyMarchPenaltyCurrentOwnerQueries? currentOwners = null)
    {
        package = ownedPackage; map = ownedMap; source = nativeSource; owners = currentOwners;
        if (package.Seed.Language != source.Language || package.Seed.Source.RuleVersion != source.Source.RuleVersion)
            throw new InvalidDataException("Penalty and current Army source profiles differ.");
        var skills = repository.LoadSkillDefinitions($"0010_{source.Language}/fixdataexce.s14");
        terrain = repository.LoadTerrainTypes($"0010_{source.Language}/fixdataexce.s14");
        catalog = OriginalTacticSourceParameters.Read(skills.Formations, skills.Source);
    }

    public OriginalArmyMarchPenaltyCurrentResolution Resolve(GameState state, UnitState army)
    {
        var definitions = package.CaptureCurrentDefinitions(state);
        if (definitions is null) return Missing("current.Base_Hex_Person_definition_capture");
        var context = new OriginalTacticRemakeCurrentContext(definitions, catalog, map, terrain,
            new(source.Language, SimulationRuleIdentity.ForLocale(source.Language)), Revision);
        if (context.ArmyValid(state, army) != true || army.NativeTacticRuntime?.Status10 != 1)
            return Missing("current.ordinary_Army_validity_Status10");
        if (state.Officers.GetValueOrDefault(army.OfficerId)?.NativeAbilityRuntime is not { } commander ||
            definitions.ReadPersonIdentity(commander.PhysicalSlot) != army.OfficerId)
            return Missing("current.commander_Person_identity");
        bool? exempt = ReadActiveCommanderEffect51(definitions, commander.PhysicalSlot);
        if (exempt is null) return Missing("current.commander_activation_mask_and_nine_personalities2306D0");
        if (exempt.Value) return Result(0, null, true, 0, 0, 0, 0, "active-commander-effect51");
        if (army.HexId is < 0 or >= 48400) return Missing("current.Army_Hex2A");
        var field = owners?.ReadArmyFields(state, army);
        if (field is not null && (field.UnitId != army.Id || field.NativePoolSlot != army.NativeArmyOrigin!.NativeSlotIndex ||
            field.OwnedUpdateOrdinal != state.NativeClock!.UpdateOrdinal || string.IsNullOrWhiteSpace(field.SourceProvenance)))
            throw new InvalidDataException("Penalty Army140 field belongs to another actor or current update.");
        //A nonzero140 reaches additional Hex3A/policy65/tactic/category owners.
        //Do not turn this field into an invented completed boolean callback.
        if (field is { Flag140: not 0 }) return Missing("current.special2C4420_Hex3A_policy65_object_tactic2C");
        var ring = OriginalTacticRemakeCurrentContext.NativeHexOrder(army.HexId, 3).Skip(1).ToArray();
        var radiusOne = OriginalTacticRemakeCurrentContext.NativeHexOrder(army.HexId, 1).Skip(1).ToHashSet();
        var radiusTwo = OriginalTacticRemakeCurrentContext.NativeHexOrder(army.HexId, 2).Skip(1).ToHashSet();
        var armyAtHex = new Dictionary<int, UnitState>();
        var slots = new HashSet<int>();
        foreach (UnitState candidate in state.Units.Values)
        {
            bool? valid = context.ArmyValid(state, candidate);
            if (valid is null) return Missing("current.complete_owned_Army_allocation");
            if (!valid.Value) continue;
            if (candidate.HexId is < 0 or >= 48400 || candidate.NativeArmyOrigin!.NativeSlotIndex is not int nativeSlot || !slots.Add(nativeSlot) ||
                !armyAtHex.TryAdd(candidate.HexId, candidate)) return Missing("current.unique_native_Army_slot_and_Hex1C");
        }
        int[] counts = new int[3]; int hostileHexes = 0;
        var countedObjects = new[] { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        foreach (int hexId in ring)
        {
            var hex = definitions.ReadHexTableReference(hexId);
            if (hex?.PhysicalSlot != hexId || definitions.ReadHexForceSlot(hex) is not byte hexForce ||
                definitions.ReadHexObjectLink(hex) is not ushort objectLink)
                return Missing("current.radius3_Hex_pointer_owner14_object1A");
            //2B5E00: first argument is the neighboring Hex's owner, not caster.
            bool? hostileHex = OriginalArmyLogisticsPreparation.ReadNativeRelationGate(state, hexForce, army.ForceId);
            if (hostileHex is null) return Missing("current.Hex_owner_Force_relation2CA650");
            if (hostileHex.Value) hostileHexes++;
            int maxIndex = radiusOne.Contains(hexId) ? 0 : radiusTwo.Contains(hexId) ? 1 : 2;
            if (armyAtHex.TryGetValue(hexId, out UnitState? candidate))
            {
                bool? excluded = context.ArmyManagerExcluded(state, candidate.Id);
                if (excluded is null) return Missing("current.Army_manager233560");
                if (!excluded.Value)
                {
                    //26BDA0 passes candidateForce first; retain that direction.
                    bool? hostile = OriginalArmyLogisticsPreparation.ReadNativeRelationGate(state, candidate.ForceId, army.ForceId);
                    if (hostile is null) return Missing("current.Army_Force_relation2CA650");
                    if (hostile.Value && candidate.NativeTacticRuntime is { Abnormal23: 0, Abnormal24: 0, Abnormal25: 0 })
                        for (int i = maxIndex; i < 3; i++) counts[i]++;
                }
            }
            var obj = definitions.ReadObjectTableReference(objectLink <= 3000 ? objectLink : 0);
            if (obj is null || definitions.ReadObjectNativeValidity(obj) is not bool objectValid)
                return Missing("current.Hex_Object_lookup_and_validity");
            if (!objectValid) continue;
            if (obj.PhysicalSlot is not int physical || definitions.ReadObjectForceSlot13(obj) is not byte objectForce)
                return Missing("current.Object_physical_identity_and_Force13");
            //2A8080 retains City1..51 and Gate52..61 without its later generic
            //Object16 bit1/exclusion test;26BDA0 then counts each pointer once.
            if (physical is < 1 or > 61) return Missing("current.generic_Object16_bit1_and_manager2336B0");
            bool? hostileObject = OriginalArmyLogisticsPreparation.ReadNativeRelationGate(state, objectForce, army.ForceId);
            if (hostileObject is null) return Missing("current.Object_Force_relation2CA650");
            if (hostileObject.Value)
                for (int i = maxIndex; i < 3; i++) if (countedObjects[i].Add(physical)) counts[i]++;
        }
        int value;
        if (counts[0] == 0 && counts[1] == 0 && counts[2] == 0 && hostileHexes == 0) value = 0;
        else
        {
            if (ReadWeight(OriginalArmyMarchPenaltyParameterSeed.RadiusOneWeightScRva) is not int w1 ||
                ReadWeight(OriginalArmyMarchPenaltyParameterSeed.RadiusTwoWeightScRva) is not int w2 ||
                ReadWeight(OriginalArmyMarchPenaltyParameterSeed.RadiusThreeWeightScRva) is not int w3 ||
                ReadWeight(OriginalArmyMarchPenaltyParameterSeed.TerritoryWeightScRva) is not int wt)
                return Missing("current.penalty_four_weights_including_runtime27D47A4");
            value = unchecked(unchecked((hostileHexes - counts[2]) * wt) + unchecked((counts[2] - counts[1]) * w3) +
                unchecked((counts[1] - counts[0]) * w2) + unchecked(counts[0] * w1));
        }
        //An unknown140 can only change this zero value to the same zero via
        //the special early return. A nonzero ordinary result requires its raw
        //current owner; missing140 never means false.
        if (field is null && value != 0) return Missing("current.Army_byte140");
        return Result(value, army.HexId, false, counts[0], counts[1], counts[2], hostileHexes,
            field?.SourceProvenance ?? "all-return-values-zero-independent-of-Army140");

        int? ReadWeight(int rva) => state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(
            OriginalTacticRemakeParameterSeed.Key(rva), out int weight) ? weight : null;
        OriginalArmyMarchPenaltyCurrentResolution Result(int value, int? center, bool immune,
            int n1, int n2, int n3, int territoryCount, string owner) => new(new(value, center, immune, n1, n2, n3, territoryCount,
                $"{Revision};{source.Source.ResourceUri}@{source.Source.RuleVersion};owned-clock={state.NativeClock!.UpdateOrdinal};{owner}"), null);
    }

    private static bool? ReadActiveCommanderEffect51(OriginalEventInitializationContext definitions, int person)
    {
        if (definitions.ReadPersonTraitMask1BC(person) is not ushort active || definitions.ReadPersonIdentity(person) is not ushort identity)
            return null;
        for (int index = 0; index < 9; index++)
        {
            if ((active & (1 << index)) == 0) continue;
            if (definitions.ReadPersonTraitSlot150(person, index) is not ushort raw) return null;
            var candidate = definitions.ReadPersonalityTableReference(raw <= 355 ? raw : 0);
            if (candidate is null || definitions.ReadSkillPersonalityValidity(candidate) is not bool valid) return null;
            bool custom = unchecked((uint)(identity - 5101)) <= 99;
            if (!custom && valid)
            {
                if (definitions.ReadSkillPersonalityFlags(candidate) is not byte flags) return null;
                if ((flags & 0x10) == 0) candidate = definitions.ReadPersonalityTableReference(0);
            }
            if (index >= 5) candidate = definitions.ReadPersonalityTableReference(0);
            if (candidate is null || definitions.ReadSkillPersonalityValidity(candidate) is not bool selectedValid) return null;
            if (!selectedValid) continue;
            if (definitions.ReadPersonalityByte(candidate, 0xB6) is not byte first ||
                definitions.ReadPersonalityByte(candidate, 0xBA) is not byte second) return null;
            if (first == 51 || second == 51) return true;
        }
        return false;
    }
    private static OriginalArmyMarchPenaltyCurrentResolution Missing(string owner) => new(null, owner);
}
