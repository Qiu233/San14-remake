using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelCommandTasksSaveMigration
{
    internal static void Apply(JsonObject state)
    {
        // Old saves own only Return tasks. Preserve every allocator value and
        // mark the newly introduced command kind unknown; never invent empty.
        if (state.Remove("PersonnelReturns", out var old))
        {
            if (state.ContainsKey("PersonnelCommands"))
                throw new InvalidDataException("Saved personnel owners conflict.");
            if (old is JsonObject owner)
            {
                if (owner.Remove("Tasks", out var returns)) owner["Returns"] = returns;
                owner["Disappearances"] = null;
            }
            state["PersonnelCommands"] = old;
        }
        else if (!state.ContainsKey("PersonnelCommands")) state["PersonnelCommands"] = null;
    }
}
