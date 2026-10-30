namespace San14.Data;

/// <summary>The eighteen independently saved paragraphs of the eighth course.</summary>
public static class TutorialEighthLessonPlan
{
    public const int DomesticCityPhysicalSlot = 9;
    public const int FirstAreaPhysicalSlot = 65;
    public const int SecondAreaPhysicalSlot = 62;
    public const int DomesticOfficerId = 185;
    public const int SearchCityPhysicalSlot = 10;
    public const int SearchOfficerId = 683;
    public const int SceneryPhysicalSlot = 23;
    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();
    public static TutorialParagraphPlan Read(int index,int phase)
    {
        TutorialEighthLessonRules.CheckPhase(index,phase);
        var stories = (index,phase) switch
        {
            (0,0)=>Events(237),(1,0)=>Events(238),(2,3)=>Events(239),
            (3,0)=>Events(240),(9,0)=>Events(241),(11,0)=>Events(242),
            (12,0)=>Events(243),(13,0)=>Events(244),(13,2)=>Events(245),
            (13,5)=>Events(246),(13,7)=>Events(247),(14,0)=>Events(248),
            (15,0)=>Events(249),(15,3)=>Events(250),(15,7)=>Events(251),
            (15,11)=>Events(252),(15,14)=>Events(253),(16,0)=>Events(254),_=>Empty
        };
        int? information = index switch { 2=>26,12=>14,14=>19,_=>null };
        return new(index,phase,stories,index == 3 ? 3 : null,information,index == 14 && phase == 7 || index == 15 && phase == 9)
        {
            CallbackPhase = stories.Count > 0 ? phase + 1 : phase,
            UsesInstructionCallback = stories.Count > 0 && index is 1 or 3 or 9 or 11 or 12 or 14 or 15,
            EnabledCommandMask = index == 3 && phase == 1 ? (1u<<3)|(1u<<2) :
                index == 0 && phase == 0 || index == 14 && phase == 7 || index == 15 && phase == 9 ? 0 : null,
            ResetLastCommandId = index == 3 && phase == 1 ? 13 : null,
            ResetLastInformationId = index == 2 && phase == 1 || index is 12 or 14 && phase == 1 ? 51 : null,
            BeforeStoryClearEventFlags = index == 3 && phase == 1 ? Events(60) : Empty,
            CompletionClearEventFlags = index switch { 5 or 8=>Events(11),10=>Events(66,99),_=>Empty },
            CompletionSetEventFlags = index switch { 2=>Events(99),3 or 6 or 9=>Events(66),_=>Empty }
        };
    }
    public static bool ShouldRunStories(int index,int phase,TutorialEighthLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (Read(index,phase).StoryEventIds.Count == 0) return false;
        return index != 15 || phase != 0 || observation.Common.ActionStackTop == 1;
    }
    public static bool IsSynchronousSetup(int index,int phase) => phase == 0 && index is 2 or 4 or 5 or 6 or 7 or 8 or 10 or 17;
    public static int? RequiredDoctrineLayoutSlot(int index,int phase) => index != 13 ? null : phase switch { 1=>6,3=>12,4=>27,5 or 6=>50,_=>null };
    public static bool AllowsDoctrineResearch(int index,int phase,int selectedLayoutSlot) => index != 13 ||
        phase is 1 or 3 or 4 && RequiredDoctrineLayoutSlot(index,phase) == selectedLayoutSlot;
    //Footer5 is the ordinary commit. Footer4/Cancel remains hidden; closing
    //a changed draft through discard must not satisfy the source stack wait.
    public static bool AllowsDoctrineConfirmation(int index,int phase) => index != 13 || phase == 6;
    public static bool AllowsDoctrineAuxiliary(int index,int phase) => index != 13;
    public static bool AllowsDoctrineReturn(int index,int phase) => index != 13;
    public static bool AllowsCreditReturn(int index,int phase,bool selectorActive) => index != 2;
    public static bool AllowsCreditConfirmation(int index,int phase,bool selectorActive,bool firstThreeSelected) =>
        index != 2 || (selectorActive ? phase == 2 && firstThreeSelected : phase >= 4);
    public static int? RequiredCreditSelectionCount(int index,int phase) => index == 2 && phase == 2 ? 3 : null;
    public static int? RequiredSceneryAction(int index,int phase) => index == 15 ? phase switch { 1 or 12=>2,5=>0,_=>null } : null;
    public static bool ResetsSceneryAction(int index,int phase) => index == 15 && phase is 1 or 5 or 12;
    public static bool AllowsSceneryInvestmentConfirmation(int index,int phase) => index != 15 || phase == 8;
    public static bool RetiresSceneryHexParent(int index,int phase) => index == 15 && phase is 5 or 9 or 16;
    private static IReadOnlyList<int> Events(params int[] ids) => Array.AsReadOnly(ids);
}
