using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum HeightFogFieldSemantic : uint
{
    Projection, InverseView, HeightColors, HeightParameters, Fluctuations, Mask,
    LightDirection, LightColor, Eye, Fog, FogColor, Scattering
}

public enum HeightFogTextureRole : uint { HeightMask, SceneDepth, SceneColor, Acceleration }

public readonly record struct HeightFogProgramKey(int Feature, bool Background,
    int FirstType, int SecondType, int ThirdType);

public sealed record HeightFogConstantField(HeightFogFieldSemantic Semantic, int Offset,
    int WordsPerElement, int Elements, int Stride);

/// <summary>A stage's imported constant layout. An absent buffer has no slot or fields.</summary>
public sealed class HeightFogStageLayout
{
    internal HeightFogStageLayout(DxbcResourceDefinitions definitions)
    {
        var bindings = definitions.Bindings.Where(binding => binding.InputType == 0).ToArray();
        if (definitions.Buffers.Count == 0 && bindings.Length == 0)
        {
            Slot = -1;
            Fields = Array.Empty<HeightFogConstantField>();
            return;
        }
        if (definitions.Buffers.Count != 1 || bindings.Length != 1 || bindings[0].BindCount != 1 ||
            bindings[0].BindPoint >= 14)
            throw new NotSupportedException("Height fog requires one supported constant buffer per stage.");
        var buffer = definitions.Buffers[0];
        if (buffer.Name.Text != bindings[0].Name.Text || buffer.ByteSize <= 0 || buffer.ByteSize % 16 != 0)
            throw new InvalidDataException("Height fog constant buffer declaration and binding disagree.");
        Slot = checked((int)bindings[0].BindPoint);
        ByteSize = checked((int)buffer.ByteSize);
        var fields = new List<HeightFogConstantField>();
        foreach (var variable in buffer.Variables)
        {
            var (semantic, expectedWords, expectedElements) = variable.Name.Text switch
            {
                "vP2V" => (HeightFogFieldSemantic.Projection, 4, 1),
                "mV2W" => (HeightFogFieldSemantic.InverseView, 16, 1),
                "cHFog" => (HeightFogFieldSemantic.HeightColors, 4, 3),
                "hFogParam" => (HeightFogFieldSemantic.HeightParameters, 4, 3),
                "hFogFlucPrms" => (HeightFogFieldSemantic.Fluctuations, 1, 60),
                "hFogMaskPrm" => (HeightFogFieldSemantic.Mask, 4, 1),
                "litDir" => (HeightFogFieldSemantic.LightDirection, 4, 1),
                "litCol" => (HeightFogFieldSemantic.LightColor, 4, 1),
                "vEye" => (HeightFogFieldSemantic.Eye, 4, 1),
                "fog" => (HeightFogFieldSemantic.Fog, 4, 1),
                "cFog" => (HeightFogFieldSemantic.FogColor, 4, 1),
                "Scat" => (HeightFogFieldSemantic.Scattering, 4, 4),
                _ => throw new NotSupportedException("Height fog field has no producer: " + variable.Name.Text)
            };
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            int words = type.Rows * type.Columns, elements = Math.Max(1, (int)type.Elements);
            int stride = type.Elements == 0 ? words * 4 : (words * 4 + 15) & ~15;
            bool shape = semantic == HeightFogFieldSemantic.InverseView
                ? type.Class == 2 && type.Rows == 4 && type.Columns == 4
                : expectedWords == 1
                    ? type.Class == 0 && type.Rows == 1 && type.Columns == 1
                    : type.Class == 1 && type.Rows == 1 && type.Columns == 4;
            if (type.BaseType != 3 || type.Members.Count != 0 || words != expectedWords ||
                !shape || elements != expectedElements || variable.ByteSize != (elements - 1) * stride + words * 4 ||
                variable.StartOffset < 0 || (long)variable.StartOffset + variable.ByteSize > ByteSize ||
                fields.Any(field => field.Semantic == semantic))
                throw new NotSupportedException("Height fog field has an unsupported shape: " + variable.Name.Text);
            fields.Add(new(semantic, variable.StartOffset, words, elements, stride));
        }
        Fields = fields.AsReadOnly();
    }

    public int Slot { get; }
    public int ByteSize { get; }
    public IReadOnlyList<HeightFogConstantField> Fields { get; }

