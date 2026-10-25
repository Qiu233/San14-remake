using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CoalitionCameraPresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 176 && state["CoalitionCameraPresentation"] is not null)
            throw new InvalidDataException("Legacy save contains future admitted coalition camera state.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "CoalitionCameraPresentation";
        if (version < 176) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw new InvalidDataException("Saved coalition camera knowledge is missing.");
        if (state[key] is not JsonNode saved) return;
        if (saved is not JsonObject value || value["TargetHexId"] is not JsonValue target ||
            !target.TryGetValue<int>(out int hex) || hex is < 0 or > 48400 ||
            value["SavedCamera"] is not JsonObject camera ||
            new[] { "FocusX", "FocusY", "FocusZ", "FocusW", "Distance", "Yaw", "Pitch" }.Any(key =>
                camera[key] is not JsonValue scalar || !scalar.TryGetValue<double>(out double number) || !double.IsFinite(number)))
            throw new InvalidDataException("Saved coalition camera is incomplete or invalid.");
    }
}
