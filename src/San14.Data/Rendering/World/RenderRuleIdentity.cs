namespace San14.Data;

/// <summary>Version of the rendering rules implemented by this project.
/// It identifies our implementation, not an original executable or its bytes.</summary>
public static class RenderRuleIdentity
{
    public const string Version = "san14-remake.render-rules/v1";

    public static string For(string rule, string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new NotSupportedException("The rendering rules currently support SC and TC layouts.");
        return $"{Version}/{rule}/{locale}";
    }
}
