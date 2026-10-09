namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static string ReadDirectionalSkySourceSelectedRecipe(string sourceDirectory,string locale)
    {
        return $"{RenderRuleIdentity.For("directional-sky-selected-atmosphere",locale)};D3AE2D-request48=sourceL160;"+
            "D3AEC8-copyL96->M96/D3AF58-Main1EC->M84;980850-renderer-match/97EAF7-Generic-mode-profile;"+
            "source97C1F0-mode7-finalFog/976909->97CCE0-pendingScat/97690E-clear121;not-binding-commit-or-GPU";
    }
}
