using San14.Core;

namespace San14.Data;

public sealed partial class TutorialLessonSession
{
    /// <summary>The real UI stream precedes the next lesson instruction.
    /// Borrowed worker candidates continue to own their separate current stream.</summary>
    public void AdoptApplicationRandom(CampaignAppRandomState actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (borrowingPeriod) throw Missing("borrowed_application_random_owner");
        if (World.CampaignAppRandom32 != actual) World = World with { CampaignAppRandom32 = actual };
    }

    public void ObserveAdvantageEntered()
    {
        if (Completed) return;
        World = Rules.SetTutorialLessonEventCurrent(World, 42, true);
        World = Rules.SetTutorialLessonEventCurrent(World, 67, false);
    }

    public bool CanAcceptDiplomacyForceSelection(DiplomaticMissionKind kind, int forceId)
    {
        if (Progress.CourseIndex == 4 && kind == DiplomaticMissionKind.Alliance && Progress.ActiveParagraphIndex == 3)
            return forceId == TutorialFifthLessonPlan.AllianceForceId;
        if (Progress.CourseIndex == 5 && kind == DiplomaticMissionKind.Goodwill && Progress.ActiveParagraphIndex == 5)
            return forceId == TutorialSixthLessonPlan.RequiredGoodwillForceId;
        if (Progress.CourseIndex == 6 && kind == DiplomaticMissionKind.Alliance && Progress.ActiveParagraphIndex == 17)
            return forceId == TutorialSeventhLessonPlan.RequiredAllianceForceId;
        return true;
    }
    public bool CanAcceptDiplomacyOfficerSelection(DiplomaticMissionKind kind, IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(officerIds);
        int? required = Progress.CourseIndex switch
        {
            4 when kind == DiplomaticMissionKind.Alliance && Progress.ActiveParagraphIndex == 5 => 147,
            5 when kind == DiplomaticMissionKind.Goodwill && Progress.ActiveParagraphIndex == 7 => 952,
            6 when kind == DiplomaticMissionKind.Alliance && Progress.ActiveParagraphIndex == 19 => 952,
            _ => null
        };
        return required is not int physical || officerIds.Count == 1 &&
            officerIds[0] == Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, physical);
    }
    public bool CanAcceptForeignTradeCountrySelection(int countryId) =>
        Progress.CourseIndex != 5 || Progress.ActiveParagraphIndex != 16 || countryId == TutorialSixthLessonPlan.RequiredCountryId;
    public bool CanAcceptForeignTradePersonSelection(IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(officerIds);
        return Progress.CourseIndex != 5 || Progress.ActiveParagraphIndex != 18 || officerIds.Count == 1 &&
            officerIds[0] == Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, TutorialSixthLessonPlan.CaravanOfficerPhysicalSlot);
    }

    private TutorialLessonGuidance AdaptFifthGuidance(TutorialLessonGuidance guidance, int index, int phase) => guidance with
    {
        CityId = TutorialFifthLessonPlan.PlayerCityId,
        OfficerId = null,
        ConstructionSummaryIndex = index is >= 21 and <= 23 ? index - 21 : null,
        RequiredDiplomacyForceId = index == 3 ? TutorialFifthLessonPlan.AllianceForceId : null,
        RequiredDiplomacyOfficerId = index == 5 ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, 147) : null,
        AllowsSelectionReturn = index is not (3 or 5),
        AllowsPeriodAdvance = index == 10 && phase == 1 || index == 17 && phase == 1 ||
            index == 23 && phase == 8 || index == 27 && phase == 2,
        AllowsDeploymentSetupConfirmation = index == 20 && phase == 2 &&
            lastUiObservation?.FifthLesson?.ConstructionDeploymentReady == true,
        AllowsDeploymentTargetConfirmation = index == 23 && phase == 7,
        BlocksOrdinaryCommands = index is 0 or 19 or 25 or 26 or 30
    };
    private TutorialLessonGuidance AdaptSixthGuidance(TutorialLessonGuidance guidance, int index, int phase) => guidance with
    {
        CityId = index == 1 ? TutorialSixthLessonPlan.InformationCityId : TutorialSixthLessonPlan.SourceCityId,
        OfficerId = null,
        RequiredCityInformationActionId = index == 1 ? 2 : null,
        ResetCityInformationActionId = index == 1 && phase == 0 ? 5 : null,
        RequiredDiplomacyForceId = index == 5 ? TutorialSixthLessonPlan.RequiredGoodwillForceId : null,
        RequiredDiplomacyOfficerId = index == 7 ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, 952) : null,
        RequiredForeignTradeCountryId = index == 16 ? TutorialSixthLessonPlan.RequiredCountryId : null,
        RequiredForeignTradeOfficerId = index == 18 ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, 147) : null,
        AllowsSelectionReturn = index is not (5 or 7 or 16 or 18),
        AllowsPeriodAdvance = index == 10 && phase == 1 || index == 11 && phase == 0 ||
            index == 20 && phase == 3 || index is 21 or 22 && phase == 0,
        AllowsDeploymentSetupConfirmation = false,
        AllowsDeploymentTargetConfirmation = false,
        BlocksOrdinaryCommands = index is 0 or 2 or 12 or 13 or 23
    };
}
