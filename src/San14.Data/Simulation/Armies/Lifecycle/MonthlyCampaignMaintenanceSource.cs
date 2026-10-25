using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual monthly Turn action, on one operation's latest world,
    /// current and shared pool. Nothing here commits an enclosing Session.</summary>
    internal sealed partial class MonthlyCampaignMaintenanceSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        Action<string, GameState>? observe = null)
    {
        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("shared_pool_binding");
            //Only bind existing owners, never install fresh values here.
            package._personAssignmentPrecallInputs.BindAreaAdministrationScalars(state, current);
            //Bind saved aftermath fields through the actual current identities.
            //Generated hires also own these fields; the original resource-only
            //identity directory is not their current owner.
            var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
            foreach (var person in state.Officers.Values)
            {
                int physical = current.ResolveOfficerSource(person.Id) ?? throw Missing("aftermath_current_identity");
                var residue = knowledge.FindAftermath(person.Id);
                current.SetOfficerAftermath(physical, person.Resentment is null && person.AllegianceDisposition is null && residue is null
                    ? null : new(person.Resentment, person.AllegianceDisposition, residue));
            }
            package._personRelationInputs.BindCurrent(() => state, current);
            ProcessPersons(); Observe("monthly.persons.completed");
            ProcessAreas(); Observe("monthly.areas.completed");
            ProcessAlliances(); Observe("monthly.alliances.completed");
            ProcessRequestTerms(); Observe("monthly.request-terms.completed");
            state = package.SynchronizeDiplomacyProjection(state);
            var arena = state.ReadArmyLifecycle() ?? throw Missing("lifecycle");
            state = state.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });
            Observe("monthly.completed");
            return state;
        }

        private IEnumerable<int> Registry(RegistryKind kind)
        {
            foreach (var row in current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind))
            {
                if (row is null || row.Kind != kind) throw Missing("registry_payload_" + kind);
                yield return Need(row.PhysicalSlot, "registry_pointer_" + kind);
            }
        }
        private void ProcessPersons()
        {
            foreach (int person in Registry(RegistryKind.Person))
            {
                if (Need(current.ReadPersonStatus(person), "person_status") == 6)
                {
                    //2346D0 decodes current E4, without a Base validity gate.
                    var decoded = PersonPositionRepair.DecodeSecondLocation(
                        Need(current.ReadPersonSecondLocation(person), "person_position"));
                    if (decoded.Kind is 5 or 6)
                    {
                        int old = current.ReadOfficerAftermath(person)?.SourceKnowledge?.CaptureResetCode
                            ?? throw Missing("captivity_progress");
                        if ((uint)old > byte.MaxValue) throw Missing("captivity_progress_domain");
                        state = current.WriteMonthlyCaptivityProgress(state, person, (byte)Math.Min(old + 1, 100));
                        Observe($"monthly.person.captivity.{person}");
                    }
                }
                int intensity = current.ReadOfficerAftermath(person)?.Resentment?.Intensity
                    ?? throw Missing("resentment_intensity");
                if ((uint)intensity > byte.MaxValue) throw Missing("resentment_intensity_domain");
                if (intensity == 0) continue;
                int difference = intensity - 1;
                state = current.WriteMonthlyResentment(state, person, (byte)Math.Clamp(difference, 0, 255), difference == 0);
                Observe($"monthly.person.resentment.{person}");
            }
        }
        private void ProcessAreas()
        {
            foreach (int physical in Registry(RegistryKind.Area))
            {
                var owner = state.Areas.GetValueOrDefault(physical) ?? throw Missing("area_owner");
                byte first = Need(owner.MonthlyTimers?.First, "area_first_timer");
                if (first != 0)
                {
                    var timer = owner.MonthlyTimers! with { First = (byte)Math.Clamp(first - 1, 0, 12) };
                    state = state with { Areas = new(state.Areas) { [physical] = owner with { MonthlyTimers = timer } } };
                    Observe($"monthly.area.first.{physical}");
                }
                owner = state.Areas[physical];
                byte second = Need(owner.MonthlyTimers?.Second, "area_second_timer");
                if (second == 0) continue;
                state = state with { Areas = new(state.Areas) { [physical] = owner with
                    { MonthlyTimers = owner.MonthlyTimers! with { Second = (byte)Math.Clamp(second - 1, 0, 12) } } } };
                Observe($"monthly.area.second.{physical}");
            }
        }
        private ForceReference Force(int physical) => new(physical);
        private int Physical(ForceReference reference) => Need(current.ReadForcePhysicalSlot(reference), "force_physical");
        private bool Valid(ForceReference reference) => Need(current.ReadForceNativeValidity(reference), "force_validity");
        private bool Player(ForceReference reference)
        {
            int own = Physical(reference);
            var selected = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_force");
            int player = Physical(selected);
            return player != 0 && own == player;
        }
        private int Ruler(ForceReference reference)
        {
            int ordinal = Physical(reference);
            return ordinal == 0 ? 5014 : Need(current.ReadForceRecord(
                Need(reference.PhysicalSlot, "ruler_force_pointer"))?.RawRuler10, "force_ruler");
        }
        private int PersonLookup(int id) => Need((id <= 6000 ? current.ReadPersonLookupReference(id) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "person_lookup");
        private OfficerState PersonOwner(int physical)
        {
            int id = Need(current.ReadPersonIdentity(physical), "person_identity");
            return current.ResolveOfficerSource(id) == physical && state.Officers.TryGetValue(id, out var owner)
                ? owner : throw Missing("person_current_owner");
        }
        private ForceCombatObjective Objective(ForceReference force) =>
            state.Forces.GetValueOrDefault(Need(force.PhysicalSlot, "objective_force_pointer"))?.CombatObjective
                ?? throw Missing("attack_request_owner");
        private void WriteObjective(ForceReference force, ForceCombatObjective objective)
        {
            int physical = Need(force.PhysicalSlot, "objective_force_pointer");
            var owner = state.Forces.GetValueOrDefault(physical) ?? throw Missing("attack_request_force_owner");
            state = state with { Forces = new(state.Forces) { [physical] = owner with { CombatObjective = objective } } };
        }
        private ForceReference Requester(ForceReference force)
        {
            int raw = Need(Objective(force).RequestingForceId, "requester_identity");
            if ((uint)raw > byte.MaxValue) throw Missing("requester_identity_domain");
            return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("requester_lookup");
        }
        private byte RequesterCode(ForceReference force) => checked((byte)Need(Objective(force).RequestingForceId, "requester_code"));
        private bool RequesterEquals(ForceReference force, int physical) =>
            Need(Objective(force).RequestingForceId, "requester_comparison") == physical;
        private int P(string key) => MonthlyCampaignMaintenanceRules.ReadCurrent(state, key) ?? throw Missing("parameter_" + key);
        private string Format(int root, HistoryMessageOperands? operands = null)
        {
            operands ??= new(Force0: 0, Force1: 0);
            var result = package.FormatMonthlyMaintenanceReport(state, current, root, operands, value => state = value);
            state = result.State;
            string text = result.Text;
            if (text.Length > 338 || text.Any(c => char.IsSurrogate(c) || c == '\0')) throw Missing("complete_short_text");
            return text;
        }
        private void Queue(int kind) => Append(new(MonthlyMaintenanceOperation.ControllerQueueEvent, kind, null, null));
        private void Narration(string text) => Append(new(MonthlyMaintenanceOperation.NarrationText, null, null, text));
        private void Speaker(int id, string text) => Append(new(MonthlyMaintenanceOperation.ConditionalSpeakerText,
            null, checked((ushort)id), text));
        private void Append(MonthlyMaintenanceRequest request)
        {
            if (request.TextAtSource is { } text &&
                (text.Length > 338 || text.Any(c => char.IsSurrogate(c) || c == '\0')))
                throw Missing("complete_short_text");
            var pending = state.CampaignEffectRequests ?? throw Missing("pending_owner");
            long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("source_revision");
            state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance,
                revision, [new(null, null) { MonthlyMaintenance = request }]) };
            Observe("monthly.report." + request.Operation);
        }
        private int Draw(int bound)
        {
            if (bound < 2) return 0;
            var random = new SharedSourceRandom32((state.CampaignAppRandom32 ?? throw Missing("App32_state")).State);
            int value = random.Next(bound);
            state = state with { CampaignAppRandom32 = new(random.State) };
            return value;
        }
        private (bool Found, int Value) Policy(ForceReference force)
        {
            if (!Valid(force)) return (false, 100);
            int physical = Need(force.PhysicalSlot, "policy_force_pointer");
            bool found = state.Forces.GetValueOrDefault(physical)?.PolicyEffects?.IsEnabled(8)
                ?? throw Missing("policy8_presence");
            int value = found ? package._baseCapacityInputs.ReadPanelModifier(state, physical, 8)
                ?? throw Missing("policy8_value") : 100;
            return (found, value);
        }
        private void Observe(string point) { if (observe is not null) observe(point, GameStateCopy.Copy(state)); }
        private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        private static InvalidDataException Missing(string name) => new("monthly-maintenance." + name);
    }
}
