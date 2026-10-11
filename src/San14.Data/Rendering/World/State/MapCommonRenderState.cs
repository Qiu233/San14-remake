namespace San14.Data;

public enum CommonRenderPublicationKind { InitialSettings, Runtime }
[Flags]
public enum CommonLightingOptions : uint { None = 0, PreserveAmbient = 0x800 }
public enum AmbientMapVariant { Unsupported = -1, Variant0, Variant1, Variant2 }

/// <summary>Selected ambient packet classifications, independent of GPU texture
/// allocation. A present packet with unsupported variants differs from no packet.</summary>
public readonly record struct AmbientMapSelection(AmbientMapVariant First,
    AmbientMapVariant Second, AmbientMapVariant Third);

/// <summary>Resolved Common settings. Optional fields remain unknown when the
/// initial settings publication does not supply their runtime writers.</summary>
public sealed record CommonAuxiliaryState(uint Flags, CommonLightingOptions LightingOptions,
    uint ModelExclusionMask, byte ViewGroupSelector, byte ModelGroupSelector,
    byte SkyEnableFlags, int? ShadowAngleMode = null, uint? ClipPlaneMask = null,
    ModelAuxiliaryColorMasks? ColorWriteMasks = null, bool? SuppressOceanDrawing = null);

/// <summary>Current Common render state. Raw resource and runtime record layouts
/// are decoded at their respective import boundaries, never retained here.</summary>
public sealed class MapCommonRenderState
{
    internal MapCommonRenderState(long frameNumber, string settingsOwner,
        string commonTableOwner, string selectedViewOwner, string provenance,
        CommonRenderPublicationKind scope, CommonAuxiliaryState? auxiliary,
        AmbientMapSelection? ambient)
    {
        FrameNumber = frameNumber; SettingsOwnerIdentity = settingsOwner;
        CommonTableOwnerIdentity = commonTableOwner; SelectedViewOwnerIdentity = selectedViewOwner;
        ProducerProvenance = provenance; Scope = scope; Auxiliary = auxiliary; Ambient = ambient;
    }
    public long FrameNumber { get; }
    public string SettingsOwnerIdentity { get; }
    public string CommonTableOwnerIdentity { get; }
    public string SelectedViewOwnerIdentity { get; }
    public string ProducerProvenance { get; }
    public CommonRenderPublicationKind Scope { get; }
    public CommonAuxiliaryState? Auxiliary { get; }
    public AmbientMapSelection? Ambient { get; }
    public uint CommonFlags => Auxiliary?.Flags ?? 0;
    public bool AuxiliaryPresent => Auxiliary is not null;
    public uint? AuxiliaryLightingFlags => Auxiliary is { } aux ? (uint)aux.LightingOptions : null;
    public bool SuppressAmbient => Auxiliary is { } aux && !aux.LightingOptions.HasFlag(CommonLightingOptions.PreserveAmbient);
    public bool AmbientSelectionPresent => Ambient.HasValue;
    public uint? ModelExclusionMask => Auxiliary?.ModelExclusionMask;
    public byte? ViewGroupSelector => Auxiliary?.ViewGroupSelector;
    public byte? ModelGroupSelector => Auxiliary?.ModelGroupSelector;
    public byte? SkyEnableFlags => Auxiliary?.SkyEnableFlags;
    public bool AmbientMapFirstAvailable => Ambient is { First: not AmbientMapVariant.Unsupported };
    public bool AmbientMapSecondAvailable => Ambient is { Second: not AmbientMapVariant.Unsupported };
    public bool AmbientMapThirdAvailable => Ambient is { Third: not AmbientMapVariant.Unsupported };

    public static MapCommonRenderState FromSettings(CommonRenderSettings source,
        long frameNumber, string settingsOwnerIdentity, string commonTableOwnerIdentity,
        string selectedViewOwnerIdentity, string producerProvenance)
    {
        ValidateIdentity(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity, producerProvenance);
        return new(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity,
            producerProvenance + ";" + source.SourceIdentity + ";settings-applied;before-ambient-publication",
            CommonRenderPublicationKind.InitialSettings,
            source.InitialAuxiliary, null);
    }

    internal static void ValidateIdentity(long frameNumber, params string[] identities)
    {
        if (frameNumber <= 0 || identities.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Common settings require a current frame and producer identities.");
    }

    public void ValidateFrame(long frameNumber, string commonTableOwner, string selectedViewOwner)
    {
        if (frameNumber != FrameNumber || commonTableOwner != CommonTableOwnerIdentity || selectedViewOwner != SelectedViewOwnerIdentity)
            throw new InvalidDataException("Common settings belong to a different accepted frame or View owner.");
    }
}
