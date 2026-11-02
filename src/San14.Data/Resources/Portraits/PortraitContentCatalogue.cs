using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace San14.Data;

/// <summary>Independent platform facts. A local metadata file proves neither.</summary>
public sealed record PortraitPlatformEntitlement(bool? Owned, bool? Installed);

public sealed record PortraitContentRecord(int Slot, uint AppId, int Type, int Id,
    int Classification, string ResourceUri)
{
    public bool WritesSecondaryBitmap => Classification < 19;
    public bool IsClassifiedPortrait => Type == 3 && Id is >= 5000 and < 7000
        && Id is not (>= 5106 and <= 5205) && Id is not (>= 6000 and <= 6214);
}

public sealed record PortraitContentFile(int Slot, uint AppId, SourceProvenance Source,
    PortraitPlatformEntitlement Platform, IReadOnlyList<PortraitContentRecord> Records)
{
    public bool? Admitted => Platform.Owned == false || Platform.Installed == false ? false
        : Platform.Owned == true && Platform.Installed == true ? true : null;
}

/// <summary>Installed DLC metadata, decoded once at resource catalog load. Platform
/// ownership and installation stay explicit and independent of resource presence.</summary>
public sealed class PortraitContentCatalogue
{
    private readonly IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> admissions;
    // Offline extracted DLCInfo slot mapping. Zero and signed-negative slots are
    // inactive in the source loader; the running game never reads an executable.
    private static readonly uint[] AppIds =
    [
        0,0,0,0,0,0,0,0,1198834,1198835,1198836,1198837,0,1198839,1198840,1198841,
        0,1198843,1198844,1198845,0,0,0,1198849,1213611,1213612,1258150,1258151,
        1258152,0,0,1213613,0,1213610,1258155,1293030,0,1431481,1431483,1431484,
        0,1431487,0,1431489,0,1431491,1431492,1431493,0,1431495,1431496,0,1431498,
        1431486,1543950,1638310,1638311,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2441078279
    ];
    public IReadOnlyList<PortraitContentFile> Files { get; }
    public IReadOnlyList<PortraitContentRecord> PortraitRecords { get; }
    public IReadOnlyDictionary<uint, PortraitPlatformEntitlement> Platform { get; }

    private PortraitContentCatalogue(PortraitContentFile[] files,
        IReadOnlyDictionary<uint, PortraitPlatformEntitlement> platform)
    {
        Files = Array.AsReadOnly(files);
        PortraitRecords = Array.AsReadOnly(files.SelectMany(file => file.Records)
            .Where(record => record.IsClassifiedPortrait).ToArray());
        Platform = new ReadOnlyDictionary<uint, PortraitPlatformEntitlement>(
            platform.ToDictionary(pair => pair.Key, pair => pair.Value));
        var resolved = new Dictionary<int, AreaAdministrationQuery<bool>>();
        foreach (var group in files.SelectMany(file => file.Records
                     .Where(record => record.IsClassifiedPortrait && record.WritesSecondaryBitmap)
                     .Select(record => (File: file, Record: record))).GroupBy(entry => entry.Record.Id))
        {
            if (group.Any(entry => entry.File.Admitted == true))
                resolved[group.Key] = AreaAdministrationQuery<bool>.Known(true);
            else
            {
                var unknown = group.FirstOrDefault(entry => entry.File.Admitted is null).File;
                resolved[group.Key] = unknown is null ? AreaAdministrationQuery<bool>.Known(false)
                    : AreaAdministrationQuery<bool>.Unknown("officer-editor.portrait.platform_entitlement:" + unknown.AppId);
            }
        }
        admissions = new ReadOnlyDictionary<int, AreaAdministrationQuery<bool>>(resolved);
    }

    public static uint? AppIdForSlot(int slot) => slot >= 0 && slot < AppIds.Length
        && unchecked((int)AppIds[slot]) > 0 ? AppIds[slot] : null;

    public static bool IsClassified(int appearance) => appearance is >= 5000 and < 7000
        && appearance is not (>= 5106 and <= 5205) && appearance is not (>= 6000 and <= 6214);

