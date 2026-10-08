using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalEnvironmentScriptInitialValue(uint Value, ushort Tag, ushort Auxiliary);
public sealed record OriginalEnvironmentScriptInstruction(int Offset, ushort Opcode, uint? Operand,
    ushort? ExtraOperand, IReadOnlyList<OriginalEnvironmentScriptInitialValue> InitialValues);
public sealed record OriginalEnvironmentScriptHostBinding(string Name, uint NameHash, ushort GlobalSlot);

/// <summary>The finite publication performed by the original Environment Code.
/// Argument two is the VM bank offset of node5's output Data array, supplied by
/// F40050/B4CA60's AA_ signature. It is not a native pointer or a light mask.</summary>
public sealed record OriginalEnvironmentWorldLightPublicationPlan(uint SourceListObjectId,
    int OutputScriptArgument, int DestinationDataIndex, int SourceListItemIndex,
    int SourceStartIndex, int RequestedSourceCount)
{
    public uint ResolveDestination(IReadOnlyList<uint> outputDataObjectIds)
    {
        ArgumentNullException.ThrowIfNull(outputDataObjectIds);
        if (OutputScriptArgument != 2 || !OriginalEnvironmentWorldLightPublicationSource.IsOriginalOutputOrder(outputDataObjectIds) ||
            DestinationDataIndex < 0 || DestinationDataIndex >= outputDataObjectIds.Count ||
            outputDataObjectIds[DestinationDataIndex] != OriginalEnvironmentWorldLightPublicationSource.WorldLightOutputObjectId)
            throw new InvalidDataException("Environment publication requires the actual node5 output descriptor and its world-light Data item.");
        return outputDataObjectIds[DestinationDataIndex];
    }
}

/// <summary>Original source identities and decoded finite script. Scene-load
/// completion inserts Game+18 into Game+10 at 3FD9BE/3FDD68. Node5 F40050 then
/// evaluates the Folder's Code with its input and output Data descriptors.
/// These sources prove publication topology, not a sampled current frame.</summary>
public sealed record LoadedOriginalEnvironmentWorldLightPublicationSource(LoadedObjects FolderResource,
    LoadedObjects ProjectResource, LoadedObjects CodeResource, LoadedTaskGraph TaskGraph,
    IReadOnlyList<uint> OutputDataObjectIds,
    IReadOnlyList<OriginalEnvironmentScriptHostBinding> HostBindings,
    IReadOnlyList<OriginalEnvironmentScriptInstruction> Instructions,
    OriginalEnvironmentWorldLightPublicationPlan Plan)
{
    public string Identity => $"{FolderResource.Source.ResourceUri}@{FolderResource.Source.DecodedSha256}/93A75767|" +
        $"{ProjectResource.Source.ResourceUri}@{ProjectResource.Source.DecodedSha256}/4E508572-RDOK|" +
        $"{CodeResource.Source.ResourceUri}@{CodeResource.Source.DecodedSha256}/4E508572|" +
        $"{TaskGraph.Source.ResourceUri}@{TaskGraph.Source.DecodedSha256}/node5/arguments2";
    public string SourceProvenance => Identity +
        ";TC:3FE9BD/3FD9BE/3FDD68/F400DB/F40264/B4CCA8/B898E1/B4D8B2;" +
        "Environment-Code554B:Find30C0B07F/ListContainer-all/Append-output-argument2-item2";

    public void ValidateSourceUnchanged()
    {
        OriginalEnvironmentWorldLightPublicationSource.VerifyResources(FolderResource, ProjectResource, CodeResource);
        var actual = OriginalEnvironmentWorldLightPublicationSource.ReadOutputDataObjectIds(TaskGraph);
        if (!OutputDataObjectIds.SequenceEqual(actual))
            throw new InvalidDataException("Environment node5 output identity changed after construction.");
    }
}

/// <summary>Reads the pinned Environment program without executing the native
/// game or implementing a general script VM. Unsupported source changes fail
/// explicitly; the original 554-byte program is parsed completely.</summary>
public static class OriginalEnvironmentWorldLightPublicationSource
{
    public const uint FolderResourceId = 0xD2ECCCDF, FolderObjectId = 0x93A75767;
    public const uint ProjectResourceId = 0xD8307CAE, CodeResourceId = 0x4E508572;
    public const uint CodeClassId = 0xE0D88E3F, WorldLightOutputObjectId = 0x175E5FDB;
    public const uint TaskGraphResourceId = 0xBD13A52E;
    public const string TaskGraphSha256 = "470BA20ADB71D936AD3C41ABE2FF7C21BBD6CCF4D8CD707BFF53355840F6482B";
    public const string FolderSha256 = "BEC57B5340D4FE8B4762DDA0B857BFC7717F971391E7B271EFA0728386EF99EE";
    public const string ProjectSha256 = "BF79FE373AF34FF9BDD741D051BAEF9FF7EAF1C52FDC1FAC5BB7CA336790F730";
    public const string CodeSha256 = "4DFDB1F12D3A1ABB4F343FF4CF0955B6960CD78622F5FFE01D64015199034B7A";
    public const string ByteCodeSha256 = "6D975E851E610E8F16806DAA018DB721FF98C8DEBD09E490CD12E2185CA38CDB";

