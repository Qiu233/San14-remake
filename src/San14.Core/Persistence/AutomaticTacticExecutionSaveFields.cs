using System.Text.Json.Nodes;

namespace San14.Core;

internal static class AutomaticTacticExecutionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 177 && state["NormalCombatImmediateContinuation"]?["AutomaticExecution"] is not null)
            throw new InvalidDataException("Legacy combat continuation contains a future automatic execution.");
    }

    internal static void Migrate(JsonObject immediate, int version)
    {
        const string key = "AutomaticExecution";
        if (version < 177) { immediate[key] = null; return; }
        if (!immediate.ContainsKey(key)) throw Invalid();
        if (immediate[key] is not JsonNode saved) return;
        if (saved is not JsonObject value ||
            !I(value, "CasterUnitId", 1) || !I(value, "TacticId", 0, 200) ||
            value["Instructions"] is not JsonArray instructions || instructions.Count != 2 ||
            !Instruction(instructions[0], 0) || !Instruction(instructions[1], 1) ||
            !I(value, "InstructionIndex", 0, 1) || !I(value, "TargetIndex", 0) ||
            !I(value, "TargetUnitId", 1) || !I(value, "TargetArmyPhysicalSlot", 0) ||
            !Receiver(value["TargetReceiverId"]) || !I(value, "TargetHexId", 0, 48400) ||
            !I(value, "RequestedLoss") || !I(value, "ChainWaitingContribution") ||
            value["ChainRandomState"] is not JsonValue random || !random.TryGetValue<ulong>(out _) ||
            !Array(value, "DamageContributions", Damage) || !Array(value, "Details", Detail) ||
            !Array(value, "FirstInstructionResults", Result) || !Array(value, "SecondInstructionResults", Result) ||
            !Array(value, "ArmyPresentationEffects", Effect) ||
            !Nullable(value, "TroopPrefix", Troop) || !Nullable(value, "MoralePrefix", Morale)) throw Invalid();
        int index = value["InstructionIndex"]!.GetValue<int>();
        var instruction = (JsonObject)instructions[index]!;
        int opcode = instruction["Opcode"]!.GetValue<int>();
        if (opcode is not (1 or 16) ||
            value["TargetIndex"]!.GetValue<int>() >= ((JsonArray)instruction["OrderedHexIds"]!).Count ||
            (opcode == 1) != (value["TroopPrefix"] is not null) ||
            (opcode == 16) != (value["MoralePrefix"] is not null)) throw Invalid();
    }

    internal static bool Instruction(JsonNode? node, int index) => node is JsonObject value &&
        I(value, "Index", index, index) && I(value, "Opcode", 0, byte.MaxValue) &&
        I(value, "Raw64", 0, byte.MaxValue) && I(value, "EffectValue", 0, byte.MaxValue) &&
        I(value, "Raw68", 0, byte.MaxValue) && value["OrderedHexIds"] is JsonArray hexes &&
        hexes.All(hex => Integer(hex, 0, 48400));

    internal static bool Damage(JsonNode? node) => node is JsonObject value &&
        (I(value, "Kind", 5, 6) || I(value, "Kind", 27, 27)) &&
        I(value, "PhysicalSlot", 0) && I(value, "RequestedLoss");

    private static bool Troop(JsonNode? node) => node is JsonObject value &&
        I(value, "SourceArmyPhysicalSlot", 0) && I(value, "SourceTroops", 0, ushort.MaxValue) &&
        I(value, "SourceForceSlot", 0, 51) && I(value, "StageProduct");

    private static bool Morale(JsonNode? node) => node is JsonObject value &&
        I(value, "SourceArmyPhysicalSlot", 0) && I(value, "SourceHexId", 0, 48400) &&
        Float(value, "StageFactor") && Float(value, "WaitingFactor");

    internal static bool Detail(JsonNode? node) => node is JsonObject value && Text(value, "Kind") &&
        I(value, "Period") && I(value, "ForceId") && I(value, "UnitId") && I(value, "OfficerId") &&
        I(value, "TacticId") && NI(value, "TargetUnitId") && NI(value, "ExpiresAtPeriod") &&
        Nullable(value, "Effect", EnumValue<PlayableTacticEffectKind>) && I(value, "Amount") &&
        Nullable(value, "Reason", node => node is JsonValue text && text.TryGetValue<string>(out _)) &&
        Nullable(value, "NativeUpdateOrdinal", node => node is JsonValue ordinal && ordinal.TryGetValue<long>(out _)) &&
        NI(value, "TargetBaseLocationId") && NI(value, "TargetHexId") && NI(value, "InstructionIndex");

    internal static bool Result(JsonNode? node) => node is JsonObject value &&
        EnumValue<TacticResultTargetKind>(value["TargetKind"]) && I(value, "TargetId", 1) &&
        I(value, "HexId", 0, 48400) && I(value, "Amount", 0, ushort.MaxValue);

    internal static bool Effect(JsonNode? node) => node is JsonObject value &&
        I(value, "Instruction", 0, 1) && Receiver(value["Target"]) &&
        EnumValue<PlayableTacticEffectKind>(value["Kind"]) && I(value, "Amount") &&
        Nullable(value, "Healing", node => node is JsonObject healing &&
            I(healing, "TroopsAfter", 0, ushort.MaxValue) && I(healing, "WoundedAfter", 0, ushort.MaxValue)) &&
        Nullable(value, "Abnormal", Abnormal) && NI(value, "MoraleAfter");

    private static bool Abnormal(JsonNode? node) => node is JsonObject value &&
        I(value, "Abnormal23", 0, byte.MaxValue) && I(value, "Abnormal24", 0, byte.MaxValue) &&
        I(value, "Abnormal25", 0, byte.MaxValue) &&
        Nullable(value, "ProvocationOwner", node => node is JsonObject owner && I(owner, "ForceId", 0, 51) &&
            EnumValue<DeploymentTargetKind>(owner["Kind"]) && I(owner, "Id")) &&
        Nullable(value, "OverrideTarget", Override);

    private static bool Override(JsonNode? node) => node is JsonObject value &&
        Nullable(value, "Kind", EnumValue<ArmyOverrideTargetKind>) &&
        Nullable(value, "Destination", node => node is JsonObject destination &&
            EnumValue<ArmyOverrideDestinationKind>(destination["Kind"]) && NI(destination, "EntityId") &&
            Nullable(destination, "ReceiverId", Receiver));

    private static bool Array(JsonObject value, string key, Func<JsonNode?, bool> valid) =>
        value[key] is JsonArray items && items.All(valid);
    private static bool Nullable(JsonObject value, string key, Func<JsonNode?, bool> valid) =>
        value.ContainsKey(key) && (value[key] is null || valid(value[key]));
    private static bool NI(JsonObject value, string key) => Nullable(value, key, node => Integer(node, int.MinValue, int.MaxValue));
    private static bool I(JsonObject value, string key, int min = int.MinValue, int max = int.MaxValue) => Integer(value[key], min, max);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue scalar &&
        scalar.TryGetValue<int>(out int number) && number >= min && number <= max;
    internal static bool Receiver(JsonNode? node) => node is JsonObject value && I(value, "Value", 1);
    private static bool Text(JsonObject value, string key) => value[key] is JsonValue scalar && scalar.TryGetValue<string>(out _);
    private static bool Float(JsonObject value, string key) => value[key] is JsonValue scalar &&
        scalar.TryGetValue<float>(out float number) && float.IsFinite(number);
    private static bool EnumValue<T>(JsonNode? node) where T : struct, Enum =>
        node is JsonValue scalar && scalar.TryGetValue<int>(out int number) && Enum.IsDefined(typeof(T), number);
    private static InvalidDataException Invalid() => new("Saved automatic tactic execution is incomplete or invalid.");
}
