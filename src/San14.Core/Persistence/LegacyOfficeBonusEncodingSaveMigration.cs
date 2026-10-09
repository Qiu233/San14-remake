using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Translates the historical getter identity once at the save boundary.</summary>
internal static class LegacyOfficeBonusEncodingSaveMigration
{
    private const string LegacyGetterKey = "ce-army-morale-wave-current:post-vtable:slot9";
    private const int LegacyRotateByteGetter = 0x00D9D0;

    public static void Apply(JsonObject state)
    {
        if (state["NativeArmySimulation"]?["Parameters"] is not JsonObject parameters) return;
        JsonObject? values = parameters["Int32Values"] as JsonObject;
        // Unsupported old integers are not enum values. In particular, legacy
        // getter 1 must remain unavailable even though the new codec has value 1.
        parameters["OfficeBonusEncoding"] = values?[LegacyGetterKey]?.GetValue<int>() == LegacyRotateByteGetter
            ? JsonValue.Create((int)OfficeBonusEncoding.RotateByteXorOffice) : null;
        values?.Remove(LegacyGetterKey);
    }
}
