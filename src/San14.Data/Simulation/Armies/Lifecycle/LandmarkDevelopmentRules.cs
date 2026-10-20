using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current landmark development. Starting constants are extracted
/// offline; level increments come from the installed experience resource.</summary>
internal sealed class LandmarkDevelopmentRules
{
    private readonly byte[][] _increments;
    private readonly SourceProvenance _source;
    internal LandmarkDevelopmentRules(LoadedExperienceData source)
    {
        if (source.Source.SourcePath.Replace('\\', '/') != "0000/experience_data_exce.bin" ||
            source.Document.Tables.Count != 4 || source.Document.Tables[2] is not { RecordStride: 15 } table ||
            table.Records.Count != 7) throw new InvalidDataException("Landmark development requires the CE investment table.");
        _increments = table.Records.Select(row => new[] { row.RawBytes[7], row.RawBytes[11], row.RawBytes[14] }).ToArray();
        _source = source.Source;
    }

    internal GameState InstallFreshParameters(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("simulation");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        //TC1A495C4/D0/DC/E8/F4 and independently matched SC1A4A5F4/600/60C/618/624.
        foreach (var (key, value) in new (string, int)[] {
            (SimulationParameterKeys.Landmark.InitialRandomRange, 3), (SimulationParameterKeys.Landmark.TypeQuota, 3),
            (SimulationParameterKeys.Landmark.SecurityInitialEffect, 3), (SimulationParameterKeys.Landmark.DurabilityInitialEffect, 2),
            (SimulationParameterKeys.Landmark.MoraleInitialEffect, 2),
            (SimulationParameterKeys.Landmark.CompletionSecurity, 5), (SimulationParameterKeys.Landmark.CompletionDurability, 100),
            (SimulationParameterKeys.Landmark.CompletionMorale, 5), (SimulationParameterKeys.Landmark.CompletionPopularity, 15) })
            if (simulation.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value)) throw Missing("fresh_parameters");
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    internal LandmarkDevelopmentState Initialize(GameState state, int id, ushort hex,
        IEnumerable<LandmarkDevelopmentState> enabled, TacticSourceSamplerWorld sampler)
    {
        var counts = new int[3];
        foreach (var entry in enabled) if (entry.EffectKind is >= 1 and <= 3) counts[entry.EffectKind - 1]++;
        int minimum = counts.Min(), quota = Parameter(state, SimulationParameterKeys.Landmark.TypeQuota);
        var kinds = new List<int>();
        for (int i = 0; i < 3; i++)
            for (int n = unchecked(quota - counts[i] + minimum); n > 0; n--) kinds.Add(i + 1);
        int salt = unchecked(id + sampler.Day450 + hex);
        Shuffle(kinds, salt, sampler);
        if (kinds.Count == 0) throw Missing("empty_type_candidates");
        byte kind = unchecked((byte)kinds[0]);
        var values = new List<int>();
        int range = Parameter(state, SimulationParameterKeys.Landmark.InitialRandomRange);
        for (int i = 0; i < range; i++) values.Add(i);
        Shuffle(values, salt, sampler);
        if (values.Count == 0) throw Missing("empty_effect_candidates");
        string key = kind switch { 1 => SimulationParameterKeys.Landmark.SecurityInitialEffect,
            2 => SimulationParameterKeys.Landmark.DurabilityInitialEffect, 3 => SimulationParameterKeys.Landmark.MoraleInitialEffect,
            _ => throw Missing("effect_kind") };
        int effect = unchecked((byte)((byte)Parameter(state, key) + (byte)values[0]));
        effect = Adjust(state, effect, 17);
        return new(1, kind, unchecked((byte)effect));
    }

    internal LandmarkDevelopmentState Advance(GameState state, LandmarkDevelopmentState previous)
    {
        if (state.ContentHashes.GetValueOrDefault(_source.ResourceUri) != _source.Sha256) throw Missing("experience_resource_identity");
        byte level = (byte)Math.Min(previous.Level + 1, 6);
        int mode = BattleRemakeCurrentSettings.ReadInt32(state, 0x114) ?? throw Missing("effect_growth_mode");
        int increment = _increments[level][mode == 0 ? 1 : mode == 2 ? 2 : 0];
        increment = Adjust(state, increment, 18);
        return previous with { Level = level, Effect = unchecked((byte)Math.Max(unchecked(previous.Effect + increment), 0)) };
    }

    private static int Adjust(GameState state, int value, int index) =>
        (BattleRemakeCurrentSettings.ReadAdvancedSettings(state) ?? throw Missing("advanced_settings"))
            ? unchecked(value * (BattleRemakeCurrentSettings.ReadAdvancedParameter(state, index) ?? throw Missing("advanced_coefficient"))) / 100 : value;
    private static int Parameter(GameState state, string key) => state.NativeArmySimulation?.Parameters is { } bank &&
        !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value) ? value : throw Missing(key);
    //1D70E0 shuffles list payloads, not list links. It uses the source's pure
    //sampler twice per round and does not consume GameState.RandomState.
    private static void Shuffle(List<int> values, int salt, TacticSourceSamplerWorld sampler)
    {
        int count = values.Count;
        for (int n = (count / 2) * count; n > 0; n--)
        {
            int a = TacticEffectSourceSamplerMath.Sample(count, 1, salt, 0, 0, 0, sampler);
            int b = count - 1 - TacticEffectSourceSamplerMath.Sample(count, 1, unchecked(salt + 1), 0, 0, 0, sampler);
            salt = unchecked(salt + 2);
            (values[a], values[b]) = (values[b], values[a]);
        }
    }
    private static InvalidDataException Missing(string field) => new("current.landmark_development." + field);
}
