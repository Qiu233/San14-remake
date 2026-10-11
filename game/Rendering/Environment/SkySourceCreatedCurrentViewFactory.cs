using San14.Data;
using System.Numerics;

/// <summary>The accepted source1022 session's current Main/View publication
/// supplies the source-created seasonal Sky assembler. The session prepares
/// this frame first; the source seasonal producer supplies its independent
/// World third-date and current Main post-phase. No renderer clock is inferred.
/// Shared atmosphere and package+10 light remain explicit current producers.</summary>
public static class SkySourceCreatedCurrentViewFactory
{
    public static SkyAmbientSourceFrameInputs Prepare(
        GameResourceRepository repository, MapSourceCreatedNativeSessionPreparedFrame currentView,
        SkySeasonMainCameraRgbFrame currentSeasonCamera, string ownedSelectedRecordIdentity,
        Vector4 actualPackageLightPosition, string actualPackageLightOwner,
        SkySceneSharedAtmosphere currentAtmosphere, SkySceneProducerFrame producers,
        Sky2TextureBindingSnapshot textureBindings)
    {
        ArgumentNullException.ThrowIfNull(currentView); ArgumentNullException.ThrowIfNull(currentSeasonCamera);
         currentView.Feature.ValidateFrame(currentView.MainCamera, currentView.Lighting);
        if (currentView.Context.FrameNumber != currentView.Lighting.FrameNumber ||
            currentView.Context.FrameNumber != currentSeasonCamera.Inputs.FrameNumber ||
            currentView.Context.PreparedCamera is null ||
            currentView.Context.PreparedCamera != currentView.MainCamera.SceneInputs ||
            currentView.Selection.Combination.Index != 1022 || currentView.Selection.PixelProgramIndex != 767 ||
            !ReferenceEquals(currentView.Selection.OwnedFeatureSource, currentView.Feature))
            throw new InvalidDataException("Source Sky publication requires the actual same-frame accepted Main/View kind1 and selected original1022/767 program.");
        return SkyAmbientSourceFrameAssembler.CreateAppliedOwnedSeasonSourceInputs(repository,
            currentView.Lighting, currentSeasonCamera, currentView.Feature, ownedSelectedRecordIdentity,
            actualPackageLightPosition, actualPackageLightOwner, currentAtmosphere, producers, textureBindings);
    }
}
