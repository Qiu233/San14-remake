using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>Fresh native Forward inputs from the actual loaded primary bank
/// and this request's Main/season/light owners. Primitive and shadow matrices
/// are written by Native before the first draw, never borrowed from old CBs.</summary>
public static class D3D11PrimaryForwardInputs
{
    public const string NativeDirectory = "primary-forward-current-bank-input";
    private const string BaseDirectory = "primary-forward-source-input";
    // The owned renderer has no optional wrappers or local light banks.
    // RenderCore::publishCurrentView writes its actual 1BFC count to zero.
    private const uint OwnedOptionalSubCount = 0;
    // Source Cluster global construction, before temporary scale setters.
    private const float FreshClusterScalingDivisor = 1;

    public static int WriteFreshNativeInputs(GameResourceRepository repository,
        D3D11CityGateTemplateBank primary, D3D11CurrentSeasonResources resources,
        D3D11CurrentSeasonFrame current, string inputRoot, int viewportWidth, int viewportHeight)
    {
        var main = current.Mode.AcceptedMainFrame.Camera;
        var package = current.Rgb.Inputs.Source.Selection.FirstPackage;
        if (current.Rgb.Inputs.Source.Selection.SecondPackage is not null)
            throw new NotSupportedException("Forward initialization requires the actual unpaired Main lens owner.");
        var words = current.PreparedWords.ToDictionary(word => word.Offset, word => word.Bits);
        uint ActualWord(int offset, int ordinal, uint hash, uint type)
        {
            if (words.TryGetValue(offset, out uint value)) return value;
            if (main.CurrentSeasonPostEffectScalars is { } scalars && scalars.TryReadWord(offset, out value)) return value;
            return Word(Field(package.Camera, ordinal, hash, type));
        }
        float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(current.PreparedMain.Span.Slice(at, 4));
        if (unchecked((int)ActualWord(0xA00, 24, 0x0611DBC4, 4)) <= 0)
            throw new NotSupportedException("Forward requires the current normal receiver's selected light writer.");
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var common = MapCommonRenderState.FromSettings(
            CommonRenderResources.ImportSettings(repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId)),
            main.FrameNumber, "d3d11-season/settings", "d3d11-season/common", "d3d11-season/view", "fresh-source-before-ambient");
        var groupArgument=graph.Document.Nodes[187].Call.Arguments.Single(value=>value.NameHash==0x59BD6FC4);
        if(groupArgument.RawValue.Length!=4)throw new InvalidDataException("Forward light group requires its scalar declaration.");
        uint declaredLightGroup=Word(groupArgument.RawValue);
        uint selector=common.ViewGroupSelector is >0?common.ViewGroupSelector.Value:declaredLightGroup&0x7FFFFFFF;
        uint outerMask = 1u << (unchecked((int)selector - 1) & 31);
        var outer = current.Lights.First(light => (light.GroupMask & outerMask) != 0);
        var inner = current.Lights.FirstOrDefault(light => (light.GroupMask & 2) != 0) ?? outer;
        var preparedLighting=outer.Lighting.PrepareLighting(inner.Lighting,common.SuppressAmbient).Lighting;
        var modifierLight=inner.Lighting.Modifier.Present?inner:outer;
        float ambientRate = Float(Field(package.Camera, 977, 0x08367252, 8), 0);
        var firstColor=preparedLighting.Lights[0].Color;
        var lighting = new DeferredSceneInputs(
            Lighting:preparedLighting,
            Camera:DeferredSceneConstants.ReadPreparedCamera(current.PreparedMain.Span.Slice(0x10,0x130)),
            Fog:DeferredSceneConstants.ReadSharedFog(outer.CompleteData.Span[..0x1B0],
                SkySceneSourceDefaults.SharedContextInitializer().FogEnabled120,new(firstColor.X,firstColor.Y,firstColor.Z)),
            Ambient:modifierLight.Lighting.PrepareAmbient(ambientRate),
            Occlusion:new(modifierLight.Lighting.Modifier.OcclusionRates));
        var textures = repository.LoadObjects("system", 0x93FB3280);
        int query = unchecked((int)ActualWord(0xA98, 359, 0x32CD5688, 5));
        // C99980's supported normal query1024 selects mode1. Other queries
        // need their actual selector; do not choose a similarly sized resource.
        if (query != 1024) throw new NotSupportedException("Forward needs the selected Sub wrapper for this Main query.");
        var subKeys = graph.Document.Nodes[187].Call.Arguments.Single(value => value.NameHash == 0xBC31BFD4).RawValue;
        var sub = RenderTextureResources.Read(textures, Word(subKeys.AsSpan(4, 4)));
        // These are factory entry values. Native applies this draw's actual
        // special or ordinary camera and modifier before uploading the fields.
        var pattern = LoadedDeferredEarlyIndirectPatternMapSource.Load(repository, resources.Source.Locale)
            .ConstructAfterSourceFactoryPreparation();
        Vector4[] patternExtras = [Vector(pattern.PatternPlaneSubAndPattern1.Span, 0), Vector(pattern.PatternPlaneSubAndPattern1.Span, 16)];
        var fields = new D3D11PrimaryForwardMainFields(F(0x140), F(0x144),
            (Word(current.PreparedMain.Span.Slice(0x158, 4)) & 2) != 0,
            BitConverter.UInt32BitsToSingle(ActualWord(0xE84, 166, 0xE836FC0A, 8)),
            BitConverter.UInt32BitsToSingle(ActualWord(0xE88, 167, 0x0AB376BC, 8)),
            viewportWidth, viewportHeight, FreshClusterScalingDivisor,
            // The fresh owned Q88 is null: D77EC0 borrows Common348 white
            // volume and explicitly publishes 9BC900(0,0).
            0, 0);
        var numeric = D3D11PrimaryForwardNumericInputPreparation.Load(repository);
        var packet = numeric.PrepareFreshRetained(new(lighting, fields, patternExtras,
            new(1f / sub.Width, 1f / sub.Height, sub.Width, sub.Height),
            D3D11PrimaryForwardNumericInputPreparation.FreshAmbientTransformRows(), OwnedOptionalSubCount),
            DeferredClusterFloatLogPath.Sse);
        string baseDirectory = Path.Combine(inputRoot, BaseDirectory), bankDirectory = Path.Combine(inputRoot, NativeDirectory);
        Directory.CreateDirectory(baseDirectory); Directory.CreateDirectory(bankDirectory);
        File.WriteAllText(Path.Combine(baseDirectory,"forward-light-group.txt"),declaredLightGroup.ToString(CultureInfo.InvariantCulture)+"\n");
        var shader = repository.LoadShaderContainer("system", 0x8F6C9FB9);
        File.WriteAllBytes(Path.Combine(baseDirectory, "vs.dxbc"), shader.Container.GetProgramBytes(shader.Source.Bytes, 0).ToArray());
        File.WriteAllBytes(Path.Combine(baseDirectory, "ps.dxbc"), shader.Container.GetProgramBytes(shader.Source.Bytes, 11).ToArray());
        D3D11PrimaryForwardNumericInputPreparation.WriteNativeRanges(baseDirectory, packet);
        WriteLightingLayout(shader, baseDirectory);
        WritePatternLayout(shader, baseDirectory);
        int eligible = WriteTemplates(repository, primary, shader, packet, baseDirectory);
        File.WriteAllText(Path.Combine(bankDirectory, "base-input.txt"), "../" + BaseDirectory + "\n");
        File.WriteAllText(Path.Combine(bankDirectory, "template-count.txt"), primary.TemplateCount.ToString(CultureInfo.InvariantCulture) + "\n");
        File.WriteAllLines(Path.Combine(bankDirectory, "templates.txt"), File.ReadAllLines(Path.Combine(baseDirectory, "templates.txt")));
        WriteAuxiliary(repository, resources.Source.Locale, shader, textures, baseDirectory);
        var view = repository.LoadObjects("system", DefaultView0Source.ViewResourceId).Document.Objects
            .Single(value => value.ObjectId == DefaultView0Source.ViewObjectId);
        uint U(uint hash, int lane = 0) => Word(view.Properties.Single(value => value.NameHash == hash).RawValue.AsSpan(lane * 4, 4));
        D3D11PrimaryForwardNumericInputPreparation.WriteNativeAllocation(baseDirectory,
            new(U(0x91221B9A), U(0x2103C71C), U(0x3C7FF710), U(0x2563818C), U(0x2563818C, 1), U(0x2563818C, 2),
                U(0x739F83E1), checked((uint)sub.Width), checked((uint)sub.Height), checked((uint)sub.ArrayDepth!.Value),
                RenderTextureFormatMath.Resolve(sub)), viewportWidth, viewportHeight);
        numeric.WriteNativeFreshCountZeroSparseInputs(baseDirectory, 1, 1);
        return eligible;
    }

    private static int WriteTemplates(GameResourceRepository repository, D3D11CityGateTemplateBank primary,
        LoadedShaderContainer shader, D3D11PrimaryForwardNumericPacket packet, string directory)
    {
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var signature = shader.ReadSignatures(0).Parts.Single(part => part.Kind == DxbcSignatureKind.Input).Parameters
            .Where(value => value.RawSystemValue == 0).Select(value => (value.SemanticName.Text, value.SemanticIndex)).ToArray();
        string[] models = File.ReadAllLines(Path.Combine(primary.Directory, "models.txt"));
        var rows = new SortedDictionary<int, string>(); int eligibleCount = 0;
        foreach (var profile in primary.Profiles)
        {
            string[] key = profile.AssetSourceKey.Split('|');
            var resource = GameResourceId.Parse(key[1]); string[] parts = resource.Key.Split('/');
            var dictionary = repository.LoadObjects(parts[0], uint.Parse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            var model = ModelTextureResources.Load(repository, dictionary, uint.Parse(key[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            var bindings = ModelShaderResources.LoadBindings(repository, model);
            for (int i = 0; i < profile.DrawIndices.Count; i++)
            {
                int index = profile.TemplateIndices[i], draw = profile.DrawIndices[i];
                var named = ModelShaderResources.ResolveDrawBaseSource(bindings, draw, shaderObjects).Binding.Shader;
                var state0 = ModelShaderResources.ResolveType16Interface(named, 0);
                if (ModelShaderResources.ResolveType16Interface(named, 10).Provider is not null)
                    throw new NotSupportedException("Forward state10 redirect requires its actual owner.");
                var selected = state0.Provider is { } provider ? ModelShaderResources.LoadDeclaredG1S(repository, provider) : null;
                var configuration = selected is null ? null : G1ShaderConfigurationReader.Read(selected.Source.Bytes, selected.Container);
                bool eligible = selected is not null && !selected.Container.NamePrefix.StartsWith("CNS-", StringComparison.Ordinal) &&
                    configuration!.Records.Any(value => value.Name is "AlphaTest" or "Dissolve");
                if (!eligible) { rows.Add(index, $"{index} 0 -"); continue; }
                if (named.Object.ObjectId != 0x2F9960EF || selected!.Source.FileId != shader.Source.FileId)
                    throw new NotSupportedException("Forward template selects another state0 shader family.");
                if (configuration!.Records.Any(value => value.Name == "LightMap"))
                    throw new NotSupportedException("Forward LightMap requires its current primitive Pattern-skip flag producer.");
                var matrix = ModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings, draw)
                    ?? throw new NotSupportedException("Forward template needs its nonrigid owner.");
                if (ModelMatrixBindingSourceReader.Select(shader, matrix).Selection.RowIndex != 0)
                    throw new NotSupportedException("Forward template needs its actual nonzero matrix program.");
                string name = "template-" + index.ToString(CultureInfo.InvariantCulture), folder = Path.Combine(directory, name);
                D3D11PrimaryForwardNumericInputPreparation.WriteNativeTemplate(folder, packet);
                // Resource-derived vertex layout only; no cached CB is read.
                var layout = File.ReadAllLines(Path.Combine(primary.Directory, models[index], "layout.txt")).Where(line =>
                {
                    string[] values = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    return signature.Any(value => value.Text == values[0] && value.SemanticIndex == uint.Parse(values[1], CultureInfo.InvariantCulture));
                }).ToArray();
                if (layout.Length != signature.Length) throw new InvalidDataException("Forward source vertex layout is incomplete.");
                File.WriteAllLines(Path.Combine(folder, "layout.txt"), layout);
                rows.Add(index, $"{index} 1 {name}"); eligibleCount++;
            }
        }
        if (rows.Count != primary.TemplateCount || rows.Keys.Where((value, index) => value != index).Any())
            throw new InvalidDataException("Forward bank template bindings are incomplete.");
        File.WriteAllText(Path.Combine(directory, "template-count.txt"), primary.TemplateCount.ToString(CultureInfo.InvariantCulture) + "\n");
        File.WriteAllLines(Path.Combine(directory, "templates.txt"), rows.Values);
        return eligibleCount;
    }

    private static void WriteLightingLayout(LoadedShaderContainer shader,string directory)
    {
        var forward=shader.ReadResourceDefinitions(11).Buffers.Single(value=>value.Name.Text=="$Globals");
        string[] names=["litDir","litCol","litOcc","ambLit","nDirLit","fog","cFog","Scat",
            "fAmbMapsDORate","vAmbDifScl","vAmbSpcScl","vOccRate","vEye"];
        int[] sizes=[64,64,16,16,4,16,16,64,4,16,16,8,16];
        var rows=new List<string>();
        for(int field=0;field<names.Length;field++)
        {
            var variable=forward.Variables.Single(value=>value.Name.Text==names[field]);
            if(variable.ByteSize!=sizes[field])throw new InvalidDataException("Forward lighting field shape differs: "+names[field]);
            rows.Add(FormattableString.Invariant($"{field} {variable.StartOffset} 0 {variable.ByteSize}"));
        }
        // These rows consume current lighting, modifier occlusion and Eye,
        // independently of HDR layout.
        File.WriteAllLines(Path.Combine(directory,"forward-lighting-writes.txt"),rows);
    }
    private static void WritePatternLayout(LoadedShaderContainer shader, string directory)
    {
        var vertex = shader.ReadResourceDefinitions(0).Buffers.Single(buffer => buffer.Name.Text == "$Globals");
        var pixel = shader.ReadResourceDefinitions(11).Buffers.Single(buffer => buffer.Name.Text == "$Globals");
        var rows = new List<string>();
        foreach (var field in new[] { (Id: 0, Name: "mW2Pt", Bytes: 64, Buffer: vertex),
            (Id: 1, Name: "fPtn", Bytes: 4, Buffer: pixel), (Id: 2, Name: "fPtnPlSub", Bytes: 32, Buffer: pixel) })
        {
            var variable = field.Buffer.Variables.Single(value => value.Name.Text == field.Name);
            if (variable.ByteSize != field.Bytes)
                throw new NotSupportedException("Forward Pattern field shape requires its actual producer: " + field.Name);
            rows.Add(FormattableString.Invariant($"{field.Id} {variable.StartOffset} 0 {variable.ByteSize}"));
        }
        File.WriteAllLines(Path.Combine(directory, "forward-pattern-writes.txt"), rows);
    }
    private static void WriteAuxiliary(GameResourceRepository repository, string locale, LoadedShaderContainer shader,
        LoadedObjects textures, string directory)
    {
        var native = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        var fallback = DeferredSourceFallbackShadowImages.Load(repository, locale);
        var construction = new DeferredFallbackShadowConstruction(
            DeferredFallbackShadowStateScope.SuccessfulFreshGraphicsInitializationUnassignedCommonSettings,
            "native-primary-forward/graphics", "native-primary-forward/settings", true, true);
        var current = new DeferredFallbackShadowCurrentSelection(construction.CommonSettingsOwnerIdentity, true, true);
        var rdef = shader.ReadResourceDefinitions(11);
        foreach (var (kind, slot) in new[] { (DeferredFallbackShadowKind.Point, 8), (DeferredFallbackShadowKind.Spot, 9) })
        {
            var selected = fallback.SamplerInput(kind, rdef.Bindings.Single(value => value.InputType == 2 && value.BindPoint == slot),
                rdef.Bindings.Single(value => value.InputType == 3 && value.BindPoint == slot), construction, current, 0);
            WriteSampler(slot, selected.SourceKey);
            File.WriteAllBytes(Path.Combine(directory, $"fallback{slot}-pixel4.bin"), fallback.LayerPixels(kind));
        }
        File.WriteAllBytes(Path.Combine(directory, "sampler3.bin"), RenderSamplerSourceAdapters.BackendDefaultDescriptor(native));
        WriteSampler(10, RenderSamplerSourceAdapters.RenderTextureKey(native, 1));
        WriteSampler(11, RenderSamplerSourceAdapters.FromAmbientCube(native, CubeRenderTextureResources.Read(textures, 0x23F44EB5)).SourceKey);
        var white = StaticTextureResources.Load(repository, textures, "system", [0x80F9DB7B]).Single();
        File.WriteAllBytes(Path.Combine(directory, "sampler6.bin"), G1TextureSamplerReader.ReadRetainedSource(white.Image).EncodeD3D11Descriptor());
        var depth = DeferredEarlyPredecessorSource.Load(repository, locale).ShadowDepthDeclaration;
        if (depth.PixelFormat != 0x4E) throw new NotSupportedException("Forward requires another depth sampler class.");
        byte[] key = RenderSamplerSourceAdapters.FinalRenderTextureKey(native, depth);
        ushort low = BinaryPrimitives.ReadUInt16LittleEndian(key), high = BinaryPrimitives.ReadUInt16LittleEndian(key.AsSpan(2));
        BinaryPrimitives.WriteUInt16LittleEndian(key, (ushort)((low & 0x3F0) | 0x1405));
        BinaryPrimitives.WriteUInt16LittleEndian(key.AsSpan(2), (ushort)(high | 8));
        WriteSampler(2, key);
        byte[] pointKey = RenderSamplerSourceAdapters.RenderTextureKey(native, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(pointKey, (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(pointKey) & 0x3FF));
        WriteSampler(15, pointKey);
        File.WriteAllLines(Path.Combine(directory, "samplers.txt"), new[] { 2, 3, 6, 8, 9, 10, 11, 15 }.Select(slot => $"{slot} sampler{slot}.bin"));
        File.WriteAllBytes(Path.Combine(directory, "volume-white-pixel4.bin"), [255, 255, 255, 255]);
        void WriteSampler(int slot, byte[] selectedKey) => File.WriteAllBytes(Path.Combine(directory, $"sampler{slot}.bin"),
            RenderSamplerSourceAdapters.DecodeKey(native, selectedKey).EncodeD3D11Descriptor());
    }

    private static byte[] Field(DokObject camera, int ordinal, uint hash, uint type)
    {
        var value = camera.Properties[ordinal];
        if (value.NameHash != hash || value.TypeCode != type || value.ElementCount != 1 || value.RawValue.Length != 4)
            throw new InvalidDataException("Forward current Main declaration has another typed field: " + ordinal);
        return value.RawValue;
    }
    private static uint Word(ReadOnlySpan<byte> value) => BinaryPrimitives.ReadUInt32LittleEndian(value);
    private static float Float(ReadOnlySpan<byte> value, int at) => BinaryPrimitives.ReadSingleLittleEndian(value.Slice(at, 4));
    private static Vector4 Vector(ReadOnlySpan<byte> value, int at) => new(Float(value, at), Float(value, at + 4), Float(value, at + 8), Float(value, at + 12));
}
