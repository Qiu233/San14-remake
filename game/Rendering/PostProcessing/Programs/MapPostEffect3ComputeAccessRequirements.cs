using System.Buffers.Binary;
using San14.Data;

internal enum MapPostEffect3ComputeWorkgroupProfile
{
    None, EyeHistogram, DofSprite, LensFlareFft
}

/// <summary>Static storage accesses of one final device program. Frame permissions
/// and external resource contents are supplied separately by the dispatcher.</summary>
internal sealed class MapPostEffect3ComputeAccessRequirements
{
    internal IReadOnlyList<TranslatedShaderBinding> StorageReads { get; }
    private readonly Lazy<bool>[] workgroupPermissions;

    private MapPostEffect3ComputeAccessRequirements(TranslatedShaderBinding[] reads,
        WorkgroupDeclarations workgroups, (uint Root, ushort Opcode, int Offset)[] localOperations)
    {
        StorageReads = Array.AsReadOnly(reads);
        workgroupPermissions = Enum.GetValues<MapPostEffect3ComputeWorkgroupProfile>().Select(profile =>
            new Lazy<bool>(() =>
            {
                var roots = workgroups.RequireRoots(profile);
                foreach (var operation in localOperations)
                    if (!roots.Contains(operation.Root))
                        throw new NotSupportedException($"Selected storage read/atomic opcode{operation.Opcode} at byte{operation.Offset} has no source-closed descriptor origin.");
                return true;
            }, LazyThreadSafetyMode.ExecutionAndPublication)).ToArray();
    }

    internal void RequireWorkgroupPermission(MapPostEffect3ComputeWorkgroupProfile profile)
        => _ = workgroupPermissions[(int)profile].Value;

