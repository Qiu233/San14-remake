using San14.Core;

namespace San14.Data;

/// <summary>The finite current definition fields consumed by ordinary new-draft
/// tactic selection. Fresh loading supplies them once; queries bind saved values
/// and never reconstruct absent current knowledge from fixed resource bytes.</summary>
public static class DeploymentTacticCurrentInputs
{
    private const string Prefix = "deployment-tactic-current:";
    public const string VersionKey = Prefix + "version";
    private static readonly int[] RawOffsets = [0x44, 0x62, 0x63, 0x6A, 0x6C, 0x6D, 0x6E, 0x6F];
    public static string RawKey(int tactic, int offset) => $"{Prefix}{tactic}:raw:{offset:X2}";
    public static string VectorCountKey(int tactic) => $"{Prefix}{tactic}:vector-count";
    public static string VectorKey(int tactic, int index) => $"{Prefix}{tactic}:vector:{index}";
    public static string GemKey(int gem, int offset) => $"{Prefix}gem:{gem}:{offset:X2}";

    public static GameState InstallFresh(GameState state, SourceWorldContext definitions)
    {
        if (state.NativeArmySimulation is not { } simulation || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Language is not ("SC" or "TC") ||
            simulation.Parameters.Int32Values.Keys.Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("Current deployment tactic fields require a fresh owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        for (int tactic = 0; tactic < 201; tactic++)
        {
            var reference = definitions.ReadSkillTacticReference(tactic);
            if (reference?.PhysicalSlot != tactic) throw new InvalidDataException("Fresh tactic identity is incomplete.");
            foreach (int offset in RawOffsets)
                values[RawKey(tactic, offset)] = definitions.ReadSkillTacticRawByte(reference, offset)
                    ?? throw new InvalidDataException("Fresh tactic raw field is unknown.");
            var vector = definitions.ReadSkillTacticEncodedValues70(tactic)
                ?? throw new InvalidDataException("Fresh tactic vector is unknown.");
            if (vector.Count != 10) throw new InvalidDataException("Fresh CE tactic vector layout differs.");
            values[VectorCountKey(tactic)] = vector.Count;
            for (int index = 0; index < vector.Count; index++) values[VectorKey(tactic, index)] = vector[index];
        }
        for (int gem = 0; gem < 31; gem++)
        {
            var reference = definitions.ReadSkillGemReference(gem);
            if (reference?.PhysicalSlot != gem) throw new InvalidDataException("Fresh gem identity is incomplete.");
            values[GemKey(gem, 0x52)] = definitions.ReadSkillGemRawByte(reference, 0x52)
                ?? throw new InvalidDataException("Fresh gem tactic is unknown.");
            values[GemKey(gem, 0x53)] = definitions.ReadSkillGemRawByte(reference, 0x53)
                ?? throw new InvalidDataException("Fresh gem validity is unknown.");
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        var values = state.NativeArmySimulation?.Parameters.Int32Values;
        bool owned = values?.GetValueOrDefault(VersionKey) == 1;
        for (int tactic = 0; tactic < 201; tactic++)
        {
            foreach (int offset in RawOffsets) current.SetSkillTacticRawByte(tactic, offset, Byte(RawKey(tactic, offset)));
            IReadOnlyList<int>? vector = null;
            if (owned && values!.TryGetValue(VectorCountKey(tactic), out int count) && count is >= 0 and <= 10)
            {
                var items = new int[count]; bool complete = true;
                for (int index = 0; index < count; index++)
                    if (!values.TryGetValue(VectorKey(tactic, index), out items[index])) { complete = false; break; }
                if (complete) vector = items;
            }
            current.SetSkillTacticEncodedValues(tactic, vector);
        }
        for (int gem = 0; gem < 31; gem++) current.SetSkillGemFields(gem, Byte(GemKey(gem, 0x52)), Byte(GemKey(gem, 0x53)));
        // Qualification consumes only vector length and, for length10, its
        // first encoded byte. Other consumers still require the whole vector.
        current.CurrentTacticQualificationFlagsReader = physical =>
        {
            if (!owned || !values!.TryGetValue(VectorCountKey(physical), out int count) || count < 0) return null;
            if (count == 10)
                return values.TryGetValue(VectorKey(physical, 0), out int first)
                    ? SourceWorldContext.DecodeSkillTacticFlags(physical, first) : null;
            return Byte(RawKey(physical, 0x44));
        };
        byte? Byte(string key) => owned && values!.TryGetValue(key, out int value) && value is >= 0 and <= 255 ? (byte)value : null;
    }
}
