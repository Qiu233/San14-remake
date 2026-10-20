using San14.Core;

namespace San14.Data;

public sealed record OrdinaryArmyOccupationReceipt(int UnitId, int ArmyPhysical, int Width,
    int[] CandidateHexes, int[] TransferredHexes, int[] DirtyAreas,
    PersonGrowthReceipt? Growth, PersonPendingMeritReceipt? Merit);
public sealed record OrdinaryArmyOccupationResult(GameState? State,
    OrdinaryArmyOccupationReceipt[] Receipts, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Stage11 consumes the actual movement flag in manager order.
    /// Neutral ordinary terrain and player-owned capture of neutral centers
    /// include commander rewards. Other sites, enemy-force callbacks and active objectives remain explicit
    /// unresolved branches. Any failure discards the entire private batch.</summary>
    public OrdinaryArmyOccupationResult TryAdvanceOrdinaryOccupation(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Occupation ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Army occupation requires its reached source stage11.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var order = ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current);
            var working = state;
            var receipts = new List<OrdinaryArmyOccupationReceipt>();
            foreach (var entry in order)
            {
                current = CaptureWritableTransportRouteCurrent(working);
                if (entry.PhysicalSlot is not int physical) continue;
                var receiver = new ArmyUnitReference(physical);
                if (!OccupationKnown(current.ReadArmyUnitNativeValidity(receiver), "Army_validity") ||
                    OccupationKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, physical), "Army_excluded")) continue;
                if ((OccupationKnown(current.ReadArmyUnitFlags150(receiver), "Army_flags") & 1) == 0) continue;
                var arena = working.ReadArmyLifecycle()!;
                if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 0, false)) throw OccupationMissing("Army_flags_clear");
                working = working.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(arena, current));
                if (OccupationKnown(current.ReadArmyReturnUnitStatus10(receiver), "Army_status") != 1) continue;
                int id = OccupationKnown(arena.ReceiverSlots()[physical].ActiveUnitId, "active_Army");
                if (!working.Units.TryGetValue(id, out var army)) throw OccupationMissing("active_Army_owner");
                var result = OccupyOrdinaryNeutralTerrain(working, current, army, physical);
                working = result.State; receipts.Add(result.Receipt);
            }
            return new(working, receipts.ToArray(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private (GameState State, OrdinaryArmyOccupationReceipt Receipt) OccupyOrdinaryNeutralTerrain(
        GameState state, SourceWorldContext current, UnitState army, int physical)
    {
        var receiver = new ArmyUnitReference(physical);
        int force = OccupationKnown(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(receiver)), "Army_force");
        if (force is < 1 or > 45 || force != army.ForceId) throw OccupationMissing("ordinary_force_scope");
        var home = current.FindArmyUnitCurrentBase(receiver);
        var city = CurrentGateMapQueries.FindCity(home, current);
        int occupationCity = OccupationKnown(city.PhysicalSlot, "home_City");
        int raw = OccupationKnown(current.ReadArmyUnitRawHex2A(receiver), "Army_position");
        var center = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw OccupationMissing("center_Hex");
        int width = 2;
        var candidates = new List<HexReference>();
        if (center.PhysicalSlot is int centerId)
        {
            width = ReadOccupationWidth(state, current, army);
            candidates.Add(center);
            if (width < 2)
            {
                int direction = OccupationKnown(army.NativeDirection36, "Army_direction");
                var (left, right) = OccupationSides(centerId, direction);
                Append(left); Append(right);
                if (width == 1) { Append(OccupationSides(left, direction).Left); Append(OccupationSides(right, direction).Right); }
            }
            candidates = candidates.Where(hex =>
                OccupationKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force,
                    OccupationKnown(current.ReadHexForceSlot(hex), "candidate_force")), "candidate_relation") &&
                GateCaptureTerritorySource.Eligible(state, current, hex, force, hex.PhysicalSlot != centerId)).ToList();
        }
        foreach (var hex in candidates)
        {
            //257670(...,0,FF,0,0) has no state write when the objective is
            //inactive or complete. Its ignored selected-force result is immaterial.
            var objective = state.Forces.GetValueOrDefault(force)?.CombatObjective ?? throw OccupationMissing("Force_objective");
            if (objective.Enabled != false && !(objective.Enabled == true && objective.Progress is >= 60000))
                throw OccupationMissing("active_or_unknown_Hex_objective");
            if (OccupationKnown(current.ReadHexSurfaceCode(hex), "candidate_surface") is >= 15 and <= 18)
            {
                int areaId = OccupationKnown(current.ReadHexAreaSlot(hex), "candidate_Area");
                int original = OccupationKnown(current.ReadAreaValidityCode(new(areaId)), "candidate_original_City");
                var originalCity = current.ReadCityTableReference(original <= 51 ? original : 0)
                    ?? throw OccupationMissing("candidate_original_City_pointer");
                if (OccupationKnown(current.ReadBaseForceSlot(originalCity), "candidate_original_City_force") == force)
                    occupationCity = OccupationKnown(originalCity.PhysicalSlot, "candidate_original_City_identity");
            }
            //The complete fixed scenery placement map proves absence. Any
            //actual site remains unsupported instead of inventing flags47.
            if (_occupationSceneryHexes.Contains(hex.PhysicalSlot!.Value))
                throw OccupationMissing("scenery_callbacks");
            if (OccupationKnown(current.ReadHexForceSlot(hex), "former_force") != 0)
                throw OccupationMissing("former_Force_callbacks");
        }
        //Numeric campaign has no native scenery renderer; the special scenery
        //branch above was nevertheless read and qualified from actual resources.
        current.CurrentSceneryLayout = new(null);
        //Same ordinary application lifetime as CaptureOwnedArmyReturnWorld:
        //this client has no original tutorial-mode receiver or its counter.
        current.ArmyReturnTransferMode = new(false, null);
        current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        current.TransferService = new();
        var transferred = new List<int>(); var dirty = new List<int>();
        var centers = new List<int>();
        var working = state;
        foreach (var hex in candidates)
        {
            if (!GateCaptureTerritorySource.Eligible(state, current, hex, force, false) ||
                OccupationKnown(current.ReadHexForceSlot(hex), "wrapper_force") == force) continue;
            if (OccupationKnown(current.ReadHexSurfaceCode(hex), "wrapper_surface") is >= 15 and <= 18)
            {
                if (OwnedCurrentPlayerForce.ReadRawForce3A(state) != force)
                    throw OccupationMissing("nonplayer_center_log_advisor");
                working = PrepareRegionCenterTransfer(working, current, hex, force, occupationCity, enqueueEncirclement: true);
                centers.Add(hex.PhysicalSlot!.Value);
            }
            current.AssignHexForce(hex, force);
            var mode = current.ArmyReturnTransferMode ?? throw OccupationMissing("transfer_mode");
            if (mode.Present && OccupationKnown(mode.Value, "transfer_mode_value") != 0)
                throw OccupationMissing("transfer_mode_counter");
            transferred.Add(hex.PhysicalSlot!.Value);
            dirty.Add(OccupationKnown(current.ReadHexAreaSlot(hex), "dirty_area"));
        }
        var territory = new Dictionary<int, TerritoryState>(state.Territory);
        foreach (int hex in transferred)
        {
            if (!territory.TryGetValue(hex, out var before)) throw OccupationMissing("territory_owner");
            territory[hex] = before with { ForceId = force };
        }
        if (transferred.Count != 0) working = working with { Territory = territory };
        var regions = PublishOccupiedRegions(working, current, army, centers);
        working = regions.State;
        PersonGrowthReceipt? growth = null; PersonPendingMeritReceipt? merit = null;
        int commander = OccupationKnown(current.ResolveOfficerSource(army.OfficerId), "Commander_pointer");
        if (transferred.Count > 0 && OccupationKnown(current.ReadPersonNativeValidity(commander), "Commander_validity"))
        {
            int personForce = OccupationKnown(current.ReadPersonForce(commander)?.PhysicalSlot, "Commander_force");
            int personId = OccupationKnown(current.ReadPersonIdentity(commander), "Commander_identity");
            int first = OccupationKnown(current.ReadPersonFirstLocation(commander), "Commander_home");
            bool special = first is >= 1 and <= 61 && (personForce is >= 46 and <= 50 ||
                BattleRemakeCurrentRegionFactor.IsNativeBandPerson(personId));
            if (!special)
            {
                var position = PersonPositionRepair.DecodeSecondLocation(OccupationKnown(current.ReadPersonSecondLocation(commander), "Commander_position"));
                var personArmy = current.ReadArmyUnitTableReference(position.Kind == 27 && position.Index <= 500 ? position.Index : 0)
                    ?? throw OccupationMissing("Commander_Army");
                if (OccupationKnown(current.ReadArmyUnitCommanderId(personArmy), "Commander_Army_person") != personId ||
                    !OccupationKnown(current.ReadArmyUnitNativeValidity(personArmy), "Commander_Army_validity"))
                    throw OccupationMissing("nondeployed_Commander_reward");
                var grown = ApplyPersonGrowthDuringArmyOperation(working, army.OfficerId, 4,
                    unchecked(OccupationParameter(state, SimulationParameterKeys.ArmyMarch.OccupationGrowthScale) * transferred.Count), 0);
                if (grown.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved) throw OccupationMissing("Commander_growth");
                working = grown.State; growth = grown.Receipt;
                var rewarded = ApplyPendingMeritDuringArmyOperation(working, army.OfficerId,
                    unchecked(OccupationParameter(state, SimulationParameterKeys.ArmyMarch.OccupationMeritScale) * transferred.Count * 10));
                if (rewarded.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) throw OccupationMissing("Commander_merit");
                working = rewarded.State; merit = rewarded.Receipt;
            }
        }
        if (regions.Logs.Length != 0)
            working = working with { PendingCommandReports = [.. working.PendingCommandReports, .. regions.Logs] };
        return (working, new(army.Id, physical, width, candidates.Select(h => h.PhysicalSlot!.Value).ToArray(),
            transferred.ToArray(), dirty.ToArray(), growth, merit));

        void Append(int index)
        {
            var hex = current.ReadHexTableReference(index < 48400 ? index : 0) ?? throw OccupationMissing("side_Hex");
            if (hex.PhysicalSlot.HasValue) candidates.Add(hex);
        }
    }

    private int ReadOccupationWidth(GameState state, SourceWorldContext current, UnitState army)
    {
        int formation = OccupationKnown(Context(current).EffectiveFormation(state, army), "effective_formation");
        if (formation == 0) return 2;
        if (formation is < 0 or > 20) throw OccupationMissing("Formation_scope");
        int commander = OccupationKnown(current.ResolveOfficerSource(army.OfficerId), "width_Commander");
        if (!OccupationKnown(current.ReadPersonNativeValidity(commander), "width_Commander_validity")) return 2;
        int effect = OccupationKnown(TacticRemakeCurrentTraits.CommanderEffectSum(current, commander, 14), "width_trait");
        float fraction = (float)OccupationParameter(state, SimulationParameterKeys.ArmyMarch.OccupationTraitScale) * (float)effect / 100f;
        int low = OccupationKnown(TacticRemakeCurrentFormations.ReadParameter(state, formation, 0x80), "Formation_width_low") * 100;
        int high = OccupationKnown(TacticRemakeCurrentFormations.ReadParameter(state, formation, 0x81), "Formation_width_high") * 100;
        low = unchecked(low - (int)((float)low * fraction)); high = unchecked(high - (int)((float)high * fraction));
        if (high <= 0 && low <= 0) return 2;
        int physical = OccupationKnown(army.NativeArmyOrigin?.NativeSlotIndex, "width_Army_physical");
        int troops = OccupationKnown(current.ReadArmyReturnResourceFields(new(physical))?.Ready16, "width_ready_troops");
        return high > 0 && troops >= high ? 1 : low > 0 && troops >= low ? 0 : 2;
    }

    private static (int Left, int Right) OccupationSides(int hex, int direction)
    {
        (int left, int right) = direction switch { 1 => (5, 3), 2 => (4, 6), 3 => (1, 5),
            4 => (6, 2), 5 => (3, 1), 6 => (2, 4), _ => (0, 0) };
        if (left == 0) return (0, 0);
        //2A7B60 preserves its invalid-index sentinel; later pointer lookup uses
        //table0, not an omitted side. Keep list duplicates and original order.
        return (OccupationNeighbor(hex, left), OccupationNeighbor(hex, right));
    }
    private static int OccupationNeighbor(int hex, int direction)
    {
        int row = hex / 220, column = hex % 220, odd = column % 2;
        return direction switch
        {
            1 => column > 0 ? odd < 1 ? hex - 1 : row > 0 ? hex - 221 : 48400 : 48400,
            2 => row > 0 ? hex - 220 : 48400,
            3 => column > 218 ? 48400 : odd > 0 ? row < 1 ? 48400 : hex - 219 : hex + 1,
            4 => column > 0 ? odd < 1 && row < 219 ? hex + 219 : hex - 1 : 48400,
            5 => row < 219 ? hex + 220 : 48400,
            6 => column > 218 ? 48400 : odd < 1 && row < 219 ? hex + 221 : hex + 1,
            _ => hex
        };
    }
    private static int OccupationParameter(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } p && p.Int32Values.TryGetValue(key, out int value) &&
        !p.Float32Values.ContainsKey(key) ? value : throw OccupationMissing(key);
    private static T OccupationKnown<T>(T? value, string field) where T : struct => value ?? throw OccupationMissing(field);
    private static InvalidDataException OccupationMissing(string field) => new("current.Army_occupation." + field);
}
