using System.Globalization;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    private sealed record PortraitLinkBin(LinkBinIndex Index, SourceProvenance Source);
    private sealed record PortraitLinkBinTexture(LoadedTexture Texture, LoadedRdbAsset Source);
    private readonly Dictionary<int, AreaAdministrationQuery<PortraitLinkBin?>> _portraitLinkBins = [];
    private readonly Dictionary<(int Selector, int Entry), PortraitLinkBinTexture> _portraitLinkBinTextures = [];

    /// <summary>Normal bank10 resource routing after the caller's actual
    /// Person validity/hide and low-appearance application admission checks.</summary>
    public ArmyMapPortraitNames ResolveFacePortrait(int appearance)
    {
        int resolved = ResolvePortraitResourceIndex(appearance, 10);
        var route = DlcPortraitDescriptor.ForAppearance(resolved) is not null ? ResolveDlcFacePortrait(resolved)
            : resolved is >= 7000 and <= 7999 ? ResolveUserFacePortrait(resolved)
            : RegisteredOfficerPortraitCatalog.FacePortrait(resolved);
        return route with { AppearanceIndex = appearance };
    }

    /// <summary>Normal standing resource dispatch; source animated appearances
    /// keep their own bank12 publication before bank9's external resource gates.</summary>
    public DeploymentNarrationPortraitRoute ResolveStandingPortrait(int appearance)
    {
        if (appearance is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(appearance));
        if (DeploymentNarrationPortraitRoute.IsAnimatedAppearance((ushort)appearance))
            return DeploymentNarrationPortraitRoute.Animated((ushort)appearance);
        int resolved = ResolvePortraitResourceIndex(appearance, 9);
        var route = DlcPortraitDescriptor.ForAppearance(resolved) is not null ? ResolveDlcStandingPortrait(resolved)
            : resolved is >= 7000 and <= 7999 ? ResolveUserStandingPortrait(resolved)
            : RegisteredOfficerPortraitCatalog.StandingPortrait(resolved);
        return route with { CurrentAppearanceIndex = (ushort)appearance };
    }

    private int ResolvePortraitResourceIndex(int appearance, int bank)
    {
        if (appearance is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(appearance));
        int resolved = appearance;
        if (DlcPortraitDescriptor.ForAppearance(resolved) is not null)
        {
            var available = ReadDlcPortraitAvailability(resolved);
            if (!available.Available) throw new InvalidDataException(available.UnresolvedInput);
            if (!available.Value) resolved = 2000;
        }
        else if (PortraitContentCatalogue.IsClassified(resolved))
        {
            var admitted = LoadPortraitContent().ReadAdmission(resolved);
            if (!admitted.Available) throw new InvalidDataException(admitted.UnresolvedInput);
            if (!admitted.Value) resolved = 2000;
        }
        if (resolved is >= 7000 and <= 7999 && !LoadUserPortraits().HasResource(resolved, bank)) resolved = 2000;
        if (!RegisteredOfficerPortraitCatalog.IsStockAppearance(resolved)
            && DlcPortraitDescriptor.ForAppearance(resolved) is null && resolved is not (>= 7000 and <= 7999))
            throw new InvalidDataException("officer.portrait.unregistered_portrait_range:" + resolved);
        return resolved;
    }

    /// <summary>The source loaded capability: actual file open and supported
    /// index. This is independent of DLC ownership and installed platform facts.</summary>
    public AreaAdministrationQuery<bool> ReadDlcPortraitAvailability(int appearance)
    {
        var descriptor = DlcPortraitDescriptor.ForAppearance(appearance)
            ?? throw new ArgumentOutOfRangeException(nameof(appearance));
        //The ordinary appearance gate is per portrait. Initial archive loading
        //also requires the descriptor's first appearance to pass the content gate.
        var own = LoadPortraitContent().ReadAdmission(appearance);
        if (!own.Available || !own.Value) return own;
        var first = LoadPortraitContent().ReadAdmission(descriptor.First);
        if (!first.Available || !first.Value) return first;
        var archive = LoadPortraitLinkBin(descriptor);
        return archive.Available ? AreaAdministrationQuery<bool>.Known(archive.Value is not null)
            : AreaAdministrationQuery<bool>.Unknown(archive.UnresolvedInput!);
    }

    internal string? DlcPortraitResourceIssue(int appearance)
    {
        var descriptor = DlcPortraitDescriptor.ForAppearance(appearance)
            ?? throw new ArgumentOutOfRangeException(nameof(appearance));
        var available = ReadDlcPortraitAvailability(appearance);
        if (!available.Available) return available.UnresolvedInput;
        if (!available.Value) return "officer-editor.portrait.dlc_archive_unavailable:" + descriptor.ResourceUri;
        var archive = LoadPortraitLinkBin(descriptor).Value!;
        foreach (int bank in new[] { 9, 10, 11 })
        {
            int entry = descriptor.EntryIndex(appearance, bank);
            if (entry >= archive.Index.Entries.Count || !archive.Index.Entries[entry].HasPayload)
                return "officer-editor.portrait.dlc_entry_missing:" + descriptor.ResourceUri + ":" + entry;
        }
        return null;
    }

    public ArmyMapPortraitNames ResolveDlcFacePortrait(int appearance)
    {
        var descriptor = RequireDlcPortrait(appearance, 10);
        return new(appearance, appearance, descriptor.ResourceUri, descriptor.SpriteName(appearance, 10));
    }

    public DeploymentNarrationPortraitRoute ResolveDlcStandingPortrait(int appearance)
    {
        var descriptor = RequireDlcPortrait(appearance, 9);
        //The authored draw order is b,a,c; source841F00 binds parts2,1,3.
        return new(checked((ushort)appearance), appearance, descriptor.ResourceUri,
            [descriptor.SpriteName(appearance, 9, 2), descriptor.SpriteName(appearance, 9, 1), descriptor.SpriteName(appearance, 9, 3)]);
    }

    internal DlcPortraitDescriptor RequireDlcPortrait(int appearance, int bank)
    {
        var descriptor = DlcPortraitDescriptor.ForAppearance(appearance)
            ?? throw new ArgumentOutOfRangeException(nameof(appearance));
        var available = ReadDlcPortraitAvailability(appearance);
        if (!available.Available) throw new InvalidDataException(available.UnresolvedInput);
        if (!available.Value) throw new InvalidDataException("officer.portrait.dlc_archive_unavailable:" + descriptor.ResourceUri);
        var archive = LoadPortraitLinkBin(descriptor).Value!;
        int entry = descriptor.EntryIndex(appearance, bank);
        if (entry >= archive.Index.Entries.Count || !archive.Index.Entries[entry].HasPayload)
            throw new InvalidDataException("officer.portrait.dlc_entry_missing:" + descriptor.ResourceUri + ":" + entry);
        return descriptor;
    }

    internal static bool IsDlcPortraitResource(string resourceName) => DlcPortraitDescriptor.ForResource(resourceName) is not null;
    internal bool ContainsDlcPortraitResource(string resourceName)
    {
        var descriptor = DlcPortraitDescriptor.ForResource(resourceName);
        return descriptor is not null && ReadDlcPortraitAvailability(descriptor.First) is { Available: true, Value: true };
    }

    private AreaAdministrationQuery<PortraitLinkBin?> LoadPortraitLinkBin(DlcPortraitDescriptor descriptor)
    {
        if (_portraitLinkBins.TryGetValue(descriptor.Selector, out var cached)) return cached;
        AreaAdministrationQuery<PortraitLinkBin?> loaded;
        var id = GameResourceId.File(descriptor.RelativePath);
        string? replacement = _overrides.Find(id);
        string path = replacement ?? SourcePath(descriptor.RelativePath);
        try
        {
            //Native CLinkBinSeek uses an opened CFile and mode0's little-endian
            //count/table. Missing is known unavailable; malformed is not missing.
            using var stream = File.OpenRead(path);
            byte[] header = new byte[4]; stream.ReadExactly(header);
            int indexLength = LinkBinReader.IndexLength(header);
            byte[] bytes = new byte[indexLength]; header.CopyTo(bytes, 0);
            stream.ReadExactly(bytes.AsSpan(4));
            var index = LinkBinReader.ReadIndex(bytes, stream.Length);
            var source = new SourceProvenance(descriptor.RelativePath, indexLength,
                Convert.ToHexString(SHA256.HashData(bytes)))
            {
                ResourceUri = id.ToString(), PhysicalPath = path, IsOverride = replacement is not null,
                RuleVersion = "linkbin-index-prefix-v1"
            };
            _readFiles[id.ToString()] = source;
            loaded = AreaAdministrationQuery<PortraitLinkBin?>.Known(new(index, source));
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        { loaded = AreaAdministrationQuery<PortraitLinkBin?>.Known(null); }
        catch (Exception error) when (error is InvalidDataException or EndOfStreamException)
        { loaded = AreaAdministrationQuery<PortraitLinkBin?>.Unknown("officer.portrait.dlc_archive_format:" + id + ":" + error.Message); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { loaded = AreaAdministrationQuery<PortraitLinkBin?>.Unknown("officer.portrait.dlc_archive_read:" + id + ":" + error.Message); }
        return _portraitLinkBins[descriptor.Selector] = loaded;
    }

    internal LoadedSprite LoadDlcPortraitSprite(string resourceName, string spriteName)
    {
        var descriptor = DlcPortraitDescriptor.ForResource(resourceName)
            ?? throw new InvalidDataException("officer.portrait.dlc_resource_identity");
        string[] parts = spriteName.Split('_');
        if (parts.Length != 4 || parts[0] != "portrait"
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int appearance)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int bank)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int part)
            || !descriptor.Contains(appearance) || bank is < 9 or > 11
            || (bank == 9 ? part is < 1 or > 3 : part != 0)
            || descriptor.SpriteName(appearance, bank, part) != spriteName)
            throw new InvalidDataException("officer.portrait.dlc_sprite_identity");
        _ = RequireDlcPortrait(appearance, bank);
        int entry = descriptor.EntryIndex(appearance, bank);
        if (!_portraitLinkBinTextures.TryGetValue((descriptor.Selector, entry), out var loaded))
        {
            var archive = LoadPortraitLinkBin(descriptor).Value!;
            var record = archive.Index.Entries[entry];
            if (record.Length > int.MaxValue) throw new InvalidDataException("officer.portrait.dlc_entry_size");
            using var stream = File.OpenRead(archive.Source.PhysicalPath);
            if (stream.Length != archive.Index.ContainerLength)
                throw new InvalidDataException("officer.portrait.dlc_archive_changed_reimport_required");
            stream.Position = record.Offset;
            byte[] bytes = new byte[(int)record.Length]; stream.ReadExactly(bytes);
            byte[] decoded = Zp1Reader.Read(bytes).DecodedPayload;
            var pixels = G1TextureReader.Decode(decoded);
            string hash = Convert.ToHexString(SHA256.HashData(decoded));
            var texture = new LoadedTexture(pixels, archive.Source.PhysicalPath, (uint)entry, hash, archive.Source)
            {
                OverrideSource = archive.Source.IsOverride ? archive.Source : null,
                SourceImage = G1TextureContainerReader.Read(decoded).Textures[0]
            };
            var asset = new LoadedRdbAsset(decoded, descriptor.ResourceUri, archive.Source.PhysicalPath,
                (uint)entry, hash, archive.Source, archive.Source.IsOverride ? archive.Source : null);
            loaded = new(texture, asset);
            _portraitLinkBinTextures[(descriptor.Selector, entry)] = loaded;
        }
        //8AA0C0 kind2/3: original padded texture crops, applied once before
        //the authored node's own local UV. bank9 part is source order a,b,c.
        float left = 0, right = 1, bottom = 1;
        if (bank == 9)
        {
            (left, right) = part switch { 1 => (0f, 636f / 2048f),
                2 => (637f / 2048f, 1273f / 2048f), _ => (1274f / 2048f, 1910f / 2048f) };
            bottom = 900f / 1024f;
        }
        else if (bank == 10) right = 242f / 256f;
        var definition = new SpriteDefinition(spriteName, 0, new(left, 0), new(right, 0),
            new(left, bottom), new(right, bottom), 0);
        _ = definition.GetRectangle(loaded.Texture.Texture.Width, loaded.Texture.Texture.Height);
        return new(definition, loaded.Texture, loaded.Source);
    }
}
