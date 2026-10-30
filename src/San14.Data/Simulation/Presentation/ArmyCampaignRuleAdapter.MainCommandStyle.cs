using San14.Core;

namespace San14.Data;

/// <summary>Current normal main-command layout. Zero and two select the basic six-category layout; three selects seven.
/// Restricted application selection separately affects category admission.
/// A null style retains an explicit missing input rather than guessing a rank.</summary>
public sealed record MapMainCommandStyle(int? Style, string? UnresolvedInput = null);

public sealed partial class ArmyCampaignRuleAdapter
{
    //882B70: Force native validity, application user-selection gate, then
    //the fixed rank flags. Reuses the map header's operation-local capture.
    private MapMainCommandStyle ReadMainCommandStyleCurrent(GameState state, SourceWorldContext current)
    {
        try
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("player_force_pointer");
            if (!(current.ReadForceNativeValidity(player) ?? throw Missing("force_validity")))
                return new(0);
            bool restricted = ReadApplicationInformationRestriction(state)
                ?? throw Missing("application_user_selection");
            if (restricted) return new(0);
            int physical = current.ReadForcePhysicalSlot(player) ?? throw Missing("force_identity");
            //The mature binder selects current ForceState.RankId, with the
            //saved placeholder field for receivers outside the domain roster.
            _moraleWaveInputs.BindCurrent(state, current);
            int rawRank = current.ReadForceRankSlot47(physical) ?? throw Missing("current_force_rank");
            int rank = rawRank < 13 ? rawRank : 0;
            var ranks = _scenario.Snapshot.RankParameters?.Ranks ?? throw Missing("rank_definitions");
            if ((uint)rank >= (uint)ranks.Count || ranks[rank].Id != rank)
                throw Missing("rank_definition_identity");
            byte flags = ranks[rank].MainCommandFlags ?? throw Missing("rank_main_command_flags");
            return new(((flags & 8) | 16) >> 3);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }

        static InvalidDataException Missing(string field) => new("map.main_command." + field);
    }
}
