namespace San14.Data;

/// <summary>The external face tool's registration table, not a scan of image files.</summary>
public sealed record UserPortraitRegistration(int Slot, byte State, byte Metadata1, byte Metadata2)
{
    public int Appearance => 7000 + Slot;
    public bool Registered => State == 1;
    public string Medium => RelativePath(Slot, 9);
    public string Small => RelativePath(Slot, 10);
    public string Large => RelativePath(Slot, 11);

    public static string RelativePath(int slot, int bank)
    {
        if (slot is < 0 or >= 1000) throw new ArgumentOutOfRangeException(nameof(slot));
        string prefix = bank switch
        {
            9 => "EDIT_KAO_M", 10 => "EDIT_KAO_S", 11 => "EDIT_KAO_L",
            _ => throw new ArgumentOutOfRangeException(nameof(bank))
        };
        return $"ADD_FACE/{prefix}/{prefix}_{slot}.s14.zp";
    }
}

public sealed class UserPortraitCatalogue
{
    private readonly HashSet<(int Appearance, int Bank)> available = [];
    public SourceProvenance? RegistrationSource { get; }
    public IReadOnlyList<UserPortraitRegistration> Slots { get; }
    public IReadOnlyList<UserPortraitRegistration> Registered { get; }

    internal UserPortraitCatalogue(byte[]? bytes, SourceProvenance? source, Func<string, bool>? exists = null)
    {
        RegistrationSource = source;
        if (bytes is not null && bytes.Length != 3000)
            throw new InvalidDataException("officer-editor.portrait.user_registration_size");
        //8AB270 leaves all1000 registration bits clear if ManageData is absent.
        Slots = Array.AsReadOnly(Enumerable.Range(0, 1000).Select(slot => bytes is null
            ? new UserPortraitRegistration(slot, 0, 0, 0)
            : new UserPortraitRegistration(slot, bytes[slot * 3], bytes[slot * 3 + 1], bytes[slot * 3 + 2])).ToArray());
        Registered = Array.AsReadOnly(Slots.Where(slot => slot.Registered).ToArray());
        if (exists is not null)
            foreach (var slot in Registered)
                foreach (int bank in new[] { 9, 10, 11 })
                    if (exists(UserPortraitRegistration.RelativePath(slot.Slot, bank))) available.Add((slot.Appearance, bank));
    }

    public bool IsRegistered(int appearance) => appearance is >= 7000 and <= 7999
        && Slots[appearance - 7000].Registered;
    public bool HasResource(int appearance, int bank) => available.Contains((appearance, bank));
}

public sealed partial class GameResourceRepository
{
    private string? _userPortraitDirectory;
    private int _portraitResourceVersion;
    private UserPortraitCatalogue? _userPortraits;
    private PortraitContentCatalogue? _portraitContent;
    private IReadOnlyDictionary<uint, PortraitPlatformEntitlement> _portraitEntitlements =
        new Dictionary<uint, PortraitPlatformEntitlement>();

    public int PortraitResourceVersion
    {
        get
        {
            SynchronizeResidentInvalidation();
            return _portraitResourceVersion;
        }
    }
    //57DDF0 ->57E290 ->3F9410 uses CSIDL_PERSONAL, then these exact components.
    //GetFolderPath respects the system's redirected Documents location.
    public string? UserPortraitDirectory
    {
        get
        {
            if (_userPortraitDirectory is not null) return _userPortraitDirectory;
            string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return string.IsNullOrWhiteSpace(personal) ? null
                : Path.Combine(personal, "KoeiTecmo", "SAN14", "ADD_FACE");
        }
    }

    public void ConfigureUserPortraitDirectory(string? actualAddFaceDirectory)
    {
        _userPortraitDirectory = actualAddFaceDirectory is null ? null
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(actualAddFaceDirectory));
        InvalidatePortraitResources();
    }

    public void ConfigurePortraitPlatformEntitlements(
        IReadOnlyDictionary<uint, PortraitPlatformEntitlement> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        _portraitEntitlements = facts.ToDictionary(pair => pair.Key, pair => pair.Value);
        InvalidatePortraitResources();
    }

    /// <summary>Call once after the external tool or resource override changes.
    /// Owners holding UI sprite/catalog snapshots recreate them after this call.</summary>
    public void InvalidatePortraitResources()
    {
        // The externally editable DLC metadata uses the normal resident source
        // path. Invalidate only the metadata files this catalog actually read.
        _residentSources.InvalidatePortraitSources();
        SynchronizeResidentInvalidation();
    }

    public PortraitContentCatalogue LoadPortraitContent()
    {
        SynchronizeResidentInvalidation();
        return _portraitContent ??= PortraitContentCatalogue.Load(this, _portraitEntitlements);
    }

    public UserPortraitCatalogue LoadUserPortraits()
    {
        SynchronizeResidentInvalidation();
        if (_userPortraits is not null) return _userPortraits;
        const string relative = "ADD_FACE/ManageData.s14";
        string? path = FindUserPortraitFile(relative);
        if (path is null) return _userPortraits = new(null, null);
        var source = ReadUserPortraitFile(relative);
        return _userPortraits = new(source.Bytes, source.Provenance, resourceRelative => FindUserPortraitFile(resourceRelative) is not null);
    }

    internal string? FindUserPortraitFile(string relative)
    {
        var id = GameResourceId.File(relative);
        if (!relative.StartsWith("ADD_FACE/", StringComparison.Ordinal))
            throw new ArgumentException("Not a user portrait resource.", nameof(relative));
        string? replacement = _overrides.Find(id);
        string? root = UserPortraitDirectory;
        string? path = root is null ? replacement : Path.Combine(root, relative[9..].Replace('/', Path.DirectorySeparatorChar));
        if (path is null) throw new InvalidDataException("officer-editor.portrait.user_documents_directory");
        // Track the real requested registration/image, even when absent. This
        // observes its later creation without scanning the external face tree.
        TrackPortraitSource(id, path, replacement);
        return replacement ?? (File.Exists(path) ? path : null);
    }

    internal (byte[] Bytes, SourceProvenance Provenance) ReadUserPortraitFile(string relative)
    {
        var id = GameResourceId.File(relative);
        string path = FindUserPortraitFile(relative) ?? throw new FileNotFoundException(
            "User portrait resource is missing: " + id);
        string fallback = UserPortraitDirectory is { } root
            ? Path.Combine(root, relative[9..].Replace('/', Path.DirectorySeparatorChar)) : path;
        var generation = BeginResourceRead(id, fallback, path);
        byte[] bytes = File.ReadAllBytes(path);
        ObserveResidentRead(generation);
        var provenance = new SourceProvenance(relative, bytes.LongLength,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)))
        {
            PhysicalPath = path, ResourceUri = id.ToString(), IsOverride = _overrides.Find(id) is not null
        };
        _readFiles[id.ToString()] = provenance;
        return (bytes, provenance);
    }
}
