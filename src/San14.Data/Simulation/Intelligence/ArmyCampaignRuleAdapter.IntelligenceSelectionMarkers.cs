using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private IReadOnlyDictionary<int, AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>>
        ReadIntelligenceSelectionSpyMarkersCurrent(GameState state, SourceWorldContext current)
    {
        var result = new Dictionary<int, AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>>();
        var own = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw IntelligenceMissing("selection.spy_marker.player");
        int player = IntelligenceNeed(own.PhysicalSlot, "selection.spy_marker.player_identity");
        string background = ArmyMapUiSources.Create().IconName(new(0x221, 0));
        for (int id = 0; id < 52; id++)
        {
            var target = current.ReadForceTableReference(id) ?? throw IntelligenceMissing("selection.spy_marker.force");
            if (!IntelligenceNeed(current.ReadForceNativeValidity(target), "selection.spy_marker.force_validity")) continue;
            int targetId = IntelligenceNeed(current.ReadForcePhysicalSlot(target), "selection.spy_marker.target_identity");
            var markers = new List<MapDiplomacyMarker>();
            if (player != 0 && player == targetId)
            {
                result[id] = AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>.Known(markers);
                continue;
            }
            string? missing = null;
            try
            {
                if (IntelligenceAssociationSlot(current, own, targetId, false) >= 0)
                    markers.Add(new(ReadHistoryDataText(1, 0x837), background));
                if (HasIntelligenceAwayMarkerCurrent(state, current, own, target))
                    markers.Add(new(ReadHistoryDataText(1, 0x838), background));
            }
            catch (InvalidDataException error) { missing = error.Message; }
            // Retain already known assignment labels when the second label's
            // exact command-admission owner is unavailable.
            result[id] = new(markers, missing);
        }
        return result;
    }
}