    public static LoadedOriginalEnvironmentWorldLightPublicationSource Load(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Read(repository.LoadObjects("KIDSSystemResource", FolderResourceId),
            repository.LoadObjects("system", ProjectResourceId), repository.LoadObjects("system", CodeResourceId),
            repository.LoadTaskGraph("system", TaskGraphResourceId));
    }

    public static IReadOnlyList<uint> LoadOutputDataObjectIds(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadOutputDataObjectIds(repository.LoadTaskGraph("system", TaskGraphResourceId));
    }

    public static IReadOnlyList<uint> ReadOutputDataObjectIds(LoadedTaskGraph taskGraph)
    {
        ArgumentNullException.ThrowIfNull(taskGraph);
        VerifyAsset(taskGraph.Source, "system", TaskGraphResourceId, TaskGraphSha256);
        var graph = TaskGraphReader.Read(taskGraph.Source.Bytes);
        if (graph.Nodes.Count != 27 || graph.EntryIndex != 0 || graph.RawTrailingBytes.Length != 0)
            throw new InvalidDataException("Environment publication requires the complete original TGK.");
        var node = graph.Nodes[5].Call;
        if (node.LookupKey != 0x55EA3F29 || node.FactoryClassId != 0xD4430275 || node.Arguments.Count != 3 ||
            node.UninterpretedTrailingBytes != 0 || !node.ConnectedNodeIndices.SequenceEqual(new uint[] { 6 }))
            throw new InvalidDataException("Environment publisher must use actual node5's Code evaluator factory.");
        uint[] hashes = [0x2068BEE2, 0xA7DFA945, 0x02EB563C];
        for (int i = 0; i < 3; i++)
        {
            var argument = node.Arguments[i]; int count = i == 2 ? 9 : 1;
            if (argument.Index != i || argument.NameHash != hashes[i] || argument.RawType != 5 ||
                argument.RawElementCount != count || argument.RawValue.Length != count * 4)
                throw new InvalidDataException("Environment evaluator TGK argument descriptor changed.");
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(node.Arguments[0].RawValue) != 0x2710F411 ||
            BinaryPrimitives.ReadUInt32LittleEndian(node.Arguments[1].RawValue) != 0x4BAEAED6)
            throw new InvalidDataException("Environment evaluator TGK selects a different input or parallel parameter.");
        uint[] values = Enumerable.Range(0, 9).Select(i =>
            BinaryPrimitives.ReadUInt32LittleEndian(node.Arguments[2].RawValue.AsSpan(i * 4, 4))).ToArray();
        if (!IsOriginalOutputOrder(values)) throw new InvalidDataException("Environment output Data order differs from actual TGK node5.");
        return Array.AsReadOnly(values);
    }

    internal static bool IsOriginalOutputOrder(IReadOnlyList<uint> values) => values.SequenceEqual(new uint[]
        { 0x127E9E0D, 0x38D04576, 0x175E5FDB, 0xE65D70AE, 0xE1B6A687, 0x36C14145, 0x79BEDC2A, 0xF0792D7D, 0x0DF449AE });

