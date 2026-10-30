using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The first lesson's genuinely fresh playable services. The
    /// title preparation loads and selects its scenario first; actual lesson
    /// entry supplies the real application controller before these producers.
    /// A loaded lesson never enters this initialization boundary.</summary>
    internal GameState InitializeSelectedTutorialBusinessCurrent(GameState state)
    {
        if (state.TutorialLessonProgress is not { CourseIndex: >= 0 and <= 7, CompletedParagraphMask: 0 } ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            ReadApplicationGameplay() is not { Mode: ApplicationGameplayMode.Tutorial,
                TutorialServicePresent: true, TutorialActive: true })
            throw new InvalidDataException("tutorial.first_lesson.fresh_business_entry");

        state = InstallFreshTalentRules(state);
        state = InstallFreshDiplomacyRules(state);
        state = TransportSetupCurrentInputs.InstallFresh(state);
        state = InitializeFreshDoctrineResearch(state);
        if (Seed.Language is "TC" or "SC")
        {
            //All eight title courses use the completed load's521980 callback.
            //4C6F20's fresh post-load path reaches335D00 before the game
            //entry44C180 reaches3035A0. Keep both actual settings and
            //current-user guards, including every skipped or consumed draw.
            state = InitializeFreshPersonnelHire(state);
            var merchants = InitializeFreshCityMerchants(state);
            state = merchants.Available ? merchants.Value! : throw new InvalidDataException(
                merchants.UnresolvedInput ?? "tutorial.first_lesson.fresh_merchants");
        }
        return state;
    }
}
