using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapPostEffect3DofSpriteStorageDescriptorProof(
    int ProgramIndex, uint SourceRegister, uint SourceInputType,
    int SourceBufferParameter, uint SourceNativeViewFormat,
    uint VariableId, uint DescriptorSet, uint DeviceBinding, uint DeviceStorageClass,
    uint DeviceWordStride, uint NativeElementBytes, uint DeviceWordsPerElement,
    int ReplacedRawImageWrites, int ReplacedAtomicPointers, int ReplacedRgbaImageWrites,
    string SourceEvidence);

public sealed record OriginalMapPostEffect3DofSpriteStorageAdaptation(
    byte[] Bytes, string SourceProgramSha256, string TranslatedSpirvSha256,
    string AdaptedSpirvSha256,
    IReadOnlyList<OriginalMapPostEffect3DofSpriteStorageDescriptorProof> DescriptorProofs,
    int RetainedInstructions, int RemovedImageLoads, int AddedInstructions,
    string SourceEvidence)
{
    public IReadOnlyList<OriginalMapPostEffect3DofSpriteFetchBoundsProof> FetchBoundsProofs { get; init; } = [];
}

public sealed record OriginalMapPostEffect3DofSpriteFetchBoundsProof(
    uint SourceRegister, int SourceTextureParameter, uint VariableId,
    uint DescriptorSet, uint DeviceBinding, uint OriginalResultId,
    uint OriginalImageId, uint OriginalCoordinateId, uint OriginalLodId,
    uint ImageOperandsMask, uint? OriginalConstOffsetId, int ConstOffsetX, int ConstOffsetY,
    uint QueryResultId, uint QueryLodId, uint ConditionId, uint InBoundsBlockId,
    uint OutOfBoundsBlockId, uint MergeBlockId, uint GuardedFetchResultId, uint ZeroFloat4Id,
    string SourceEvidence);

/// <summary>Exact CS45/46 storage representation adapter. Original DXBC and
/// every unrelated translated instruction remain unchanged. Raw u0 retains its
/// byte-address-to-word coordinate and atomic operands. Typed u1/u2 retain all
/// four float components through explicit D3D11 round-to-zero binary16 bit
/// conversion, matching native DXGI10 RGBA16F's eight bytes per texel. This
/// preserves finite saturation, infinities, NaNs, denorms and signed zero.
/// Exact CS46's five Texture2D.Load sites additionally guard real view bounds
/// before fetching and return complete float4 zero outside. No lane returns
/// early, so original shared memory and synchronization continue unchanged.
/// This proves interface and instruction
/// conversion only; it grants no initialized contents or GPU completion.</summary>
public static class OriginalMapPostEffect3DofSpriteStorageAdapter
{
    public const string ClearSourceSha256 = "A3C90ECCB018FCFC8A34FDFD81755CA7FB84AFC667D852F1BD60511CC440063B";
    public const string BuildSourceSha256 = "04CB59EB2DDD04998EB07CAC1721512B4866A4863DB48B3559AB7B3C345D9149";
    public const string ClearDefaultTranslationSha256 = "A5CB090D1F42CD70E67F65BCA8B99A5B8980525C39C0C7DEC01400FFD07C8442";
    public const string BuildDefaultTranslationSha256 = "FA2CE2BE6EDCD25C622F9ABECF66D65C1C4E031B7BA4A5C88120602809AD7EB0";

