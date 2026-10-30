namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    private Func<int, S14EventConditionPersonReference>? _storyActorResolver;

    internal void BindStoryActorResolver(Func<int, S14EventConditionPersonReference> resolve) =>
        _storyActorResolver = resolve;
}
