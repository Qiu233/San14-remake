namespace San14.Data;

/// <summary>
/// Current CE SC and TC executables' physical Hex index to terrain X/Z calculation.
/// Constants and float32 operation order are from the installed executable;
/// scene selection and height sampling are separate unresolved questions.
/// </summary>
public static class OriginalCeHexWorldCoordinates
{
    public const string FormulaId = "ce-20260928-hex-world";
    public const string VerifiedScExecutableSha256 =
        "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88";
    public const string VerifiedTcExecutableSha256 =
        "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464";
    public const int Width = 220;
    public const int Height = 220;
    public const double OriginX = 37594.0;
    public const double ColumnStep = 702.0;
    public const double InitializedOriginZ = 13335.2998046875;
    public const double RowStep = 810.5997924804688;
    public const double EvenColumnRowOffset = 0.5;

    public static string VerifyInstalledExecutable(string sourceDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        (string fileName, string expected) = locale switch
        {
            "SC" => ("SAN14CE_SC.exe", VerifiedScExecutableSha256),
            "TC" => ("SAN14CE_TC.exe", VerifiedTcExecutableSha256),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale,
                "CE Hex world coordinates have only been verified for SC and TC.")
        };
        string path = Path.Combine(sourceDirectory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Verified CE Hex coordinate source is missing: {fileName}.", path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 128 * 1024, FileOptions.SequentialScan);
        string actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Installed {fileName} SHA-256 changed: {actual}. " +
                $"The {FormulaId} map coordinate formula must be checked against this executable revision before use.");
        return actual;
    }

    public static ProjectedMapPoint Project(int hexId)
    {
        if ((uint)hexId >= Width * Height)
            throw new ArgumentOutOfRangeException(nameof(hexId));
        int column = hexId % Width, row = hexId / Width;
        float x = column * 702f;
        x += 37594f;

        float root3 = MathF.Sqrt(3f);
        // RVA 0x11D0 first-startup initializer: sqrtss, mulss, mulss, addss.
        float initialHalf = root3 * 0.5f;
        float initialOffset = initialHalf * 468f;
        float zOrigin = initialOffset + 12930f;
        // RVA 0x1888C0: even columns add a separate half-row product.
        float evenOffset = column % 2 == 0 ? (root3 * 468f) * 0.5f : 0f;
        float zStride = root3 * 468f;
        float z = zStride * row;
        z += evenOffset;
        z += zOrigin;
        return new ProjectedMapPoint(hexId, x, z);
    }
}
