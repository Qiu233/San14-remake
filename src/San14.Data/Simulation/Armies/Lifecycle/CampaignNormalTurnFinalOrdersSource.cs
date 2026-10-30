using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The Turn action table owns the monthly/annual guards. Each wrapper
    //repeats these two independent application gates before its own body.
    internal GameState ApplyNormalTurnFinalOrdersCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int action)
    {
        if (action is < 73 or > 77) throw new ArgumentOutOfRangeException(nameof(action));
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw FinalOrdersMissing("shared_current_pool");
        if (FinalOrdersNeed(ReadApplicationInformationRestriction(state), "application_selection")) return state;
        if (FinalOrdersNeed(ReadApplicationTutorialActive(), "tutorial_active") &&
            FinalOrdersNeed(TacticRemakeCurrentBattleControl.Read(state, 81), "tutorial_byte0A_bit1")) return state;
        if (action == 77) return ForceUnregisterSource.RankCurrent(state, current, pool).State;
        return new NormalTurnFinalOrdersSource(this, state, current, pool).Run(action);
    }

    private sealed partial class NormalTurnFinalOrdersSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        internal GameState Run(int action)
        {
            package._baseCapacityInputs.BindCurrent(state, current);
            AreaFlagsSourceAdapter.BindMutation(current, () => state, next => state = next);
            switch (action)
            {
                case 73: GoodOmen(); break;
                case 74: BadOmen(); break;
                case 75: AccumulateDevelopment(); break;
                case 76: AnnualDoctrineCompetition(); break;
            }
            return state;
        }

        private NativeMapSeasonDate Date => state.NativeMapSeason?.ThirdDate ?? throw Missing("calendar");
        private int Elapsed => unchecked((state.CampaignEventWorld ?? throw Missing("elapsed_owner")).ElapsedPeriodOffset +
            (int)(state.NativeMapSeason ?? throw Missing("calendar")).AdvancedPeriods);
        private TacticSourceSamplerWorld? sampler;
        private int Sample(int bound, int a, int b = 0, int c = 0, int d = 0)
        {
            if (bound < 2) return 0;
            sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw Missing("sampler");
            return TacticEffectSourceSamplerMath.Sample(bound, 1, a, b, c, d, sampler);
        }
        private int Day => (sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw Missing("day450")).Day450;
        private ForceReference Force(int raw) => current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("Force_table");
        private BaseReference City(int raw) => current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw Missing("City_table");
        private ForceReference Force(BaseReference home) => Force(N(current.ReadBaseForceSlot(home), "Base_Force"));
        private int Id(ForceReference force) => N(current.ReadForcePhysicalSlot(force), "Force_identity");
        private int At(BaseReference home) => N(home.PhysicalSlot, "Base_pointer");
        private bool Valid(ForceReference force) => N(current.ReadForceNativeValidity(force), "Force_validity");
        private bool Valid(BaseReference home) => N(current.ReadBaseNativeValidity(home), "Base_validity");
        private bool Ordinary(ForceReference force)
        {
            if (!Valid(force)) return false;
            int id = Id(force); return id != 0 && id is not (>= 46 and <= 51);
        }
        private ForceReference Player => OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
        private bool IsPlayer(ForceReference force) => Valid(Player) && Player == force;
        private int Person(int raw) => N((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private int PersonId(int physical) => N(current.ReadPersonIdentity(physical), "Person_identity");
        private bool PersonValid(int physical) => N(current.ReadPersonNativeValidity(physical), "Person_validity");
        private int Ruler(ForceReference force) => !Valid(force) ? Person(0) :
            Person(N(current.ReadForceRecord(Id(force))?.RawRuler10, "Force_ruler10"));
        private NativeArmyForceState Owner(int force) => state.NativeArmySimulation?.Forces.GetValueOrDefault(force) ?? throw Missing("Force_owner");
        private void WriteForce(int force, Func<NativeArmyForceState, NativeArmyForceState> write)
        {
            var simulation = state.NativeArmySimulation ?? throw Missing("simulation");
            state = state with { NativeArmySimulation = simulation with { Forces = new(simulation.Forces) { [force] = write(Owner(force)) } } };
        }
        private byte Flags(int force) => N(Owner(force).Flags12, "Force_flags12");
        private short Development(int force) => N(current.ReadRulerReleasePopularity1BE(force), "Force_development1BE");
        private void AddDevelopment(int force, int amount) => state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, force, amount);
        private ushort Score(int force) => N(state.NativeForceUnregisterCurrent?.ForceRankings.GetValueOrDefault(force)?.Word1BC, "Force_score1BC");
        private void SetScore(int force, int value)
        {
            var module = state.NativeForceUnregisterCurrent ?? throw Missing("ranking_owner");
            var before = module.ForceRankings.GetValueOrDefault(force) ?? throw Missing("ranking_Force");
            state = state with { NativeForceUnregisterCurrent = module with { ForceRankings = new(module.ForceRankings)
                { [force] = before with { Word1BC = (ushort)Math.Clamp(value, 0, 36000) } } } };
        }
        private void AddScore(int force, int amount) => SetScore(force, unchecked(Score(force) + amount));
        private IEnumerable<int> Registry(RegistryKind kind)
        {
            foreach (var row in current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind))
            {
                if (row is null || row.Kind != kind || row.PhysicalSlot is not int physical) throw Missing("registry_payload_" + kind);
                yield return physical;
            }
        }
        private IEnumerable<AreaReference> Areas(ForceReference force)
        {
            foreach (int physical in Registry(RegistryKind.Area))
            {
                var area = new AreaReference(physical);
                if (N(current.ReadAreaCurrentForceSlot(area), "Area_Force") == Id(force)) yield return area;
            }
        }
        private IEnumerable<int> ForcePeople(ForceReference force)
        {
            foreach (int physical in Registry(RegistryKind.Person))
                if (N(current.ReadPersonOldForceSlot(physical), "force_person_membership") == Id(force) &&
                    N(current.ReadPersonStatus(physical), "force_person_status") is >= 1 and <= 4) yield return physical;
        }
        private IEnumerable<int> HomePeople(BaseReference home)
        {
            int location = N(current.ReadBaseObjectLink(home), "home_location");
            if (location is < 1 or > 61) yield break;
            foreach (int physical in Registry(RegistryKind.Person))
                if (N(current.ReadPersonFirstLocation(physical), "home_person_E2") == location &&
                    N(current.ReadPersonStatus(physical), "home_person_status") is >= 1 and <= 4) yield return physical;
        }
        private int Doctrine(ForceReference force)
        {
            var ruler = state.Officers.GetValueOrDefault(PersonId(Person(N(current.ReadForceRecord(Id(force))?.RawRuler10, "doctrine_Force10"))))
                ?? throw Missing("doctrine_ruler_owner");
            int raw = ruler.DoctrineId ?? ruler.AbilityState?.ObservedDoctrineId ?? throw Missing("doctrine_Person14C");
            if (raw is < 1 or > 6) return 0;
            var definition = state.Doctrines.GetValueOrDefault(raw) ?? throw Missing("doctrine_definition");
            return definition.Name.Length != 0 ? raw : 0;
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary");
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private void Add(HexListHandle list, RegistryKind kind, int physical)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(kind, physical));
        }
        private List<(HexListNodeReference Node, int Physical)> Values(HexListHandle list, RegistryKind kind)
        {
            var values = new List<(HexListNodeReference, int)>();
            for (var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head"); node.Identity.HasValue;
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next"))
            {
                var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
                if (payload.Kind != kind) throw Missing("temporary_kind");
                values.Add((node, N(payload.PhysicalSlot, "temporary_receiver")));
            }
            return values;
        }
        private List<int> Shuffle(HexListHandle list, RegistryKind kind)
        {
            var values = Values(list, kind); int size = values.Count, salt = 0;
            for (int rounds = unchecked(size / 2 * size); rounds > 0; rounds--)
            {
                int a = Sample(size, salt++), b = size - Sample(size, salt++) - 1;
                if (a == b) continue;
                var left = values[a]; var right = values[b];
                current.WriteRegistryNodePayload(new(left.Node.Identity), new(kind, right.Physical));
                current.WriteRegistryNodePayload(new(right.Node.Identity), new(kind, left.Physical));
                values[a] = (left.Node, right.Physical); values[b] = (right.Node, left.Physical);
            }
            return values.Select(x => x.Physical).ToList();
        }
        private string Format(int root, HistoryMessageOperands operands)
        { var result = package.FormatNormalTurnReport(state, current, root, operands); state = result.State; return result.Text; }
        private void Append(IReadOnlyList<CampaignEffectRequest> requests)
        {
            if (requests.Count == 0) return;
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner"))
                .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), requests) };
        }
        private void Sound(int id) => Append([new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) }]);
        private void Narration(string text) => Append([new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.NarrationText, null, null, text) }]);
        private void Conditional(int person, string text) => Append([new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ConditionalSpeakerText, null, (ushort)PersonId(person), text) }]);
        private void Scene(int? kind) => state = package.AppendNormalTurnFinalOrderSceneCurrent(state, kind);
        private void History(string text, int person, int hex, bool mark4 = false)
        {
            var requests = new List<CampaignEffectRequest>();
            state = package.WriteHistory(state, current, new(1, 0, hex, Day, string.Empty, text, PersonId(person), MarkBit0: 1,
                MarkBit4: mark4 ? 1 : 0), requests);
            Append(requests);
        }
        private int Speaker()
        {
            var selection = new PersonDeathHistorySelection(state, current, pool,
                new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)), package._governorInputs,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect));
            return N(selection.Select(selection.PrimaryDistrict(Player)).PhysicalSlot, "speaker_receiver");
        }
        private int P(string name, int tc)
        {
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("parameters");
            string key = "simulation.normal-turn.final-orders." + name;
            if (bank.Float32Values.ContainsKey(key)) throw Missing("integer_parameter_" + name);
            if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
            return NormalTurnFinalOrderParameters.ReadInitial(bank.Language, name, tc);
        }
        private static T N<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        private static InvalidDataException Missing(string name) => FinalOrdersMissing(name);
    }
    private static T FinalOrdersNeed<T>(T? value, string name) where T : struct => value ?? throw FinalOrdersMissing(name);
    private static InvalidDataException FinalOrdersMissing(string name) => new("normal-turn.final-orders." + name);

    internal GameState AppendNormalTurnFinalOrderSceneCurrent(GameState state, int? scene)
    {
        var requests = new CampaignEffectRequest[] { new(null, null) { MonthlyMaintenance = new(
            scene.HasValue ? MonthlyMaintenanceOperation.FinalOrderSceneEnter : MonthlyMaintenanceOperation.FinalOrderSceneExit,
            scene, null, null) } };
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw FinalOrdersMissing("scene_pending_owner"))
            .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw FinalOrdersMissing("scene_revision"), requests) };
    }
    internal GameState AppendNormalTurnFinalOrderResourceCurrent(GameState state, SourceWorldContext current, int force, int location,
        int kind, int root, int amount, int currentAmount)
    {
        //6C5EB0 submits the complete CDestinyRandomBonusState operand packet.
        //It is not a generic narration popup and has not yet been played.
        var formatted = FormatNormalTurnReport(state, current, root,
            new(Force0: force, Base0: checked((ushort)location), Integers: [amount, currentAmount]));
        state = formatted.State;
        var requests = new CampaignEffectRequest[] { new(null, null) { NormalTurnForceResourceNotice =
            new(force, location, kind, root, amount, currentAmount) { TextAtSource = formatted.Text } } };
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw FinalOrdersMissing("resource_pending_owner"))
            .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw FinalOrdersMissing("resource_revision"), requests) };
    }
}
