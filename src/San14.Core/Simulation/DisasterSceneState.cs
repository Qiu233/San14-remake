namespace San14.Core;

/// <summary>The disaster background actually admitted by the presentation
/// host. Pending scene requests alone never set this state.</summary>
public sealed record DisasterSceneState(int EventKind);

public sealed partial record GameState
{
    public DisasterSceneState? ActiveDisasterScene { get; init; }
}
