using Godot;
using San14.Data;

/// <summary>Source target format plus the actual owned device target shape.
/// Depth-only targets have no stencil test/write aspect irrespective of an
/// unresolved global stencil state; this does not claim that state is off.</summary>
public sealed class DeferredAuxiliaryDepthCopyStencilAdmission
{
    private DeferredAuxiliaryDepthCopyStencilAdmission(RenderTextureDefinition source,
        RenderingDevice.DataFormat actualFormat, bool? globalDisabled, bool noStencil)
    { SourceTarget = source; ActualTargetFormat = actualFormat; CurrentGlobalStencilIsDisabled = globalDisabled;
      TargetHasNoStencilAspect = noStencil; }
    public RenderTextureDefinition SourceTarget { get; }
    public RenderingDevice.DataFormat ActualTargetFormat { get; }
    public bool? CurrentGlobalStencilIsDisabled { get; }
    public bool TargetHasNoStencilAspect { get; }

    public static DeferredAuxiliaryDepthCopyStencilAdmission ForSourceTarget(
        RenderTextureDefinition sourceTarget, RenderingDevice.DataFormat actualTargetFormat,
        bool? currentGlobalStencilIsDisabled)
    {
        ArgumentNullException.ThrowIfNull(sourceTarget);
        uint native = RenderTextureFormatMath.Resolve(sourceTarget).TargetViewFormat;
        if (native == 40 && actualTargetFormat == RenderingDevice.DataFormat.D32Sfloat)
            return new(sourceTarget, actualTargetFormat, currentGlobalStencilIsDisabled, true);
        if (native == 20 && actualTargetFormat == RenderingDevice.DataFormat.D32SfloatS8Uint &&
            currentGlobalStencilIsDisabled == true)
            return new(sourceTarget, actualTargetFormat, currentGlobalStencilIsDisabled, false);
        throw new InvalidDataException("Depth copy needs matching source/device depth-only targets, or an explicitly disabled current stencil on a matching stencil target.");
    }
}
