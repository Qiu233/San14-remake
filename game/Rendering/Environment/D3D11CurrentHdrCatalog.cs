using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

public sealed record D3D11CurrentHdrWrite(string Name, int Offset, int SourceOffset, int Bytes);
public sealed record D3D11CurrentHdrRoute(D3D11PreparedHdrPass Pass, int Slot,
    int ConstantBytes, bool Arg18,
    bool FogOwnerEnabled, IReadOnlyList<D3D11CurrentHdrWrite> Writes)
{
    public string Identity => Pass.Identity;
    public int Node => Pass.Declaration.NodeIndex;
    public uint OuterMask => Pass.OuterMask;
    public uint InnerMask => Pass.InnerMask;
    public string ResourceDirectory => Pass.ResourceDirectory;
    public string CameraResourceDirectory => Pass.CameraResourceDirectory;
    public int PixelProgramIndex => Pass.Program.PixelProgramIndex;
    public DxbcResourceDefinitions PixelDefinitions => Pass.Program.PixelDefinitions;
}

/// <summary>Typed HDR routes and shader layouts used by current numeric encoding.
/// Camera, material and unused CB bytes retain their existing owners.
/// Media selection belongs to the caller, independently of this layout catalog.</summary>
public sealed class D3D11CurrentHdrCatalog
{
    private static readonly string[] Names = ["litDir", "litCol", "litOcc", "ambLit", "nDirLit",
        "fog", "cFog", "Scat", "fAmbMapsDORate", "vAmbDifScl", "vAmbSpcScl"];
    private static readonly int[] Sizes = [64,64,16,16,4,16,16,64,4,16,16];
    public IReadOnlyList<D3D11CurrentHdrRoute> Routes { get; }
    public float AmbientMapsRate { get; }
    public HdrShaderResources Resources { get; }
    private D3D11CurrentHdrCatalog(HdrShaderResources resources, List<D3D11CurrentHdrRoute> routes, float ambientMapsRate)
    { Resources = resources; Routes = routes.AsReadOnly(); AmbientMapsRate = ambientMapsRate; }

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
        var routes = new List<D3D11CurrentHdrRoute>();
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
            var writes = new List<D3D11CurrentHdrWrite>();
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
            routes.Add(new(pass, slot, globals.ByteSize, common.SuppressAmbient, fogOwner, writes.AsReadOnly()));
        }
        return new(resources, routes, ambientRate);
    }
}
