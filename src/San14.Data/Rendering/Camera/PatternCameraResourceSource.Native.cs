namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedPatternCameraResourceSource LoadPatternCameraResourceSource(string directory, string locale)
    {
        const uint defaultOfflinePatternObjectKey=0x80F9DB7B;
        string identity=RenderRuleIdentity.For("PatternCameraResource",locale);
        return new(locale,identity+
            ";actual-registry-handle-Data/retain/temp-release;preserves-three-resources;Skylight-context3E4.low2=1;"+
            "raw2D30-to-K1680/raw2F18-to-K1888/raw2F20-to-K1890;Main-raw-bank-separate-from-selected-embedded",
            defaultOfflinePatternObjectKey);
    }
}