    internal static MapPostEffect3ComputeAccessRequirements Read(ReadOnlySpan<byte> bytes,
        TranslatedShaderBindings bindings)
    {
        var workgroups = WorkgroupDeclarations.Read(bytes);
        var declarations = bindings.Bindings.ToDictionary(d => d.Device.VariableId);
        var localRoots = workgroups.Variables.Select(v => v.Id).ToHashSet();
        var origins = declarations.Keys.ToDictionary(i => i, i => i);
        foreach (uint root in localRoots) origins.Add(root, root);
        var reads = new HashSet<uint>();
        var localOperations = new List<(uint Root, ushort Opcode, int Offset)>();
        for (int at = 20; at < bytes.Length;)
        {
            uint header = BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
            int words = checked((int)(header >> 16)); ushort op = (ushort)header;
            if (words == 0 || at + words * 4 > bytes.Length)
                throw new InvalidDataException("Malformed selected CS instruction extent.");
            if (op is 61 or 65 or 66 or 67 or 83 or 143)
            {
                if (words < 4) throw new InvalidDataException("Malformed translated buffer pointer/load.");
                uint result = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]),
                    source = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..]);
                if (origins.TryGetValue(source, out uint origin))
                {
                    origins[result] = origin;
                    if (op == 61 && !localRoots.Contains(origin) && declarations[origin].DescriptorKind == "StorageBuffer")
                        reads.Add(origin);
                }
            }
            if (op is 98 or 143 or >= 227 and <= 242)
            {
                int sourceWord = op == 228 ? 1 : 3;
                if (words <= sourceWord || !origins.TryGetValue(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + sourceWord * 4)..]), out uint origin))
                    throw new NotSupportedException($"Selected storage read/atomic opcode{op} at byte{at} has no source-closed descriptor origin.");
                if (localRoots.Contains(origin)) localOperations.Add((origin, op, at));
                else reads.Add(origin);
            }
            at += words * 4;
        }
        return new(reads.Select(id => declarations[id]).ToArray(), workgroups, localOperations.ToArray());
    }

    private sealed class WorkgroupDeclarations
    {
        private readonly Dictionary<uint, (uint Storage, uint Element)> pointerTypes = [];
        private readonly Dictionary<uint, (uint Element, uint Length)> arrays = [];
        private readonly HashSet<uint> uintTypes = [];
        private readonly Dictionary<uint, (uint Type, uint Value)> constants = [];
        internal List<(uint Type, uint Id, int Words)> Variables { get; } = [];
        private readonly List<(uint ExecutionScope, uint MemoryScope, uint Semantics)> barriers = [];
        private bool fftLocalSize;

        internal static WorkgroupDeclarations Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 20 || bytes.Length % 4 != 0 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x07230203)
                throw new InvalidDataException("Selected compute SPIR-V header is invalid.");
            var result = new WorkgroupDeclarations();
            for (int at = 20; at < bytes.Length;)
            {
                uint header = BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
                int count = checked((int)(header >> 16)); ushort op = (ushort)header;
                if (count == 0 || at + count * 4 > bytes.Length)
                    throw new InvalidDataException("Malformed selected CS declaration extent.");
                if (op == 21 && count == 4 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]) == 32 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..]) == 0)
                    result.uintTypes.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]));
                else if (op == 28 && count == 4)
                    result.arrays.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]),
                        (BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..])));
                else if (op == 32 && count == 4)
                    result.pointerTypes.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]),
                        (BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..])));
                else if (op == 43 && count == 4)
                    result.constants.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]),
                        (BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..])));
                else if (op == 59 && count >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..]) == 4)
                    result.Variables.Add((BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]), count));
                else if (op == 224 && count == 4)
                    result.barriers.Add((BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]),
                        BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..])));
                else if (op == 16 && count == 6 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]) == 17)
                    result.fftLocalSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..]) == 1024 &&
                        BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 16)..]) == 1 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 20)..]) == 1;
                at += count * 4;
            }
            return result;
        }

        private bool IsConstant(uint id, uint value)
            => constants.TryGetValue(id, out var c) && uintTypes.Contains(c.Type) && c.Value == value;

        internal HashSet<uint> RequireRoots(MapPostEffect3ComputeWorkgroupProfile profile)
        {
            if (profile == MapPostEffect3ComputeWorkgroupProfile.LensFlareFft)
            {
                // The old FFT parser considers only four-word local variables,
                // and requires its exact local size even when no root exists.
                var roots = Variables.Where(v => v.Words == 4).ToArray();
                if (!fftLocalSize || roots.Length != 1 || barriers.Count != 2 || barriers.Any(b =>
                    !IsConstant(b.ExecutionScope, 2) || !IsConstant(b.MemoryScope, 2) || !IsConstant(b.Semantics, 264)) ||
                    !pointerTypes.TryGetValue(roots[0].Type, out var p) || p.Storage != 4 || !arrays.TryGetValue(p.Element, out var a) ||
                    !uintTypes.Contains(a.Element) || !IsConstant(a.Length, 2048))
                    throw new NotSupportedException("LensFlare2 FFT requires its original1024-lane uint32[2048] initialization and two static barrier sites.");
                return [roots[0].Id];
            }
            // General/Eye/DOF preserve the old early return for no local storage.
            if (Variables.Count == 0) return [];
            bool eye = profile == MapPostEffect3ComputeWorkgroupProfile.EyeHistogram;
            if (profile is not (MapPostEffect3ComputeWorkgroupProfile.EyeHistogram or MapPostEffect3ComputeWorkgroupProfile.DofSprite) ||
                Variables.Count != 1 || Variables[0].Words != 4 || barriers.Count != (eye ? 2 : 1) || barriers.Any(b =>
                !IsConstant(b.ExecutionScope, 2) || !IsConstant(b.MemoryScope, 2) || !IsConstant(b.Semantics, 264)))
                throw new NotSupportedException("Selected workgroup storage has no exact original Eye/DOF initialization source.");
            var variable = Variables[0];
            if (!pointerTypes.TryGetValue(variable.Type, out var pointer) || pointer.Storage != 4 ||
                !arrays.TryGetValue(pointer.Element, out var array) || !uintTypes.Contains(array.Element) || !IsConstant(array.Length, eye ? 64u : 2048u))
                throw new NotSupportedException("Original Eye/DOF workgroup storage differs from its source uint32 counter/shared-row declaration.");
            return [variable.Id];
        }
    }
}
