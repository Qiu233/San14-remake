using San14.Core;

namespace San14.Data;

public sealed record ArmyMarchPenaltyCurrentArmyFields(int UnitId, int NativePoolSlot,
    long OwnedUpdateOrdinal, byte Flag140, string SourceProvenance);
/// <summary>Optional current field capture. The saved Army command control
/// supplies the ordinary owner when no external capture is installed.</summary>
public interface IArmyMarchPenaltyCurrentOwnerQueries
{
    ArmyMarchPenaltyCurrentArmyFields? ReadArmyFields(GameState state, UnitState army);
}
public sealed record ArmyMarchPenaltyCurrentReceipt(int Value, int? CenterHex,
    bool CommanderEffect51Exemption, int RadiusOneEnemies, int RadiusTwoEnemies, int RadiusThreeEnemies,
    int RadiusThreeHostileHexes, string SourceProvenance);
public sealed record ArmyMarchPenaltyCurrentResolution(ArmyMarchPenaltyCurrentReceipt? Receipt, string? UnresolvedOwner)
{ public bool Complete => Receipt is not null; }

/// <summary>TC2B5C10 as reached by ordinary2695D0 with null Hex override.
/// Current Army/Person/Force/map owners produce actual neighborhood counts.
/// Special Army140 still requires its reached policy and tactic owners.</summary>
public sealed class ArmyMarchPenaltyCurrentProducer
{
    public const string Revision = "san14:ce-ordinary-current-march-penalty-20261003";
    private readonly ArmyCampaignRuleAdapter package;
    private readonly LoadedMap map;
    private readonly LoadedTerrainTypes terrain;
    private readonly TacticSourceCatalog catalog;
    private readonly ArmyMarchPenaltyParameterSeed source;
    private readonly IArmyMarchPenaltyCurrentOwnerQueries? owners;
    public ArmyMarchPenaltyCurrentProducer(GameResourceRepository repository,
        ArmyCampaignRuleAdapter ownedPackage, LoadedMap ownedMap,
        ArmyMarchPenaltyParameterSeed nativeSource, IArmyMarchPenaltyCurrentOwnerQueries? currentOwners = null)
    {
        package = ownedPackage; map = ownedMap; source = nativeSource; owners = currentOwners;
        if (package.Seed.Language != source.Language || package.Seed.Source.RuleVersion != source.Source.RuleVersion)
            throw new InvalidDataException("Penalty and current Army source profiles differ.");
        var skills = repository.LoadSkillDefinitions($"0010_{source.Language}/fixdataexce.s14");
        terrain = repository.LoadTerrainTypes($"0010_{source.Language}/fixdataexce.s14");
        catalog = TacticSourceParameters.Read(skills.Formations, skills.Source);
    }

    internal ArmyMarchPenaltyCurrentProducer(ArmyCampaignRuleAdapter ownedPackage,
        LoadedMap ownedMap, LoadedTerrainTypes ownedTerrain, TacticSourceCatalog ownedCatalog,
        ArmyMarchPenaltyParameterSeed nativeSource)
    {
        package = ownedPackage; map = ownedMap; terrain = ownedTerrain;
        catalog = ownedCatalog; source = nativeSource;
    }

    public ArmyMarchPenaltyCurrentResolution Resolve(GameState state, UnitState army)
    {
        var definitions = package.CaptureCurrentDefinitions(state);
        if (definitions is null) return Missing("current.Base_Hex_Person_definition_capture");
        return Resolve(state, army, definitions);
    }

    /// <summary>Borrows the same operation's current owners instead of
    /// capturing another initial image for each actor.</summary>
    public ArmyMarchPenaltyCurrentResolution Resolve(GameState state, UnitState army, SourceWorldContext definitions) =>
        ResolveCurrent(state, army, definitions);

