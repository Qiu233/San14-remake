using System.Buffers.Binary;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual resource-only Forward media. No numeric packet, current
/// camera, local light storage, depth or target allocation belongs here.</summary>
public static class D3D11PrimaryPassStaticResources
{
    public static D3D11PrimaryForwardCpuResources PrepareForward(GameResourceRepository repository,
        D3D11CityGateStaticResources models,string locale,string directory,CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var shader=repository.LoadShaderContainer("system",0x8F6C9FB9);
        var numeric=D3D11PrimaryForwardNumericInputPreparation.FromShader(shader);
        var cpu=D3D11PrimaryForwardCpuResources.Prepare(repository,numeric,models.Forward.Templates.Count(t=>t.Eligible));
        numeric.WriteNativeRetainedRanges(directory);
        WriteLightingLayout(shader,directory);WritePatternLayout(shader,directory);
        File.WriteAllText(Path.Combine(directory,"forward-light-group.txt"),cpu.DeclaredLightGroup.ToString(CultureInfo.InvariantCulture)+"\n");
        File.WriteAllBytes(Path.Combine(directory,"vs.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,0).ToArray());
        File.WriteAllBytes(Path.Combine(directory,"ps.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,11).ToArray());
        var rows=new List<string>();
        foreach(var template in models.Forward.Templates)
        {
            token.ThrowIfCancellationRequested();
            string name="template-"+template.Index.ToString(CultureInfo.InvariantCulture);
            rows.Add(FormattableString.Invariant($"{template.Index} {(template.Eligible?1:0)} {name}"));
            if(!template.Eligible)continue;
            string target=Path.Combine(directory,name);Directory.CreateDirectory(target);
            File.WriteAllLines(Path.Combine(target,"layout.txt"),template.Layout);
        }
        File.WriteAllText(Path.Combine(directory,"template-count.txt"),models.Forward.Templates.Count.ToString(CultureInfo.InvariantCulture)+"\n");
        File.WriteAllLines(Path.Combine(directory,"templates.txt"),rows);
        WriteAuxiliary(repository,locale,shader,repository.LoadObjects("system",0x93FB3280),directory);
        return cpu;
    }
    public static void WriteLightingLayout(LoadedShaderContainer shader,string directory)
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
    public static void WritePatternLayout(LoadedShaderContainer shader, string directory)
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
    public static void WriteAuxiliary(GameResourceRepository repository, string locale, LoadedShaderContainer shader,
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
        // The current environment image owner supplies its sampler at each draw.
        File.WriteAllLines(Path.Combine(directory, "samplers.txt"), new[] { 2, 3, 6, 8, 9, 10, 15 }.Select(slot => $"{slot} sampler{slot}.bin"));
        File.WriteAllBytes(Path.Combine(directory, "volume-white-pixel4.bin"), [255, 255, 255, 255]);
        void WriteSampler(int slot, byte[] selectedKey) => File.WriteAllBytes(Path.Combine(directory, $"sampler{slot}.bin"),
            RenderSamplerSourceAdapters.DecodeKey(native, selectedKey).EncodeD3D11Descriptor());
    }

}
