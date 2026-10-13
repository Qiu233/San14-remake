using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>Prepare node 137 from its resource declarations. Both specular
/// resolutions own their targets and programs; the current camera selects
/// which one executes. Mip zero always receives the then-current HDR image.</summary>
public static class D3D11RefractionFilterInputPreparation
{
    private sealed record Target(RenderTextureDefinition Declaration, int Width, int Height, int Mips)
    {
        internal uint Key => Declaration.Object.ObjectId;
        internal (int Width, int Height) Mip(int level) => (Math.Max(1, Width >> level), Math.Max(1, Height >> level));
    }

    private sealed record GaussianConstantLayout(int ByteCount, int ParametersOffset, int WeightsOffset)
    {
        internal byte[] Pack(byte[] parameters, byte[] weights)
        {
            byte[] bytes = new byte[ByteCount];
            parameters.CopyTo(bytes, ParametersOffset);
            if (WeightsOffset >= 0) weights.CopyTo(bytes, WeightsOffset);
            return bytes;
        }
    }

    private sealed record GaussianProgram(int Vertex, int Pixel,
        GaussianConstantLayout? VertexConstants, GaussianConstantLayout PixelConstants);

    public static void WriteNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current, string directory, int outputWidth, int outputHeight)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var node = graph.Document.Nodes[137];
        if (node.Call.FactoryClassId != 0xC41F73B1)
            throw new InvalidDataException("Refraction filter graph node has an unsupported factory.");
        var textureObjects = repository.LoadObjects("system", 0x93FB3280);
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var hdr = RenderGraphTargetResources.Read(graph, 73, textureObjects).ColorTargets.Single().Texture;
        int hdrWidth = DeferredAuxiliaryTargetSources.ResolveNativeDimension(hdr.Width, outputWidth);
        int hdrHeight = DeferredAuxiliaryTargetSources.ResolveNativeDimension(hdr.Height, outputHeight);
        uint Reference(uint hash)
        {
            var argument = node.Call.Arguments.Single(value => value.NameHash == hash);
            if (argument.DescriptorWord != 0x05000001 || argument.RawValue.Length != 4)
                throw new InvalidDataException("Refraction filter requires a typed resource reference.");
            return BinaryPrimitives.ReadUInt32LittleEndian(argument.RawValue);
        }
        Target ReadTarget(uint hash)
        {
            var declaration = RenderTextureResources.Read(textureObjects, Reference(hash));
            if (declaration.Kind != RenderTextureKind.Texture2D || declaration.MultiSample != 0 || declaration.MinMipLevel != 0)
                throw new InvalidDataException("Refraction filter requires a single-sample 2D target starting at mip zero.");
            int width = DeferredAuxiliaryTargetSources.ResolveNativeDimension(declaration.Width, outputWidth);
            int height = DeferredAuxiliaryTargetSources.ResolveNativeDimension(declaration.Height, outputHeight);
            if (width <= 0 || height <= 0) throw new InvalidDataException("Refraction target has an empty extent.");
            int maximum = 1;
            for (int largest = Math.Max(width, height); largest > 1; largest >>= 1) maximum++;
            int mips = declaration.LevelNum == 0 ? maximum : declaration.LevelNum;
            if (mips > maximum) throw new InvalidDataException("Refraction target mip chain exceeds its extent.");
            return new(declaration with { LevelNum = checked((byte)mips) }, width, height, mips);
        }
        LoadedShaderContainer ReadShader(uint hash)
        {
            var provider = shaderObjects.Document.Objects.Single(value => value.ObjectId == Reference(hash));
            if (provider.Kind != DokObjectKind.Inline || provider.ClassId != 0x01818D62)
                throw new InvalidDataException("Refraction filter requires a shader module provider.");
            var field = provider.Properties.Single(value => value.NameHash == 0x193025DF);
            if (field.TypeCode != 5 || field.ElementCount != 1 || field.RawValue.Length != 4)
                throw new InvalidDataException("Refraction shader requires its declared file reference.");
            return repository.LoadShaderContainer("system", BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue));
        }
        var half = ReadTarget(0x1F5E2225);
        var full = ReadTarget(0xAC84A635);
        var halfScratch = ReadTarget(0x30C9C178);
        var fullScratch = ReadTarget(0xB6114288);
        var blurred = ReadTarget(0x4DAA4058);
        var depthScratch = ReadTarget(0xC0FB7D85);
        var mainDepth = RenderTextureResources.Read(textureObjects, Reference(0x56AB46F0));
        var geometry = RenderGraphTargetResources.Read(graph, 53, textureObjects);
        if (geometry.DepthTarget is null || mainDepth.Object.ObjectId != geometry.DepthObjectNameHash || geometry.DepthSurfaceId != 0)
            throw new NotSupportedException("Refraction filter source depth is not the actual geometry depth producer.");
        var filter = ReadShader(0x29DD7D66);
        var depthFilter = ReadShader(0x877B56E1);
        var common = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId);
        uint mode = CommonRenderResources.ReadScalar(common, 0xD5222D67, 5);
        uint size = CommonRenderResources.ReadScalar(common, 0xF4BAA840, 5);
        float dispersion = BitConverter.UInt32BitsToSingle(CommonRenderResources.ReadScalar(common, 0xFD4C0435, 8));
        int commonDepth = unchecked((int)CommonRenderResources.ReadScalar(common, 0x5692BB6E, 4));
        int cameraDepth = current.Mode.AcceptedMainFrame.Camera.Source.Declaration.RefractionFilters.DepthFilterOverride;
        int depthMode = cameraDepth >= 0 ? cameraDepth : commonDepth;
        if (mode != 1 || depthMode != 1)
            throw new NotSupportedException("Refraction filter currently requires Gaussian specular and depth modes.");
        string locale = current.Mode.AcceptedMainFrame.Camera.ExecutableLocale;
        var stock = DeferredAuxiliaryStockCopySource.ReadCurrent(repository.SourceDirectory, locale);
        var targets = new[] { half, full, halfScratch, fullScratch, blurred, depthScratch };
        if (targets.Select(value => value.Key).Distinct().Count() != targets.Length)
            throw new InvalidDataException("Refraction filter destinations and scratch resources must be distinct.");
        if (halfScratch.Mips != 1 || fullScratch.Mips != 1 || blurred.Mips != 1 || depthScratch.Mips != 1)
            throw new NotSupportedException("Refraction filter scratch and depth targets require one mip.");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "targets.txt"), targets.Select(target =>
        {
            var formats = RenderTextureFormatMath.Resolve(target.Declaration);
            return FormattableString.Invariant($"{target.Key:X8} {target.Width} {target.Height} {target.Mips} {formats.ResourceFormat} {formats.ShaderViewFormat} {formats.TargetViewFormat}");
        }));
        WriteText(directory, "source-depth.txt", $"{mainDepth.Object.ObjectId:X8}\n");
        // These are the screen helper's own state changes. Other raster fields
        // and the enclosing framebuffer remain owned and restored by Native.
        WriteText(directory, "raster-cull.txt", "1\n");
        WriteText(directory, "blend-enabled.txt", "0\n");
        var definitions = targets.ToDictionary(value => value.Key, value => value.Declaration);
        definitions.Add(mainDepth.Object.ObjectId, mainDepth);
        var selectedPrograms = new Dictionary<(LoadedShaderContainer Shader, uint Size, uint Pass), GaussianProgram>();
        var passes = new List<string>();
        WriteSpecular(0, half, halfScratch);
        WriteSpecular(1, full, fullScratch);
        byte[] depthWeights = DeferredAuxiliaryGaussianSchedule.Weights(3, blurred.Width, new Vector4(25));
        WriteGaussian(2, "depth-00", depthFilter, mainDepth.Object.ObjectId, 0, depthScratch, 0,
            blurred.Width, blurred.Height, 3, 0, depthWeights, Parameters(1f / blurred.Width, 0, 0, 0));
        WriteGaussian(2, "depth-01", depthFilter, depthScratch.Key, 0, blurred, 0,
            blurred.Width, blurred.Height, 3, 1, depthWeights, Parameters(1f / depthScratch.Height,
                (float)blurred.Width / depthScratch.Width, (float)blurred.Height / depthScratch.Height, 0));
        File.WriteAllLines(Path.Combine(directory, "passes.txt"), passes);

        void WriteSpecular(int branch, Target target, Target scratch)
        {
            D3D11RefractionScreenCopyMedia.WriteColor(Path.Combine(directory, "mip0-" + branch), target.Width, target.Height, stock,
                target.Width == hdrWidth && target.Height == hdrHeight);
            byte[] weights = DeferredAuxiliaryGaussianSchedule.Weights(size, target.Width, new Vector4(dispersion));
            for (int level = 1; level < target.Mips; level++)
            {
                var viewport = target.Mip(level);
                if (viewport.Width > scratch.Width || viewport.Height > scratch.Height)
                    throw new InvalidDataException("Refraction Gaussian viewport exceeds its scratch target.");
                string prefix = branch == 0 ? "half-" : "full-";
                WriteGaussian(branch, prefix + ((level - 1) * 2).ToString("D2", CultureInfo.InvariantCulture), filter,
                    target.Key, (uint)level - 1, scratch, 0, viewport.Width, viewport.Height, size, 0, weights,
                    Parameters(1f / viewport.Width, 0, 0, 0));
                WriteGaussian(branch, prefix + ((level - 1) * 2 + 1).ToString("D2", CultureInfo.InvariantCulture), filter,
                    scratch.Key, 0, target, (uint)level, viewport.Width, viewport.Height, size, 1, weights,
                    Parameters(1f / scratch.Height, (float)viewport.Width / scratch.Width, (float)viewport.Height / scratch.Height, 0));
            }
        }

        void WriteGaussian(int branch, string name, LoadedShaderContainer shader, uint input, uint inputMip,
            Target target, uint targetMip, int width, int height, uint kernelSize, uint pass, byte[] weights, byte[] parameters)
        {
            if (!selectedPrograms.TryGetValue((shader, kernelSize, pass), out var selected))
            {
                var tables = shader.ReadG1Bindings();
                if (tables.Variants.RecordWidth != 8 || tables.RawDimensionCounts.Take(6).Any(value => value > 1))
                    throw new NotSupportedException("Refraction Gaussian shader requires an unsupported selector dimension.");
                var variant = tables.Variants.Records.FirstOrDefault(value =>
                    BinaryPrimitives.ReadUInt32LittleEndian(value.RawBytes.Span) == 1 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(value.RawBytes.Span[4..]) == kernelSize)
                    ?? throw new NotSupportedException("Refraction Gaussian kernel has no declared shader variant.");
                var combination = tables.ResolveCombination(new uint[] { 0, 0, 0, 0, 0, 0, (uint)variant.Index }, pass);
                var stages = combination.Stages.Where(value => value.Entry is not null).ToArray();
                if (stages.Length != 2 || stages.Any(value => value.StageCode is not (0 or 1)))
                    throw new NotSupportedException("Refraction Gaussian requires an unsupported shader stage.");
                int vertex = stages.Single(value => value.StageCode == 1).Entry!.ProgramIndex;
                int pixel = stages.Single(value => value.StageCode == 0).Entry!.ProgramIndex;
                RequireInterface(shader, vertex, pixel);
                selected = new(vertex, pixel,
                    ImportGaussianConstants(shader.ReadResourceDefinitions(vertex), false, pass == 1, 0),
                    ImportGaussianConstants(shader.ReadResourceDefinitions(pixel), true, true, checked((int)kernelSize + 1))!);
                selectedPrograms.Add((shader, kernelSize, pass), selected);
            }
            string path = Path.Combine(directory, name);
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, "vs.dxbc"), shader.Container.GetProgramBytes(shader.Source.Bytes, selected.Vertex).ToArray());
            File.WriteAllBytes(Path.Combine(path, "ps.dxbc"), shader.Container.GetProgramBytes(shader.Source.Bytes, selected.Pixel).ToArray());
            File.WriteAllBytes(Path.Combine(path, "ps-b0.bin"), selected.PixelConstants.Pack(parameters, weights));
            string constants = "PS 0 ps-b0.bin\n";
            if (pass == 1)
            {
                File.WriteAllBytes(Path.Combine(path, "vs-b0.bin"), selected.VertexConstants!.Pack(parameters, []));
                constants += "VS 0 vs-b0.bin\n";
            }
            WriteText(path, "cbuffers.txt", constants);
            byte[] key = RenderSamplerSourceAdapters.FinalRenderTextureKey(stock.Native, definitions[input]);
            File.WriteAllBytes(Path.Combine(path, "sampler0-desc.bin"), RenderSamplerSourceAdapters.DecodeKey(stock.Native, key).EncodeD3D11Descriptor());
            WriteText(path, "samplers.txt", "0 sampler0-desc.bin\n");
            WriteText(path, "input.txt", FormattableString.Invariant($"0 {input:X8} {inputMip} 1\n"));
            WriteText(path, "target.txt", FormattableString.Invariant($"{target.Key:X8} {targetMip}\n"));
            WriteText(path, "viewport.txt", FormattableString.Invariant($"0 0 {width} {height} 0 1\n"));
            passes.Add(FormattableString.Invariant($"{branch} {name}"));
        }
    }

    private static void RequireInterface(LoadedShaderContainer shader, int vertex, int pixel)
    {
        var inputs = shader.ReadSignatures(vertex).Parts.Where(part => part.Kind == DxbcSignatureKind.Input)
            .SelectMany(part => part.Parameters);
        if (inputs.Any(input => input.RawSystemValue != 6))
            throw new NotSupportedException("Refraction Gaussian vertex program requires an input outside vertex ID.");
        var outputs = shader.ReadSignatures(pixel).Parts.Where(part => part.Kind == DxbcSignatureKind.Output)
            .SelectMany(part => part.Parameters).ToArray();
        if (outputs.Length != 1 || outputs[0].SemanticIndex != 0 || outputs[0].Register != 0 ||
            !outputs[0].SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Refraction Gaussian pixel program requires an output outside its single target.");
    }

    private static GaussianConstantLayout? ImportGaussianConstants(DxbcResourceDefinitions definitions,
        bool pixel, bool hasConstants, int weightElements)
    {
        var constants = definitions.Bindings.Where(value => value.InputType == 0).ToArray();
        if (definitions.Buffers.Count != (hasConstants ? 1 : 0) || constants.Length != (hasConstants ? 1 : 0) ||
            (hasConstants && (constants[0].BindPoint != 0 || constants[0].BindCount != 1)))
            throw new NotSupportedException("Refraction Gaussian constant layout has no producer.");
        var resources = definitions.Bindings.Where(value => value.InputType != 0).ToArray();
        if ((!pixel && resources.Length != 0) || (pixel && (resources.Length != 2 ||
            resources.Any(value => value.BindPoint != 0 || value.BindCount != 1) ||
            resources.Count(value => value.InputType == 2 && value.Dimension == 4) != 1 ||
            resources.Count(value => value.InputType == 3) != 1)))
            throw new NotSupportedException("Refraction Gaussian resource layout has no producer.");
        if (!hasConstants) return null;
        var buffer = definitions.Buffers.Single();
        if (buffer.Variables.Any(field => (field.Flags & 2) != 0 &&
            field.Name.Text != "vGusPrm" && (field.Name.Text != "vGusWgt" || weightElements == 0)))
            throw new NotSupportedException("Refraction Gaussian constant field has no producer.");
        int Field(string name, int elements)
        {
            var field = buffer.Variables.SingleOrDefault(value => value.Name.Text == name)
                ?? throw new NotSupportedException("Refraction Gaussian constant field is missing: " + name);
            var type = definitions.Types.Single(value => value.Offset == field.TypeOffset);
            if (type.Class != 1 || type.BaseType != 3 || type.Rows != 1 || type.Columns != 4 ||
                type.Elements != elements || field.ByteSize != Math.Max(1, elements) * 16)
                throw new NotSupportedException("Refraction Gaussian constant field requires float4 data: " + name);
            return field.StartOffset;
        }
        return new(buffer.ByteSize, Field("vGusPrm", 0), weightElements == 0 ? -1 : Field("vGusWgt", weightElements));
    }

    private static byte[] Parameters(float x, float y, float z, float w)
    {
        byte[] bytes = new byte[16];
        float[] values = [x, y, z, w];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * 4), values[index]);
        return bytes;
    }

    private static void WriteText(string directory, string file, string value)
        => File.WriteAllText(Path.Combine(directory, file), value);
}
