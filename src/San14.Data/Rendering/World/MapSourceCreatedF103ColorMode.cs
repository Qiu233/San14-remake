using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum MapSourceCreatedF103ColorScope
{
    FreshSourceNamedModulesWithoutHeaderEdits,
    ExplicitCurrentHeader
}

/// <summary>Original F103 color branch and admission result. The source recipe
/// resolves the required branch bits, without fabricating other header bits.</summary>
public sealed class MapSourceCreatedF103ColorPacket
{
    internal MapSourceCreatedF103ColorPacket(long frame, uint mode, bool enabled,
        uint contextFlags, uint knownMask, uint knownBits, MapSourceCreatedF103ColorScope scope,
        string provenance)
    { FrameNumber = frame; NativeColorMode = mode; PreparationEnabled = enabled;
      ContextFlags3F0 = contextFlags; KnownHeaderFlagMask = knownMask; KnownHeaderFlags = knownBits;
      Scope = scope; SourceProvenance = provenance; }
    public long FrameNumber { get; }
    public uint NativeColorMode { get; }
    public bool PreparationEnabled { get; }
    public uint ContextFlags3F0 { get; }
    public uint KnownHeaderFlagMask { get; }
    public uint KnownHeaderFlags { get; }
    public MapSourceCreatedF103ColorScope Scope { get; }
    public string SourceProvenance { get; }
}

/// <summary>Validated immutable source color branch, retained once per source
/// shader generation. Per-frame preparation does not hash the38MB program.</summary>
public sealed class MapSourceCreatedF103ColorSource
{
    internal MapSourceCreatedF103ColorSource(string provenance)
    { SourceProvenance = provenance; }
    public string SourceProvenance { get; }
    public MapSourceCreatedF103ColorPacket Prepare(MainMapContextCurrentPacket context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.FrameNumber <= 0)
            throw new InvalidDataException("F103 color requires the current accepted Host context frame.");
        return new(context.FrameNumber, 0x60, true, context.ContextFlags3F0, 0x214, 4,
            MapSourceCreatedF103ColorScope.FreshSourceNamedModulesWithoutHeaderEdits,
            context.SourceProvenance + ";" + SourceProvenance);
    }
}

/// <summary>SC EAE238..EAE33D / TC EAD248..EAD34D color producer.
/// A fresh source Refraction module sets header.bit2; source ThinSurfaceBSDF
/// TransSpecSrc=0 and TransTarget=0 leave bits4/9 clear. Thus the actual fresh
/// non-ocean node166 branch produces60, not the diagnostic colorMode0.</summary>
public static class MapSourceCreatedF103ColorMode
{
    public static MapSourceCreatedF103ColorPacket PrepareFreshSource(
        LoadedDeferredTexturePlan source, MainMapContextCurrentPacket context,
        MapSourceCreatedF103ColorScope explicitScope) => ReadFreshSource(source, explicitScope).Prepare(context);

