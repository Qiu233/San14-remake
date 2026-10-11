using System.Buffers.Binary;
using San14.Data;

/// <summary>Scene lighting policy and prepared HDR media. Numeric layouts are
/// written once from resource programs; current requests borrow only policy.
/// The caller still owns the transitional cached media selection.</summary>
public sealed class D3D11CurrentHdrCatalog
{
    private sealed record LightingWrite(string Name, int Offset, int SourceOffset, int Bytes);
    private static readonly string[] Names = ["litDir", "litCol", "litOcc", "ambLit", "nDirLit",
        "fog", "cFog", "Scat", "fAmbMapsDORate", "vAmbDifScl", "vAmbSpcScl"];
    private static readonly int[] Sizes = [64,64,16,16,4,16,16,64,4,16,16];
    public IReadOnlyList<D3D11PreparedHdrPass> Routes { get; }
    public float AmbientMapsRate { get; }
    public bool SuppressAmbient { get; }
    public bool FogOwnerEnabled { get; }
    public HdrShaderResources Resources { get; }
    private D3D11CurrentHdrCatalog(HdrShaderResources resources, IReadOnlyList<D3D11PreparedHdrPass> routes,
        float ambientMapsRate, bool suppressAmbient, bool fogOwnerEnabled)
    {
        Resources = resources; Routes = routes; AmbientMapsRate = ambientMapsRate;
        SuppressAmbient = suppressAmbient; FogOwnerEnabled = fogOwnerEnabled;
    }

    public static D3D11CurrentHdrCatalog Create(GameResourceRepository repository,
        MainMapCameraFrame initialMain, HdrShaderResources resources,
        IReadOnlyList<D3D11PreparedHdrPass> preparedPasses)
    {
        var common = MapCommonRenderState.FromSettings(
            CommonRenderResources.ImportSettings(repository.LoadObjects(
                "KIDSSystemResource", CommonRenderResources.SourceFileId)),
            initialMain.Camera.FrameNumber, "current-hdr/settings", "current-hdr/common",
            "current-hdr/view", "source-created-before-ambient");
        uint selector = common.ViewGroupSelector is > 0 ? common.ViewGroupSelector.Value
            : resources[HdrShaderResources.MainLightingNode].LightGroup;
        uint outerMask = 1u << (int)((selector - 1) & 31);
        bool fogOwner = SkySceneSourceDefaults.SharedContextInitializer().FogEnabled120;
        const uint ambientMapsRateProperty = 0x08367252;
        var ambient = initialMain.Camera.Source.Camera.Properties.Single(property =>
            property.NameHash == ambientMapsRateProperty);
        if (ambient.TypeCode != 8 || ambient.RawValue.Length != 4)
            throw new InvalidDataException("Main ambient maps rate requires a scalar float declaration.");
        float ambientRate = BinaryPrimitives.ReadSingleLittleEndian(ambient.RawValue);
        foreach (var pass in preparedPasses)
        {
            if (pass.OuterMask != outerMask)
                throw new InvalidDataException("Prepared HDR route disagrees with the loaded Common light selection.");
            int node = pass.Declaration.NodeIndex;
            var definitions = pass.Program.PixelDefinitions;
            var globals = definitions.Buffers.Single(buffer => buffer.Name.Text == "$Globals");
            int slot = checked((int)definitions.Bindings.Single(binding => binding.InputType == 0 &&
                binding.Name.Text == "$Globals").BindPoint);
            bool normalized383 = node == HdrShaderResources.MainLightingNode && pass.Program.PixelProgramIndex == 383;
            var active = normalized383 ? DeferredSelected383Constants.ActiveScalarByteOffsets.ToHashSet() : null;
            var writes = new List<LightingWrite>();
            for (int i = 0; i < Names.Length; i++)
            {
                var variable = globals.Variables.SingleOrDefault(field => field.Name.Text == Names[i]);
                if (variable is null) continue; // Early/Ocean programs do not declare shared Fog.
                if (variable.ByteSize != Sizes[i])
                    throw new InvalidDataException("HDR named field extent changed: " + Names[i]);
                for (int local = 0; local < variable.ByteSize;)
                {
                    if (active is not null && !active.Contains(variable.StartOffset + local)) { local += 4; continue; }
                    int begin = local; local += 4;
                    while (local < variable.ByteSize && (active is null || active.Contains(variable.StartOffset + local))) local += 4;
                    writes.Add(new(Names[i], variable.StartOffset + begin, begin, local - begin));
                }
            }
            WriteLightingLayout(pass.ResourceDirectory, slot, globals.ByteSize, writes);
        }
        return new(resources, Array.AsReadOnly(preparedPasses.ToArray()), ambientRate, common.SuppressAmbient, fogOwner);
    }

    // Layout belongs to program preparation. Current requests carry the same
    // typed light inputs regardless of route identity, shader slot or offsets.
    private static void WriteLightingLayout(string directory, int slot, int constantBytes,
        IReadOnlyList<LightingWrite> writes)
    {
        using var stream = File.Create(Path.Combine(directory, "hdr-lighting-layout.bin"));
        using var writer = new BinaryWriter(stream);
        writer.Write(0x314C4C48u); // HLL1: HDR lighting layout.
        writer.Write(slot); writer.Write(constantBytes); writer.Write(writes.Count);
        foreach (var write in writes)
        {
            writer.Write(Array.IndexOf(Names, write.Name)); writer.Write(write.Offset);
            writer.Write(write.SourceOffset); writer.Write(write.Bytes);
        }
    }
}
