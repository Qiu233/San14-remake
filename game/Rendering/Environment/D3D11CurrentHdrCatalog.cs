using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using San14.Data;
using San14.Data.Formats;

public sealed record D3D11CurrentHdrWrite(string Name, int Offset, int SourceOffset, int Bytes);
public sealed record D3D11CurrentHdrRoute(string Identity, int Node, int Slot,
    int ConstantBytes, uint OuterMask, uint InnerMask, bool Arg18,
    bool FogOwnerEnabled, IReadOnlyList<D3D11CurrentHdrWrite> Writes)
{
    public string ResourceDirectory { get; init; } = "";
    public string CameraResourceDirectory { get; init; } = "";
    public int PixelProgramIndex { get; init; }
    public DxbcResourceDefinitions? PixelDefinitions { get; init; }
}

/// <summary>Actual imported HDR directories + their selected DXBC layouts.
/// Parse once at load; ordinary requests use current light owners and retain
/// all unrelated camera, material, refraction and normalized unused CB bytes.</summary>
public sealed class D3D11CurrentHdrCatalog
{
    private static readonly string[] Names = ["litDir", "litCol", "litOcc", "ambLit", "nDirLit",
        "fog", "cFog", "Scat", "fAmbMapsDORate", "vAmbDifScl", "vAmbSpcScl"];
    private static readonly int[] Sizes = [64,64,16,16,4,16,16,64,4,16,16];
    public IReadOnlyList<D3D11CurrentHdrRoute> Routes { get; }
    public float AmbientMapsRate { get; }
    public string SourceScope => "original fresh Common/SSFog/retained HDR programs; current runtime owner edits require their own writer";
    private D3D11CurrentHdrCatalog(List<D3D11CurrentHdrRoute> routes, float ambientMapsRate)
    { Routes = routes.AsReadOnly(); AmbientMapsRate = ambientMapsRate; }

