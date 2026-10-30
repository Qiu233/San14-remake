using System.Globalization;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Checks policy snapshots at the resource/save boundary only. No
/// current-world roster or behavior-tree execution is needed to load them.</summary>
internal static class OriginalAiPolicyExecutionSaveFields
{
    private const string Owner = "OriginalAiPolicyExecution";
    private const string Contexts = "OriginalAiContexts";
    private const string Prototype = "OriginalAiContextPrototype";

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 215)
            RejectNewBlackboardValues(state, version);
        if (version >= 212) return;
        if (state.ContainsKey(Owner) || Groups(state) is { } groups &&
            (groups.ContainsKey(Contexts) || groups.ContainsKey(Prototype))) throw Invalid();
        if (Groups(state)?["Objectives"] is JsonObject objectives)
            foreach (var pair in objectives)
                if (pair.Value?["Purpose"] is JsonObject purpose &&
                    (Number(purpose["Kind"], out int kind) && kind == 13 ||
                     Number(purpose["Target"]?["Kind"], out int target) && target == 3)) throw Invalid();
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 212)
        {
            state[Owner] = null;
            if (Groups(state) is { } groups)
            {
                groups[Contexts] = null;
                groups[Prototype] = null;
            }
        }
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        if (!state.ContainsKey(Owner)) throw Invalid();
        if (state[Owner] is JsonNode saved)
        {
            var owner = Object(saved, "Source", "Actors", "Evaluations", "RootBundles");
            var source = Object(owner["Source"], "BehaviorResourceUri", "BehaviorSha256", "ScriptResourceUri", "ScriptSha256");
            if (!Text(source["BehaviorResourceUri"]) || !Text(source["ScriptResourceUri"]) ||
                !Hash(source["BehaviorSha256"]) || !Hash(source["ScriptSha256"])) throw Invalid();
            if (owner["RootBundles"] is JsonNode rootsNode)
            {
                var roots = Object(rootsNode, "Force", "District", "MilitaryGroup", "Army");
                if (!Unsigned(roots["Force"]) || !Unsigned(roots["District"]) ||
                    !Unsigned(roots["MilitaryGroup"]) || !Unsigned(roots["Army"])) throw Invalid();
            }
            var actors = Array(owner["Actors"]);
            var identities = new HashSet<(int, int)>();
            foreach (var item in actors)
            {
                var actor = Actor(item);
                if (actor.Kind == 2 || !identities.Add(actor)) throw Invalid();
            }
            var receivers = new HashSet<OriginalAiBlackboardValue>();
            foreach (var item in Array(owner["Evaluations"]))
            {
                var evaluation = Object(item, "Receiver", "Value");
                var receiver = Value(evaluation["Receiver"]);
                if (receiver is not (OriginalAiEntityValue or OriginalAiHexValue or OriginalAiHireOfferValue or
                    OriginalAiFormationValue or OriginalAiTacticValue or OriginalAiFacilityDefinitionValue or OriginalAiGemDefinitionValue) || !receivers.Add(receiver) ||
                    !Number(evaluation["Value"], out _)) throw Invalid();
            }
        }
        if (Groups(state) is not { } groups) return;
        if (!groups.ContainsKey(Contexts) || !groups.ContainsKey(Prototype)) throw Invalid();
        if (groups[Prototype] is JsonNode prototypeNode)
        {
            var prototype = Object(prototypeNode, "BundleKey", "InitialBlackboard");
            if (!Unsigned(prototype["BundleKey"])) throw Invalid();
            Blackboard(prototype["InitialBlackboard"]);
        }
        if (groups[Contexts] is not JsonNode contextNode) return;
        if (contextNode is not JsonObject contexts) throw Invalid();
        foreach (var pair in contexts)
        {
            if (!int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                id is < 1 or > 500 || id.ToString(CultureInfo.InvariantCulture) != pair.Key ||
                Actor(pair.Value) != (2, id)) throw Invalid();
        }
    }

    private static (int Kind, int Id) Actor(JsonNode? node)
    {
        var row = Object(node, "Actor", "BundleKey", "Controls", "Blackboard");
        var actor = Object(row["Actor"], "Kind", "Id");
        if (!Number(actor["Kind"], out int kind) || !Number(actor["Id"], out int id) ||
            kind is < 1 or > 4 || id <= 0 || kind == 2 && id > 500 || kind == 4 && id > 51 ||
            !Unsigned(row["BundleKey"])) throw Invalid();
        var controls = new HashSet<(uint, int)>();
        foreach (var item in Array(row["Controls"]))
        {
            var control = Object(item, "BundleKey", "NodeIndex", "ChildIndex");
            if (control["BundleKey"] is not JsonValue value || !value.TryGetValue<uint>(out uint bundle) ||
                !Number(control["NodeIndex"], out int index) || index < 0 ||
                !Number(control["ChildIndex"], out int child) || child < 0 || !controls.Add((bundle, index))) throw Invalid();
        }
        Blackboard(row["Blackboard"]);
        return (kind, id);
    }

    private static void Blackboard(JsonNode? node)
    {
        var row = Object(node, "Bindings", "Lists");
        var lists = new HashSet<int>();
        foreach (var item in Array(row["Lists"]))
        {
            var list = Object(item, "ListId", "Kind", "Values");
            if (!Number(list["ListId"], out int id) || id < 0 || !lists.Add(id) ||
                !Number(list["Kind"], out int kind) || !Enum.IsDefined((OriginalAiBlackboardListKind)kind)) throw Invalid();
            foreach (var entry in Array(list["Values"]))
                if (!ListValue((OriginalAiBlackboardListKind)kind, Value(entry))) throw Invalid();
        }
        var keys = new HashSet<uint>();
        foreach (var item in Array(row["Bindings"]))
        {
            var binding = Object(item, "Key", "Value");
            if (binding["Key"] is not JsonValue value || !value.TryGetValue<uint>(out uint key) || !keys.Add(key)) throw Invalid();
            int? alias = Value(binding["Value"]) switch
            {
                OriginalAiListValue local => local.ListId,
                OriginalAiSharedListValue shared => shared.ListId,
                _ => null
            };
            if (alias is int listId && !lists.Contains(listId)) throw Invalid();
        }
    }

    private static OriginalAiBlackboardValue Value(JsonNode? node)
    {
        if (node is not JsonObject row || row["kind"] is not JsonValue discriminator ||
            !discriminator.TryGetValue<string>(out string? kind)) throw Invalid();
        if (kind == "null-object") { Object(row, "kind"); return new OriginalAiNullObjectValue(); }
        if (kind == "entity")
        {
            Object(row, "kind", "EntityKind", "Id");
            if (!Number(row["EntityKind"], out int type) || !Enum.IsDefined((WorldEntityKind)type) ||
                !Number(row["Id"], out int id) || id < 0) throw Invalid();
            return new OriginalAiEntityValue((WorldEntityKind)type, id);
        }
        string field = kind == "integer" ? "Value" : kind is "list" or "shared-list" ? "ListId" : "Id";
        Object(row, "kind", field);
        if (!Number(row[field], out int number) || kind != "integer" && number < 0) throw Invalid();
        return kind switch
        {
            "integer" => new OriginalAiIntegerValue(number), "list" => new OriginalAiListValue(number),
            "shared-list" => new OriginalAiSharedListValue(number),
            "region" when number < 18 => new OriginalAiRegionValue(number),
            "facility-definition" when number <= 30 => new OriginalAiFacilityDefinitionValue(number),
            "gem-definition" when number <= 30 => new OriginalAiGemDefinitionValue(number),
            "hex" => new OriginalAiHexValue(number), "military-group" when number <= 500 => new OriginalAiMilitaryGroupValue(number),
            "trade-country" => new OriginalAiTradeCountryValue(number), "hire-offer" => new OriginalAiHireOfferValue(number),
            "formation" => new OriginalAiFormationValue(number), "tactic" => new OriginalAiTacticValue(number),
            _ => throw Invalid()
        };
    }

    private static bool ListValue(OriginalAiBlackboardListKind kind, OriginalAiBlackboardValue value) => kind switch
    {
        OriginalAiBlackboardListKind.HireOffers => value is OriginalAiHireOfferValue,
        OriginalAiBlackboardListKind.Hexes => value is OriginalAiHexValue,
        OriginalAiBlackboardListKind.MilitaryGroups => value is OriginalAiMilitaryGroupValue,
        OriginalAiBlackboardListKind.Formations => value is OriginalAiFormationValue,
        OriginalAiBlackboardListKind.Tactics => value is OriginalAiTacticValue,
        OriginalAiBlackboardListKind.FacilityDefinitions => value is OriginalAiFacilityDefinitionValue,
        OriginalAiBlackboardListKind.GemDefinitions => value is OriginalAiGemDefinitionValue,
        _ => value is OriginalAiEntityValue entity && entity.EntityKind == (kind switch
        {
            OriginalAiBlackboardListKind.Officers => WorldEntityKind.Officer,
            OriginalAiBlackboardListKind.Areas => WorldEntityKind.Area,
            OriginalAiBlackboardListKind.Footholds => entity.EntityKind is WorldEntityKind.City or WorldEntityKind.Gate
                ? entity.EntityKind : (WorldEntityKind)(-1),
            OriginalAiBlackboardListKind.Forces => WorldEntityKind.Force,
            OriginalAiBlackboardListKind.Armies => WorldEntityKind.Army,
            OriginalAiBlackboardListKind.Districts => WorldEntityKind.District,
            OriginalAiBlackboardListKind.CautionRelations => WorldEntityKind.ArmyCaution,
            OriginalAiBlackboardListKind.CautionGroups => WorldEntityKind.ArmyCautionGroup,
            OriginalAiBlackboardListKind.MapObjects => WorldEntityKind.MapObject,
            OriginalAiBlackboardListKind.Landmarks => WorldEntityKind.Landmark,
            _ => (WorldEntityKind)(-1)
        })
    };

    // Version gates inspect only this owner's persisted blackboards. They do
    // not execute policies or inspect the gameplay world on a query path.
    private static void RejectNewBlackboardValues(JsonObject state, int version)
    {
        static void Check(JsonNode? node, int version)
        {
            if (node is not JsonObject snapshot) return;
            if (snapshot["Bindings"] is JsonArray bindings)
                foreach (var binding in bindings)
                    if (binding?["Value"]?["kind"]?.GetValue<string>() is string tag &&
                        (tag is "facility-definition" or "gem-definition" ||
                         version < 214 && tag is ("shared-list" or "region"))) throw Invalid();
            if (snapshot["Lists"] is JsonArray lists)
                foreach (var list in lists)
                    if (Number(list?["Kind"], out int kind) &&
                        (kind >= (int)OriginalAiBlackboardListKind.FacilityDefinitions ||
                         version < 214 && kind >= (int)OriginalAiBlackboardListKind.MapObjects))
                        throw Invalid();
        }
        if (state[Owner]?["Actors"] is JsonArray actors)
            foreach (var actor in actors) Check(actor?["Blackboard"], version);
        if (state[Owner]?["Evaluations"] is JsonArray evaluations)
            foreach (var evaluation in evaluations)
                if (evaluation?["Receiver"]?["kind"]?.GetValue<string>() is "facility-definition" or "gem-definition")
                    throw Invalid();
        if (Groups(state) is JsonObject groups)
        {
            Check(groups[Prototype]?["InitialBlackboard"], version);
            if (groups[Contexts] is JsonObject contexts)
                foreach (var row in contexts) Check(row.Value?["Blackboard"], version);
        }
    }

    private static JsonObject? Groups(JsonObject state) => state["ArmyLifecycle"]?["ArmyGroups"] as JsonObject;
    private static JsonObject Object(JsonNode? node, params string[] keys) => node is JsonObject row &&
        row.Count == keys.Length && keys.All(row.ContainsKey) ? row : throw Invalid();
    private static JsonArray Array(JsonNode? node) => node as JsonArray ?? throw Invalid();
    private static bool Number(JsonNode? node, out int result)
    { result = 0; return node is JsonValue value && value.TryGetValue<int>(out result); }
    private static bool Unsigned(JsonNode? node) => node is JsonValue value && value.TryGetValue<uint>(out _);
    private static bool Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out string? text) &&
        !string.IsNullOrWhiteSpace(text) && !text.Contains('\0');
    private static bool Hash(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out string? text) &&
        text is { Length: 64 } && text.All(Uri.IsHexDigit);
    private static InvalidDataException Invalid() => new("Saved original-policy execution state is missing or invalid.");
}
