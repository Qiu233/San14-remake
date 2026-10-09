using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum TerrainRendererKind
{
    OrdinaryMaterial
}

/// <summary>Current View19C8 item and its actual item+8 WorldPQ. This is an
/// independent native owner publication; a Kind1ViewRecord.Mask78 or its
/// shared LightOwnerIdentity cannot create this packet implicitly.</summary>
public sealed record TerrainCurrentKind1WorldPQPublication(long FrameNumber,
    string ViewIdentity, string CurrentQueueOwnerIdentity, int CurrentItemIndex,
    ulong CurrentItemTagWord, string CurrentItemIdentity, string CurrentWorldPQOwnerIdentity,
    uint CurrentWorldPQWord78, bool? CurrentItemPlus8AndWorldPQWord78Retained,
    string CurrentProducerProvenance);

/// <summary>One current owner binding for the source-created native kind1
/// record. Actual queue order is provided by its owner, never inferred from
/// source mask order; the Light owner is shared, each record owner is distinct.</summary>
public sealed record TerrainSourceCreatedKind1Owner(uint SourceMask78,
    int CurrentItemIndex, ulong CurrentItemTagWord, string CurrentItemIdentity,
    string CurrentWorldPQOwnerIdentity, bool? FreshSourceConstructorAndKind1BankBindingRetained,
    bool? NoCurrentRecord78Assignment);

public static class TerrainSourceCreatedKind1WorldPQOwners
{
    /// <summary>CFD964 constructs the record; CFD994 puts that same pointer at
    /// item+8; CFD9E0 copies the pair and CFDA36 publishes it at View19C8.
    /// Thus native D0681E and D324C7 access the same record's DWORD78.</summary>
    public static IReadOnlyList<TerrainCurrentKind1WorldPQPublication> PrepareCurrent(
        MapSourceCreatedViewFieldsPacket view, string currentQueueOwnerIdentity,
        IReadOnlyList<TerrainSourceCreatedKind1Owner> completeCurrentOwners,
        string currentProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(view); ArgumentNullException.ThrowIfNull(completeCurrentOwners);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentQueueOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentProducerProvenance);
        var records = view.Kind1Records;
        if (view.Camera.FrameNumber <= 0 || view.CurrentPublication.FrameNumber != view.Camera.FrameNumber ||
            view.CurrentPublication.ViewIdentity != view.Binding.ViewIdentity || records.Count == 0 ||
            !records.Select(r => r.Mask78).Order().SequenceEqual(completeCurrentOwners.Select(o => o.SourceMask78).Order()) ||
            completeCurrentOwners.Select(o => o.CurrentItemIndex).Distinct().Count() != records.Count ||
            completeCurrentOwners.Select(o => o.CurrentItemIdentity).Distinct(StringComparer.Ordinal).Count() != records.Count ||
            completeCurrentOwners.Select(o => o.CurrentWorldPQOwnerIdentity).Distinct(StringComparer.Ordinal).Count() != records.Count ||
            completeCurrentOwners.Any(o => o.CurrentItemIndex < 0 || unchecked((uint)(o.CurrentItemTagWord >> 28)) != 1 ||
                string.IsNullOrWhiteSpace(o.CurrentItemIdentity) || string.IsNullOrWhiteSpace(o.CurrentWorldPQOwnerIdentity) ||
                o.CurrentWorldPQOwnerIdentity == view.Binding.LightOwnerIdentity ||
                o.FreshSourceConstructorAndKind1BankBindingRetained != true || o.NoCurrentRecord78Assignment != true))
            throw new InvalidDataException("Source-created kind1 WorldPQ needs complete distinct current record owners and retained constructor/bank/78 bindings.");
        string provenance = currentProducerProvenance + ";" + view.SourceProvenance +
            ";CE31D0/D653F0/D65620/CFD2D0/D0681E-source-mask78;" +
            "CFD964-record/CFD994-item8/CFD9E0-pair/CFDA36-View19C8;D31F8F/D324C7-same-record78";
        return Array.AsReadOnly(completeCurrentOwners.Select(o => new TerrainCurrentKind1WorldPQPublication(
            view.Camera.FrameNumber, view.Binding.ViewIdentity, currentQueueOwnerIdentity, o.CurrentItemIndex,
            o.CurrentItemTagWord, o.CurrentItemIdentity, o.CurrentWorldPQOwnerIdentity, o.SourceMask78, true, provenance)).ToArray());
    }
}