    public static LoadedOriginalEnvironmentWorldLightPublicationSource Read(LoadedObjects folderResource,
        LoadedObjects projectResource, LoadedObjects codeResource, LoadedTaskGraph taskGraph)
    {
        VerifyResources(folderResource, projectResource, codeResource);
        var outputs = ReadOutputDataObjectIds(taskGraph);
        var folder = DokReader.Read(folderResource.Source.Bytes).Objects.Single(o => o.ObjectId == FolderObjectId);
        if (folder.Kind != DokObjectKind.Inline || folder.ClassId != 0x63026395 || folder.Properties.Count != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(Field(folder, 0, 0xE21FF09F, 5, 1).RawValue) != CodeResourceId)
            throw new InvalidDataException("Environment Folder must retain its exact declared Code member.");
        var project = DokReader.Read(projectResource.Source.Bytes).Objects;
        if (project.Count != 1 || project[0].ObjectId != CodeResourceId || project[0].Kind != DokObjectKind.Reference ||
            project[0].Reference != new DokReference(CodeResourceId, CodeResourceId) || project[0].Properties.Count != 0)
            throw new InvalidDataException("Environment script project must retain the formal Code RDOK target.");
        var objects = DokReader.Read(codeResource.Source.Bytes).Objects;
        if (objects.Count != 1 || objects[0].ObjectId != CodeResourceId || objects[0].Kind != DokObjectKind.Inline ||
            objects[0].ClassId != CodeClassId || objects[0].Properties.Count != 6)
            throw new InvalidDataException("Environment publication requires the exact inline Code prototype.");
        var code = objects[0];
        byte[] bytes = Field(code, 0, 0x7AE38475, 1, 554).RawValue;
        if (Convert.ToHexString(SHA256.HashData(bytes)) != ByteCodeSha256)
            throw new InvalidDataException("Environment Code bytecode differs from the source-proven program.");
        _ = Field(code, 1, 0x6DE669DF, 1, 0);
        _ = Field(code, 2, 0xBEF7C3E9, 5, 0);
        byte[] hostReferences = Field(code, 3, 0x418A81B1, 5, 4).RawValue;
        if (!hostReferences.AsSpan().SequenceEqual(Convert.FromHexString("E1302A9F335DFB58D7ABB23B97EBED04")) ||
            !Field(code, 4, 0x39A84A47, 1, 8).RawValue.AsSpan().SequenceEqual(Convert.FromHexString("1010100420042000")))
            throw new InvalidDataException("Environment Code host references or evaluator parameters changed.");
        _ = Field(code, 5, 0x6274D665, 5, 0);
        var (bindings, instructions) = Parse(bytes);
        var byOffset = instructions.ToDictionary(i => i.Offset);
        Require(0x1A2, 0x29); Require(0x1A8, 0x1B, 3); Require(0x1C0, 0x1B, 4);
        Require(0x1E2, 0x29, uint.MaxValue); Require(0x1E8, 0x50, 0);
        Require(0x1EC, 0x2A); Require(0x1EE, 0x2A); Require(0x1F0, 0x1B, 2);
        Require(0x200, 0x3E, 0x00020002); Require(0x206, 0x29);
        Require(0x20C, 0x3E, 0x00010001); Require(0x212, 0x2A);
        Require(0x214, 0x29, uint.MaxValue); Require(0x21A, 0x1B, 1);
        var plan = new OriginalEnvironmentWorldLightPublicationPlan(byOffset[0x1A2].Operand!.Value, 2,
            checked((int)byOffset[0x206].Operand!.Value), 0, 0, -1);
        if (plan.SourceListObjectId != 0x30C0B07F || plan.DestinationDataIndex != 2)
            throw new InvalidDataException("Environment Code publication operands do not match their actual source.");
        _ = plan.ResolveDestination(outputs);
        return new(folderResource, projectResource, codeResource, taskGraph, outputs, bindings, instructions, plan);

        void Require(int offset, ushort opcode, uint? operand = null)
        {
            if (!byOffset.TryGetValue(offset, out var instruction) || instruction.Opcode != opcode ||
                operand.HasValue && instruction.Operand != operand)
                throw new InvalidDataException($"Environment source operation {offset:X4} differs from the proven script.");
        }
    }

    internal static void VerifyResources(LoadedObjects folder, LoadedObjects project, LoadedObjects code)
    {
        Verify(folder, "KIDSSystemResource", FolderResourceId, FolderSha256);
        Verify(project, "system", ProjectResourceId, ProjectSha256);
        Verify(code, "system", CodeResourceId, CodeSha256);
    }

    private static void Verify(LoadedObjects resource, string archive, uint id, string hash)
    {
        ArgumentNullException.ThrowIfNull(resource);
        VerifyAsset(resource.Source, archive, id, hash);
    }

