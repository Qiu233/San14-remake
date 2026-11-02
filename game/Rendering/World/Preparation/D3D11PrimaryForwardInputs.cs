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
        D3D11CurrentSeasonFrame current, string inputRoot, int viewportWidth, int viewportHeight, D3D11PrimaryForwardCpuResources? staticCpu = null)
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
        var graph = staticCpu is null ? repository.LoadRenderGraph("system", 0x024496EE) : null;
        var common = MapCommonRenderState.FromSettings(
            CommonRenderResources.ImportSettings(repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId)),
            main.FrameNumber, "d3d11-season/settings", "d3d11-season/common", "d3d11-season/view", "fresh-source-before-ambient");
        uint declaredLightGroup;
        if(staticCpu is not null)declaredLightGroup=staticCpu.DeclaredLightGroup;
        else
        {
            var groupArgument=graph!.Document.Nodes[187].Call.Arguments.Single(value=>value.NameHash==0x59BD6FC4);
            if(groupArgument.RawValue.Length!=4)throw new InvalidDataException("Forward light group requires its scalar declaration.");
            declaredLightGroup=Word(groupArgument.RawValue);
        }
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
            Fog:DeferredSceneConstants.PrepareSharedFog(outer.State.Fog,
                SkySceneSourceDefaults.SharedContextInitializer().FogEnabled120,new(firstColor.X,firstColor.Y,firstColor.Z)),
            Ambient:modifierLight.Lighting.PrepareAmbient(ambientRate),
            Occlusion:new(modifierLight.Lighting.Modifier.OcclusionRates));
        var textures = staticCpu is null ? repository.LoadObjects("system", 0x93FB3280) : null;
        int query = unchecked((int)ActualWord(0xA98, 359, 0x32CD5688, 5));
        // C99980's supported normal query1024 selects mode1. Other queries
        // need their actual selector; do not choose a similarly sized resource.
        if (query != 1024) throw new NotSupportedException("Forward needs the selected Sub wrapper for this Main query.");
        var sub = staticCpu?.SubDeclaration ?? RenderTextureResources.Read(textures!, Word(
            graph!.Document.Nodes[187].Call.Arguments.Single(value=>value.NameHash==0xBC31BFD4).RawValue.AsSpan(4,4)));
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
        var numeric = staticCpu?.Numeric ?? D3D11PrimaryForwardNumericInputPreparation.Load(repository);
        var packet = numeric.PrepareFreshRetained(new(lighting, fields, patternExtras,
            new(1f / sub.Width, 1f / sub.Height, sub.Width, sub.Height),
            D3D11PrimaryForwardNumericInputPreparation.FreshAmbientTransformRows(), OwnedOptionalSubCount),
            DeferredClusterFloatLogPath.Sse);
        string baseDirectory = Path.Combine(inputRoot, BaseDirectory), bankDirectory = Path.Combine(inputRoot, NativeDirectory);
        Directory.CreateDirectory(baseDirectory); Directory.CreateDirectory(bankDirectory);
        int eligible;
        File.WriteAllText(Path.Combine(bankDirectory,"base-input.txt"),"../"+BaseDirectory+"\n");
        if(staticCpu is not null)
        {
            // This opening's real retained numeric packet is identical before
            // each template's existing live primitive writers run in Native.
            D3D11PrimaryForwardNumericInputPreparation.WriteNativeTemplate(baseDirectory,packet);
            eligible=staticCpu.EligibleCount;
        }
        else
        {
            var shader=repository.LoadShaderContainer("system",0x8F6C9FB9);
            File.WriteAllText(Path.Combine(baseDirectory,"forward-light-group.txt"),declaredLightGroup.ToString(CultureInfo.InvariantCulture)+"\n");
            File.WriteAllBytes(Path.Combine(baseDirectory,"vs.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,0).ToArray());
            File.WriteAllBytes(Path.Combine(baseDirectory,"ps.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,11).ToArray());
            D3D11PrimaryForwardNumericInputPreparation.WriteNativeRanges(baseDirectory,packet);
            D3D11PrimaryPassStaticResources.WriteLightingLayout(shader,baseDirectory);
            D3D11PrimaryPassStaticResources.WritePatternLayout(shader,baseDirectory);
            eligible=WriteTemplates(primary,packet,baseDirectory);
            File.WriteAllText(Path.Combine(bankDirectory,"template-count.txt"),primary.TemplateCount.ToString(CultureInfo.InvariantCulture)+"\n");
            File.WriteAllLines(Path.Combine(bankDirectory,"templates.txt"),File.ReadAllLines(Path.Combine(baseDirectory,"templates.txt")));
            D3D11PrimaryPassStaticResources.WriteAuxiliary(repository,resources.Source.Locale,shader,textures!,baseDirectory);
        }
        D3D11PrimaryForwardAllocationFields allocation;
        if(staticCpu is not null)allocation=staticCpu.Allocation;
        else
        {
        var view = repository.LoadObjects("system", DefaultView0Source.ViewResourceId).Document.Objects
            .Single(value => value.ObjectId == DefaultView0Source.ViewObjectId);
        uint U(uint hash, int lane = 0) => Word(view.Properties.Single(value => value.NameHash == hash).RawValue.AsSpan(lane * 4, 4));
            allocation=new(U(0x91221B9A), U(0x2103C71C), U(0x3C7FF710), U(0x2563818C), U(0x2563818C, 1), U(0x2563818C, 2),
                U(0x739F83E1), checked((uint)sub.Width), checked((uint)sub.Height), checked((uint)sub.ArrayDepth!.Value),RenderTextureFormatMath.Resolve(sub));
        }
        D3D11PrimaryForwardNumericInputPreparation.WriteNativeAllocation(baseDirectory,allocation,viewportWidth,viewportHeight);
        numeric.WriteNativeFreshCountZeroSparseInputs(baseDirectory, 1, 1);
        return eligible;
    }

    private static int WriteTemplates(D3D11CityGateTemplateBank primary,
        D3D11PrimaryForwardNumericPacket packet, string directory)
    {
        var rows = new SortedDictionary<int, string>(); int eligibleCount = 0;
        foreach (var template in primary.Forward.Templates)
        {
            int index = template.Index;
            if (!template.Eligible) { rows.Add(index, $"{index} 0 -"); continue; }
            string name = "template-" + index.ToString(CultureInfo.InvariantCulture), folder = Path.Combine(directory, name);
            D3D11PrimaryForwardNumericInputPreparation.WriteNativeTemplate(folder, packet);
            // Only the small prepared layout is copied beside this opening's
            // actual numeric packet; no model resources or old CBs are read.
            File.WriteAllLines(Path.Combine(folder, "layout.txt"), template.Layout);
            rows.Add(index, $"{index} 1 {name}"); eligibleCount++;
        }
        if (rows.Count != primary.TemplateCount || rows.Keys.Where((value, index) => value != index).Any())
            throw new InvalidDataException("Forward bank template bindings are incomplete.");
        File.WriteAllText(Path.Combine(directory, "template-count.txt"), primary.TemplateCount.ToString(CultureInfo.InvariantCulture) + "\n");
        File.WriteAllLines(Path.Combine(directory, "templates.txt"), rows.Values);
        return eligibleCount;
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
