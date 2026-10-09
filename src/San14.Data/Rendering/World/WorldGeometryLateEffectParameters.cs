using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The CPU resource owner and the source member address used by the
/// module. A null owner plus offset10 represents 9BC910's literal10 result;
/// it must not be normalized to a null native member address.</summary>
public readonly record struct WorldGeometryLateResourceMember(object? Owner, int Offset);

/// <summary>Actual finite Aux fields used by D77EC0 and D74500. Null resource
/// references are produced by the settings constructor, loader and two copies,
/// not inferred from an absent image or a null selected primitive shader.</summary>
public sealed record WorldGeometryLateAuxParameters(
    object? Resource338, object? Resource348, uint? Mode408, string SourceProvenance);

/// <summary>Current Q88 fields needed by D77EC0 if that Host publication exists.
/// The earlier geometry phase normally borrows null after the Host begin reset;
/// Sky's distinct Q80 publication is retained and is not queried here.</summary>
public sealed record WorldGeometryLateFogPublication(object? Resource70,
    float CurrentView1024, float CurrentView144);

/// <summary>Cleanup bytes D9..D2 returned by D19050. The actual normal short
/// path writes D6 and D5 even when every primitive shaderState2 is null.</summary>
public readonly record struct WorldGeometryLateEffectCleanup(
    bool D9Colour, bool D8HostColour, bool D7Range, bool D6Fog,
    bool D5Fallback, bool D4CameraResource, bool D3Global, bool D2Other);

/// <summary>9BC520's real registered40B module. Uninitialized weights are kept
/// unknown until the source writer runs; no parameter getter reads them first.</summary>
public sealed class WorldGeometryLateFogModuleState
{
    internal WorldGeometryLateFogModuleState() { }
    public WorldGeometryLateResourceMember Member18 { get; private set; }
    public object? Resource20 { get; private set; }
    public Vector2? Weights28 { get; private set; }
    public void SetResource(object? currentResource)
    {
        // These are managed strong loans. Native increment/decrement transfers
        // the module's reference; it does not free the parent resource owner.
        Member18 = new(currentResource, 0x10); Resource20 = currentResource;
    }
    public void SetWeights(float first, float second) => Weights28 = new(first, second);
}

/// <summary>9AED40/9AEFF0's real registered280B module CPU storage. Source
/// setters update this box; constant-buffer group commit is a later binding
/// operation and is not invented when shaderState2 resolves null.</summary>
public sealed class WorldGeometryLateFallbackModuleState
{
    private readonly Matrix4x4[] matrices10 = [Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity];
    private readonly Matrix4x4[] matrices110 = [Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity];
    internal WorldGeometryLateFallbackModuleState() { }
    public IReadOnlyList<Matrix4x4> Matrices10 => Array.AsReadOnly(matrices10);
    public IReadOnlyList<Matrix4x4> Matrices110 => Array.AsReadOnly(matrices110);
    public object? Resource210 { get; private set; }
    public object? Resource218 { get; private set; }
    public WorldGeometryLateResourceMember? Member220 { get; private set; }
    public int Width228 { get; private set; }
    public int Height22C { get; private set; }
    public int Layers230 { get; private set; }
    public float Scalar234 { get; private set; } = 1;
    public int Enabled238 { get; private set; }
    public int Mode23C { get; private set; }
    public int Enabled240 { get; private set; }
    public Vector4 Scale250 { get; private set; } = new(.3f);
    public int Slot260 { get; private set; }
    public Vector3 Affine268 { get; private set; }
    public float Current274 { get; private set; } = -1;
    public void SetResource(object? resource)
    {
        Resource210 = resource;
        // 9AF8A0(null) preserves220. The subsequent 9AF890(null) cleanup
        // clears220 independently; it does not release resource218.
        if (resource is not null) Member220 = new(resource, 0x10);
    }
    public void SetMember(WorldGeometryLateResourceMember? member) => Member220 = member;
    public void SetCounts(int width, int height, int layers) => (Width228, Height22C, Layers230) = (width, height, layers);
    public void SetScalar(float value) => Scalar234 = value;
    public void SetEnabled238(sbyte value) => Enabled238 = value;
    public void SetEnabled240(sbyte value) => Enabled240 = value;
    public void SetAffine(float lower, float upper, int mode, float strength, float sourceEpsilon, float sourceBias)
    {
        float clamped = SourceClamp(strength);
        float oneMinus = 1 - clamped;
        float denominator = SourceMax(upper - lower, sourceEpsilon);
        float doubled = oneMinus + oneMinus;
        float scaledLower = oneMinus * lower;
        Affine268 = new(doubled / denominator, scaledLower / denominator - clamped, mode - sourceBias);
    }
    internal static float SourceMax(float first, float second) => first > second ? first : second;
    internal static float SourceClamp(float value) => value >= 1 ? 1 : SourceMax(0, value);
}

