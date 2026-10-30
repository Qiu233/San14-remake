namespace San14.Data.AI.Original;

/// <summary>The current-owner Force information family. Existing ancestor and
/// strategy-memory bindings retain their own modes0/2/19/21/37.</summary>
public static class OriginalAiForceInformationBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (!node.IsNative || !node.ChildIndices.IsEmpty) return false;
        var a = node.Arguments;
        bool myself = node.TypeId == 0x9CDB6038, variable = node.TypeId == 0xEBBF8C69;
        if (!myself && !variable) return false;
        if (myself ? a.Length != 2 : a.Length != 3)
        { unresolved = "Force information parameter shape"; return false; }
        int mode = unchecked((int)a[myself ? 0 : 1]);
        bool supported = mode is 9 or 10 or 11 or >= 12 and <= 18 or 20 or 24 or 25 or 28 or 29 or 30 or 32 or 35;
        if (variable && mode is 0 or 2) supported = true; // Reuse established getters.
        if (!supported)
        {
            unresolved = mode switch
            {
                19 or 21 or 37 => null, // Owned by the existing family.
                1 => "ForceInfo1 current signed Force4B crisis producer",
                3 => "ForceInfo3 current2690D0 capacity consumer",
                4 or 5 => "ForceInfo weighted evaluation current source inputs",
                6 or 7 or 27 => "ForceInfo current Force17A..17F planning targets",
                8 => "ForceInfo8 source object identity08 owner",
                22 or 31 => "ForceInfo current Area planning-field producer",
                23 => "ForceInfo23 actual0B8890 candidate producer",
                26 => "ForceInfo26 ordered repeated doctrine-panel list owner",
                33 => "ForceInfo33 saved third Force238 interior classification",
                34 => "ForceInfo34 CTroopsData group list; discriminator2 is not District",
                36 => "ForceInfo36 current2313C0 District24 ratio classification",
                38 or 39 => "ForceInfo specialized6D9490/6DBC10 list producer",
                _ => "Force information source mode " + mode
            };
            return false;
        }
        execute = myself ? operation => operation.MyselfForceInformationCurrent(mode, a[1])
            : operation => operation.VariableForceInformationCurrent(a[0], mode, a[2]);
        return true;
    }

    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        predicate = null; unresolved = null;
        if (query.Record.QueryId != 63 || query.Record.Parameters.Length != 1) return false;
        uint key = query.Record.Parameters[0];
        predicate = operation => operation.ForceIsCurrentPlayer(key);
        return true;
    }
}