    public static OriginalMapPostEffect3DofSpriteStorageAdaptation Adapt(
        OriginalMapPostEffect3Program source, OriginalShaderSpirv defaultTranslation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(defaultTranslation);
        bool clear = source.StageIndex == 45;
        string expectedSource = clear ? ClearSourceSha256 : BuildSourceSha256;
        string expectedTranslation = clear ? ClearDefaultTranslationSha256 : BuildDefaultTranslationSha256;
        if (source.StageCode != 5 || source.StageIndex is not (45 or 46) ||
            source.Sha256 != expectedSource || Hash(source.Bytes.Span) != expectedSource ||
            defaultTranslation.SourceProgramSha256 != expectedSource ||
            defaultTranslation.SpirvSha256 != expectedTranslation ||
            Hash(defaultTranslation.Bytes) != expectedTranslation ||
            defaultTranslation.BufferUavsAsStorageBuffers || defaultTranslation.ExplicitDescriptorSet is not null)
            throw new InvalidDataException("DOF sprite storage requires exact original CS45/46 and its complete default official translation; changed compiler output must be audited first.");

        var before = SpirvShaderInterfaceReader.Read(defaultTranslation.Bytes);
        if (before.EntryPoints is not { Count: 1 } || before.EntryPoints[0].ExecutionModel != 5 ||
            before.ExecutionModes.Single(m => m.Mode == 17).Operands.SequenceEqual(
                clear ? new uint[] { 1, 1, 1 } : new uint[] { 16, 16, 1 }) == false)
            throw new InvalidDataException("Exact DOF source entry point/local group dimensions changed.");
        var rdef = DxbcResourceDefinitionReader.Read(source.Bytes.Span);
        if (rdef.Bindings.Count != (clear ? 1 : 6) || before.Descriptors.Count != rdef.Bindings.Count)
            throw new InvalidDataException("Exact DOF sprite descriptor count changed.");

        List<Instruction> instructions = Read(defaultTranslation.Bytes);
        uint uintType = instructions.Single(i => i.Opcode == 21 && i.Args.SequenceEqual(new uint[] { i.Args[0], 32, 0 })).Args[0];
        uint zero = Constant(instructions, uintType, 0);
        uint one = clear ? 0 : Constant(instructions, uintType, 1);
        uint two = clear ? 0 : Constant(instructions, uintType, 2);
        uint floatType = clear ? 0 : instructions.Single(i => i.Opcode == 22 && i.Args[1] == 32).Args[0];
        uint float4 = clear ? 0 : instructions.Single(i => i.Opcode == 23 && i.Args[1] == floatType && i.Args[2] == 4).Args[0];
        uint uint4 = clear ? instructions.Single(i => i.Opcode == 23 && i.Args[1] == uintType && i.Args[2] == 4).Args[0] : 0;
        uint boolType = clear ? 0 : instructions.Single(i => i.Opcode == 20).Args[0];
        uint intType = clear ? 0 : instructions.Single(i => i.Opcode == 21 && i.Args[1] == 32 && i.Args[2] == 1).Args[0];
        uint int2Type = clear ? 0 : instructions.Single(i => i.Opcode == 23 && i.Args[1] == intType && i.Args[2] == 2).Args[0];
        uint intZero = clear ? 0 : Constant(instructions, intType, 0);
        uint floatZero = clear ? 0 : Constant(instructions, floatType, 0);
        uint float4Zero = clear ? 0 : instructions.Single(i => i.Opcode == 44 &&
            i.Args.SequenceEqual(new uint[] { float4, i.Args[1], floatZero, floatZero, floatZero, floatZero })).Args[1];

        // This finite graph has no existing Phi whose predecessor would need
        // rewriting after the five local block splits. A different graph is
        // never admitted by the complete default-module identity gate.
        if (!clear && instructions.Any(i => i.Opcode == 245))
            throw new InvalidDataException("Exact DOF fetch graph has changed Phi predecessors.");
        uint[][] expectedFetches = clear ? [] : [
            [float4, 97, 90, 93, 2, 96], [float4, 111, 102, 105, 10, 108, 110],
            [float4, 123, 115, 118, 10, 121, 122], [float4, 134, 127, 130, 10, 133, 77],
            [float4, 422, 415, 418, 2, 421]];
        var actualFetches = instructions.Where(i => i.Opcode == 95).ToArray();
        if (actualFetches.Length != expectedFetches.Length ||
            !actualFetches.Zip(expectedFetches).All(p => p.First.Args.SequenceEqual(p.Second)))
            throw new InvalidDataException("DOF bounds conversion requires exactly the five original Texture2D.Load SSA graphs and their Lod/ConstOffset operands.");
        var fetchInputs = new Dictionary<uint, SpirvInterfaceVariable>();
        foreach (var fetch in actualFetches)
        {
            var imageLoad = instructions.Single(i => i.Opcode == 61 && i.Args[1] == fetch.Args[2]);
            var device = before.Descriptors.Single(d => d.VariableId == imageLoad.Args[2]);
            uint register = fetch.Args[1] == 422 ? 0u : 1u;
            var original = rdef.Bindings.Single(b => b.InputType == 2 && b.BindPoint == register);
            if (device.Name != $"t{register}" || device.StorageClass != 0 || device.DescriptorSet != 0 || device.Binding != register + 1 ||
                device.Type is not { Kind: "image", Dimension: 1, Arrayed: false, Multisampled: false, SampledCode: 1,
                    ComponentType: { Kind: "float", Bits: 32 } } ||
                original.Dimension != 4 || original.ReturnType != 5 || original.BindCount != 1 ||
                imageLoad.Args.Length != 3 || ResultType(instructions, fetch.Args[3]) != int2Type ||
                ResultType(instructions, fetch.Args[5]) != intType)
                throw new InvalidDataException("DOF fetch bounds require the exact nonarray/nonMS float Texture2D source views.");
            // Offset constants are retained in the real fetch and checked in
            // the guard. Only the original (1,0)/(0,1)/(1,1) graph is admitted.
            if (fetch.Args.Length == 7)
            {
                int[] offsets = fetch.Args[1] switch { 111 => [1, 0], 123 => [0, 1], 134 => [1, 1], _ => throw new InvalidDataException("Unknown source DOF offset.") };
                var offset = instructions.Single(i => i.Opcode == 44 && i.Args[1] == fetch.Args[6]);
                if (offset.Args.Length != 4 || offset.Args[0] != int2Type ||
                    !offset.Args.Skip(2).Select(id => unchecked((int)instructions.Single(i => i.Opcode == 43 && i.Args[1] == id).Args[2])).SequenceEqual(offsets))
                    throw new InvalidDataException("Source DOF ConstOffset value changed.");
            }
            fetchInputs.Add(fetch.Args[1], device);
        }

        var descriptors = new Dictionary<uint, SpirvInterfaceVariable>();
        foreach (uint register in clear ? new uint[] { 0 } : new uint[] { 0, 1, 2 })
        {
            var original = rdef.Bindings.Single(b => b.BindPoint == register && b.InputType == (register == 0 ? 8u : 4u));
            var device = before.Descriptors.Single(d => d.Name == $"u{register}");
            if (original.BindCount != 1 || original.Dimension != 1 ||
                (register == 0 && original.ReturnType != 6) ||
                (register != 0 && original.ReturnType != 5) || device.StorageClass != 0 ||
                device.DescriptorSet != 0 || device.Binding != (clear ? 0 : register + 3) ||
                device.Type is not { Kind: "image", Dimension: 5, SampledCode: 2 } ||
                (register == 0 && (device.Type.ImageFormat != 33 ||
                    device.Type.ComponentType is not { Kind: "int", Bits: 32, Signed: false })) ||
                (register != 0 && (device.Type.ImageFormat != 0 ||
                    device.Type.ComponentType is not { Kind: "float", Bits: 32 })))
                throw new InvalidDataException("Original DOF sprite UAV declaration differs from raw R32_UINT u0 or float4 buffer u1/u2.");
            descriptors.Add(register, device);
        }

        // Only the exact immutable modules above enter this scan. Every image
        // load, write and atomic pointer still joins its real descriptor ID;
        // no unknown pointer is presumed to name an output resource.
        var byVariable = descriptors.ToDictionary(x => x.Value.VariableId, x => x.Key);
        var imageLoads = new Dictionary<uint, uint>();
        foreach (var i in instructions.Where(i => i.Opcode == 61 && byVariable.ContainsKey(i.Args[2])))
        {
            if (i.Args.Length != 3) throw new InvalidDataException("DOF image load has unrecognized memory operands.");
            imageLoads.Add(i.Args[1], byVariable[i.Args[2]]);
        }
        int[] writes = new int[3], atomics = new int[3];
        var atomicPointerIds = new HashSet<uint>();
        foreach (var i in instructions)
        {
            if (i.Opcode == 99)
            {
                if (i.Args.Length != 3 || !imageLoads.TryGetValue(i.Args[0], out uint register))
                    throw new InvalidDataException("DOF ImageWrite is not joined to an exact output image load.");
                writes[register]++;
            }
            if (i.Opcode == 60)
            {
                if (i.Args.Length != 5 || !byVariable.TryGetValue(i.Args[2], out uint register) || register != 0 || i.Args[4] != zero)
                    throw new InvalidDataException("DOF atomic image pointer is unknown or has a changed sample operand.");
                atomics[register]++; atomicPointerIds.Add(i.Args[1]);
            }
        }
        if (clear ? writes[0] != 8 || imageLoads.Count != 2 || atomics.Sum() != 0 :
            writes[0] != 0 || writes[1] != 2 || writes[2] != 2 || imageLoads.Count != 4 || atomics[0] != 2)
            throw new InvalidDataException("Complete source DOF sprite write/atomic domain changed.");
        if (!clear && (instructions.Count(i => i.Opcode == 234) != 2 ||
            instructions.Where(i => i.Opcode == 234).Any(i => i.Args.Length != 6 || i.Args[0] != uintType ||
                !atomicPointerIds.Contains(i.Args[2]) || i.Args[3] != one || i.Args[4] != zero || i.Args[5] != one)))
            throw new InvalidDataException("DOF source AtomicIAdd pointer, device scope, semantics or increment changed.");

        uint nextId = BinaryPrimitives.ReadUInt32LittleEndian(defaultTranslation.Bytes.AsSpan(12));
        uint Next() => nextId++;
        uint runtimeArray = Next(), storageStruct = Next(), storagePointer = Next(), wordPointer = Next();
        Instruction[] declarations = [Make(29, runtimeArray, uintType), Make(30, storageStruct, runtimeArray),
            Make(32, storagePointer, 2, storageStruct), Make(32, wordPointer, 2, uintType)];
        Instruction[] decorations = [Make(71, runtimeArray, 6, 4), Make(71, storageStruct, 3),
            Make(72, storageStruct, 0, 35, 0)];
        var conversionConstants = new List<Instruction>();
        var unsignedConstants = instructions.Where(i => i.Opcode == 43 && i.Args.Length == 3 && i.Args[0] == uintType)
            .ToDictionary(i => i.Args[2], i => i.Args[1]);
        uint C(uint value)
        {
            if (unsignedConstants.TryGetValue(value, out uint id)) return id;
            id = Next(); unsignedConstants.Add(value, id);
            conversionConstants.Add(Make(43, uintType, id, value)); return id;
        }
        if (!clear)
            foreach (uint value in new uint[] { 0x8000, 0x7FFFFFFF, 0x7FFFFF, 0x800000,
                0x1C000, 103, 113, 126, 143, 255, 0x7E00, 0x7C00, 0x7BFF, 13, 16, 23, 31 }) _ = C(value);
        var output = new List<Instruction>();
        bool declared = false, decorated = false;
        bool queryCapabilityInserted = clear;
        var fetchBounds = new List<OriginalMapPostEffect3DofSpriteFetchBoundsProof>();
        int retained = 0, added = 0;
        uint Binary(ushort op, uint type, uint lhs, uint rhs)
        { uint id = Next(); output.Add(Make(op, type, id, lhs, rhs)); return id; }
        uint Select(uint condition, uint whenTrue, uint whenFalse)
        { uint id = Next(); output.Add(Make(169, uintType, id, condition, whenTrue, whenFalse)); return id; }
        uint HalfBits(uint vector, uint channel)
        {
            // D3D11.3 §§3.2.2/22.4.9 typed float32 -> float16 format store:
            // RTZ, finite overflow -> signed max finite, Inf -> Inf, NaN ->
            // NaN, representable denorms retained, zeros retain sign. Work
            // solely on bits so shader float arithmetic/driver packing
            // modes cannot round or flush the conversion. Every shift has
            // a count <=31, including computations whose Select discards
            // their value. NaN payload is unspecified by D3D; preserve the
            // high payload bits, force quiet, and retain its source sign.
            uint scalar = Next(), bits = Next();
            output.Add(Make(81, floatType, scalar, vector, channel));
            output.Add(Make(124, uintType, bits, scalar));
            uint sign = Binary(199, uintType, Binary(194, uintType, bits, C(16)), C(0x8000));
            uint magnitude = Binary(199, uintType, bits, C(0x7FFFFFFF));
            uint exponent = Binary(194, uintType, magnitude, C(23));
            uint mantissa = Binary(199, uintType, magnitude, C(0x7FFFFF));
            uint normal = Binary(130, uintType, Binary(194, uintType, magnitude, C(13)), C(0x1C000));
            uint subnormalShift = Binary(199, uintType, Binary(130, uintType, C(126), exponent), C(31));
            uint subnormal = Binary(194, uintType, Binary(197, uintType, mantissa, C(0x800000)), subnormalShift);
            uint isTiny = Binary(176, boolType, exponent, C(103));
            uint isSubnormal = Binary(176, boolType, exponent, C(113));
            uint isFiniteOverflow = Binary(174, boolType, exponent, C(143));
            uint isSpecial = Binary(170, boolType, exponent, C(255));
            uint isNan = Binary(171, boolType, mantissa, zero);
            uint nan = Binary(197, uintType, Binary(194, uintType, mantissa, C(13)), C(0x7E00));
            uint special = Select(isNan, nan, C(0x7C00));
            uint finite = Select(isFiniteOverflow, C(0x7BFF), normal);
            uint low = Select(isSubnormal, subnormal, finite);
            uint tiny = Select(isTiny, zero, low);
            return Binary(197, uintType, sign, Select(isSpecial, special, tiny));
        }
        foreach (var i in instructions)
        {
            if (!queryCapabilityInserted && i.Opcode != 17)
            { output.Add(Make(17, 50)); added++; queryCapabilityInserted = true; }
            if (!decorated && i.Opcode is >= 19 and <= 33)
            { output.AddRange(decorations); decorated = true; added += decorations.Length; }
            if (i.Opcode == 54)
            { output.AddRange(conversionConstants); added += conversionConstants.Count; }
            if (i.Opcode == 59 && byVariable.ContainsKey(i.Args[1]))
            {
                if (i.Args.Length != 3 || i.Args[2] != 0)
                    throw new InvalidDataException("Source DOF UAV variable has unexpected initialization/storage.");
                if (!declared) { output.AddRange(declarations); declared = true; added += declarations.Length; }
                output.Add(Make(59, storagePointer, i.Args[1], 2)); added++;
            }
            else if (i.Opcode == 61 && imageLoads.ContainsKey(i.Args[1])) { }
            else if (i.Opcode == 60)
            {
                // Coordinate already equals native byteAddress >> 2. Keep
                // original result ID so AtomicIAdd operands are untouched.
                output.Add(Make(65, wordPointer, i.Args[1], i.Args[2], zero, i.Args[3])); added++;
            }
            else if (i.Opcode == 95)
            {
                int beforeFetch = output.Count;
                uint[] a = i.Args;
                uint query = Next(); output.Add(Make(103, int2Type, query, a[2], intZero));
                uint baseX = Next(), baseY = Next(), width = Next(), height = Next();
                output.Add(Make(81, intType, baseX, a[3], 0));
                output.Add(Make(81, intType, baseY, a[3], 1));
                output.Add(Make(81, intType, width, query, 0));
                output.Add(Make(81, intType, height, query, 1));
                uint checkedX = baseX, checkedY = baseY;
                uint condition = Binary(167, boolType, Binary(175, boolType, baseX, intZero), Binary(175, boolType, baseY, intZero));
                int[] offsets = a.Length == 7 ? a[1] switch { 111 => [1, 0], 123 => [0, 1], 134 => [1, 1], _ => throw new InvalidDataException("Unrecognized fetch offset.") } : [0, 0];
                if (a.Length == 7)
                {
                    uint effective = Binary(128, int2Type, a[3], a[6]);
                    checkedX = Next(); checkedY = Next();
                    output.Add(Make(81, intType, checkedX, effective, 0));
                    output.Add(Make(81, intType, checkedY, effective, 1));
                    // Reject signed overflow in base+offset as well. Every
                    // actual source coordinate is nonnegative GlobalID*2 +
                    // selected quadrant; there is no negative-to-positive
                    // offset rescue in this exact graph.
                    uint effectiveNonnegative = Binary(167, boolType,
                        Binary(175, boolType, checkedX, intZero), Binary(175, boolType, checkedY, intZero));
                    condition = Binary(167, boolType, condition, effectiveNonnegative);
                }
                condition = Binary(167, boolType, condition, Binary(167, boolType,
                    Binary(177, boolType, checkedX, width), Binary(177, boolType, checkedY, height)));
                uint good = Next(), bad = Next(), merge = Next(), fetched = Next();
                output.Add(Make(247, merge, 0)); output.Add(Make(250, condition, good, bad));
                output.Add(Make(248, good));
                uint[] guarded = a.ToArray(); guarded[1] = fetched;
                output.Add(new(95, guarded));
                output.Add(Make(249, merge)); output.Add(Make(248, bad)); output.Add(Make(249, merge));
                output.Add(Make(248, merge)); output.Add(Make(245, float4, a[1], fetched, good, float4Zero, bad));
                var input = fetchInputs[a[1]]; uint register = a[1] == 422 ? 0u : 1u;
                fetchBounds.Add(new(register, register == 0 ? 59 : 62, input.VariableId,
                    input.DescriptorSet!.Value, input.Binding!.Value, a[1], a[2], a[3], a[5], a[4],
                    a.Length == 7 ? a[6] : null, offsets[0], offsets[1], query, intZero, condition,
                    good, bad, merge, fetched, float4Zero,
                    "exact-CS46-Texture2D.Load-original-image/coordinate/load-bitcast-LOD-and-ConstOffset-retained;source-r1/r5.w-LOD0-default-graph-pinned;real-bound-view-LOD0-ImageQuerySizeLod;signed-base/effective-XY-nonnegative-and-effectiveXY<viewExtent;guard-before-fetch-structured-selection;outside-full-float4-positive-zero;no-lane-return;original-shared/barrier/atomic-order-retained"));
                added += output.Count - beforeFetch;
            }
            else if (i.Opcode == 99)
            {
                uint register = imageLoads[i.Args[0]], variable = descriptors[register].VariableId;
                uint coordinate = i.Args[1], value = i.Args[2];
                if (register == 0)
                {
                    if (!clear || ResultType(instructions, value) != uint4 || ResultType(instructions, coordinate) != uintType)
                        throw new InvalidDataException("Raw DOF ImageWrite lost its exact scalar uint coordinate/value domain.");
                    uint scalar = Next(), pointer = Next();
                    output.Add(Make(81, uintType, scalar, value, 0));
                    output.Add(Make(65, wordPointer, pointer, variable, zero, coordinate));
                    output.Add(Make(62, pointer, scalar)); added += 3;
                }
                else
                {
                    if (ResultType(instructions, value) != float4 ||
                        instructions.Single(t => t.Opcode == 21 && t.Args[0] == ResultType(instructions, coordinate)) is not { Args: [_, 32, 1] })
                        throw new InvalidDataException("Typed DOF ImageWrite lost its full float4/signed32 texel coordinate.");
                    int beforeWrite = output.Count;
                    uint unsignedCoordinate = Next(), word0 = Next(), word1 = Next();
                    output.Add(Make(124, uintType, unsignedCoordinate, coordinate));
                    output.Add(Make(132, uintType, word0, unsignedCoordinate, two));
                    output.Add(Make(128, uintType, word1, word0, one));
                    uint x = HalfBits(value, 0), y = HalfBits(value, 1), z = HalfBits(value, 2), w = HalfBits(value, 3);
                    uint packedXy = Binary(197, uintType, x, Binary(196, uintType, y, C(16)));
                    uint packedZw = Binary(197, uintType, z, Binary(196, uintType, w, C(16)));
                    uint pointer0 = Next(), pointer1 = Next();
                    output.Add(Make(65, wordPointer, pointer0, variable, zero, word0));
                    output.Add(Make(65, wordPointer, pointer1, variable, zero, word1));
                    output.Add(Make(62, pointer0, packedXy));
                    output.Add(Make(62, pointer1, packedZw)); added += output.Count - beforeWrite;
                }
            }
            else { output.Add(i); retained++; }
        }
        if (!declared || !decorated) throw new InvalidDataException("DOF storage declarations were not inserted.");
        byte[] adapted = Serialize(defaultTranslation.Bytes.AsSpan(0, 20), nextId, output);
        var after = SpirvShaderInterfaceReader.Read(adapted);
        if (after.Descriptors.Count != before.Descriptors.Count ||
            System.Text.Json.JsonSerializer.Serialize(after.StageVariables) != System.Text.Json.JsonSerializer.Serialize(before.StageVariables) ||
            !after.EntryPoints[0].InterfaceIds.SequenceEqual(before.EntryPoints[0].InterfaceIds))
            throw new InvalidDataException("DOF adapter altered stage IO or descriptor cardinality.");
        foreach (var d in after.Descriptors)
        {
            var original = before.Descriptors.Single(x => x.VariableId == d.VariableId);
            if (!byVariable.ContainsKey(d.VariableId))
            {
                // Interface records contain nested lists; use the unchanged
                // declaration instructions as the byte identity proof below.
                if (d.Name != original.Name || d.StorageClass != original.StorageClass ||
                    d.DescriptorSet != original.DescriptorSet || d.Binding != original.Binding ||
                    d.RequiredBufferBytes != original.RequiredBufferBytes || d.Type.Kind != original.Type.Kind)
                    throw new InvalidDataException("DOF adapter altered a readonly SRV or constant buffer.");
            }
            else if (d.StorageClass != 2 || d.DescriptorSet != original.DescriptorSet || d.Binding != original.Binding ||
                d.Type is not { Kind: "struct", BufferBlock: true, Block: false, Members: { Count: 1 } } ||
                d.Type.Members[0] is not { Offset: 0, Type: { Kind: "runtime-array", ArrayStride: 4,
                    ElementType: { Kind: "int", Bits: 32, Signed: false } } })
                throw new InvalidDataException("DOF adapted output is not an exact uint32/stride4 SSBO.");
        }
        var proofs = descriptors.Select(pair => new OriginalMapPostEffect3DofSpriteStorageDescriptorProof(
            source.StageIndex, pair.Key, pair.Key == 0 ? 8u : 4u,
            5 + (int)pair.Key, pair.Key == 0 ? 0u : 10u, pair.Value.VariableId,
            pair.Value.DescriptorSet!.Value, pair.Value.Binding!.Value, 2, 4,
            pair.Key == 0 ? 4u : 8u, pair.Key == 0 ? 1u : 2u,
            pair.Key == 0 ? writes[0] : 0, atomics[pair.Key], pair.Key == 0 ? 0 : writes[pair.Key],
            pair.Key == 0 ? "source-CS45/46-raw-u0;original-byte-address>>2-preserved;uint32-SSBO;atomic-operand-IDs-unchanged" :
                "source-CS46-float4-uav;original-native-buffer6/7-view-DXGI10-RGBA16F;texel-index*2;D3D11.3-3.2.2/22.4.9-typed-store-explicit-uint-binary16-RTZ;finite-overflow-signed-max-finite;Inf-preserved;NaN-class-preserved-high-payload-quiet;denorm/signed-zero-preserved;all-four-components;two-uint32-stores;8B/texel"))
            .OrderBy(x => x.SourceRegister).ToArray();
        return new(adapted, source.Sha256, defaultTranslation.SpirvSha256, Hash(adapted), proofs,
            retained, imageLoads.Count, added,
            clear ? "exact-original-3SPK1000-CS45;official-default-vkd3d-translation-pinned;DXBC-unchanged;all-non-UAV-instructions-byte-identical;no-GPU-completion-or-initialized-contents-claim" :
                "exact-original-3SPK1000-CS46;official-default-vkd3d-translation-pinned;DXBC-unchanged;five-original-Texture2D.Load-sites-bound-view-LOD0-pre-fetch-bounds-guard-and-full-zero;typed-store-D3D11-RTZ;original-math/loops/branches/shared/atomics/barrier-instructions-byte-identical;no-early-return-or-GPU-completion/initialized-contents-claim")
            { FetchBoundsProofs = fetchBounds.AsReadOnly() };
    }

