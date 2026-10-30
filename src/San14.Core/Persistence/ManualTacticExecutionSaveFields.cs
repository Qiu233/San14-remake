using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ManualTacticExecutionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && state["ManualTacticExecutionContinuation"] is not null)
            throw new InvalidDataException("Legacy world contains a future manual tactic execution.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "ManualTacticExecutionContinuation";
        if (version < 181) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode node) return;
        if (node is not JsonObject manual || manual.Count != 7 || manual["Request"] is not JsonObject request ||
            request["Actor"] is not JsonObject actor || !Integer(actor["Value"], 1) ||
            !Integer(request["TacticId"], 1, 200) || request["Target"] is not JsonObject target ||
            !Integer(target["Kind"], (int)ManualTacticTargetKind.Hex, (int)ManualTacticTargetKind.Hex) ||
            !Integer(manual["SelectedHexId"], 0, 48399) || !JsonNode.DeepEquals(target["Id"], manual["SelectedHexId"]) ||
            !Nullable(manual, "PrimaryUnitId", 1, int.MaxValue) || !Nullable(manual, "PrimaryBaseLocationId", 1, 61) ||
            manual["PrimaryUnitId"] is not null && manual["PrimaryBaseLocationId"] is not null ||
            !Integer(manual["SampleDay"], int.MinValue) || !Integer(manual["SampleSalt"], int.MinValue) ||
            manual["Execution"] is not JsonObject execution ||
            !(Integer(state["ArmyDefeatContinuation"]?["SettlementIndex"], -1, -1) ||
              Integer(state["BaseCaptureContinuation"]?["SettlementIndex"], -1, -1)) ||
            state["NormalCombatImmediateContinuation"] is not null || state["DuelAftermathContinuation"] is not null ||
            state["NativeNormalTurnContinuation"] is not null ||
            state["NativePeriodContinuation"]?["Cursor"] is JsonNode cursor && !Integer(cursor["StageIndex"], 6, 6)) throw Invalid();
        AutomaticTacticExecutionSaveFields.ValidateSaved(execution, version);
    }

    private static bool Nullable(JsonObject owner, string key, int minimum, int maximum) =>
        owner.ContainsKey(key) && (owner[key] is null || Integer(owner[key], minimum, maximum));
    private static bool Integer(JsonNode? node, int minimum, int maximum = int.MaxValue) =>
        node is JsonValue value && value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
    private static InvalidDataException Invalid() => new("Saved manual tactic execution is missing or inconsistent with its defeat decision.");
}
