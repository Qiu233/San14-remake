using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public IReadOnlyList<OriginalAiCrisisEntry> CaptureOriginalAiCrisisCurrent(GameState state) =>
        CaptureOriginalAiCrisisCurrent(state, CaptureOwnedArmyReturnWorld(state).Context);

    internal IReadOnlyList<OriginalAiCrisisEntry> CaptureOriginalAiCrisisCurrent(GameState state,
        SourceWorldContext current)
    {
        if (Seed.Language != "TC") throw AiCrisisMissing("SC_source_contract");
        var result = new List<OriginalAiCrisisEntry>();
        var strength = new AiWeightedStrengthOperation(this, state, current);
        foreach (var entry in current.ReadRegisteredBases() ?? throw AiCrisisMissing("World80"))
        {
            var at = entry ?? throw AiCrisisMissing("Base_pointer");
            int location = AiCrisisKnown(current.ReadBaseObjectLink(at), "Base4E");
            if (location >= 52) continue;
            int force = AiCrisisKnown(current.ReadBaseForceSlot(at), "Base_Force");
            if (force == 0) continue;
            var forcePointer = current.ReadForceTableReference(force < 52 ? force : 0)
                ?? throw AiCrisisMissing("Force_pointer");
            int ordinal = AiCrisisKnown(forcePointer.PhysicalSlot, "Force_identity");
            if (ordinal == 51 || ordinal is >= 46 and <= 50 or >= 38 and <= 45) continue;
            //No extra receiver native-validity test precedes source virtualB0.
            //22F720 itself admits only native-valid neighbor receivers.
            var neighbors = AiForceInformationNeighbors(current, at).ToArray();
            int selected = OriginalAiCrisisSource.SelectEnemy(strength.Read(at, true, 0, false),
                Enumerable.Range(0, neighbors.Length).ToArray(), index =>
                {
                    int other = AiCrisisKnown(current.ReadBaseForceSlot(neighbors[index]), "neighbor_Force");
                    return other != 0 && AiCrisisKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                        force, other), "2CA650");
                }, index => strength.Read(neighbors[index], true, 0, false));
            int? enemy = null, targetArea = null;
            if (selected >= 0)
            {
                var chosen = neighbors[selected];
                enemy = AiCrisisKnown(current.ReadBaseObjectLink(chosen), "enemy_Base4E");
                if (AiCrisisKnown(current.ReadBaseNativeValidity(chosen), "enemy_validity"))
                {
                    var from = ArmyMarchSourceAdapter.ReadBaseMapDestination(state, current, chosen);
                    var to = ArmyMarchSourceAdapter.ReadBaseMapDestination(state, current, at);
                    int[] path = from is { Kind: ArmyMarchDestinationKind.Hex, HexId: int start } &&
                        to is { Kind: ArmyMarchDestinationKind.Hex, HexId: int goal }
                        ? ArmyRouteSearch.OriginalAiCrisisPath(state, current, start, goal) : [];
                    foreach (int rawHex in path)
                    {
                        var hex = current.ReadHexTableReference((uint)rawHex < 48400 ? rawHex : 0)
                            ?? throw AiCrisisMissing("route_Hex_pointer");
                        int rawArea = hex.PhysicalSlot is null ? 0
                            : AiCrisisKnown(current.ReadHexAreaSlot(hex), "route_Hex12");
                        var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0)
                            ?? throw AiCrisisMissing("route_Area_pointer");
                        int otherForce = AiCrisisKnown(current.ReadAreaCurrentForceSlot(area), "route_Area_Force");
                        var otherPointer = current.ReadForceTableReference(otherForce < 52 ? otherForce : 0)
                            ?? throw AiCrisisMissing("route_Force_pointer");
                        if (otherPointer != forcePointer) continue;
                        targetArea = AiCrisisKnown(area.PhysicalSlot, "route_Area_identity");
                        break;
                    }
                }
            }
            result.Add(new(location, enemy, targetArea));
        }
        return result.AsReadOnly();
    }

    internal int ReadOriginalAiWeightedStrengthCurrent(GameState state, SourceWorldContext current,
        BaseReference source, bool includeHomeArmies, int reservedTroops, bool onlyAvailablePeople) =>
        new AiWeightedStrengthOperation(this, state, current).Read(source, includeHomeArmies,
            reservedTroops, onlyAvailablePeople);

    private sealed class AiWeightedStrengthOperation
    {
        private readonly ArmyCampaignRuleAdapter adapter;
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly Dictionary<int, int> scores = [];
        private readonly Dictionary<(BaseReference, bool, int, bool), int> values = [];
        internal AiWeightedStrengthOperation(ArmyCampaignRuleAdapter adapter, GameState state,
            SourceWorldContext current)
        {
            this.adapter = adapter; this.state = state; this.current = current;
            if (adapter.Seed.Language != "TC") throw AiCrisisMissing("SC_source_contract");
            adapter._moraleWaveInputs.BindCurrent(state, current);
            adapter._baseCapacityInputs.BindCurrent(state, current);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
        }
        internal int Read(BaseReference source, bool includeHomeArmies, int reservedTroops, bool onlyAvailablePeople)
        {
            var key = (source, includeHomeArmies, reservedTroops, onlyAvailablePeople);
            if (values.TryGetValue(key, out int cached)) return cached;
            int remaining = unchecked(AiCrisisKnown(current.ReadBaseResource(source, BaseResourceKind.Troops), "strength_Base3C") - reservedTroops);
            //The source returns BEFORE augmenting the home armies.
            if (remaining < 1) return values[key] = 0;
            int location = AiCrisisKnown(current.ReadBaseObjectLink(source), "strength_Base4E");
            if (includeHomeArmies && location is >= 1 and <= 61)
            {
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw AiCrisisMissing("strength_World60"))
                {
                    if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                        throw AiCrisisMissing("strength_Army_pointer");
                    if (AiCrisisKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "strength_Army233560")) continue;
                    var army = new ArmyUnitReference(physical);
                    int commander = Person(AiCrisisKnown(current.ReadArmyUnitCommanderId(army), "strength_Army12"));
                    if (!AiCrisisKnown(current.ReadPersonNativeValidity(commander), "strength_Commander_validity") ||
                        AiCrisisKnown(current.ReadPersonFirstLocation(commander), "strength_CommanderE2") != location) continue;
                    if (AiCrisisKnown(current.ReadArmyReturnUnitStatus10(army), "strength_Army10") != 1) continue;
                    remaining = unchecked(remaining + AiCrisisKnown(current.ReadArmyReturnResourceFields(army)?.Ready16, "strength_Army16"));
                }
            }
            var available = new AutomaticDraftOperation(adapter, state, location, current).Candidates();
            var people = new List<int>();
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw AiCrisisMissing("strength_World100"))
            {
                int person = AiCrisisKnown(entry, "strength_Person_pointer");
                if (location is < 1 or > 61) continue;
                if (AiCrisisKnown(current.ReadPersonFirstLocation(person), "strength_PersonE2") != location) continue;
                if (AiCrisisKnown(current.ReadPersonStatus(person), "strength_PersonE6") is < 1 or > 4) continue;
                people.Add(person);
            }
            OriginalAiCrisisSource.SortPeople(people, Score);
            float total = 0f;
            foreach (int person in people)
            {
                if (onlyAvailablePeople && !available.Contains(person)) continue;
                var command = OfficerCommandCapacity.Resolve(state, current, person);
                int capacity = command.Capacity ?? throw AiCrisisMissing(command.UnresolvedInput ?? "strength_command_capacity");
                int troops = Math.Clamp(Math.Min(capacity, remaining), 0, ushort.MaxValue);
                float common = Common(source, person, troops);
                float amount = (float)troops * common;
                amount /= Parameter(OriginalAiCrisisSource.StrengthDivisorKey);
                total += amount;
                remaining = unchecked(remaining - troops);
            }
            return values[key] = BattleUnitSourceMath.TruncateSingleToInt32(total);
        }
        private int Person(int id) => AiCrisisKnown((id <= 6000 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "strength_Person_lookup");
        private int Score(int person)
        {
            if (scores.TryGetValue(person, out int score)) return score;
            int id = AiCrisisKnown(current.ReadPersonIdentity(person), "strength_Person10");
            return scores[person] = unchecked(adapter.ReadOriginalAiCityDutyAbility(state, current, id, 1) +
                adapter.ReadOriginalAiCityDutyAbility(state, current, id, 0));
        }
        private float Common(BaseReference source, int originalPerson, int troops)
        {
            //2209A0's stack receiver is never registered in the current Army
            //arena. Its explicit constructor/setters are resolved here through
            //the same pure common-attribute kernel used by deployment figures.
            int id = Math.Min((int)AiCrisisKnown(current.ReadPersonIdentity(originalPerson), "strength_Person10"), 6000);
            int commander = Person(id);
            bool valid = AiCrisisKnown(current.ReadPersonNativeValidity(commander), "strength_temp_Commander_validity");
            int leadership = valid ? adapter.ReadOriginalAiCityDutyAbility(state, current, id, 0) : 0;
            var forcePointer = valid ? current.ReadPersonForce(commander) ?? throw AiCrisisMissing("strength_temp_Force_pointer")
                : current.ReadForceTableReference(0) ?? throw AiCrisisMissing("strength_temp_Force0_pointer");
            int force = AiCrisisKnown(forcePointer.PhysicalSlot, "strength_temp_Force");
            int moraleMaximum;
            if (!AiCrisisKnown(current.ReadForceNativeValidity(forcePointer), "strength_temp_Force_validity"))
                moraleMaximum = Parameter(SimulationParameterKeys.Morale.Maximum);
            else
            {
                var abilities = new TacticRemakeCurrentAbilities(current, adapter._scenario, adapter.Seed.Language,
                    SimulationRuleIdentity.ForLocale(adapter.Seed.Language));
                var tactics = new TacticRemakeCurrentContext(current, adapter._catalog, adapter._map, adapter._terrain,
                    new(adapter.Seed.Language, SimulationRuleIdentity.ForLocale(adapter.Seed.Language)), adapter.Revision, abilities,
                    new AiCrisisTacticQueries(adapter));
                moraleMaximum = AiCrisisKnown(tactics.ForceMoraleMaximum(state, force), "strength_morale_maximum");
            }
            int morale = unchecked((byte)Math.Min(Math.Min((int)AiCrisisKnown(current.ReadBaseMorale44(source), "strength_Base44"), 100), moraleMaximum));
            int hexId = Math.Clamp((int)current.ReadBaseCurrentHexIndex(source), 0, 48399);
            //The home word152 is written by source even though modifiers0
            //means its region query does not contribute to this common factor.
            var home = BaseLookup.Find(AiCrisisKnown(current.ReadPersonFirstLocation(originalPerson), "strength_homeE2"), current);
            _ = Math.Clamp((int)current.ReadBaseCurrentHexIndex(home), 0, 48399);
            int naval = DeploymentNavalFormationSource.Resolve(state, current,
                AiCrisisKnown(current.ReadPersonIdentity(originalPerson), "strength_naval_Person10"), originalPerson);
            if (naval >= 21 || AiCrisisKnown(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "strength_naval_kind") != 2)
            {
                //241E20 admits the requested kind2 before consulting the
                //commander. A rejected request uses042950 for this kind1
                //prediction receiver; its returned byte is NOT revalidated.
                //An invalid commander retains2269E0's constructed zero byte.
                naval = valid ? PersonNavalFormationSelection.Resolve(state, current, commander) : 0;
            }
            int land = AiCrisisKnown(TacticRemakeCurrentFormations.ReadRawByte(state, 1, 0x7E), "strength_land_kind") != 2 ? 1 : 0;
            var hex = current.ReadHexTableReference(hexId) ?? throw AiCrisisMissing("strength_Hex_pointer");
            byte terrain = AiCrisisKnown(current.ReadHexSurfaceCode(hex), "strength_Hex10");
            int selected = (adapter._terrain.Catalog.Types[terrain <= 30 ? terrain : 0].RawField2C & 2) != 0 ? naval : land;
            var moraleCoefficients = AiCrisisKnown(BattleRemakeCurrentSettings.ReadCommonMoraleCoefficients(state), "strength_morale_settings");
            float terrainFactor = selected is 6 or 14 && terrain is 5 or 6 or >= 8 and <= 11
                ? (float)Parameter(selected == 6 ? SimulationParameterKeys.BattleAttributes.Formation6TerrainPercent
                    : SimulationParameterKeys.BattleAttributes.Formation14TerrainPercent) / adapter._battleSeed.PercentDenominator : 1f;
            return BattleUnitSourceMath.CommonFactor(new(leadership, troops, 1, morale, false, false,
                moraleCoefficients.Offset, moraleCoefficients.Divisor,
                1f, terrainFactor, 1f), new(adapter._battleSeed.CommonPowerExponent,
                    Parameter(SimulationParameterKeys.BattleAttributes.CommonRootDivisor),
                    Parameter(SimulationParameterKeys.BattleAttributes.CommonLeadershipScale),
                    Parameter(SimulationParameterKeys.BattleAttributes.CommonAdditive),
                    adapter._battleSeed.FlagNumerator, adapter._battleSeed.FlagDenominator,
                    Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorNumerator),
                    Parameter(SimulationParameterKeys.BattleAttributes.CommonDivisorOffset)));
        }
        private int Parameter(string key) => state.NativeArmySimulation?.Parameters is { } parameters &&
            parameters.Int32Values.TryGetValue(key, out int value) ? value : throw AiCrisisMissing("current_parameter." + key);
    }
    private sealed class AiCrisisTacticQueries(ArmyCampaignRuleAdapter adapter) : ITacticRemakeUnresolvedQueries
    {
        //The normal2690D0 kernel owns the arithmetic, active/suppressed gate,
        //and ordered force-effect10 bank. Supply its existing current panel
        //reader instead of treating an enabled71 panel as unresolved.
        public int? ReadForcePolicyEffect(GameState state, ForceState force, int effect) =>
            adapter._baseCapacityInputs.ReadPanelModifier(state, force.Id, effect);
    }
    private static T AiCrisisKnown<T>(T? value, string field) where T : struct => value ?? throw AiCrisisMissing(field);
    private static InvalidDataException AiCrisisMissing(string field) => new("original_ai.crisis." + field);
}
