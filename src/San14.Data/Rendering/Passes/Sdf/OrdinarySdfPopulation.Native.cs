namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static string LoadOrdinarySdfPopulationNativeSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("OrdinarySdfPopulation",locale);

        return $"{RenderRuleIdentity.For("OrdinarySdfPopulation",locale)};" +
            "Frame3D8-and-400;current-D69D70-ModelSdfD0/D65F30-SQT;operation0-four-side-ring;load-once";
    }
}
