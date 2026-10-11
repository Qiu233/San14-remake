using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>Prepare the depth and optional scene copies at node 97 from the
/// actual graph targets and the project's screen-copy programs. Native borrows
/// the geometry depth and then-current HDR images from their physical owners.</summary>
public static class D3D11RefractionCopyInputPreparation
{
    public static void WriteNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current, string directory, int outputWidth, int outputHeight)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var textureObjects = repository.LoadObjects("system", 0x93FB3280);
        var node = graph.Document.Nodes[97];
        if (node.Call.FactoryClassId != 0xFD1176C6)
            throw new NotSupportedException("Refraction copy graph node has an unsupported factory.");
        uint Reference(string name)
        {
            var argument = node.Call.Arguments.Single(value => value.NameHash == ResourceNameHash.Compute(name));
            if (argument.DescriptorWord != 0x05000001 || argument.RawValue.Length != 4)
                throw new InvalidDataException("Refraction copy requires a typed texture reference: " + name);
            return BinaryPrimitives.ReadUInt32LittleEndian(argument.RawValue);
        }
        var geometry = RenderGraphTargetResources.Read(graph, 53, textureObjects);
        uint sourceDepthKey = Reference("SrcDepthTextureObjectNameHash");
        if (geometry.DepthTarget is null || sourceDepthKey != geometry.DepthObjectNameHash || geometry.DepthSurfaceId != 0)
            throw new NotSupportedException("Refraction copy source depth is not the actual geometry depth producer.");
        var sourceDepth = geometry.DepthTarget;
        var output = RenderGraphTargetResources.Read(graph, 73, textureObjects);
        var sourceColor = output.ColorTargets.Single();
        if (sourceColor.Slot != 0 || sourceColor.SurfaceId != 0)
            throw new NotSupportedException("Refraction scene copy requires HDR attachment zero at mip zero.");
        var depthTarget = RenderTextureResources.Read(textureObjects, Reference("DstDepthTextureObjectNameHash"));
        var sceneTarget = RenderTextureResources.Read(textureObjects, Reference("DstSceneTextureObjectNameHash"));
        ValidateSource(sourceDepth);
        ValidateSource(sourceColor.Texture);
        var depthExtent = Extent(depthTarget);
        var sceneExtent = Extent(sceneTarget);
        depthTarget = ImportTarget(depthTarget, depthExtent.Width, depthExtent.Height);
        sceneTarget = ImportTarget(sceneTarget, sceneExtent.Width, sceneExtent.Height);
        var depthFormats = RenderTextureFormatMath.Resolve(depthTarget);
        var sceneFormats = RenderTextureFormatMath.Resolve(sceneTarget);
        if (depthFormats.TargetViewFormat is not (20 or 40) || sceneFormats.TargetViewFormat is 20 or 40)
            throw new NotSupportedException("Refraction copy target formats differ from their depth and color roles.");
        if (sourceDepthKey == depthTarget.Object.ObjectId || sourceColor.ObjectNameHash == sceneTarget.Object.ObjectId)
            throw new InvalidDataException("Refraction copy cannot read and write the same texture.");
        var colorExtent = Extent(sourceColor.Texture);
        string locale = current.Mode.AcceptedMainFrame.Camera.ExecutableLocale;
        var depthProgram = DeferredAuxiliaryDepthCopySource.ReadCurrent(locale);
        var colorProgram = DeferredAuxiliaryStockCopySource.ReadCurrent(repository.SourceDirectory, locale);
        string depthDirectory = Path.Combine(directory, "copy97");
        string sceneDirectory = Path.Combine(directory, "copy-scene97");
        D3D11RefractionScreenCopyMedia.WriteDepth(depthDirectory, depthExtent.Width, depthExtent.Height, depthProgram);
        D3D11RefractionScreenCopyMedia.WriteColor(sceneDirectory, sceneExtent.Width, sceneExtent.Height, colorProgram,
            sceneExtent == colorExtent);
        WriteSource(depthDirectory, sourceDepthKey, sourceDepth);
        WriteSource(sceneDirectory, sourceColor.ObjectNameHash, sourceColor.Texture);
        WriteTarget(depthDirectory, depthTarget, depthExtent.Width, depthExtent.Height);
        WriteTarget(sceneDirectory, sceneTarget, sceneExtent.Width, sceneExtent.Height);
        // The copy PS loads integer pixel coordinates and writes depth. The
        // target extent determines the quad; it never scales depth samples.
        File.WriteAllText(Path.Combine(depthDirectory, "depth.txt"), "1 1 8\n");
        foreach (string path in new[] { depthDirectory, sceneDirectory })
        {
            File.WriteAllText(Path.Combine(path, "raster-cull.txt"), "1\n");
            File.WriteAllText(Path.Combine(path, "blend-enabled.txt"), "0\n");
        }
        // The depth copy itself uses Load, while the Ocean pass samples its
        // resulting wrapper using that target's own sampler declaration.
        byte[] samplerKey = RenderSamplerSourceAdapters.FinalRenderTextureKey(colorProgram.Native, depthTarget);
        File.WriteAllBytes(Path.Combine(depthDirectory, "target-sampler-desc.bin"),
            RenderSamplerSourceAdapters.DecodeKey(colorProgram.Native, samplerKey).EncodeD3D11Descriptor());

        (int Width, int Height) Extent(RenderTextureDefinition texture)
        {
            int width = DeferredAuxiliaryTargetSources.ResolveNativeDimension(texture.Width, outputWidth);
            int height = DeferredAuxiliaryTargetSources.ResolveNativeDimension(texture.Height, outputHeight);
            if (width <= 0 || height <= 0) throw new InvalidDataException("Refraction copy texture has an empty extent.");
            return (width, height);
        }
        void WriteSource(string path, uint key, RenderTextureDefinition texture)
        {
            var formats = RenderTextureFormatMath.Resolve(texture);
            var extent = Extent(texture);
            File.WriteAllText(Path.Combine(path, "source.txt"), FormattableString.Invariant(
                $"{key:X8} {formats.ResourceFormat} {formats.ShaderViewFormat} {extent.Width} {extent.Height}\n"));
        }
    }

    private static void ValidateSource(RenderTextureDefinition texture)
    {
        if (texture.Kind != RenderTextureKind.Texture2D || texture.MultiSample != 0 || texture.MinMipLevel != 0)
            throw new NotSupportedException("Refraction copy requires a single-sample 2D source at mip zero.");
    }

    private static RenderTextureDefinition ImportTarget(RenderTextureDefinition texture, int width, int height)
    {
        ValidateSource(texture);
        // Zero requests the complete chain. It has one mip only at 1x1;
        // these copy owners expose a single destination surface.
        if (texture.LevelNum != 1 && (texture.LevelNum != 0 || width != 1 || height != 1))
            throw new NotSupportedException("Refraction copy destination requires one resolved mip.");
        return texture with { LevelNum = 1 };
    }

    private static void WriteTarget(string path, RenderTextureDefinition texture, int width, int height)
    {
        var formats = RenderTextureFormatMath.Resolve(texture);
        File.WriteAllText(Path.Combine(path, "target.txt"), FormattableString.Invariant(
            $"{formats.ResourceFormat} {formats.ShaderViewFormat} {formats.TargetViewFormat} {width} {height}\n"));
    }
}