    internal byte[] Pack(IReadOnlyDictionary<HeightFogFieldSemantic, float[]> values)
    {
        byte[] bytes = new byte[ByteSize];
        foreach (var field in Fields)
        {
            if (!values.TryGetValue(field.Semantic, out var supplied) ||
                supplied.Length != field.WordsPerElement * field.Elements)
                throw new NotSupportedException("Height fog is missing its actual field producer: " + field.Semantic);
            for (int element = 0; element < field.Elements; element++)
                for (int word = 0; word < field.WordsPerElement; word++)
                    BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(field.Offset + element * field.Stride + word * 4),
                        supplied[element * field.WordsPerElement + word]);
        }
        return bytes;
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Slot); writer.Write((uint)ByteSize); writer.Write((uint)Fields.Count);
        foreach (var field in Fields)
        {
            writer.Write((uint)field.Semantic); writer.Write((uint)field.Offset);
            writer.Write((uint)field.WordsPerElement); writer.Write((uint)field.Elements); writer.Write((uint)field.Stride);
        }
    }
}

/// <summary>One real shader choice and all layouts and bindings belonging to it.</summary>
public sealed class HeightFogProgram
{
    internal HeightFogProgram(HeightFogProgramKey key, LoadedShaderContainer shader, G2ShaderCombination combination)
    {
        Key = key;
        Shader = shader;
        Programs = new(combination.Index,
            combination.Stages.Single(stage => stage.StageCode == 1).Entry!.ProgramIndex,
            combination.Stages.Single(stage => stage.StageCode == 0).Entry!.ProgramIndex);
        var vertex = shader.ReadResourceDefinitions(Programs.VertexProgram);
        var pixel = shader.ReadResourceDefinitions(Programs.PixelProgram);
        VertexLayout = new(vertex);
        PixelLayout = new(pixel);
        var textures = new List<(HeightFogTextureRole Role, uint Slot)>();
        var samplers = new List<uint>();
        foreach (var binding in pixel.Bindings.Where(binding => binding.InputType != 0))
        {
            if (binding.BindCount != 1 || binding.BindPoint >= 32)
                throw new NotSupportedException("Height fog has an unsupported resource binding range.");
            if (binding.InputType == 3 && binding.Name.Text == "__smpsHFogMask")
                samplers.Add(binding.BindPoint);
            else if (binding.InputType == 2 && binding.Dimension == 4)
                textures.Add((binding.Name.Text switch
                {
                    "sHFogMask" => HeightFogTextureRole.HeightMask,
                    "sSceneDepth" => HeightFogTextureRole.SceneDepth,
                    "sSceneColor" => HeightFogTextureRole.SceneColor,
                    "sS2fAccl" => HeightFogTextureRole.Acceleration,
                    _ => throw new NotSupportedException("Height fog texture has no provider: " + binding.Name.Text)
                }, binding.BindPoint));
            else throw new NotSupportedException("Height fog resource has no provider: " + binding.Name.Text);
        }
        if (vertex.Bindings.Any(binding => binding.InputType != 0))
            throw new NotSupportedException("Height fog vertex texture bindings need their actual producer.");
        Textures = textures.AsReadOnly();
        SamplerSlots = samplers.AsReadOnly();
        var inputs = shader.ReadSignatures(Programs.VertexProgram).Parts
            .Where(part => part.Kind == DxbcSignatureKind.Input).SelectMany(part => part.Parameters).ToArray();
        if (inputs.Length != 1 || inputs[0].SemanticName.Text != "POSITION" ||
            inputs[0].SemanticIndex != 0 || inputs[0].ComponentType != 3 || (inputs[0].ReadWriteMask & ~7) != 0)
            throw new NotSupportedException("Height fog requires its supported full-screen position geometry.");
    }

    public HeightFogProgramKey Key { get; }
    public LoadedShaderContainer Shader { get; }
    public D3D11AtmosphereProgramSelection Programs { get; }
    public HeightFogStageLayout VertexLayout { get; }
    public HeightFogStageLayout PixelLayout { get; }
    public IReadOnlyList<(HeightFogTextureRole Role, uint Slot)> Textures { get; }
    public IReadOnlyList<uint> SamplerSlots { get; }
}

/// <summary>Imports the supported height-layer and raw-light choices once from the resource container.</summary>
public sealed class HeightFogProgramResources
{
    private readonly Dictionary<HeightFogProgramKey, HeightFogProgram> programs = new();
    private MapSeasonCameraPackage? acceptedMaskPackage;
    private DokObject? acceptedMaskMainCamera;

