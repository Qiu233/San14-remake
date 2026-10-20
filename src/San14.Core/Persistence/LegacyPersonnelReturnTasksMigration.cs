using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Decodes the old saved command image once. Simulation code never reads
/// these byte windows, allocation lists, union tags or descriptor identities.</summary>
internal static class LegacyPersonnelCommandTasksMigration
{
    public static PersonnelCommandTasks Translate(NativePersonCommandManagerState legacy,
        Func<int, PersonnelReturnDestination?> resolveBaseCode, ISet<int>? associatedTaskIds = null,
        int[]? pendingRetirements = null)
    {
        if (legacy.FreeNext30 is not { Length: 1852 } || legacy.FreeHeadEA8 is null ||
            legacy.Commands is null || legacy.IdToReceiver is null)
            throw new InvalidDataException("Legacy personnel dispatch storage is incomplete.");
        var slots = new List<int>();
        for (int? slot = legacy.FreeHeadEA8; slot is >= 0 and < PersonnelCommandTasks.CompatibleDispatchCapacity;
             slot = legacy.FreeNext30[slot.Value])
        {
            if (slots.Contains(slot.Value)) throw new InvalidDataException("Legacy return dispatch slots contain a cycle.");
            slots.Add(slot.Value);
        }
        var tasks = new Dictionary<int, PersonnelReturnTask>();
        var registered = legacy.IdToReceiver.ToDictionary(pair => pair.Value, pair => pair.Key);
        foreach (var pair in legacy.Commands)
        {
            var old = pair.Value;
            if (old is null || old.Kind != "return" || old.RawFields is not { Length: 128 })
                throw new InvalidDataException("Unsupported or incomplete legacy personnel command.");
            int? slot = registered.TryGetValue(pair.Key, out int registeredSlot) ? registeredSlot : null;
            if (slot.HasValue && Int(old, 0x58) != slot.Value)
                throw new InvalidDataException("Legacy personnel dispatch registration disagrees with its command.");
            var members = Values(legacy.MembersPool, old.Members20).Select(value =>
                value is { Kind: "MemberId32", Identity: int id } ? id :
                    throw new InvalidDataException("Invalid legacy return member.")).ToArray();
            var targets = Values(legacy.TargetsPool, old.Targets38).Select(value =>
            {
                var target = value?.Identity is int node ? legacy.TargetNodeValues.GetValueOrDefault(node) : null;
                return target is { FirstPhysical0: int physical, FirstKind8: 3 or 4 }
                    ? new PersonnelReturnDestination(target.FirstKind8 == 3 ? PersonnelReturnBaseKind.City : PersonnelReturnBaseKind.Gate,
                        target.FirstKind8 == 3 ? physical : physical + 51)
                    : throw new InvalidDataException("Invalid legacy return destination.");
            }).ToArray();
            bool retiring = Int(old, 0x54) != 0;
            tasks[pair.Key] = new(pair.Key, slot, members, targets,
                resolveBaseCode(Word(old, 0x2A)), Byte(old, 0x28), Word(old, 0x4C),
                Int(old, 0x40) != 0, Int(old, 0x44) != 0, Int(old, 0x50) != 0, retiring,
                associatedTaskIds?.Contains(pair.Key) ?? (slot.HasValue && !retiring), Date(old, 0x60), Date(old, 0x66));
        }
        return new(legacy.NextReceiverIdentity, slots.ToArray(), tasks, pendingRetirements ?? []);
    }

    public static void Apply(JsonObject state)
    {
        if (state["NativePersonReturnCommands"] is not JsonObject old) return;
        if (state["PersonnelReturns"] is not null)
            throw new InvalidDataException("Save contains both legacy and typed personnel return owners.");
        var legacy = old.Deserialize<NativePersonCommandManagerState>()
            ?? throw new InvalidDataException("Legacy personnel return state is missing.");
        if (state["NativeArmyLifecycle"] is not JsonObject lifecycle ||
            lifecycle["Pool"]?.Deserialize<NativeSharedListPoolState>() is not { } pool ||
            lifecycle["Auxiliaries"]?["AssociatedCommand18Descriptor"]?["Identity"]?.GetValue<int>() is not int associated)
            throw new InvalidDataException("Legacy personnel association index is missing.");
        int[] associations = ReadAssociations(pool, associated);
        int[] pending = legacy.Secondary28Descriptor.Identity is int secondary
            ? ReadAssociations(pool, secondary) : [];
        var owner = Translate(legacy, code => code switch
        {
            0 => null, //An allocated, unregistered receiver has no destination.
            >= 1 and <= 51 => new(PersonnelReturnBaseKind.City, code),
            >= 52 and <= 61 => new(PersonnelReturnBaseKind.Gate, code),
            _ => throw new InvalidDataException("Legacy return destination cannot be resolved.")
        }, associations.ToHashSet(), pending);
        PersonnelCommandSaveValidation.Validate(owner);
        state["PersonnelReturns"] = JsonSerializer.SerializeToNode(owner);
        state.Remove("NativePersonReturnCommands");
    }

    private static int[] ReadAssociations(NativeSharedListPoolState pool, int descriptor)
    {
        if (!pool.LiveDescriptors.Contains(descriptor) || !pool.Descriptors.TryGetValue(descriptor, out var row))
            throw new InvalidDataException("Legacy personnel association descriptor is unavailable.");
        var result = new List<int>();
        var visited = new HashSet<int>();
        for (int? at = row.Head; at is int node; at = pool.Nodes[node].Next)
        {
            if (!visited.Add(node) || !pool.Nodes.TryGetValue(node, out var entry) ||
                entry.Payload is not { Kind: "LinkedEntity", Identity: int taskId })
                throw new InvalidDataException("Legacy personnel association chain is invalid.");
            result.Add(taskId);
        }
        return result.ToArray();
    }

    private static IEnumerable<NativeSharedListPayload?> Values(NativeSharedListPoolState pool, NativeSharedListHandle handle)
    {
        if (handle.Identity is not int descriptor) yield break;
        if (!pool.LiveDescriptors.Contains(descriptor) || !pool.Descriptors.TryGetValue(descriptor, out var row))
            throw new InvalidDataException("Legacy personnel value descriptor is unavailable.");
        var visited = new HashSet<int>();
        for (int? at = row.Head; at is int node; at = pool.Nodes[node].Next)
        {
            if (!visited.Add(node) || !pool.Nodes.TryGetValue(node, out var entry))
                throw new InvalidDataException("Legacy personnel values contain an invalid chain.");
            yield return entry.Payload;
        }
    }
    private static byte Byte(NativePersonCommandState command, int offset) => command.RawFields[offset]
        ?? throw new InvalidDataException("Legacy return field was not recorded.");
    private static ushort Word(NativePersonCommandState command, int offset) =>
        (ushort)(Byte(command, offset) | Byte(command, offset + 1) << 8);
    private static int Int(NativePersonCommandState command, int offset) => Byte(command, offset) |
        Byte(command, offset + 1) << 8 | Byte(command, offset + 2) << 16 | Byte(command, offset + 3) << 24;
    private static NativeMapSeasonDate Date(NativePersonCommandState command, int offset) =>
        new(unchecked((short)Word(command, offset)), unchecked((sbyte)Byte(command, offset + 2)),
            unchecked((sbyte)Byte(command, offset + 3)), unchecked((sbyte)Byte(command, offset + 4)));
}
