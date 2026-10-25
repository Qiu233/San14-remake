using System.Text.Json.Nodes;
using A = San14.Core.AutomaticTacticExecutionSaveFields;

namespace San14.Core;

internal static class AutomaticTacticLinkedExecutionSaveFields
{
    internal static void Migrate(JsonObject automatic, int version)
    {
        const string key = "LinkedExecution";
        if (version < 181) { automatic[key] = null; return; }
        if (!automatic.ContainsKey(key)) throw Invalid();
        if (automatic[key] is not JsonNode node) return;
        if (node is not JsonObject linked || linked.Count != 3 ||
            !Integer(linked["CategoryPolicyReduction"], int.MinValue) ||
            linked["RemainingActors"] is not JsonArray remaining ||
            linked["CompletedActors"] is not JsonArray completed ||
            (long)remaining.Count + completed.Count > 10 || !remaining.All(Plan) || !completed.All(Result)) throw Invalid();
    }

    private static bool Plan(JsonNode? node) => node is JsonObject plan && plan.Count == 4 &&
        Integer(plan["CasterUnitId"], 1) && Integer(plan["TacticId"], 0, 200) &&
        Integer(plan["SourceArmyPhysicalSlot"], 1, 500) &&
        plan["Instructions"] is JsonArray { Count: 2 } instructions &&
        A.Instruction(instructions[0], 0) && A.Instruction(instructions[1], 1);

    private static bool Result(JsonNode? node) => node is JsonObject result && result.Count == 8 &&
        Integer(result["CasterUnitId"], 1) && A.Receiver(result["Actor"]) &&
        Integer(result["SourceArmyPhysicalSlot"], 1, 500) &&
        result["WasExecuted"] is JsonValue executed && executed.TryGetValue<bool>(out bool wasExecuted) &&
        Integer(result["TacticId"], 0, 200) && Array(result, "FirstInstructionResults", A.Result) &&
        Array(result, "SecondInstructionResults", A.Result) && Array(result, "ArmyPresentationEffects", A.Effect) &&
        (wasExecuted || ((JsonArray)result["FirstInstructionResults"]!).Count == 0 &&
            ((JsonArray)result["SecondInstructionResults"]!).Count == 0 &&
            ((JsonArray)result["ArmyPresentationEffects"]!).Count == 0);

    private static bool Array(JsonObject value, string key, Func<JsonNode?, bool> valid) =>
        value[key] is JsonArray items && items.All(valid);
    private static bool Integer(JsonNode? node, int minimum, int maximum = int.MaxValue) =>
        node is JsonValue value && value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
    private static InvalidDataException Invalid() => new("Saved automatic tactic linked actors are missing or invalid.");
}