    internal ArmyMarchPenaltyCurrentResolution ResolveCurrent(GameState state, UnitState army,
        SourceWorldContext definitions, int? centerHexOverride = null)
    {
        var context = new TacticRemakeCurrentContext(definitions, catalog, map, terrain,
            new(source.Language, SimulationRuleIdentity.ForLocale(source.Language)), Revision);
        if (context.ArmyValid(state, army) != true || army.NativeTacticRuntime?.Status10 is not (1 or 2))
            return Missing("current.ordinary_Army_validity_Status10");
        if (!state.Officers.ContainsKey(army.OfficerId) ||
            definitions.ResolveOfficerSource(army.OfficerId) is not int commanderPhysical)
            return Missing("current.commander_Person_identity");
        bool? exempt = ReadActiveCommanderEffect51(definitions, commanderPhysical);
        if (exempt is null) return Missing("current.commander_activation_mask_and_nine_personalities2306D0");
        if (exempt.Value) return Result(0, null, true, 0, 0, 0, 0, "active-commander-effect51");
        int centerHex = centerHexOverride ?? army.HexId;
        if (centerHex is < 0 or >= 48400) return Missing("current.Army_Hex2A");
        var field = owners is not null ? owners.ReadArmyFields(state, army) :
            ArmySavedCommandKnowledge.ReadControl(state, army) is byte commandControl && army.NativeArmyOrigin?.NativeSlotIndex is int savedReceiver
                ? new ArmyMarchPenaltyCurrentArmyFields(army.Id, savedReceiver, state.NativeClock!.UpdateOrdinal,
                    commandControl, "saved-current-army-command-control") : null;
        if (field is not null && (field.UnitId != army.Id || field.NativePoolSlot != army.NativeArmyOrigin!.NativeSlotIndex ||
            field.OwnedUpdateOrdinal != state.NativeClock!.UpdateOrdinal || string.IsNullOrWhiteSpace(field.SourceProvenance)))
            throw new InvalidDataException("Penalty Army140 field belongs to another actor or current update.");
        //A nonzero140 reaches additional Hex3A/policy65/tactic/category owners.
        //Do not turn this field into an invented completed boolean callback.
        if (field is { Flag140: not 0 })
        {
            bool? special = ResolveSavedCommandPenaltyExemption(state, definitions, army);
            if (special is null) return Missing("current.special2C4420_Hex3A_policy65_object_tactic2C");
            if (special.Value) return Result(0, centerHex, false, 0, 0, 0, 0, "saved-command-policy65-facility");
        }
        var ring = TacticRemakeCurrentContext.NativeHexOrder(centerHex, 3).Skip(1).ToArray();
        var radiusOne = TacticRemakeCurrentContext.NativeHexOrder(centerHex, 1).Skip(1).ToHashSet();
        var radiusTwo = TacticRemakeCurrentContext.NativeHexOrder(centerHex, 2).Skip(1).ToHashSet();
        definitions.BindArmyMapOccupancy(state);
        int[] counts = new int[3]; int hostileHexes = 0;
        var countedObjects = new[] { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        foreach (int hexId in ring)
        {
            var hex = definitions.ReadHexTableReference(hexId);
            if (hex?.PhysicalSlot != hexId || definitions.ReadHexForceSlot(hex) is not byte hexForce ||
                definitions.ReadHexObjectLink(hex) is not ushort objectLink)
                return Missing("current.radius3_Hex_pointer_owner14_object1A");
            //2B5E00: first argument is the neighboring Hex's owner, not caster.
            bool? hostileHex = ArmyLogisticsPreparation.ReadNativeRelationGate(state, hexForce, army.ForceId);
            if (hostileHex is null) return Missing("current.Hex_owner_Force_relation2CA650");
            if (hostileHex.Value) hostileHexes++;
            int maxIndex = radiusOne.Contains(hexId) ? 0 : radiusTwo.Contains(hexId) ? 1 : 2;
            if (definitions.ReadHexRaw1C(hex) is not ushort rawOccupant)
                return Missing("current.radius3_Hex_signed1C");
            int occupant = unchecked((short)rawOccupant);
            if (occupant < 0) return Missing("branch.nearby_negative_Army_pointer_index");
            if (occupant is >= 1 and <= 500)
            {
                if (state.ReadArmyLifecycle() is not { } arena)
                    return Missing("current.nearby_Army_receiver_owner");
                var receiver = definitions.ReadArmyUnitTableReference(occupant);
                if (receiver is null)
                {
                    if (occupant >= arena.ReceiverSlots().Length || arena.ReceiverSlots()[occupant].PhysicalSlot != occupant)
                        return Missing("current.nearby_Army_pointer_table");
                    definitions.SetArmyUnitTablePointer(occupant, occupant);
                    receiver = new(occupant);
                }
                if (receiver.PhysicalSlot is int armyPhysical)
                {
                    if (armyPhysical >= arena.ReceiverSlots().Length || arena.ReceiverSlots()[armyPhysical].PhysicalSlot != armyPhysical)
                        return Missing("current.nearby_Army_receiver");
                    var slot = arena.ReceiverSlots()[armyPhysical];
                    definitions.BindUnspecifiedOwnedArmyQueryHeader(slot);
                    if (definitions.ReadArmyUnitNativeValidity(receiver) is not bool valid)
                        return Missing("current.nearby_Army_native_validity");
                    if (valid)
                    {
                        if (definitions.ReadArmyExclusionForQuerySnapshot(state, armyPhysical) is not bool excluded)
                            return Missing("current.Army_manager233560");
                        if (!excluded)
                        {
                            if (definitions.ReadArmyUnitCommanderId(receiver) is not ushort commanderId)
                                return Missing("current.nearby_Army_commander12");
                            var person = definitions.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0);
                            if (person is null) return Missing("current.nearby_Army_Person_pointer");
                            int candidateForce = 0;
                            if (person.PhysicalSlot is int personPhysical)
                            {
                                if (definitions.ReadPersonNativeValidity(personPhysical) is not bool personValid)
                                    return Missing("current.nearby_Army_Person_validity");
                                if (personValid)
                                {
                                    if (definitions.ReadPersonOldForceSlot(personPhysical) is not byte force)
                                        return Missing("current.nearby_Army_Force");
                                    candidateForce = force;
                                }
                            }
                            //26BDA0 passes candidateForce first; retain that direction.
                            bool? hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(state, candidateForce, army.ForceId);
                            if (hostile is null) return Missing("current.Army_Force_relation2CA650");
                            if (hostile.Value)
                            {
                                var candidate = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
                                if (candidate is null || candidate.NativeArmyOrigin?.NativeSlotIndex != armyPhysical || candidate.OfficerId != commanderId ||
                                    candidate.NativeTacticRuntime is not { } runtime)
                                    return Missing("current.nearby_Army_logical_projection_and_abnormal_counters");
                                if (runtime is { Abnormal23: 0, Abnormal24: 0, Abnormal25: 0 })
                                    for (int i = maxIndex; i < 3; i++) counts[i]++;
                            }
                        }
                    }
                }
            }
            var obj = definitions.ReadObjectTableReference(objectLink <= 3000 ? objectLink : 0);
            if (obj is null || definitions.ReadObjectNativeValidity(obj) is not bool objectValid)
                return Missing("current.Hex_Object_lookup_and_validity");
            if (!objectValid) continue;
            if (obj.PhysicalSlot is not int physical)
                return Missing("current.Object_physical_identity");
            //2A8080 retains City1..51 and Gate52..61 without its later generic
            //Object16 bit0/exclusion test;26BDA0 then counts each pointer once.
            if (physical is < 1 or > 61)
            {
                if (definitions.ReadMapObjectFields16(obj) is not ushort fields)
                    return Missing("current.generic_Object16");
                if ((fields & 1) == 0) continue;
                var pendingRetirements = definitions.ReadOwnedCurrentListPool()?.PendingObjectRetirements;
                var directory = definitions.CurrentMapObjectDirectory;
                if (pendingRetirements is null || directory is null)
                    return Missing("current.generic_Object_pending_F0_owner");
                var objectReceiver = directory.ReceiverAtPhysical(physical);
                bool excluded = false;
                //2336B0 compares receiver pointers in the actual F0 list.
                //A null payload is a normal nonmatch, not an unknown object.
                foreach (var pending in pendingRetirements)
                    if (pending == objectReceiver) { excluded = true; break; }
                if (excluded) continue;
            }
            if (definitions.ReadObjectForceSlot13(obj) is not byte objectForce)
                return Missing("current.Object_Force13");
            bool? hostileObject = ArmyLogisticsPreparation.ReadNativeRelationGate(state, objectForce, army.ForceId);
            if (hostileObject is null) return Missing("current.Object_Force_relation2CA650");
            if (hostileObject.Value)
                for (int i = maxIndex; i < 3; i++) if (countedObjects[i].Add(physical)) counts[i]++;
        }
        int value;
        if (counts[0] == 0 && counts[1] == 0 && counts[2] == 0 && hostileHexes == 0) value = 0;
        else
        {
            if (ReadWeight(SimulationParameterKeys.ArmyMarchPenalty.RadiusOneArmyWeight) is not int w1 ||
                ReadWeight(SimulationParameterKeys.ArmyMarchPenalty.RadiusTwoArmyWeight) is not int w2 ||
                ReadWeight(SimulationParameterKeys.ArmyMarchPenalty.RadiusThreeArmyWeight) is not int w3 ||
                ReadWeight(SimulationParameterKeys.ArmyMarchPenalty.TerritoryWeight) is not int wt)
                return Missing("current.penalty_four_weights_including_runtime27D47A4");
            value = unchecked(unchecked((hostileHexes - counts[2]) * wt) + unchecked((counts[2] - counts[1]) * w3) +
                unchecked((counts[1] - counts[0]) * w2) + unchecked(counts[0] * w1));
        }
        //An unknown140 can only change this zero value to the same zero via
        //the special early return. A nonzero ordinary result requires its raw
        //current owner; missing140 never means false.
        if (field is null && value != 0) return Missing("current.Army_byte140");
        return Result(value, centerHex, false, counts[0], counts[1], counts[2], hostileHexes,
            field?.SourceProvenance ?? "all-return-values-zero-independent-of-Army140");

        int? ReadWeight(string key) => state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(
            key, out int weight) ? weight : null;
        ArmyMarchPenaltyCurrentResolution Result(int value, int? center, bool immune,
            int n1, int n2, int n3, int territoryCount, string owner) => new(new(value, center, immune, n1, n2, n3, territoryCount,
                $"{Revision};{source.Source.ResourceUri}@{source.Source.RuleVersion};owned-clock={state.NativeClock!.UpdateOrdinal};{owner}"), null);
    }

