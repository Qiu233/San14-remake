namespace San14.Data;

internal static class ArmyMaterialParameterRules
{
    internal static LoadedOriginalArmyMaterialParameterNativeSource Create(string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        return new(locale,ArmyGraphicsRules.RegularMaterialSpecularW,ArmyGraphicsRules.RenderTargetMaterialSpecularW);
    }
}
