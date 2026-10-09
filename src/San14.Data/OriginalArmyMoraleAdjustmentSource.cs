using System.Text.Json;
using San14.Core;

namespace San14.Data;

public enum OriginalArmyMoraleAdjustmentOutcome
{
    Committed, CurrentInputsUnresolved, ZeroMoraleAftermathUnresolved
}

public sealed record OriginalArmyMoraleAdjustmentReceipt(byte Before1A, int RequestedAdjustment,
    int? Trait13, int? TraitScale, int TraitCorrection, int EffectiveAdjustment,
    int GetterCap, int AdjustmentCap, int SetterCap, int RoutingTestValue, byte After1A,
    string Revision);

public sealed record OriginalArmyMoraleAdjustmentResult(OriginalArmyMoraleAdjustmentOutcome Outcome,
    GameState State, OriginalArmyMoraleAdjustmentReceipt? Receipt);

/// <summary>Actual2706A0 gameplay writer through its nonzero-result return.
/// A zero routing-test value reaches independent routing/event aftermath and
/// rejects the whole operation. The native byte store is not published first.
/// Source Hex/report flags are not reached on the supported return branch.</summary>
public sealed class OriginalArmyMoraleAdjustmentSource(
    OriginalTacticRemakeCurrentContext current,
    OriginalEventInitializationContext definitions, string revision)
{
    public OriginalArmyMoraleAdjustmentResult TryApply(GameState input, int armyId, int adjustment)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Native morale adjustment needs source revision.");
        if (!input.Units.TryGetValue(armyId, out var army) || current.ArmyValid(input, army) != true ||
            army.NativeMorale1A is not byte raw || input.NativeArmySimulation?.Parameters is not { } parameters ||
            definitions.StaticSource is not { } source ||
            input.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256)
            return Unknown();
        int? trait = null, scale = null;
        if (adjustment < 0)
        {
            if (input.Officers.GetValueOrDefault(army.OfficerId)?.NativeAbilityRuntime is not { } person ||
                person.Revision != OriginalTacticRemakeCurrentAbilities.ProfileId || person.Language != parameters.Language ||
                person.RuleVersion != parameters.RuleVersion || person.SourceScenarioUri != source.ResourceUri ||
                person.SourceScenarioSha256 != source.Sha256 || definitions.ReadPersonIdentity(person.PhysicalSlot) != army.OfficerId ||
                OriginalTacticRemakeCurrentTraits.ArmyContributions(definitions, person.PhysicalSlot,
                    army.NativeArmyConfiguration?.TraitSlots64) is not { Length: 127 } contributions)
                return Unknown();
            trait = contributions[13];
            if (trait != 0)
            {
                string key = SimulationParameterKeys.Morale.NegativeAdjustmentScale;
                if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int value))
                    return Unknown();
                scale = value;
            }
        }
        int? getter = current.ForceMoraleMaximum(input, army.ForceId);
        int? clamp = current.ForceMoraleMaximum(input, army.ForceId);
        int? setter = current.ForceMoraleMaximum(input, army.ForceId);
        if (getter is null || clamp is null || setter is null) return Unknown();
        var receipt = Calculate(raw, adjustment, trait, scale, getter.Value, clamp.Value, setter.Value,
            $"{revision}:{parameters.Revision}:{input.NativeClock!.Revision}:{source.Sha256}");
        if (receipt is null) return Unknown();
        if (receipt.RoutingTestValue == 0)
            return new(OriginalArmyMoraleAdjustmentOutcome.ZeroMoraleAftermathUnresolved, input, receipt);
        GameState detached = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(input))
            ?? throw new InvalidDataException("Native morale adjustment state failed to detach.");
        var units = new Dictionary<int, UnitState>(detached.Units) {
            [armyId] = detached.Units[armyId] with { NativeMorale1A = receipt.After1A }
        };
        GameState result = detached with { Units = units };
        NativeSubdayStateValidation.Validate(result);
        return new(OriginalArmyMoraleAdjustmentOutcome.Committed, result, receipt);

        OriginalArmyMoraleAdjustmentResult Unknown() => new(OriginalArmyMoraleAdjustmentOutcome.CurrentInputsUnresolved, input, null);
    }

    /// <summary>Source scalar i32 wrap→f32→CVTTSS2SI. A nonzero trait with
    /// a zero product still reaches the source minimum signed-one correction.
    /// Routing tests the pre-setter EBX i32, not the narrowed byte1A.</summary>
    public static OriginalArmyMoraleAdjustmentReceipt? Calculate(byte raw, int requested,
        int? trait13, int? traitScale, int getterCap, int adjustmentCap, int setterCap, string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Native morale arithmetic needs source revision.");
        int correction = 0;
        if (requested < 0)
        {
            if (trait13 is null) return null;
            if (trait13 != 0)
            {
                if (traitScale is null) return null;
                int absolute = requested < 0 ? unchecked(-requested) : requested;
                int product = unchecked(unchecked(absolute * trait13.Value) * traitScale.Value);
                float scaled = (float)product / 100f;
                correction = Truncate(scaled);
                if (correction == 0) correction = scaled < 0f ? -1 : 1;
            }
        }
        int effective = unchecked(requested + correction);
        int sum = unchecked(Math.Min((int)raw, getterCap) + effective);
        int routing = sum < 0 ? 0 : Math.Min(sum, adjustmentCap);
        byte after = OriginalArmyMoraleSource.SetByte2419A0(routing, setterCap);
        return new(raw, requested, trait13, traitScale, correction, effective,
            getterCap, adjustmentCap, setterCap, routing, after, revision);
    }

    private static int Truncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? int.MinValue : (int)value;
}
