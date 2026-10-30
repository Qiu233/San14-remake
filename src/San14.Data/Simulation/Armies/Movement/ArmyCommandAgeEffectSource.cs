using San14.Core;

namespace San14.Data;

/// <summary>The ordinary commander-age contribution. The caller supplies the
/// distinct commander-only or cached/activation aggregation requested by its
/// stat query. Dynamic deployment conditions are never reused as age effects.</summary>
internal static class ArmyCommandAgeEffectSource
{
    private static readonly ushort[] ResetCache = new ushort[36];
    internal static int ReadCurrent(GameState state, SourceWorldContext current, UnitState army,
        IReadOnlyList<int>? effects = null)
    {
        if (effects is { Count: 127 } && effects[31] == 0 && effects[97] == 0) return 0;
        int slot = Need(army.NativeArmyOrigin?.NativeSlotIndex, "receiver");
        var receiver = new ArmyUnitReference(slot);
        if (!Need(current.ReadArmyUnitNativeValidity(receiver), "validity")) return 0;
        if (Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, slot), "excluded")) return 0;
        ushort identity = Need(current.ReadArmyUnitCommanderId(receiver), "commander_identity");
        int person = (current.ReadPersonLookupReference(identity <= 6000 ? identity : 0)
            ?? throw Missing("person_pointer")).PhysicalSlot ?? throw Missing("person_receiver");
        if (!Need(current.ReadPersonNativeValidity(person), "person_validity")) return 0;
        effects ??= TacticRemakeCurrentTraits.ArmyContributions(current, person, ArmyTraitSourceAdapter.Encode(state, army))
            ?? throw Missing("current_traits");
        return ReadCommander(state, current, person, effects);
    }

    // Route search requests commander-only unconditional effects. Its actual
    // receiver still retains the independent pending/interaction exclusion.
    internal static int ReadReceiver(GameState state, SourceWorldContext current, int slot,
        int person, IReadOnlyList<int> effects)
    {
        if (effects.Count == 127 && effects[31] == 0 && effects[97] == 0) return 0;
        if (!Need(current.ReadArmyUnitNativeValidity(new(slot)), "validity")) return 0;
        if (Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, slot), "excluded")) return 0;
        return ReadCommander(state, current, person, effects);
    }

    internal static int ReadResetPreview(GameState state, SourceWorldContext current, int person,
        bool commanderOnly, IReadOnlyList<int> unconditionalEffects)
    {
        // A reset unregistered preview is not in either exclusion registry.
        // Its 36 cache slots are all personality zero. The age query reads
        // the saved Person activation mask, not the preview self-condition list.
        IReadOnlyList<int> effects = commanderOnly ? unconditionalEffects :
            TacticRemakeCurrentTraits.ArmyContributions(current, person, ResetCache)
                ?? throw Missing("reset_preview_activation_traits");
        return ReadCommander(state, current, person, effects);
    }

    private static int ReadCommander(GameState state, SourceWorldContext current,
        int person, IReadOnlyList<int> effects)
    {
        if (effects.Count != 127) throw Missing("trait_shape");
        // Both contributions zero prove the final result zero without needing
        // otherwise irrelevant age-policy inputs from a legacy world.
        int effect = effects[31] != 0 ? effects[31] : effects[97];
        if (effect == 0) return 0;
        if (!Need(current.ReadPersonNativeValidity(person), "person_validity")) return 0;
        int age = PersonCurrentAgeSource.Read(state, current, person);
        int threshold = P(SimulationParameterKeys.BattleAttributes.ReservedAgeThreshold);
        if (age < threshold) return 0;
        string tier = age < unchecked(threshold + 5) ? SimulationParameterKeys.BattleAttributes.ReservedAgeTier1
            : age < unchecked(threshold + 10) ? SimulationParameterKeys.BattleAttributes.ReservedAgeTier2
            : age < unchecked(threshold + 20) ? SimulationParameterKeys.BattleAttributes.ReservedAgeTier3
            : age < unchecked(threshold + 30) ? SimulationParameterKeys.BattleAttributes.ReservedAgeTier4
            : SimulationParameterKeys.BattleAttributes.ReservedAgeTier5;
        return unchecked(effect + P(tier));

        int P(string key) => state.NativeArmySimulation is { Parameters: var parameters } && parameters.Int32Values.TryGetValue(key, out int value)
            ? value : throw Missing(key);
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("army.command-age." + field);
}
