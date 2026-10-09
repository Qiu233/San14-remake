using San14.Data;

public sealed partial class MapSourceCreatedDirectionalLightingDeviceOwner
{
    private PatternCameraDeviceTextureData? patternCameraWhite;
    /// <summary>Borrow the literal80F9DB7B which CreateSourceReadyNormalizedAsync
    /// already built and registered. The same lifetime covers Directional and
    /// Camera users; consumers drain before this owner retires.</summary>
    public PatternCameraDeviceTextureData BorrowCurrentPatternCameraWhite()
    {
        if (retiring || fixedWhite is null || fixedWhite.IsRetiring)
            throw new InvalidDataException("Directional owner has no live actual Pattern fallback Static Data.");
        if (patternCameraWhite is null)
        {
            var white = fixedWhite;
            patternCameraWhite = new(white.Entry, white, () =>
            {
                if (retiring || !ReferenceEquals(fixedWhite, white) || white.IsRetiring)
                    throw new InvalidDataException("Borrowed Pattern fallback Data owner has retired.");
            });
        }
        patternCameraWhite.RequireCurrent(); return patternCameraWhite;
    }
}
