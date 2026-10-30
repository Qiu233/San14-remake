namespace San14.Data;

public sealed partial class SourceWorldContext
{
    // Unknown remains null when no current Story owner has been bound.
    public Func<RegistryObjectReference, int?>? ConditionItemIllustrationReader { get; set; }
    public int? ReadConditionItemIllustrationIndex(RegistryObjectReference item) =>
        ConditionItemIllustrationReader?.Invoke(item);
}
