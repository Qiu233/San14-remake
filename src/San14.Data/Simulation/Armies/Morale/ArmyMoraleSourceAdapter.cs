using System.Text.Json.Nodes;
using San14.Core;

namespace San14.Data;

/// <summary>The original byte writer is a resource-rule capability. Wider
/// game morale remains valid domain state and is unavailable to that writer.</summary>
public static class ArmyMoraleSourceAdapter
{
    public static byte? Read(UnitState unit) => unit.MoraleState.CurrentValue is >= 0 and <= byte.MaxValue
        ? (byte)unit.MoraleState.CurrentValue.Value : null;

    internal static UnitState WithCurrent(UnitState unit, byte? value) => unit with {
        MoraleState = unit.MoraleState with { CurrentValue = value } };

    internal static void Migrate(JsonObject state, JsonObject unit)
    {
        if (unit.ContainsKey("MoraleState") || unit["Morale"] is not JsonValue provisional ||
            !provisional.TryGetValue<int>(out int provisionalValue) || !unit.ContainsKey("NativeMorale1A"))
            throw new InvalidDataException("Historical army morale ownership is invalid.");
        int? current = null;
        if (unit["NativeMorale1A"] is { } node)
        {
            if (node is not JsonValue value || !value.TryGetValue<byte>(out byte encoded) ||
                state["NativeClock"] is null || unit["NativeArmyOrigin"] is null || unit["FieldAttackPolicy"] is null)
                throw new InvalidDataException("Historical army morale source value or construction is invalid.");
            current = encoded;
        }
        unit["MoraleState"] = new JsonObject { ["ProvisionalValue"] = provisionalValue, ["CurrentValue"] = current };
        unit.Remove("Morale"); unit.Remove("NativeMorale1A");
    }
}
