using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyArmyMarchSaveMigration
{
    public static IReadOnlyList<LegacyArmyPositionKnowledge> Apply(JsonObject state)
    {
        var residues = new List<LegacyArmyPositionKnowledge>();
        if (state["Units"] is JsonObject units)
            foreach (var node in units.Select(item => item.Value)) if (node is JsonObject unit) Migrate(unit);
        if (state["NativeArmyLifecycle"]?["Slots"] is JsonArray slots)
            foreach (var slot in slots) if (slot?["PendingImage"] is JsonObject unit) Migrate(unit);
        return residues;

        void Migrate(JsonObject unit)
        {
            if (unit.ContainsKey("March") || unit.ContainsKey("PositionKnowledge"))
                throw new InvalidDataException("Historical army contains conflicting position owners.");
            int id = unit["Id"]!.GetValue<int>(), hex = unit["HexId"]!.GetValue<int>();
            int? direct = Read(unit["NativeCurrentHex2A"]);
            if (direct is > 48399) throw new InvalidDataException("Historical creation position is outside its source map.");
            int? marchHex = null;
            JsonObject? converted = null;
            if (unit["NativeMarch"] is JsonNode marchNode)
            {
                if (marchNode is not JsonObject march || !march.ContainsKey("Credit14") ||
                    !march.ContainsKey("CurrentHex2A") || !march.ContainsKey("TargetHex2C") || !march.ContainsKey("NextHex48") ||
                    march.ContainsKey("OverrideTargetType3C"))
                    throw new InvalidDataException("Historical army marching knowledge is incomplete.");
                marchHex = Read(march["CurrentHex2A"]) ?? throw new InvalidDataException("Historical march position is unknown.");
                if (marchHex != hex) throw new InvalidDataException("Historical marching position differs from its unit position.");
                converted = new() { ["MovementCredit"] = Read(march["Credit14"]) ?? throw new InvalidDataException("Historical march progress is unknown."),
                    ["Target"] = Destination(march["TargetHex2C"]), ["NextStep"] = Destination(march["NextHex48"]) };
            }
            if (unit["NativeTransport"] is JsonObject transport)
            {
                if (!transport.ContainsKey("InitialNextHex48") || !transport.ContainsKey("WrapperNextHex48") ||
                    Read(transport["InitialNextHex48"]) is null)
                    throw new InvalidDataException("Historical transport creation knowledge is incomplete.");
                _ = Read(transport["WrapperNextHex48"]);
                // The actual transport setter wrote the shared next-step field.
                // The former March copy was not refreshed by that writer.
                if (transport["CurrentNextHex48"] is JsonNode next)
                {
                    converted ??= new() { ["MovementCredit"] = null, ["Target"] = null };
                    converted["NextStep"] = Destination(next);
                }
                transport.Remove("CurrentNextHex48");
                transport.Remove("InitialNextHex48"); transport.Remove("WrapperNextHex48");
            }
            bool conflict = direct is int d && d != hex || marchHex is int m && m != hex;
            unit["PositionKnowledge"] = (int)(conflict ? ArmyPositionKnowledge.Conflicting :
                direct.HasValue || marchHex.HasValue ? ArmyPositionKnowledge.Confirmed : ArmyPositionKnowledge.Unknown);
            unit["March"] = converted;
            unit.Remove("NativeCurrentHex2A"); unit.Remove("NativeMarch");
            if (conflict || direct is null && marchHex is not null)
                residues.Add(new(id, direct is null, conflict ? direct : null, conflict ? marchHex : null));
        }
    }
    private static JsonObject Destination(JsonNode? node)
    {
        int value = Read(node) ?? throw new InvalidDataException("Historical march destination is unknown.");
        return value == 48400 ? new() { ["Kind"] = (int)ArmyMarchDestinationKind.None, ["HexId"] = null } :
            new() { ["Kind"] = (int)ArmyMarchDestinationKind.Hex, ["HexId"] = value };
    }
    private static int? Read(JsonNode? node) => node is null ? null :
        node is JsonValue value && value.TryGetValue<int>(out int number) && number is >= 0 and <= ushort.MaxValue ? number :
        throw new InvalidDataException("Historical army march field is outside its encoding.");
}