/// <summary>Screen-copy media shared by node 97 and the filter's mip-zero
/// copies. Their execution gates, physical textures and state lifetimes remain
/// with their respective Native owners.</summary>
internal static class D3D11RefractionScreenCopyMedia
{
    internal static void WriteDepth(string path, int width, int height, LoadedDeferredAuxiliaryDepthCopySource program)
    {
        WriteGeometryAndPrograms(path, width, height, program.VertexProgram.Dxbc, program.PixelProgram.Dxbc);
        File.WriteAllText(Path.Combine(path, "cbuffers.txt"), "VS 0 vs-b0.bin\n");
        File.WriteAllText(Path.Combine(path, "samplers.txt"), "");
    }

    internal static void WriteColor(string path, int width, int height, LoadedDeferredAuxiliaryStockCopySource program,
        bool equalRectangle)
    {
        WriteGeometryAndPrograms(path, width, height, program.VertexProgram.Dxbc, program.PixelProgram.Dxbc);
        byte[] pixel = new byte[16]; BinaryPrimitives.WriteUInt32LittleEndian(pixel, 0xFF7FFFFF);
        File.WriteAllBytes(Path.Combine(path, "ps-b0.bin"), pixel);
        File.WriteAllText(Path.Combine(path, "cbuffers.txt"), "VS 0 vs-b0.bin\nPS 0 ps-b0.bin\n");
        byte[] key = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(key, equalRectangle ? DeferredAuxiliaryStockCopySource.EqualRectangleSamplerKey :
            DeferredAuxiliaryStockCopySource.ResizedRectangleSamplerKey);
        File.WriteAllBytes(Path.Combine(path, "sampler0-desc.bin"),
            RenderSamplerSourceAdapters.DecodeKey(program.Native, key).EncodeD3D11Descriptor());
        File.WriteAllText(Path.Combine(path, "samplers.txt"), "0 sampler0-desc.bin\n");
    }

    private static void WriteGeometryAndPrograms(string path, int width, int height, byte[] vertex, byte[] pixel)
    {
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "vs.dxbc"), vertex);
        File.WriteAllBytes(Path.Combine(path, "ps.dxbc"), pixel);
        // The retained screen backend starts with unit screen and color scales.
        // These copy owners do not expose a later scale or custom stock shader.
        File.WriteAllBytes(Path.Combine(path, "vs-b0.bin"),
            DeferredAuxiliaryDepthCopySource.BuildStockVertexConstants(width, height, 1, 1, Vector4.One));
        File.WriteAllText(Path.Combine(path, "viewport.txt"), FormattableString.Invariant($"0 0 {width} {height} 0 1\n"));
        File.WriteAllText(Path.Combine(path, "geometry.txt"), "24 6 16 4\n");
        File.WriteAllText(Path.Combine(path, "layout.txt"), "POSITION 0 6 0 0\nCOLOR 0 28 0 12\nTEXCOORD 0 16 0 16\n");
        using (var writer = new BinaryWriter(File.Create(Path.Combine(path, "vertices.bin"))))
            foreach (var point in new (float X, float Y, float U, float V)[]
                { (0, 0, 0, 0), (0, height, 0, 1), (width, height, 1, 1), (width, 0, 1, 0) })
            {
                writer.Write(point.X); writer.Write(point.Y); writer.Write(0f); writer.Write(uint.MaxValue);
                writer.Write(point.U); writer.Write(point.V);
            }
        using (var writer = new BinaryWriter(File.Create(Path.Combine(path, "indices.bin"))))
            foreach (ushort index in new ushort[] { 0, 1, 2, 0, 2, 3 }) writer.Write(index);
    }
}
