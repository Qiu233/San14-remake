namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalArmySceneListPublicationSource LoadArmySceneListPublicationSource(
        string directory, string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("army-scene-list-publication",locale);

        return new(locale, $"{ruleVersion};" +
            "TC44C7B0/SC44C9B0->Game98.Data20->1C7270->ArmyV28:1C6FD0;" +
            "1C71F0->1C6EF0/directV8-or-1C2010/1C5040->D9F640;" +
            "then1C6E80/seven3CB8+3CF0;destinationE65D70AE;" +
            "current-group-state/batch-owners/aux580-required;not-ready-shadow-population");
    }
}
