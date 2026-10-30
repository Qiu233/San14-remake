namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    //Both original writers242/243 test unsigned(5-A1)<13. The already saved
    //condition bank owns all thirteen slots; out-of-range stores do nothing
    //without changing a successfully resolved handler's return value.
    internal void StoreStoryTargetForce(int target, S14EventConditionForceReference? force)
        => StoreTargetForce(target, force);

    //244's monotone invocation blocked latch shares the same owner that the
    //existing execution cursor captures and restores across actual slices.
    internal void BlockStoryExecution() => _blocked = true;
}
