namespace San14.Core;

public sealed record PlayableLastCityPersonnelResolution(GameState State,
    int[] ResolvedOfficerIds, PeriodReportEntry[] Reports)
{
    public string? NativeUnregisterRuleId { get; init; }
    public int[] ResourceDisposedArmyIds { get; init; } = [];
}

/// <summary>The source personnel prefix of an ordinary last-City capture.
/// Successful people are resolved independently; unsupported native branches
/// retain their current owners for the remaining aftermath policy.</summary>
public interface IPlayableLastCityPersonnelRules
{
    PlayableLastCityPersonnelResolution Settle(GameState postCapture,
        CityState beforeCaptureCity, int defendingForceId, int capturingForceId);
}
