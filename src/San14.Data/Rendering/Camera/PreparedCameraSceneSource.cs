namespace San14.Data;

/// <summary>The complete CKIDSCamera K prefix starts with its native vtable.
/// Deferred scene preparation receives K+10: Eye at0, view at70 and combined
/// projection atF0. This is distinct from a pre-sliced130-byte scene payload.</summary>
public static class PreparedCameraSceneSource
{
    public static DeferredCameraInputs ReadCompletePrefix(ReadOnlySpan<byte> complete3C0)
    {
        if (complete3C0.Length != PreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Source Camera scene input requires the complete3C0 native prefix.");
        return DeferredSceneConstants.ReadPreparedCamera(complete3C0.Slice(0x10, 0x130));
    }
}