    private HeightFogProgramResources(LoadedShaderContainer shader)
    {
        Shader = shader;
        var tables = shader.ReadG1Bindings();
        for (int background = 0; background <= 1; background++)
            for (int feature = 0; feature <= (background == 0 ? 1 : 0); feature++)
                for (int first = -1; first <= 2; first++)
                    for (int second = -1; second <= 2; second++)
                        for (int third = -1; third <= 2; third++)
                        {
                            int[] words = [1, 1, 0, background, 3, first, second, third, 3, 1];
                            byte[] variant = new byte[words.Length * 4];
                            for (int index = 0; index < words.Length; index++)
                                BinaryPrimitives.WriteInt32LittleEndian(variant.AsSpan(index * 4), words[index]);
                            var row = tables.Variants.Match(variant) ??
                                throw new NotSupportedException("Height fog layer choice is absent from its loaded shader.");
                            var combination = tables.ResolveCombination([(uint)feature, 0, 0, 0, 0, 0, (uint)row.Index], 0);
                            var key = new HeightFogProgramKey(feature, background != 0, first, second, third);
                            programs.Add(key, new(key, shader, combination));
                        }
    }

    public LoadedShaderContainer Shader { get; }
    public IReadOnlyCollection<HeightFogProgram> Programs => programs.Values;
    public HeightFogProgram Select(HeightFogProgramKey key) => programs.TryGetValue(key, out var value) ? value :
        throw new NotSupportedException("The selected height fog program is not installed.");

    public static HeightFogProgramResources Load(GameResourceRepository repository)
    {
        var objects = repository.LoadObjects("system", 0x718CCB8B);
        var module = objects.Document.Objects.Single(value => value.ObjectId == 0x74B12946);
        var field = module.Properties.Single(value => value.NameHash == 0x193025DF);
        if (field.TypeCode != 5 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
            throw new InvalidDataException("Screen-space fog requires its declared shader resource reference.");
        uint file = BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        return new(repository.LoadShaderContainer("system", file));
    }

    public void RequireCurrentMask(MapSourceCreatedSeasonLightingFrame present)
    {
        var package = present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        var mainCamera = present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.Source.Camera;
        if (ReferenceEquals(package, acceptedMaskPackage) && ReferenceEquals(mainCamera, acceptedMaskMainCamera)) return;
        foreach (var camera in new[] { mainCamera, package.Camera })
        {
            var field = camera.Properties.Single(value => value.NameHash == 0xB3B03D99);
            if (field.TypeCode != 5 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                throw new InvalidDataException("HeightFogMaskMap requires a declared texture reference.");
            if (BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue) != 0)
                throw new NotSupportedException("A nonzero height fog mask needs its selected texture resource lifecycle.");
        }
        acceptedMaskPackage = package;
        acceptedMaskMainCamera = mainCamera;
    }

    internal void WriteNativeInputs(string directory)
    {
        Directory.CreateDirectory(Path.Combine(directory, "programs"));
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "program-bank.bin")));
        writer.Write(0x31474648u); writer.Write((uint)programs.Count); // HFG1.
        foreach (var program in programs.Values)
        {
            var key = program.Key;
            writer.Write((uint)key.Feature); writer.Write(key.Background ? 1u : 0u);
            writer.Write(key.FirstType); writer.Write(key.SecondType); writer.Write(key.ThirdType);
            writer.Write(program.Programs.Combination); writer.Write(program.Programs.VertexProgram); writer.Write(program.Programs.PixelProgram);
            program.VertexLayout.Write(writer); program.PixelLayout.Write(writer);
            writer.Write((uint)program.Textures.Count);
            foreach (var binding in program.Textures) { writer.Write((uint)binding.Role); writer.Write(binding.Slot); }
            writer.Write((uint)program.SamplerSlots.Count);
            foreach (uint slot in program.SamplerSlots) writer.Write(slot);
        }
        foreach (int index in programs.Values.SelectMany(program => new[] { program.Programs.VertexProgram, program.Programs.PixelProgram }).Distinct())
            File.WriteAllBytes(Path.Combine(directory, "programs", index + ".dxbc"),
                Shader.Container.GetProgramBytes(Shader.Source.Bytes, index).ToArray());
    }
}