/// <summary>The current owned ordinary F8 renderer, its source call arguments
/// and its complete fresh canonical GBuffer lifecycle. These gates establish
/// a conditional source recipe, not current Main node execution.</summary>
public sealed record TerrainFreshOwnedStencilPublication(long FrameNumber,
    string ViewIdentity, string CameraOwnerIdentity, string CurrentLandscapeRecordIdentity,
    string SelectedF8RendererOwnerIdentity, string SelectedShaderSha256,
    TerrainRendererKind RendererKind, byte CurrentCallFlags, uint CurrentExclusion2B8,
    bool? FirstOverrideParameterPresent, bool? SecondOverrideParameterPresent,
    bool? SourceConstructorAndStartupRegistryRetained,
    bool? CompleteCreatedPluginChainAndDefaultCallbacksUnchanged,
    bool? CurrentSelectedF8AndBindingsUnchanged, bool? NoExternalHandlerOrStencilAssignment,
    string CurrentProducerProvenance);

/// <summary>Private-construction source recipe for the real ordinary F8
/// consumer. A Scene must separately bind its accepted material/patch/context
/// receipts and prove activation; this packet is not a draw receipt.</summary>
public sealed class TerrainOrdinaryNativeStencilReceipt
{
    internal TerrainOrdinaryNativeStencilReceipt(TerrainOrdinaryNativeStencilSource source,
        TerrainCurrentViewPublication terrain, TerrainCurrentKind1WorldPQPublication world,
        TerrainFreshOwnedStencilPublication current)
    { Source = source; Terrain = terrain; CurrentWorldPQ = world; CurrentPublication = current; }
    public TerrainOrdinaryNativeStencilSource Source { get; }
    public TerrainCurrentViewPublication Terrain { get; }
    public TerrainCurrentKind1WorldPQPublication CurrentWorldPQ { get; }
    public TerrainFreshOwnedStencilPublication CurrentPublication { get; }
    public long FrameNumber => Terrain.FrameNumber;
    public string ViewIdentity => Terrain.ViewIdentity;
    public bool DrawTimeStencilEnabled => true;
    public uint NativeCompareCode => 7;
    public uint RawWorldPQReference78 => CurrentWorldPQ.CurrentWorldPQWord78;
    // B044B1 explicitly zero-extends R8B into the command word while keeping
    // the original full DWORD in its context cache at118.
    public byte Reference => unchecked((byte)RawWorldPQReference78);
    public byte ReadMask => 255;
    public byte WriteMask => 255;
    public uint NativeFailOperation => 0;
    public uint NativeDepthFailOperation => 0;
    public uint NativePassOperation => 2;
    public bool StencilEnabledAfterPrimitiveCleanup => false;
    public string RecipeScope => "conditional-native-ordinary-Landscape-F8;Main-activation-requires-separate-execution-receipt";
    public string SourceProvenance => Source.SourceProvenance + ";" + CurrentWorldPQ.CurrentProducerProvenance +
        ";" + CurrentPublication.CurrentProducerProvenance + ";D2E443-stencil-disable;no-GPU-or-Main-activation-claim";
}

/// <summary>Actual Field Landscape128/DC52 source and ordinary material renderer.
/// It never substitutes the class18 PB2 stencil continuation or equates a
/// lighting mask descriptor with an unproven current WorldPQ pointer.</summary>
public sealed class TerrainOrdinaryNativeStencilSource
{
    public const string CanonicalShaderSha256 = "837B0FB842CEE4F358F34D36BB676C94F252AED67E2822A6228D39B06C93EF33";
    private readonly LoadedShaderContainer shader;
    private readonly byte[] acceptedShaderBytes;
    private TerrainOrdinaryNativeStencilSource(TerrainViewPublicationSource terrain,
        LoadedShaderContainer actualShader)
    { TerrainSource = terrain; shader = actualShader; acceptedShaderBytes = actualShader.Source.Bytes.ToArray(); }
    public TerrainViewPublicationSource TerrainSource { get; }
    public string ShaderUri => shader.Source.ResourceUri;
    public string ShaderSha256 => CanonicalShaderSha256;
    public TerrainRendererKind RendererKind => TerrainRendererKind.OrdinaryMaterial;
    public uint NativePluginHeadOffset => 0x2D10;
    public string SourceKey => TerrainSource.SourceIdentity + "|" + ShaderUri + "@" + ShaderSha256;
    public string SourceProvenance => SourceKey + ";extraCount0/ptr0;ordinary-material-renderer;" +
        "Landscape128/F8;View19C8-kind1-item8-WorldPQ78/View1BB8;" +
        "flags4clear/10set;stencil-enable/0-0-2/Always-current78-FF/writeFF;" +
        "GBuffer-only-J2D10-fixed-default-counter";