    /// <summary>The manager starts with clear bits. Only an admitted type3
    /// record with classification below19 can set both required bits.</summary>
    public AreaAdministrationQuery<bool> ReadAdmission(int appearance)
    {
        if (!IsClassified(appearance)) return AreaAdministrationQuery<bool>.Known(true);
        return admissions.TryGetValue(appearance, out var admission) ? admission
            : AreaAdministrationQuery<bool>.Known(false);
    }

    public static PortraitContentCatalogue Load(GameResourceRepository repository,
        IReadOnlyDictionary<uint, PortraitPlatformEntitlement> platform)
    {
        var files = new List<PortraitContentFile>();
        foreach (var resource in repository.ReadPortraitContentResources())
        {
            string name = Path.GetFileName(resource.Relative);
            int marker = name.IndexOf("DLCInfo", StringComparison.Ordinal);
            int slot = 0;
            // The original loader accumulates every decimal digit following the
            // marker up to the extension, including non-digit separators.
            int extension=name.IndexOf('.',marker+7);
            foreach (char c in name.AsSpan(marker+7,(extension<0?name.Length:extension)-(marker+7)))
                if (c is >= '0' and <= '9') slot = checked(slot * 10 + c - '0');
            uint? appId = AppIdForSlot(slot);
            if (appId is null) continue; //The native loader explicitly excludes inactive slots.
            if (resource.Bytes.Length > 2048) throw new InvalidDataException(
                "officer-editor.portrait.content_buffer_size:" + resource.Relative);
            byte[] bytes = (byte[])resource.Bytes.Clone();
            uint state = unchecked(appId.Value + (uint)slot);
            for (int i = 0; i < bytes.Length; i++)
            {
                state = unchecked(state * 0x6c078965u + 0x3039u);
                bytes[i] ^= (byte)((state >> 16) ^ (state >> 24));
                if (bytes[i] > 127) throw new InvalidDataException(
                    "officer-editor.portrait.content_ascii:" + resource.Relative);
            }
            string text = Encoding.ASCII.GetString(bytes);
            if (text.Length != 0 && !text.EndsWith("\r\n", StringComparison.Ordinal))
                throw new InvalidDataException("officer-editor.portrait.content_line_end:" + resource.Relative);
            var records = new List<PortraitContentRecord>();
            foreach (string line in text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Split(',');
                if (fields.Length != 3 || !int.TryParse(fields[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int type) || !int.TryParse(fields[1],
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classification))
                    throw new InvalidDataException("officer-editor.portrait.content_csv:" + resource.Relative);
                records.Add(new(slot, appId.Value, type, id, classification, resource.Source.ResourceUri));
            }
            PortraitPlatformEntitlement entitlement = platform.TryGetValue(appId.Value, out var fact)
                ? fact : new(null, null);
            files.Add(new(slot, appId.Value, resource.Source, entitlement, records.AsReadOnly()));
        }
        return new(files.ToArray(), platform);
    }
}

public sealed partial class GameResourceRepository
{
    internal IEnumerable<(string Relative, byte[] Bytes, SourceProvenance Source)> ReadPortraitContentResources()
    {
        string directory = SourcePath("DLC");
        string[] children;
        try { children=Directory.GetDirectories(directory); }
        catch(DirectoryNotFoundException) { yield break; }
        // The source loader visits the DLC directory and one immediate directory
        // level. It does not recursively search arbitrary installation contents.
        foreach (string folder in new[] { directory }.Concat(children.Order(StringComparer.OrdinalIgnoreCase)))
            foreach (string file in Directory.EnumerateFiles(folder, "*")
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                if (!Path.GetFileName(file).Contains("DLCInfo", StringComparison.Ordinal)) continue;
                string relative = Path.GetRelativePath(SourceDirectory, file).Replace('\\', '/');
                _residentSources.PortraitMetadataSources.TryAdd(GameResourceId.File(relative), 0);
                var source = ReadSource(relative);
                yield return (relative, source.Bytes, source.Provenance);
            }
    }
}
