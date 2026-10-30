using San14.Core;

namespace San14.Data;

/// <summary>The actual proposal-entry predicate and its source Game help ID.
/// This is independent of offer generation, affordability and pending work.</summary>
public sealed record ProposalAdmission(bool Allowed, int GameMessageId);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>420850's read-only entrance query. A policy rejection needs no
    /// world capture. Otherwise the actual board and handlers share one current
    /// capture; the query neither formats rows nor changes offers or RNG.</summary>
    public AreaAdministrationQuery<ProposalAdmission> QueryProposalAdmission(GameState state, int forceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (ReadProposalEntryPolicy(state) is { } blocked)
                return AreaAdministrationQuery<ProposalAdmission>.Known(blocked);
            return AreaAdministrationQuery<ProposalAdmission>.Known(
                ReadProposalAdmissionCurrent(CreateProposalLifecycle(state, forceId)));
        }
        catch (InvalidDataException missing)
        { return AreaAdministrationQuery<ProposalAdmission>.Unknown(missing.Message); }
    }

    // 420850 checks ordinary Top first, then3EB6D0. Advanced uses its actual
    // int32 member17C; ordinary uses byte88. Only raw1 rejects entry. Missing
    // saved settings stay unknown instead of adopting a new-game default.
    internal ProposalAdmission? ReadProposalEntryPolicy(GameState state)
    {
        int selector = ProposalAdmissionNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        if (selector != 0 && ProposalAdmissionNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "difficulty") == 4)
            return new(false, 0x4F1B);
        int enabled = selector == 0
            ? ProposalAdmissionNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0x17C), "advanced_setting")
            : ProposalAdmissionNeed(BattleRemakeCurrentSettings.ReadByte(state, 0x88), "ordinary_setting");
        return enabled == 1 ? new(false, 0x4F17) : null;
    }

    // 420C30 scans actual nonempty entries, proposer affiliation/busy flags
    // and the status-specific handler validity. Existing IsOfferVisible owns
    // those same rules. Affordability belongs to the later selection/adoption
    // check: lacking gold or books must not disable the whole proposal page.
    internal ProposalAdmission ReadProposalAdmissionCurrent(ProposalLifecycleSource source)
    {
        if (source.PlayerForce.PhysicalSlot != source.ForceId)
            throw ProposalLifecycleSource.Missing("player_force");
        var board = source.State.Proposals ?? throw ProposalLifecycleSource.Missing("board_owner");
        bool any = board.Slots.Skip(1).Any(entry => entry.Kind != ProposalKind.None && source.IsOfferVisible(entry));
        return new(any, any ? 0x4F1A : 0x4F19);
    }

    private static T ProposalAdmissionNeed<T>(T? value, string field) where T : struct =>
        value ?? throw ProposalLifecycleSource.Missing("admission_" + field);
}
