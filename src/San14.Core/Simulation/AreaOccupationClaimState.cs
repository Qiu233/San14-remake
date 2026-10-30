namespace San14.Core;

public sealed partial record AreaState
{
    //Current strategy/playback encirclement claim. It is independent of the
    //representative Hex owner; missing legacy knowledge is not a clear claim.
    public byte? OccupationClaimForceSlot { get; init; }
}
