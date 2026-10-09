namespace San14.Data;

public sealed partial class OriginalMapPostEffect3TextureParameterState
{
    public void ApplyLensFlare2InternalControllerTextures(OriginalMapPostEffect3LensFlare2ControllerTextures source)
    {
        ArgumentNullException.ThrowIfNull(source);source.RequireCurrentTextureTable(this);
        if(source.SourceSha256!=SourceSha256||source.ContextOwnerIdentity!=ContextOwnerIdentity||source.FrameNumber!=FrameNumber||
            allocation.TextureParameters[50].Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration||
            allocation.TextureParameters[55].Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("LensFlare2 pointer copies lack their current owned source wrappers.");
        Copy(50,51,source.SourceEvidence+"TextureParameterState.LensFlare2");Copy(55,56,source.SourceEvidence+"TextureParameterState.LensFlare2");
    }
}