    private sealed record Instruction(ushort Opcode, uint[] Args);
    private static Instruction Make(ushort opcode, params uint[] args) => new(opcode, args);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static uint Constant(List<Instruction> instructions, uint type, uint value) =>
        instructions.Single(i => i.Opcode == 43 && i.Args.SequenceEqual(new uint[] { type, i.Args[1], value })).Args[1];
    private static uint ResultType(List<Instruction> instructions, uint id) =>
        instructions.Single(i => i.Opcode is 43 or 44 or 61 or 79 or 81 or 124 or 128 or 132 or 194 &&
            i.Args.Length >= 2 && i.Args[1] == id).Args[0];
    private static List<Instruction> Read(ReadOnlySpan<byte> bytes)
    {
        var output = new List<Instruction>();
        for (int at = 20; at < bytes.Length;)
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
            int count = checked((int)(word >> 16));
            if (count == 0 || count > (bytes.Length - at) / 4)
                throw new InvalidDataException("Truncated source DOF SPIR-V instruction.");
            uint[] args = new uint[count - 1];
            for (int i = 0; i < args.Length; i++) args[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4 + i * 4)..]);
            output.Add(new((ushort)word, args)); at += count * 4;
        }
        return output;
    }
    private static byte[] Serialize(ReadOnlySpan<byte> header, uint bound, List<Instruction> instructions)
    {
        byte[] bytes = new byte[20 + instructions.Sum(i => 4 + i.Args.Length * 4)];
        header.CopyTo(bytes); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), bound);
        int at = 20;
        foreach (var i in instructions)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), checked((uint)(i.Args.Length + 1) << 16) | i.Opcode); at += 4;
            foreach (uint arg in i.Args) { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), arg); at += 4; }
        }
        return bytes;
    }
}
