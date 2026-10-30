using San14.Core;

namespace San14.Data;

/// <summary>Independent fresh TC/SC numeric catalogs and the ordinary scenario Item protection
/// category. Current consumers read only the existing saved parameter bank.</summary>
public static class DuelNumericParameters
{
    private const string Prefix = "duel-numeric:";
    private static readonly (string Name, int Value)[] FreshTc =
    [
        ("equal-strength-actor-percent", 70), ("instant-minimum-strength", 50), ("instant-probability-cap", 64),
        ("strength-divisor", 4), ("difference-divisor", 3), ("critical-cap", 15),
        ("damage-minimum", 5), ("damage-maximum", 50), ("following-counter-difference", 5),
        ("critical-counter-difference", 5), ("following-evade-cap", 10), ("instant-death-multiplier", 2),
        ("death-frequency0", 1), ("death-frequency1", 2), ("death-frequency2", 4),
        ("death-maximum-ability", 150), ("death-divisor", 15), ("effect31-age-base", 60), ("effect30-scale", 1),
        //The independent young-effect scalar has a proved TC image-loader
        //zero initial value. Install only here; old saved banks are not filled.
        ("young-effect31", 0),
        ("effect31-age-base-bonus", 3), ("effect31-age-plus5", 4), ("effect31-age-plus10", 6),
        ("effect31-age-plus20", 8), ("effect31-age-plus30", 10),
        ("special-age-person:0", 231), ("special-age-person:1", 247)
    ];

    //SC numeric consumers373810/3734B0/375D50/37AD10/376040 retain
    //their independently paired scalar bank. The two TC legacy age keys have
    //no consumer; the shared person-age owner remains separate.
    private const int ScYoungEffect31LoaderDefault = 0;
    private static readonly (string Name, int Value)[] FreshSc =
    [
        ("equal-strength-actor-percent", 70),
        ("instant-minimum-strength", 50),
        ("instant-probability-cap", 64),
        ("strength-divisor", 4),
        ("difference-divisor", 3),
        ("critical-cap", 15),
        ("damage-minimum", 5),
        ("damage-maximum", 50),
        ("following-counter-difference", 5),
        ("critical-counter-difference", 5),
        ("following-evade-cap", 10),
        ("instant-death-multiplier", 2),
        ("death-frequency0", 1),
        ("death-frequency1", 2),
        ("death-frequency2", 4),
        ("death-maximum-ability", 150),
        ("death-divisor", 15),
        ("effect31-age-base", 60),
        ("effect30-scale", 1),
        ("young-effect31", ScYoungEffect31LoaderDefault),
        ("effect31-age-base-bonus", 3),
        ("effect31-age-plus5", 4),
        ("effect31-age-plus10", 6),
        ("effect31-age-plus20", 8),
        ("effect31-age-plus30", 10),
    ];

    public static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation is not { } simulation || source.Metadata.FormatMarker != 0x66 ||
            source.Snapshot.Locale != simulation.Parameters.Language || simulation.Parameters.Language is not ("TC" or "SC") ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            source.Snapshot.ScenarioItems is not { Items.Count: 202 } catalog ||
            simulation.Parameters.Int32Values.Keys.Concat(simulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw Missing("fresh_owner");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        var fresh = simulation.Parameters.Language == "SC" ? FreshSc : FreshTc;
        foreach (var row in fresh) values.Add(Prefix + row.Name, row.Value);
        foreach (var item in catalog.Items)
        {
            if (item.Slot is < 0 or >= 202 || item.RecordStride != 65 || item.RawBytes is not { Length: 65 } ||
                item.EmbeddedDefinition?.Slot != item.Slot) throw Missing("fresh_Item_resource_identity");
            //238790 mode1 stream: owner4A,location4C,status50,flags4F,
            //name10[14],description1E[38],44..49[6],51. Byte44 is row58.
            //It is independent of ItemDefinition.RawKind (byte45, row59).
            values.Add(ItemProtectionCategoryKey(item.Slot), item.RawBytes[58]);
        }
        return state with { NativeArmySimulation = simulation with
        { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static string ItemProtectionCategoryKey(int physical) => $"{Prefix}item:{physical}:duel-protection-category";
    internal static byte ReadItemProtectionCategory(GameState state, int physical)
    {
        if ((uint)physical >= 202) throw Missing("Item_receiver");
        int value = ReadKey(state, ItemProtectionCategoryKey(physical));
        return value is >= 0 and <= 255 ? (byte)value : throw Missing("Item_protection_category_width");
    }
    internal static int Read(GameState state, string name) => ReadKey(state, Prefix + name);
    private static int ReadKey(GameState state, string key)
    {
        if (state.NativeArmySimulation?.Parameters is not { Language: "TC" or "SC" } bank ||
            bank.Float32Values.ContainsKey(key)) throw Missing("parameter_" + key);
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        //Only this SC standalone loader-zero scalar has complete audited
        //no-writer/address-escape ownership. Ordinary file-backed defaults
        //are installed once at fresh creation, never supplied to old saves.
        if (key == Prefix + "young-effect31" && bank.Language == "SC" &&
            bank.RuleVersion == SimulationRuleIdentity.ForLocale("SC")) return ScYoungEffect31LoaderDefault;
        throw Missing("parameter_" + key);
    }
    private static InvalidDataException Missing(string field) => new("current.DuelOpportunity_numeric_" + field);
}
