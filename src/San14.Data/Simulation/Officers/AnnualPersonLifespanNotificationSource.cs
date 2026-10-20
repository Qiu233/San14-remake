using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The annual player's complete current lifespan-warning scan.
    /// It preserves duplicate nodes, source payload shuffle and first-two work.
    /// All writes belong to the enclosing candidate, never to the App authority.</summary>
    internal sealed class AnnualPersonLifespanNotificationSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        Action<string, GameState>? observe = null)
    {
        internal const string NormalZeroOffsetKey = "simulation.lifespan-notification.normal-zero-category-year-offset";
        internal const string NormalNonzeroOffsetKey = "simulation.lifespan-notification.normal-nonzero-category-year-offset";
        internal const string ExtendedZeroOffsetKey = "simulation.lifespan-notification.extended-zero-category-year-offset";
        internal const string ExtendedNonzeroOffsetKey = "simulation.lifespan-notification.extended-nonzero-category-year-offset";

        // Explicit project CE fresh catalog. TC and SC have independent file
        // evidence for these three initial values, not all live writer proof.
        // The fourth has no proved initializer and is deliberately uninstalled.
        internal static GameState InstallFreshParameters(GameState input, LoadedScenario source, string locale)
        {
            if (locale is not ("TC" or "SC") || source.Snapshot.Locale != locale ||
                input.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != locale ||
                simulation.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(locale))
                throw Missing("fresh_parameter_profile");
            if (source.Metadata.FormatMarker != 0x66) return input;
            var parameters = simulation.Parameters;
            string[] keys = [NormalZeroOffsetKey, NormalNonzeroOffsetKey, ExtendedZeroOffsetKey, ExtendedNonzeroOffsetKey];
            if (keys.Any(key => parameters.Int32Values.ContainsKey(key) || parameters.Float32Values.ContainsKey(key)))
                throw Missing("fresh_parameters_already_owned");
            return input with { NativeArmySimulation = simulation with { Parameters = parameters with
            { Int32Values = new(parameters.Int32Values)
                { [NormalNonzeroOffsetKey] = 10, [ExtendedZeroOffsetKey] = 20, [ExtendedNonzeroOffsetKey] = 30 } } } };
        }

        internal GameState Run()
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("current_player_pointer");
            if (!Need(current.ReadForceNativeValidity(player), "current_player_validity"))
            { Observe("lifespan-notification.player-rejected"); return state; }
            int mode = state.CampaignEventWorld?.ScenarioMode ?? throw Missing("current_world_mode");
            if (mode == 2) { Observe("lifespan-notification.mode-rejected"); return state; }
            int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
            int setting = Need(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x1C4 : 0x2C), "selected_lifespan_setting");
            if (unchecked((uint)setting) > 1)
            { Observe("lifespan-notification.setting-rejected"); return state; }

            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("current_shared_pool_binding");
            // This established Item authority is bound only after genuine early
            // rejections. Collection does not write Items; it sees action3's state.
            PersonArmyCaptureInputs.BindCurrent(state, current);
            current.MoraleWavePersonPostE7Reader = physical =>
                OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, physical);
            HexListHandle? candidates = null;
            try
            {
                Enter();
                try { candidates = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor"); }
                finally { Exit(); }
                if (!candidates.Identity.HasValue) throw Missing("temporary_allocation_profile");
                var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("current_third_date");
                int year = unchecked((short)date.Year);
                var registry = current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("registered_person_chain");
                foreach (int? entry in registry)
                {
                    int physical = Need(entry, "registered_person_payload");
                    var force = current.ReadPersonForce(physical) ?? throw Missing("person_current_force");
                    if (force != player) continue;
                    int flags = current.ReadPersonBehavior(physical)?.UninterpretedFlags ?? throw Missing("current_person_flags");
                    if ((uint)flags > ushort.MaxValue) throw Missing("current_person_flags");
                    if ((flags & 0x400) != 0) continue;
                    var life = Lifespan(physical);
                    string key = setting == 0
                        ? life.Kind == 0 ? NormalZeroOffsetKey : NormalNonzeroOffsetKey
                        : life.Kind == 0 ? ExtendedZeroOffsetKey : ExtendedNonzeroOffsetKey;
                    int offset = Parameter(key);
                    ushort id = Identity(physical);
                    int extension = Need(current.ReadConditionPersonLifespanExtension(id), "current_item_extension");
                    // AE is a fresh current read after the Item helper. The
                    // saved typed record does not collapse it with an opening row.
                    int terminal = Lifespan(physical).TerminalYear;
                    if (unchecked(terminal + extension + offset) > year) continue;
                    Enter();
                    try
                    {
                        var node = current.AllocateRegistryNode(new(candidates.Identity)) ?? throw Missing("temporary_node");
                        if (node.Identity.HasValue)
                            current.WriteRegistryNodePayload(node, new(RegistryKind.Person, physical));
                    }
                    finally { Exit(); }
                    Observe($"lifespan-notification.qualified.{physical}");
                }
                Observe("lifespan-notification.scan-completed");
                int count = Need(current.ReadTemporaryHexListCount(candidates), "temporary_count");
                if (count > 1)
                {
                    var nodes = ReadNodes(candidates);
                    var sampler = package.Context(current).ReadCurrentSamplerWorld(state) ?? throw Missing("current_shuffle_sampler");
                    PersonFreedomHomeSource.Shuffle(current, nodes, sampler);
                    Observe("lifespan-notification.shuffled");
                }
                int notifications = 0;
                var next = current.ReadHexListHead(candidates) ?? throw Missing("temporary_head");
                while (next.Identity.HasValue && notifications < 2)
                {
                    int physical = Payload(next);
                    ushort argument0 = Identity(physical);
                    ushort argument1 = Need(current.ReadMoraleWaveRuler22EAC0(player), "current_player_ruler");
                    string text = Format(argument0, argument1);
                    Observe("lifespan-notification.formatted");
                    Append(new(PersonLifespanNotificationOperation.ControllerQueueEvent, 7, null, null));
                    Observe("lifespan-notification.queue-event7");
                    ushort freshPerson = Identity(physical);
                    Append(new(PersonLifespanNotificationOperation.Narration, null, freshPerson, text));
                    Observe("lifespan-notification.narration");
                    notifications++;
                    next = current.ReadHexListNodeNext(next) ?? throw Missing("temporary_next");
                }
                ClearRelease(candidates); candidates = null;
                var arena = state.ReadArmyLifecycle() ?? throw Missing("current_lifecycle");
                state = state.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });
                Observe("lifespan-notification.completed");
                return state;
            }
            finally { if (candidates is not null) ClearRelease(candidates); }
        }

        private OfficerLifespanState Lifespan(int physical)
        {
            ushort id = Identity(physical);
            if (current.ResolveOfficerSource(id) != physical || state.Officers.GetValueOrDefault(id)?.Lifespan is not { } value)
                throw Missing("current_person_lifespan");
            return value;
        }

        private string Format(ushort argument0, ushort argument1)
        {
            try
            {
                var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                    package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                var people = new DeploymentNarrationCurrentPeople(state, current, abilities, argument0, argument1,
                    panelReader: (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect),
                    appearanceRepository: package._repository, appearanceScenario: package._scenario,
                    appearanceLanguage: package.Seed.Language);
                var messages = new NarrationResourceMessages(package._repository.LoadMessages(package.Seed.Language, MessageKind.Game).Messages);
                return messages.ResolvePersonLifespanNotification(people, Draw);
            }
            catch (IOException error) { throw new InvalidDataException("annual-person-lifespan-notification.text_resource", error); }
        }
        private int Draw(int bound)
        {
            if (bound < 2) return 0;
            var saved = state.CampaignAppRandom32 ?? throw Missing("message_App32");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            state = state with { CampaignAppRandom32 = new(random.State) };
            return value;
        }
        private void Append(PersonLifespanNotificationRequest request)
        {
            var queue = state.CampaignEffectRequests ?? throw Missing("effect_requests");
            long revision = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("source_revision");
            state = state with { CampaignEffectRequests = queue.Append(CampaignEffectSource.PersonLifespanNotifications,
                revision, [new(null, null) { PersonLifespanNotification = request }]) };
        }
        private int Parameter(string key) => state.NativeArmySimulation?.Parameters is { } bank &&
            !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value) ? value : throw Missing(key);
        private List<PersonFreedomHomeSource.Entry> ReadNodes(HexListHandle list)
        {
            var result = new List<PersonFreedomHomeSource.Entry>();
            var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
                if (payload.Kind != RegistryKind.Person) throw Missing("temporary_payload_kind");
                result.Add(new(node, payload));
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
            }
            return result;
        }
        private int Payload(HexListNodeReference node)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
            if (value.Kind != RegistryKind.Person) throw Missing("temporary_payload_kind");
            return Need(value.PhysicalSlot, "temporary_payload_pointer");
        }
        private ushort Identity(int physical) => Need(current.ReadPersonIdentity(physical), "current_person_identity");
        private void Enter() { if (Need(current.ReadRegistryLockEnabled(), "registry_lock")) current.EnterRegistryLock(); }
        private void Exit() { if (Need(current.ReadRegistryLockEnabled(), "registry_unlock")) current.ExitRegistryLock(); }
        private void ClearRelease(HexListHandle list)
        { Enter(); try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); } finally { Exit(); } }
        private void Observe(string point) { if (observe is not null) observe(point, GameStateCopy.Copy(state)); }
        private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        private static InvalidDataException Missing(string name) => new("annual-person-lifespan-notification." + name);
    }
}