/// <summary>9B7330/9B7160's real D0-byte camera-resource module. Its weights
/// are initialized to1 by the full constructor, after the older64B prefix.</summary>
public sealed class WorldGeometryLateCameraModuleState
{
    internal WorldGeometryLateCameraModuleState() { }
    public object? Owned28 { get; private set; }
    public object? Borrowed30 { get; private set; }
    public Vector2 Weights40 { get; private set; } = Vector2.One;
    public void SelectBorrowedResource(object? resource) { Borrowed30 = resource; Owned28 = null; }
    public void SetWeights(float first, float second) => Weights40 = new(first, second);
}

/// <summary>Actual source module construction and normal58 D19050 writers.
/// Retained module state persists across records and requests. The finite
/// branch requires the current independently owned modifier classification;
/// an active optional colour/range module is a separate implemented producer.</summary>
public sealed class WorldGeometryLateEffectParameterState
{
    private readonly LoadedWorldGeometryLateEffectSource source;
    internal WorldGeometryLateEffectParameterState(LoadedWorldGeometryLateEffectSource loaded,
        LoadedWorldGeometryLateHostSource actualHostSource)
    {
        if (loaded.Native.Locale != actualHostSource.Native.Locale)
            throw new InvalidDataException("Late effect modules and Host use different rendering layouts.");
        source = loaded; HostColour = actualHostSource.ConstructFreshColourParameters();
    }
    public WorldGeometryLateFogModuleState Fog { get; } = new();
    public WorldGeometryLateFallbackModuleState Fallback { get; } = new();
    public WorldGeometryLateCameraModuleState Camera { get; } = new();
    public WorldGeometryLateHostColourParameters HostColour { get; private set; }
    public Vector2 Occlusion18 { get; private set; } = Vector2.One;
    public WorldGeometryLateEffectCleanup PrepareSource58(ulong actualCpu728,
        ReadOnlySpan<byte> actualRecordLightData, WorldGeometryLateAuxParameters aux,
        WorldGeometryLateFogPublication? currentHostQ88)
    {
        ArgumentNullException.ThrowIfNull(aux);
        int modifierOffset = WorldGeometryLateRecordParameterGates.ResolveModifierOffset(actualRecordLightData);
        var modifier = modifierOffset < 0 ? ReadOnlySpan<byte>.Empty : actualRecordLightData[modifierOffset..];
        if (WorldGeometryLateRecordParameterGates.NeedsGenericPreparation(actualCpu728, modifier) ||
            (actualCpu728 & (1UL << 21)) != 0)
            throw new NotSupportedException("Current58 requires the non-short D76780 producer when CPU728.bit21 is set.");
        bool hostColour = false;
        static float Read(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset,4));
        if (!modifier.IsEmpty)
        {
            uint flags44 = BinaryPrimitives.ReadUInt32LittleEndian(modifier.Slice(0x44, 4));
            uint flags48 = BinaryPrimitives.ReadUInt32LittleEndian(modifier.Slice(0x48, 4));
            const uint optional = (1u << 1) | (1u << 12);
            if ((flags44 & optional) != optional || (flags48 & 2) == 0)
                throw new NotSupportedException("This actual record enables the optional D19050 colour/range/global writer; it cannot be replaced by the inactive source modifier.");
            // D74E50 runs for the actual current bit7-clear modifier. Q58
            // is nonnull from the successful Host creator, and the registered
            // Occ data is nonnull from successful graphics initialization.
            if ((flags44 & 0x80) == 0)
            {
                HostColour = new(new(Read(modifier,0x168),Read(modifier,0x158),Read(modifier,0x150),Read(modifier,0x154)),
                    new(Read(modifier,0x16C),Read(modifier,0x170),Read(modifier,0x174),0),
                    new(Read(modifier,0x15C),Read(modifier,0x160),Read(modifier,0x164),0));
                Occlusion18 = new(Read(modifier,0x17C),Read(modifier,0x180));
                hostColour = true;
            }
        }
        // D77EC0 always runs and writes cleanup D6, including the source
        // fallback case after v198 reads its current Host loan.
        if (currentHostQ88 is not null && aux.Mode408 is 3 or 5)
        {
            Fog.SetResource(currentHostQ88.Resource70);
            Fog.SetWeights(WorldGeometryLateFallbackModuleState.SourceClamp(
                currentHostQ88.CurrentView1024 / currentHostQ88.CurrentView144), 1);
        }
        else { Fog.SetResource(aux.Resource348); Fog.SetWeights(0, 0); }
        // Actual Main Host v140 is intrinsic null. Aux338 source construction
        // is also null. Explicit nonnull fallback has its own typed dimensions
        // and resource-flag writer; never pretend it was the source default.
        if (aux.Resource338 is not null)
            throw new NotSupportedException("Current Aux338 requires the actual resource dimensions40/42 and flags48 writer.");
        Fallback.SetEnabled238(0); Fallback.SetEnabled240(0);
        // D74430 still visits the registered module. Current Main View1808
        // source key is absent: clear its loans, retaining initialized weights.
        Camera.SelectBorrowedResource(null);
        if (hostColour) Camera.SetWeights(Read(modifier,0x18C),Read(modifier,0x190));
        // selectedBase output remains null because D76780 was skipped.
        return new(false, hostColour, false, true, true, false, false, false);
    }
    public void Cleanup(WorldGeometryLateEffectCleanup current)
    {
        if (current.D9Colour || current.D7Range || current.D3Global || current.D2Other)
            throw new NotSupportedException("Active optional cleanup needs its actual module owner.");
        if (current.D8HostColour)
        {
            HostColour = new(Vector4.One, Vector4.One, Vector4.One); Occlusion18 = Vector2.One;
        }
        if (current.D6Fog) { Fog.SetResource(null); Fog.SetWeights(0, 0); }
        if (current.D5Fallback)
        {
            Fallback.SetResource(null); Fallback.SetMember(null); Fallback.SetScalar(1);
            Fallback.SetAffine(0, 0, 0, 0, source.Native.AffineEpsilon, source.Native.AffineBias);
            Fallback.SetEnabled238(0); Fallback.SetEnabled240(0);
        }
        if (current.D4CameraResource) { Camera.SelectBorrowedResource(null); Camera.SetWeights(1, 1); }
    }
}