    private static void VerifyAsset(LoadedRdbAsset asset, string archive, uint id, string hash)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.ResourceUri != GameResourceId.Rdb(archive, id).ToString() || asset.FileId != id ||
            asset.OverrideSource is not null || asset.DecodedSha256 != hash ||
            Convert.ToHexString(SHA256.HashData(asset.Bytes)) != hash)
            throw new InvalidDataException("Environment publication needs the exact original source identity and bytes.");
    }

    private static DokProperty Field(DokObject source, int ordinal, uint hash, uint type, int count)
    {
        var p = source.Properties[ordinal];
        if (p.Index != ordinal || p.NameHash != hash || p.TypeCode != type || p.ElementCount != count ||
            p.RawValue.Length != checked(count * (type == 1 ? 1 : 4)) || source.Properties.Count(q => q.NameHash == hash) != 1)
            throw new InvalidDataException($"Environment Code property {ordinal}/{hash:X8} changed.");
        return p;
    }

    private static (IReadOnlyList<OriginalEnvironmentScriptHostBinding>, IReadOnlyList<OriginalEnvironmentScriptInstruction>) Parse(byte[] bytes)
    {
        int position = 0;
        ushort U16() { Need(2); ushort v = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position)); position += 2; return v; }
        uint U32() { Need(4); uint v = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(position)); position += 4; return v; }
        void Need(int n) { if (n < 0 || position > bytes.Length - n) throw new InvalidDataException("Truncated Environment bytecode."); }
        Need(1); int stampLength = bytes[position++]; Need(stampLength);
        string timestamp = Encoding.ASCII.GetString(bytes, position, stampLength); position += stampLength;
        if (timestamp != "2011-04-29T14:26:48+0900" || U16() != 5) throw new InvalidDataException("Environment bytecode header changed.");
        var names = new (string Name, uint Hash)[5];
        for (int i = 0; i < names.Length; i++)
        {
            int length = U16(); uint hash = U32(); Need(length + 1);
            if (bytes[position + length] != 0) throw new InvalidDataException("Environment host name terminator changed.");
            names[i] = (Encoding.ASCII.GetString(bytes, position, length), hash); position += length + 1;
        }
        if (U16() != 5) throw new InvalidDataException("Environment host binding count changed.");
        var bindings = new List<OriginalEnvironmentScriptHostBinding>();
        for (int i = 0; i < 5; i++)
        {
            int nameIndex = U16(); ushort globalSlot = U16(), kind = U16();
            if (nameIndex >= names.Length || kind != 1) throw new InvalidDataException("Environment host binding changed.");
            bindings.Add(new(names[nameIndex].Name, names[nameIndex].Hash, globalSlot));
        }
        string[] expectedNames = ["kids::base::AddObjectArrayToListContainer", "kids::base::ObjectArrayWithListContainer",
            "kids::base::ObjectArrayWithFind", "kids::base::IsObjectArrayExist", "mud::from_kidshost::EvaluateScript"];
        if (!bindings.Select(v => v.Name).SequenceEqual(expectedNames) ||
            !bindings.Select(v => v.GlobalSlot).SequenceEqual(new ushort[] { 1, 2, 3, 4, 5 }) ||
            U16() != 0 || U16() != 0 || U16() != 1 || U16() != 4 || U16() != 4 ||
            U16() != 3 || U16() != 2 || U16() != 1 || U16() != 0 || U16() != 0 || U16() != 0)
            throw new InvalidDataException("Environment program metadata differs from the parsed original.");
        uint codeLength = U32(); position = (position + 1) & ~1;
        if (position != 0x128 || codeLength != 258 || position + codeLength != bytes.Length)
            throw new InvalidDataException("Environment program body boundary changed.");
        var instructions = new List<OriginalEnvironmentScriptInstruction>();
        while (position < bytes.Length)
        {
            int offset = position; ushort opcode = U16();
            int width = opcode switch
            {
                0x01 or 0x1B or 0x35 or 0x3B or 0x50 or 0x51 => 2,
                0x22 or 0x24 or 0x29 or 0x3D or 0x3E => 4,
                0x3F => 6,
                0x02 or 0x03 or 0x28 or 0x2A or 0x34 or 0x52 => 0,
                _ => throw new NotSupportedException($"Environment opcode {opcode:X4} has no finite source decoder.")
            };
            uint? operand = width == 2 ? U16() : width >= 4 ? U32() : null;
            ushort? extra = width == 6 ? U16() : null;
            var initial = new List<OriginalEnvironmentScriptInitialValue>();
            if (opcode == 0x35)
                for (uint i = 0; i < operand!.Value; i++) initial.Add(new(U32(), U16(), U16()));
            instructions.Add(new(offset, opcode, operand, extra, initial.AsReadOnly()));
        }
        if (instructions.Count != 42 || instructions[^1].Opcode != 0x52 || instructions[^1].Offset != 0x228)
            throw new InvalidDataException("Environment program was not completely decoded.");
        return (bindings.AsReadOnly(), instructions.AsReadOnly());
    }
}