    private static bool? ReadActiveCommanderEffect51(SourceWorldContext definitions, int person)
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
    //2C4420 reads raw3A as a Hex index independently of target kind38.
    //Its policy<=0 short circuit does not consume the later Object receiver.
    private static bool? ResolveSavedCommandPenaltyExemption(GameState state, SourceWorldContext current, UnitState army)
    {
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.ReadArmyLifecycle() is not { } lifecycle ||
            OwnedArmyLifecycleTargets.Read(state, lifecycle.Slots[physical].ReceiverId)?.Identity3A is not ushort raw) return null;
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0);
        if (hex is null) return null;
        if (!hex.PhysicalSlot.HasValue) return false;
        if (current.ReadHexAreaSlot(hex) is not ushort areaId) return null;
        var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0);
        if (area is null || current.ReadAreaCurrentForceSlot(area) is not byte rawForce) return null;
        var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0);
        if (force is null || current.ReadForceNativeValidity(force) is not bool valid) return null;
        if (!valid) return false;
        if (force.PhysicalSlot is not int forceId || !state.Forces.TryGetValue(forceId, out var owner)) return null;
        if (BattleRemakeCurrentForcePolicies.ReadForceEffect(state, owner, 65, current) is not int policy) return null;
        if (policy <= 0) return false;
        //Positive65 requires the exact Object Force pointer and definition2C;
        //the ordinary policy-zero leaf does not guess a generic facility.
        return null;
    }
    private static ArmyMarchPenaltyCurrentResolution Missing(string owner) => new(null, owner);
}