    public static TerrainOrdinaryNativeStencilSource ReadFreshFieldScene(LoadedTerrainShaderResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var terrain = TerrainViewPublicationSource.ReadFieldScene(resources.Parameters);
        var source = resources.Shader.Source;
        if (resources.Parameters.Wrapper.Source.OverrideSource is not null || source.OverrideSource is not null ||
            source.ResourceUri != GameResourceId.Rdb("system", 0xDC52FD1C).ToString() || source.FileId != 0xDC52FD1C ||
            source.DecodedSha256 != CanonicalShaderSha256 ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != CanonicalShaderSha256)
            throw new InvalidDataException("Ordinary Landscape stencil requires canonical unchanged Field/DC52 source bytes.");
        var module = G2ShaderContainerReader.Read(source.Bytes);
        var configs = G1ShaderConfigurationReader.Read(source.Bytes, module).Records;
        if (configs.Count != 1 || configs[0].Name != "GBuffer" || configs[0].NameHash != 0xA3D05FFB ||
            configs[0].RawSecondWord != 2 || configs[0].Parameters.Count != 77)
            throw new InvalidDataException("DC52 does not identify the proved single required created GBuffer chain.");
        return new(terrain, resources.Shader);
    }

    public TerrainOrdinaryNativeStencilReceipt PrepareCurrent(TerrainCurrentViewPublication terrain,
        TerrainCurrentKind1WorldPQPublication world, TerrainFreshOwnedStencilPublication current)
    {
        ArgumentNullException.ThrowIfNull(terrain); ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(current); ValidateSourceUnchanged(); terrain.Source.ValidateSourceUnchanged();
        if (terrain.Source.SourceIdentity != TerrainSource.SourceIdentity || terrain.Source.Exclusion2B8 != TerrainSource.Exclusion2B8 ||
            terrain.FrameNumber <= 0 || world.FrameNumber != terrain.FrameNumber || current.FrameNumber != terrain.FrameNumber ||
            world.ViewIdentity != terrain.ViewIdentity || current.ViewIdentity != terrain.ViewIdentity ||
            current.CameraOwnerIdentity != terrain.CameraOwnerIdentity || world.CurrentItemIndex < 0 ||
            unchecked((uint)(world.CurrentItemTagWord >> 28)) != 1 ||
            world.CurrentItemPlus8AndWorldPQWord78Retained != true ||
            new[] { world.CurrentQueueOwnerIdentity, world.CurrentItemIdentity, world.CurrentWorldPQOwnerIdentity,
                world.CurrentProducerProvenance, current.CurrentLandscapeRecordIdentity, current.SelectedF8RendererOwnerIdentity,
                current.CurrentProducerProvenance }.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Terrain F8 stencil needs same-frame typed Landscape and actual current kind1 item+8 WorldPQ publication.");
        if ((current.CurrentCallFlags & 0x10) == 0 || (current.CurrentCallFlags & (4 | 8)) != 0 ||
            current.FirstOverrideParameterPresent is not bool first || current.SecondOverrideParameterPresent is not bool second ||
            (first && second) || current.CurrentExclusion2B8 != TerrainSource.Exclusion2B8 ||
            (current.CurrentExclusion2B8 & world.CurrentWorldPQWord78) != 0)
            throw new NotSupportedException("Only the admitted original ordinary F8 flags/override/exclusion branch has a closed stencil recipe.");
        if (current.SelectedShaderSha256 != ShaderSha256 || current.RendererKind != RendererKind ||
            current.SourceConstructorAndStartupRegistryRetained != true ||
            current.CompleteCreatedPluginChainAndDefaultCallbacksUnchanged != true ||
            current.CurrentSelectedF8AndBindingsUnchanged != true || current.NoExternalHandlerOrStencilAssignment != true)
            throw new InvalidDataException("Current F8 source stencil requires its actual fresh renderer/plugin/callback/binding lifecycle.");
        return new(this, terrain, world, current);
    }

    public void ValidateSourceUnchanged()
    {
        TerrainSource.ValidateSourceUnchanged();
        if (!acceptedShaderBytes.AsSpan().SequenceEqual(shader.Source.Bytes))
            throw new InvalidDataException("DC52 changed after ordinary stencil source construction.");
    }
}
