using San14.Core;

namespace San14.Data;

public sealed record DuelPresentationPerson(int OfficerId, string? Name, OfficerPortraitRoute? Portrait,
    DetailForceAppearance? Flag, int? Strength, IReadOnlyList<MapSummaryTraitRow> Traits,
    IReadOnlyList<string> UnresolvedFields);
public sealed record DuelPresentationPage(CampaignRegisteredDuel Request, bool Display,
    DuelPresentationPerson? Left, DuelPresentationPerson? Right, string ResultText,
    IReadOnlyList<string> InjuryTexts, GameState PreparedState);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Uses the once-prepared duel, never recomputing its winner or rounds.
    /// Native37B8E0 places the player's side on the left.</summary>
    public AreaAdministrationQuery<DuelPresentationPage> QueryDuelPresentation(GameState state,
        CampaignRegisteredDuel request)
    {
        try
        {
            if (!ReferenceEquals(state.CampaignDuelService?.CompletedPresentations.FirstOrDefault(), request))
                throw new InvalidDataException("duel.presentation.queue_head");
            var aftermath = request.AftermathPresentation ?? throw new InvalidDataException("duel.presentation.aftermath_operands");
            SourceWorldContext? current = null;
            var prepared = state;
            var injuryTexts = new List<string>();
            foreach (var notice in aftermath.Injuries)
                if (notice.PlayerDistrictNotice)
                    injuryTexts.Add(Text(0x5F81, new(Person0: notice.OfficerId,
                        String0: ReadHistoryDataText(0, 0x123 + notice.AfterHealthStateId))));
            // Legacy requests may retain display=1; scenes are always skipped.
            return AreaAdministrationQuery<DuelPresentationPage>.Known(new(request, false, null, null, "", injuryTexts, prepared));

            string Text(int root, HistoryMessageOperands operands)
            {
                var formatted = FormatReportMessage(prepared, current ??= CapturePersonPresentationCurrent(prepared), root, operands, ReportMessageOrigin.DuelPresentation, null);
                //Keep real message choices in the same uncommitted owner as
                //the presentation. Abandoning a view publishes no draws.
                prepared = formatted.State;
                return formatted.Text;
            }


        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DuelPresentationPage>.Unknown(error.Message); }
    }

    /// <summary>Transfers the real pending packets to the shared speech owner.
    /// Admission, its CRT draws and queue replacement happen once with the
    /// enclosing exact-owner completion; this does not claim balloon playback.</summary>
    public AreaAdministrationQuery<GameState> PrepareDuelPresentationCompletion(GameState state,
        CampaignRegisteredDuel request)
    {
        try
        {
            if (!ReferenceEquals(state.CampaignDuelService?.CompletedPresentations.FirstOrDefault(), request))
                throw new InvalidDataException("duel.presentation.completion_queue_head");
            var aftermath = request.AftermathPresentation ?? throw new InvalidDataException("duel.presentation.aftermath_operands");
            var next = state;
            foreach (var injury in aftermath.Injuries)
                if (injury.ArmyId is int army)
                    next = QueueArmyMapSpeechCurrent(next, null, army, injury.OfficerId, 13,
                        injury.ArmyHexId ?? throw new InvalidDataException("duel.presentation.injury_Army_hex"));
            foreach (var notice in aftermath.ArmyNotices)
                next = QueueArmyMapSpeechCurrent(next, null, notice.ArmyId, notice.CommanderId,
                    notice.Outcome == CampaignDuelOutcome.Won ? 1 : notice.Outcome == CampaignDuelOutcome.Draw ? 2
                        : throw new InvalidDataException("duel.presentation.Army_notice_outcome"), notice.HexId,
                    duelOpponent: notice.OpponentId, duelAftermath: notice.Aftermath);
            return AreaAdministrationQuery<GameState>.Known(next);
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<GameState>.Unknown(error.Message); }
    }

    private (int Root, HistoryMessageOperands Operands)? ReadDuelArmySpeechCurrent(
        SourceWorldContext current, ArmyMapSpeechEntry entry)
    {
        int opponent = entry.ContextDuelOpponentOfficerId ?? throw new InvalidDataException("duel.speech.opponent_operand");
        int physical = current.ReadPersonLookupReference(opponent <= 6000 ? opponent : 0)?.PhysicalSlot
            ?? throw new InvalidDataException("duel.speech.opponent_pointer");
        if (!(current.ReadPersonNativeValidity(physical) ?? throw new InvalidDataException("duel.speech.opponent_validity"))) return null;
        int root = entry.ActionCode == 2 ? 0xC948 : entry.ContextDuelAftermath switch
        {
            CampaignDuelAftermath.None => 0xC935,
            CampaignDuelAftermath.Death => 0xC927,
            CampaignDuelAftermath.Capture => ((current.ReadArmyReturnPersonFlags196(physical)
                ?? throw new InvalidDataException("duel.speech.opponent_flags")) & 0x4000) == 0 ? 0xC92A : 0xC935,
            _ => throw new InvalidDataException("duel.speech.aftermath_operand")
        };
        return (root, new(Person0: entry.OfficerId, Person1: opponent));
    }
    private static bool DuelPresentationTraitCategory(byte effect) => effect is 29 or 30 or 31 or 47 or 53 or 54 or 57 or 82;
}
