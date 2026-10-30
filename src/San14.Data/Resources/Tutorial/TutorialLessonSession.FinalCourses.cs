using San14.Core;

namespace San14.Data;

public sealed partial class TutorialLessonSession
{
    private TutorialLessonGuidance AdaptSeventhGuidance(TutorialLessonGuidance guidance, int index, int phase) => guidance with
    {
        CityId = 7,
        OfficerId = null,
        RequiredIntelligenceForceId = index is 2 or 9 ? TutorialSeventhLessonPlan.RequiredSpyForceId : null,
        RequiredIntelligenceOfficerId = index == 4
            ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, TutorialSeventhLessonPlan.HomeSpyOfficerPhysicalSlot)
            : index == 11 ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, TutorialSeventhLessonPlan.AwaySpyOfficerPhysicalSlot) : null,
        RequiredDiplomacyForceId = index == 17 ? TutorialSeventhLessonPlan.RequiredAllianceForceId : null,
        RequiredDiplomacyOfficerId = index == 19
            ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, TutorialSeventhLessonPlan.AllianceOfficerPhysicalSlot) : null,
        RequiredStrategicTargetId = index == 25 ? Rules.QuerySeventhTutorialPlacateAreaId(World) : null,
        RequiredStrategicOfficerId = index == 27
            ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, TutorialSeventhLessonPlan.PlacateOfficerPhysicalSlot) : null,
        AllowsSelectionReturn = index is not (2 or 4 or 9 or 11 or 17 or 19 or 25 or 27),
        AllowsPeriodAdvance = index is 13 or 21 && phase is 1 or 3 || index is 29 or 32 or 33 && phase == 1,
        AllowsDeploymentSetupConfirmation = index == 32,
        AllowsDeploymentTargetConfirmation = index == 32,
        BlocksOrdinaryCommands = index is 0 or 31 or 35
    };

    private TutorialLessonGuidance AdaptEighthGuidance(TutorialLessonGuidance guidance, int index, int phase) => guidance with
    {
        CityId = index == 14 ? TutorialEighthLessonPlan.SearchCityPhysicalSlot : TutorialEighthLessonPlan.DomesticCityPhysicalSlot,
        AreaId = index is 4 or 5 or 6 ? TutorialEighthLessonPlan.FirstAreaPhysicalSlot :
            index is 7 or 8 or 9 ? TutorialEighthLessonPlan.SecondAreaPhysicalSlot : null,
        OfficerId = index is 5 or 8 ? TutorialEighthLessonPlan.DomesticOfficerId :
            index == 14 ? TutorialEighthLessonPlan.SearchOfficerId : null,
        RequiredDevelopmentFocus = null,
        RequiredSceneryActionId = TutorialEighthLessonPlan.RequiredSceneryAction(index, phase),
        ResetSceneryActionId = TutorialEighthLessonPlan.ResetsSceneryAction(index, phase) ? 3 : null,
        AllowsSelectionReturn = index != 14 || phase is < 2 or > 6,
        AllowsPeriodAdvance = index == 14 && phase == 7 || index == 15 && phase == 9,
        AllowsDeploymentSetupConfirmation = false,
        AllowsDeploymentTargetConfirmation = false,
        BlocksOrdinaryCommands = index is 0 or 1 or 11 or 16
    };
}
