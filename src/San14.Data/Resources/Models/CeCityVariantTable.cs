namespace San14.Data;

/// <summary>City-slot model variant rule. Geometry and textures are selected from the original resource catalog.</summary>
public sealed class CeCityVariantTable
{
    private enum ModelVariant { Variant0, Variant1, Variant2, Variant3, Variant4 }

    public string ImplementationVersion => RenderRuleIdentity.For("city-model-variants", Locale);
    public string Locale { get; }

    private CeCityVariantTable(string locale) => Locale = locale;

    public static CeCityVariantTable LoadCurrent(string locale)
    {
        _ = RenderRuleIdentity.For("city-model-variants", locale);
        return new(locale);
    }

    public int VariantForCitySlot(int slot)
    {
        if (slot is < 1 or > 51)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return (int)(slot switch
        {
            >= 1 and <= 7 or 18 or 20 or >= 22 and <= 24 or 41 => ModelVariant.Variant1,
            >= 25 and <= 31 or >= 36 and <= 40 or >= 43 and <= 46 => ModelVariant.Variant2,
            >= 47 and <= 49 => ModelVariant.Variant3,
            50 or 51 => ModelVariant.Variant4,
            _ => ModelVariant.Variant0
        });
    }
}
