using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Func<GameState, NativeArmyProgressStageMoment?, GameState>? _periodPresentation;
    private Func<GameState, NativeArmyProgressStageMoment, GameState>? _periodStageAdmission;

    /// <summary>Borrow the private candidate at completed native clock boundaries.
    /// The callback owns its stopped worker's candidate until it returns. Only
    /// actual ordinary-presentation manager/App changes return to the worker;
    /// it must not retain the candidate or issue simulation commands.</summary>
    public void ConfigurePeriodPresentation(Func<GameState, NativeArmyProgressStageMoment?, GameState>? presentation)
        => _periodPresentation = presentation;

    /// <summary>Admit the actual next stage on a source App frame. The stopped
    /// worker lends its current candidate only for a requested presentation,
    /// then resumes with its returned application state and random stream.</summary>
    public void ConfigurePeriodStageAdmission(Func<GameState, NativeArmyProgressStageMoment, GameState>? admission)
        => _periodStageAdmission = admission;
}
