using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Lazy<bool[]>? areaClaimRankEncirclement;
    /// <summary>Strategy/Playback entry recomputes encirclement claims. The
    /// caller supplies its accepted current view; occupation does not call this
    /// for each Hex. Commit the complete result only after all inputs resolve.</summary>
    internal GameState RefreshAreaOccupationClaimsCurrent(GameState state, SourceWorldContext current)
    {
        if (!ClaimNeed(BattleRemakeCurrentSettings.ReadEncirclementEnabled(state), "encirclement_setting")) return state;
        if (ClaimNeed(ReadApplicationTutorialActive(), "tutorial_service") &&
            ClaimNeed(TacticRemakeCurrentBattleControl.Read(ReadTutorialControlState(state), 102), "tutorial_bitmap102")) return state;

        var order = state.ForceProcessingOrder?.ForceIds ?? throw ClaimMissing("saved_Force_order");
        var rankAllows = ReadAreaClaimRankEncirclement(state);
        var registry = current.ReadRegisteredAreaReferences() ?? throw ClaimMissing("Area_registry");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw ClaimMissing("player_Force");
        int playerForce = ClaimNeed(current.ReadForcePhysicalSlot(player), "player_Force60");

        //42F7C0 clears all 501 table receivers, including the placeholder and
        //unregistered Areas. Work privately so a missing later input cannot
        //publish a partial clear into a borrowed current.
        var table = new AreaReference[501];
        for (int slot = 0; slot < table.Length; slot++)
        {
            table[slot] = current.ReadAreaTableReference(slot) ?? throw ClaimMissing("Area_table_pointer");
            _ = ClaimNeed(table[slot].PhysicalSlot, "Area_table_receiver");
        }
        var markers = new Dictionary<int, int>();
        var claims = new byte[501];
        var known = new bool[501];
        foreach (var area in table) known[ClaimNeed(area.PhysicalSlot, "Area_clear_receiver")] = true;

        //32C9D0 copies this order forwards. It differs from the actual
        //capture consumer, which walks the same saved order backwards.
        foreach (int force in order)
        {
            if (force is < 1 or > 51 || !state.Forces.TryGetValue(force, out var owner) ||
                owner.RankId is not int rank || rank is < 0 or > 255)
                throw ClaimMissing("Force_rank47");
            int selected = rank < 13 ? rank : 0;
            if (!rankAllows[selected]) continue;

            var prospective = new List<int>();
            var seen = new HashSet<int>();
            //223420/223580 filters the actual registered Area sequence by
            //current representative ownership, preserving its native order.
            foreach (var pointer in registry)
            {
                var area = pointer ?? throw ClaimMissing("registered_Area_pointer");
                int physical = ClaimNeed(area.PhysicalSlot, "registered_Area_receiver");
                if (ClaimNeed(current.ReadAreaCurrentForceSlot(area), "registered_Area_Force") != force) continue;
                foreach (int neighbor in RegionEncirclementSource.ReadNeighbors(state, current, physical))
                {
                    if (ClaimNeed(current.ReadAreaCurrentForceSlot(new(neighbor)), "neighbor_Area_Force") != force && seen.Add(neighbor))
                        prospective.Add(neighbor);
                }
            }
            foreach (int target in prospective)
            {
                int actualOwner = ClaimNeed(current.ReadAreaCurrentForceSlot(new(target)), "target_actual_Force");
                if (!ClaimNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, actualOwner), "target_relation")) continue;
                var assumed = (AreaId: target, ForceId: force);
                foreach (int candidate in RegionEncirclementSource.ExpandCandidates(state, current, target, force, assumed))
                {
                    var evaluation = RegionEncirclementSource.EvaluateCandidate(state, current, candidate, force, assumed);
                    if (!evaluation.Enclosed || evaluation.CapturedAreas.Length == 0) continue;
                    //Write the prospective border Area, not the captured
                    //component. An existing valid claim wins until player
                    //Force reaches the same Area later in the saved order.
                    byte prior = claims[target];
                    if (prior is < 1 or > 51 || force == playerForce)
                    {
                        claims[target] = (byte)Math.Clamp(force, 0, 51);
                        known[target] = true;
                    }
                    //1A48E0 shows label node18 and applies a sprite palette.
                    //This callback follows every visible successful component,
                    //even when an earlier claim is retained. Last visible
                    //callback wins; a hidden success does not erase it.
                    bool visible = force == playerForce || actualOwner == playerForce;
                    if (!visible)
                        foreach (int captured in evaluation.CapturedAreas)
                            if (ClaimNeed(current.ReadAreaCurrentForceSlot(new(captured)), "marker_captured_Force") == playerForce)
                            { visible = true; break; }
                    if (visible) markers[target] = force == playerForce ? 11 : 12;
                }
            }
        }
        Dictionary<int, AreaState>? areas = null;
        foreach (var (id, area) in state.Areas)
        {
            if (id is < 0 or > 500 || !known[id]) throw ClaimMissing("published_Area_receiver");
            if (area.OccupationClaimForceSlot == claims[id]) continue;
            areas ??= new(state.Areas);
            areas[id] = area with { OccupationClaimForceSlot = claims[id] };
        }
        //All required inputs and domain receivers have resolved. The actual
        //current setter only checks the physical501 boundary.
        for (int physical = 0; physical < known.Length; physical++)
            if (known[physical]) current.WriteAreaOccupationClaimForceSlot(new(physical), claims[physical]);
        var result = areas is null ? state : state with { Areas = areas };
        PublishAreaOccupationWarnings(result, markers);
        return result;
    }

    private bool[] ReadAreaClaimRankEncirclement(GameState state)
    {
        //Rank definitions are immutable for this adapter/content owner. Decode
        //their actual flag once rather than re-reading resource hex per Force.
        return (areaClaimRankEncirclement ??= new Lazy<bool[]>(() =>
        {
            var ranks = _scenario.Snapshot.RankParameters;
            if (ranks is not { FormatMarker: 0x66, RankRecordStride: 166, Ranks.Count: 13 } ||
                state.ContentHashes.GetValueOrDefault(_scenario.FixedDataSource.ResourceUri) != _scenario.FixedDataSource.Sha256)
                throw ClaimMissing("rank_definitions");
            var allowed = new bool[13];
            for (int index = 0; index < allowed.Length; index++)
            {
                var definition = ranks.Ranks[index];
                byte[] raw = Convert.FromHexString(definition.RawBytesHex);
                if (definition.Id != index || raw.Length != 166) throw ClaimMissing("rank_record");
                //CRankData slot3 rejects physical0 even when its raw flag is set.
                allowed[index] = index != 0 && (raw[162] & 1) != 0;
            }
            return allowed;
        })).Value;
    }

    private static T ClaimNeed<T>(T? value, string field) where T : struct => value ?? throw ClaimMissing(field);
    private static InvalidDataException ClaimMissing(string field) => new("current.Area_encirclement_claim." + field);
}
