using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //428130: administration expansion, moved-Armies occupation, logistics
    //connection/aftermath (without266B40 upkeep), then pending70 cleanup.
    internal GameState ApplyNormalTurnTerritoryMaintenanceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnTerritoryMaintenanceSource(this, state, current, pool).Run();

    private sealed partial class NormalTurnTerritoryMaintenanceSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private readonly List<CampaignEffectRequest> effects = [];
        private readonly HashSet<int> changedPeople = [];

        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("shared_current_pool");
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            current.MaterializeFixedScenery(package._landmarkDefinitions);
            for (int i = 0; i < 101; i++) current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
            current.BindFixedConstructionSceneryLinks();
            //The owned simulation has no original scenery-layout receiver.
            //Territory publication below updates the real application map;
            //preserve any explicitly installed layout reader or receiver.
            if (current.SceneryLayoutReader is null && current.CurrentSceneryLayout is null)
                current.CurrentSceneryLayout = new(null);
            package._baseCapacityInputs.BindCurrent(state, current);
            current.BaseForcePolicyEffectReader = (force, effect) =>
                ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true).Value;
            ExpandAdministration();
            OccupyMovedArmies();
            UpdateLogistics();
            PublishPeople();
            state = package.ConsumeArmyLifecyclePendingCurrent(state, current, pool)
                ?? throw Missing("28E6D0_pending_cleanup");
            if (effects.Count != 0)
            {
                var pending = state.CampaignEffectRequests ?? throw Missing("effect_owner");
                long revision = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("calendar_revision");
                state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision, effects) };
            }
            return state;
        }

        private void OccupyMovedArmies()
        {
            foreach (int physical in ArmyOrder())
            {
                var army = new ArmyUnitReference(physical);
                if (!N(current.ReadArmyUnitNativeValidity(army), "occupation_Army_validity") ||
                    N(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "occupation_233560")) continue;
                if ((N(current.ReadArmyUnitFlags150(army), "occupation_Army150") & 1) == 0) continue;
                if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 0, false)) throw Missing("occupation_clear150");
                state = state.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current));
                if (N(current.ReadArmyReturnUnitStatus10(army), "occupation_Army10") != 1) continue;
                var image = Image(physical);
                state = package.OccupyOrdinaryNeutralTerrain(state, current, image, physical,
                    state.NativeMapSeason?.ThirdDate ?? throw Missing("occupation_source_date")).State;
            }
        }

        private UnitState Image(int physical) => NotificationArmy(state, physical);
        private IEnumerable<int> ArmyOrder()
        {
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("Army60_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("Army60_head");
            while (node.Identity.HasValue)
            {
                var receiver = current.ReadRegistryNodePayload(node) ?? throw Missing("Army60_payload");
                if (receiver.Kind != RegistryKind.Army) throw Missing("Army60_payload_kind");
                yield return N(receiver.PhysicalSlot, "Army60_physical");
                //Callbacks precede the native Next read; a callback may append
                //another receiver to the same manager during this operation.
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("Army60_next");
            }
        }
        private void WriteImage(int physical, UnitState image)
        {
            if (state.Units.ContainsKey(image.Id))
                state = state with { Units = new(state.Units) { [image.Id] = image } };
            else
            {
                var arena = state.ReadArmyLifecycle() ?? throw Missing("pending_Army_lifecycle");
                var receiver = arena.ReceiverSlots()[physical].Receiver;
                if (receiver.PendingImage?.Id != image.Id) throw Missing("pending_Army_identity");
                state = state.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with { PendingImage = image }) });
            }
        }
        private void PublishPeople()
        {
            if (changedPeople.Count == 0) return;
            var people = new Dictionary<int, OfficerState>(state.Officers);
            foreach (int physical in changedPeople)
            {
                int id = N(current.ReadPersonIdentity(physical), "changed_Person10");
                var owner = people.GetValueOrDefault(id) ?? throw Missing("changed_Person_owner");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (activation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("changed_Person_activation");
                people[id] = owner with { TraitActivation = activation };
            }
            state = state with { Officers = people }; changedPeople.Clear();
        }
        private int Person(int raw) => N((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private HexReference Hex(int raw) => current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0) ?? throw Missing("Hex_lookup");
        private ForceReference Force(int raw) => current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0) ?? throw Missing("Force_lookup");
        private int ForceId(ForceReference force) => N(current.ReadForcePhysicalSlot(force), "Force60");
        private int PersonId(int person) => N(current.ReadPersonIdentity(person), "Person10");
        private int Day() => (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("World450")).Day450;
        private IEnumerable<(HexListNodeReference Node, HexReference Hex)> Hexes(HexListHandle list)
        {
            var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head");
            while (node.Identity.HasValue)
            {
                yield return (node, current.ReadHexListNodePayload(node) ?? throw Missing("temporary_Hex_payload"));
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
            }
        }
        private static T N<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.territory-maintenance." + field);
    }
}