public sealed record WorldGeometryLateEffectNativeSource(string Locale,
    string RuleVersion, float AffineEpsilon, float AffineBias);

/// <summary>Project-owned late-effect rules loaded once at engine load.
/// Current frame/record queries use the retained rules and resource owners.</summary>
public sealed class LoadedWorldGeometryLateEffectSource
{
    private readonly uint sourceMode408;
    private LoadedWorldGeometryLateEffectSource(WorldGeometryLateEffectNativeSource native, uint mode408)
        => (Native,sourceMode408) = (native,mode408);
    public WorldGeometryLateEffectNativeSource Native { get; }
    public static LoadedWorldGeometryLateEffectSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var common=repository.LoadObjects("KIDSSystemResource",MapFreshCommonResources.SourceFileId);
        _=MapFreshCommonResources.ReadSourceTemplate(common); // canonical source acceptance at load only.
        var mode=common.Document.Objects.Single(o=>o.ObjectId==MapFreshCommonResources.SourceObjectId).Properties[43];
        if(mode.Index!=43||mode.NameHash!=0xA1BA4B90||mode.TypeCode!=5||mode.ElementCount!=1||mode.RawValue.Length!=4)
            throw new InvalidDataException("Late Aux408 differs from its source loader property43 binding.");
        return new(RenderSamplerSourceAdapters.LoadWorldGeometryLateEffectNativeSource(repository.SourceDirectory, locale),
            BinaryPrimitives.ReadUInt32LittleEndian(mode.RawValue));
    }
    public WorldGeometryLateEffectParameterState ConstructRegisteredSourceModules(
        LoadedWorldGeometryLateHostSource actualHostSource) => new(this,actualHostSource);
    public WorldGeometryLateAuxParameters ReadSourceCreatedAux(MapFreshCommonResourceSnapshot actualCommon)
    {
        ArgumentNullException.ThrowIfNull(actualCommon);
        if (actualCommon.Scope != MapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication ||
            !actualCommon.CommonViewResource240Present)
            throw new NotSupportedException("Late Aux fields require the current owned source-created no-callback publication or explicit resource owners.");
        // D02010 clears338/348; D223C0 never writes either physical qword.
        // D1E410 copies both from the retained saved settings. Null callback
        // and absent Common overlay preserve them in both current copies.
        return new(null, null, sourceMode408, actualCommon.ProducerProvenance +
            ";lateAux338=source-null;lateAux348=source-null;D02010/D223C0/D1E410;mode408-property43-A1BA4B90");
    }
}