    public static MapSourceCreatedF103ColorSource ReadFreshSource(
        LoadedDeferredTexturePlan source, MapSourceCreatedF103ColorScope explicitScope)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (explicitScope != MapSourceCreatedF103ColorScope.FreshSourceNamedModulesWithoutHeaderEdits)
            throw new InvalidDataException("Fresh F103 color requires source named-module construction without later header edits.");
        if (source.Node.Index != 166 || source.Node.Call.FactoryClassId != 0xF103DB00 ||
            source.ShaderReference.ShaderObject.ClassId != 0x8687085E ||
            source.Graph.Source.DecodedSha256 != MapSourceCreatedShaderSelection.GraphSha256 ||
            source.Shader.Source.FileId != 0x9D8E811F ||
            source.Shader.Source.DecodedSha256 != MapSourceCreatedShaderSelection.ShaderSha256 ||
            source.Shader.Source.OverrideSource is not null ||
            Convert.ToHexString(SHA256.HashData(source.Shader.Source.Bytes)) != MapSourceCreatedShaderSelection.ShaderSha256 ||
            Convert.ToHexString(SHA256.HashData(source.Graph.Source.Bytes)) != MapSourceCreatedShaderSelection.GraphSha256)
            throw new InvalidDataException("F103 color requires the exact original node166 and9D source program generation.");
        var node = RenderGraphReader.Read(source.Graph.Source.Bytes).Nodes[166];
        var ocean = node.Call.Arguments.Single(a => a.NameHash == 0xE8F3DC36);
        if (ocean.DescriptorWord != 0x01000001 || ocean.RawValue.Length != 1 || ocean.RawValue[0] != 0)
            throw new InvalidDataException("Source color recipe requires the exact original non-ocean F103 constructor branch.");
        var configurations = source.Shader.ReadConfigurations();
        if (configurations.Records.Count(r => r.Name == "Refraction") != 1)
            throw new InvalidDataException("Source color recipe requires the exact registered Refraction module.");
        var thin = configurations.Records.Single(r => r.Name == "ThinSurfaceBSDF");
        if (thin.Find(0xFEB72A6D)?.RawValue != 0 || thin.Find(0x2BC1B848)?.RawValue != 0 ||
            thin.Find(0x42B7E6DD)?.RawValue != 0)
            throw new InvalidDataException("Source color recipe requires the original ThinSurface TransSpecSrc/AtDistance/TransTarget values.");
        // The selected class8687085E factory+78 callback159D80 invokes
        //1593F0 at15A065. It zeroes header0..7, then calls C14A60.
        // C14D07 sets bit2 because
        // Refraction exists; C13D8D sets bit4 only for TransSpecSrc=1;
        // C13E27 sets bit9 only for a ThinSurface TransTarget parameter=2.
        // Bits6/7 are not consumed once the bit2&&bit4 gate is false.
        return new("source=9D8E811F:" + source.Shader.Source.DecodedSha256 +
            ";factory=class8687085E/TC138B2E8+78;creator=TC159D80/SC159B30;" +
            "fresh-header=TC:15A065/1593F0;Refraction-bit2=TC:C14D07;" +
            "ThinTransSpecSrc0-bit4clear=TC:C13D8D;ThinTransTarget0-bit9clear=TC:C13E27;" +
            "F103-nonocean-color60=TC:EAD326..EAD345");
    }

    /// <summary>Projection after an explicitly owned current complete header
    /// flag word, or current null-header selection. No source construction is
    /// inferred for this route. Enable false means native stops preparation.</summary>
    public static MapSourceCreatedF103ColorPacket ReadExplicitCurrent(
        long frameNumber, uint? actualHeaderFlags4, uint currentContextFlags3F0,
        bool actualOceanBranch, string currentProducerProvenance)
    {
        if (frameNumber <= 0 || string.IsNullOrWhiteSpace(currentProducerProvenance))
            throw new InvalidDataException("Current F103 color requires its accepted frame and complete current producer provenance.");
        uint flags = actualHeaderFlags4 ?? 0;
        bool bit2 = (flags & 4) != 0, colorGate = bit2 && (flags & 0x10) != 0;
        uint mode = 0; bool enabled = true;
        if (colorGate)
        {
            if ((flags & 0x80) != 0) { mode = 0x30; enabled = (currentContextFlags3F0 & 0x08000000) != 0; }
            else if ((flags & 0x40) != 0) { mode = 0x20; enabled = (currentContextFlags3F0 & 0x10000000) != 0; }
            else { mode = 0x10; enabled = (currentContextFlags3F0 & 0x20000000) != 0; }
        }
        else if ((flags & 0x200) != 0) { mode = 0x40; enabled = (currentContextFlags3F0 & 0x04000000) != 0; }
        else if (bit2) mode = actualOceanBranch ? 0x50u : 0x60u;
        return new(frameNumber, mode, enabled, currentContextFlags3F0, uint.MaxValue, flags,
            MapSourceCreatedF103ColorScope.ExplicitCurrentHeader, currentProducerProvenance +
            $";current-header={(actualHeaderFlags4 is null ? "null" : flags.ToString("X8"))};TC:EAD248..EAD34D");
    }
}
