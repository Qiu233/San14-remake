using System.Text.Json.Nodes;
using A = San14.Core.AutomaticTacticExecutionSaveFields;

namespace San14.Core;

internal static class BaseAutomaticTacticExecutionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && state["NormalCombatImmediateContinuation"]?["BaseAutomaticExecution"]?["TargetBase"] is not null)
            throw new InvalidDataException("Legacy Base automatic tactic contains a future Base target.");
        if (version < 180 && state["NormalCombatImmediateContinuation"]?["BaseAutomaticExecution"] is not null)
            throw new InvalidDataException("Legacy combat continuation contains a future Base automatic execution.");
    }

    internal static void Migrate(JsonObject immediate, int version)
    {
        const string key = "BaseAutomaticExecution";
        if (version < 180) { immediate[key] = null; return; }
        if (!immediate.ContainsKey(key)) throw Invalid();
        if (immediate[key] is not JsonNode saved) return;
        if (immediate["AutomaticExecution"] is not null || saved is not JsonObject value ||
            !I(value, "CasterBaseLocationId", 1, 61) || !I(value, "GovernorOfficerId", 0, 6000) ||
            !I(value, "SourceForceId", 0, 51) || !I(value, "SourceHexId", 0, 48400) || !I(value, "TacticId", 0, 200) ||
            value["Instructions"] is not JsonArray instructions || instructions.Count != 2 ||
            !A.Instruction(instructions[0], 0) || !A.Instruction(instructions[1], 1) ||
            !I(value, "InstructionIndex", 0, 1) || !I(value, "TargetIndex", 0) ||
            !A.Target(value, version) || !I(value, "TargetHexId", 0, 48400) || !I(value, "RequestedLoss") ||
            !Array(value, "DamageContributions", A.Damage) || !Array(value, "Details", A.Detail) ||
            !Array(value, "FirstInstructionResults", A.Result) || !Array(value, "SecondInstructionResults", A.Result) ||
            !Array(value, "ArmyPresentationEffects", A.Effect) || !value.ContainsKey("TroopPrefix") ||
            value["TroopPrefix"] is JsonNode prefix && !Troop(prefix)) throw Invalid();
        var instruction = (JsonObject)instructions[value["InstructionIndex"]!.GetValue<int>()]!;
        int opcode = instruction["Opcode"]!.GetValue<int>();
        if (opcode is not (1 or 16) || value["TargetBase"] is not null && opcode != 1 ||
            (opcode == 1) != (value["TroopPrefix"] is not null) ||
            value["TargetIndex"]!.GetValue<int>() >= ((JsonArray)instruction["OrderedHexIds"]!).Count) throw Invalid();
    }

    private static bool Troop(JsonNode node) => node is JsonObject value && I(value, "SourceKind", 5, 6) &&
        I(value, "SourceId", 0) && I(value, "SourceTroops") && I(value, "Object14") &&
        I(value, "SourceForce", 0, 51) && I(value, "StageProduct");
    private static bool Array(JsonObject value, string key, Func<JsonNode?, bool> valid) => value[key] is JsonArray items && items.All(valid);
    private static bool I(JsonObject value, string key, int min = int.MinValue, int max = int.MaxValue) =>
        value[key] is JsonValue scalar && scalar.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved Base automatic tactic execution is incomplete or invalid.");
}