    public static D3D11CurrentHdrCatalog Load(GameResourceRepository repository,
        MainMapCameraFrame initialMain, string importedMainDirectory)
    {
        string parent = Path.GetFullPath(Path.Combine(importedMainDirectory, ".."));
        var graphSource = DeferredF103WorldPassAssociations.ReadNode166(
            repository.LoadRenderGraph("system", 0x024496EE));
        var common = MapFreshCommonResources.PrepareSourceCreatedBeforeAmbientPublication(
            MapFreshCommonResources.ReadSourceTemplate(repository.LoadObjects(
                "KIDSSystemResource", MapFreshCommonResources.SourceFileId)),
            initialMain.Camera.FrameNumber, "current-hdr/settings", "current-hdr/common",
            "current-hdr/view", "source-created-before-ambient");
        uint selector = common.CurrentViewAux3F2 is > 0 ? common.CurrentViewAux3F2.Value
            : graphSource.ConstructorFlags594;
        uint outerMask = 1u << (int)((selector - 1) & 31);
        bool fogOwner = SkySceneSourceDefaults.SharedContextInitializer().FogEnabled120;
        var ambient = initialMain.Camera.Source.Camera.Properties[977];
        if (ambient.NameHash != 0x08367252 || ambient.TypeCode != 8 || ambient.RawValue.Length != 4)
            throw new InvalidDataException("Current HDR ambient rate requires source Main977.");
        float ambientRate = BinaryPrimitives.ReadSingleLittleEndian(ambient.RawValue);
        var routes = new List<D3D11CurrentHdrRoute>();
        void AddDirectory(string directory, string passList, bool accessory)
        {
            using var accessoryJson = accessory ? JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "source.json"))) : null;
            foreach (string identity in File.ReadLines(Path.Combine(directory, passList)).Where(row => !string.IsNullOrWhiteSpace(row)))
            {
                string pass = Path.Combine(directory, identity.Trim());
                int[] gate = File.ReadAllText(Path.Combine(pass, "population-gate.txt"))
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                if (gate.Length != 4 || gate[3] != outerMask)
                    throw new InvalidDataException("Actual HDR population gate disagrees with loaded outer Common mask.");
                int node = gate[0]; uint inner = checked((uint)gate[1]);
                string sourceFile = Path.Combine(pass, "source.json");
                using var source = File.Exists(sourceFile) ? JsonDocument.Parse(File.ReadAllBytes(sourceFile)) : null;
                var metadata = source?.RootElement ?? accessoryJson?.RootElement
                    ?? throw new InvalidDataException("Actual HDR pass has no source receipt.");

                byte[] dxbc = File.ReadAllBytes(Path.Combine(pass, "ps.dxbc"));
                var definitions = DxbcResourceDefinitionReader.Read(dxbc);
                var globals = definitions.Buffers.Single(buffer => buffer.Name.Text == "$Globals");
                int slot = checked((int)definitions.Bindings.Single(binding => binding.InputType == 0 &&
                    binding.Name.Text == "$Globals").BindPoint);
                int constantBytes = globals.ByteSize;
                bool normalized383 = node == 166 && metadata.GetProperty("PixelProgramIndex").GetInt32() == 383;
                var active = normalized383 ? DeferredSelected383Constants.ActiveScalarByteOffsets.ToHashSet() : null;
                var writes = new List<D3D11CurrentHdrWrite>();
                for (int i = 0; i < Names.Length; i++)
                {
                    var variable = globals.Variables.SingleOrDefault(field => field.Name.Text == Names[i]);
                    if (variable is null) continue; // Source early/Ocean programs do not declare shared Fog.
                    if (variable.ByteSize != Sizes[i]) throw new InvalidDataException("Current HDR named numeric extent changed: " + Names[i]);
                    for (int local = 0; local < variable.ByteSize;)
                    {
                        if (active is not null && !active.Contains(variable.StartOffset + local)) { local += 4; continue; }
                        int begin = local; local += 4;
                        while (local < variable.ByteSize && (active is null || active.Contains(variable.StartOffset + local))) local += 4;
                        writes.Add(new(Names[i], variable.StartOffset + begin, begin, local - begin));
                    }
                }
                routes.Add(new(identity.Trim(), node, slot, constantBytes, outerMask, inner,
                    common.NativeArg18, fogOwner, writes.AsReadOnly())
                { ResourceDirectory = pass, PixelProgramIndex = metadata.GetProperty("PixelProgramIndex").GetInt32(),
                    CameraResourceDirectory = accessory ? Path.Combine(directory,"camera-source",identity.Trim())
                        : Path.Combine(parent,"deferred-camera-continuous-input",identity.Trim()),
                    PixelDefinitions = definitions });
            }
        }
        string early = Path.Combine(parent, "early-hdr-input");
        AddDirectory(early, "passes.txt", false); AddDirectory(early, "ocean-passes.txt", false);
        AddDirectory(early, "late-passes.txt", false);
        string accessories = Path.Combine(parent, "city-accessory-next-input", "hdr", "initial");
        AddDirectory(accessories, "passes.txt", true); AddDirectory(accessories, "late-passes.txt", true);
        return new(routes, ambientRate);
    }

    /// <summary>Only current field bytes and actual selected destination ranges.
    /// No source I/O, hashes, shader creation or whole CB template copies.</summary>
    public byte[] PrepareCurrentBytes(D3D11CurrentSeasonFrame frame)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(Routes.Count);
        foreach (var route in Routes)
        {
            var fields = frame.DeferredNamedFields(route.OuterMask, route.InnerMask,
                route.Arg18, AmbientMapsRate, route.FogOwnerEnabled);
            byte[] identity = Encoding.UTF8.GetBytes(route.Identity);
            writer.Write(identity.Length); writer.Write(route.Slot);
            writer.Write(route.ConstantBytes); writer.Write(route.Writes.Count); writer.Write(identity);
            foreach (var write in route.Writes)
            { writer.Write(write.Offset); writer.Write(write.Bytes);
                writer.Write(fields[write.Name].AsSpan(write.SourceOffset, write.Bytes)); }
        }
        return stream.ToArray();
    }
}
