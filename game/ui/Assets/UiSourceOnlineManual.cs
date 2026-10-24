/// <summary>Original external manual destinations with closed source identity.
/// Unknown locale mappings remain unsupported instead of guessing a URL.</summary>
internal static class UiSourceOnlineManual
{
    internal static string? ForLocale(string locale) => locale switch
    {
        "TC" => "https://www.gamecity.ne.jp/manual/sangokushi14-pk/ce/cht/",
        _ => null
    };
}
