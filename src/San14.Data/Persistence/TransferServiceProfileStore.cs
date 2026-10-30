using System.Security.Cryptography;
using System.Text;
using San14.Core;

namespace San14.Data;

/// <summary>Independent application progress, with one application writer.
/// Campaign saves never restore these flags. Save replaces only this profile;
/// it does not commit a campaign save or provide a cross-file transaction.</summary>
public sealed class TransferServiceProfileStore
{
    private static readonly byte[] Magic = "S14P"u8.ToArray();
    private const int Version = 5;
    private readonly string _path;
    private TransferServiceState _current;

    private TransferServiceProfileStore(string path, TransferServiceState current, bool legacyPreferencesImported = false)
    { _path = Path.GetFullPath(path); _current = current; LegacyPreferencesImported = legacyPreferencesImported; }

    public bool LegacyPreferencesImported { get; private set; }

    /// <summary>A private service copy for the next simulation operation.</summary>
    public TransferServiceState Current => _current.Copy();

    /// <summary>Explicit first creation. An existing file, including a damaged
    /// one, is never replaced. Dates have their original reset values; the real
    /// host clock is read only when a completion flag is first written.</summary>
    public static TransferServiceProfileStore CreateNew(string path)
    {
        var service = BindLocalService();
        service.InitializeConstructedDates();
        service.ResetLocalFields();
        var store = new TransferServiceProfileStore(path, service);
        AtomicSaveFiles.Write(path, Encode(store._current, false), overwrite: false);
        return store;
    }

    /// <summary>Missing, unsupported or damaged existing data fails explicitly.
    /// Opening never creates, resets, upgrades or rewrites the file.</summary>
    public static TransferServiceProfileStore OpenExisting(string path)
    {
        var decoded = Decode(File.ReadAllBytes(path));
        return new(path, decoded.State, decoded.LegacyPreferencesImported);
    }

    /// <summary>Moves only observed legacy progress into this one profile.
    /// The progress and migration marker share one atomic durable replacement.</summary>
    public TransferServiceState MergeLegacyProgress(IReadOnlyList<uint> tracks, IEnumerable<int> completedScenarios)
    {
        if (LegacyPreferencesImported) return Current;
        if (tracks.Count != 3 || tracks.Any(value => value > 2))
            throw new InvalidDataException("Legacy Ending progress has an invalid track shape.");
        var next = _current.Copy();
        for (int i = 0; i < 3; i++)
            if (tracks[i] != 0 && (next.ReadEndingTrack(i) is not uint old || old < tracks[i]))
                next.SetEndingTrack(i, tracks[i]);
        foreach (int id in completedScenarios) next.SetScenarioCompleted(id, true);
        AtomicSaveFiles.Write(_path, Encode(next, true));
        _current = next; LegacyPreferencesImported = true;
        return Current;
    }

    /// <summary>Failure propagates without replacing the store's current owner
    /// or mutating the supplied pending result. The caller decides when to retry
    /// and when to publish its paired in-memory campaign/service operation.</summary>
    public TransferServiceState Save(TransferServiceState service)
    {
        ArgumentNullException.ThrowIfNull(service);
        var next = service.Copy();
        byte[] bytes = Encode(next, LegacyPreferencesImported); //Validate before opening a temporary file.
        AtomicSaveFiles.Write(_path, bytes);
        _current = next; //No live owner changes before the durable replacement succeeds.
        return Current;
    }

    private static byte[] Encode(TransferServiceState snapshot, bool legacyPreferencesImported)
    {
        if (snapshot.PlatformPolicy != TransferServicePlatformPolicy.Absent)
            throw new InvalidDataException("The local application profile requires an explicitly absent platform service.");
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            WriteBitmap(writer, snapshot.ReadPrimaryFlag);
            WriteBitmap(writer, snapshot.ReadSecondaryFlag);
            writer.Write(snapshot.DirtyFlag ?? throw Missing("dirty flag"));
            for (int i = 0; i < 57; i++)
            {
                var date = snapshot.ReadDate(i);
                ValidateDate(date);
                writer.Write(date.Year!.Value); writer.Write(date.Month!.Value);
                writer.Write(date.Day!.Value); writer.Write(date.Hour!.Value); writer.Write(date.RawByte5!.Value);
            }
            //Version1 predates the independent service event history. Preserve
            //that explicit unknown owner on migration; never infer fresh zeros.
            bool known = snapshot.ReadEventFlag(0).HasValue;
            if (Enumerable.Range(0, 1024).Any(id => snapshot.ReadEventFlag(id).HasValue != known))
                throw Missing("partially known event history");
            writer.Write(known);
            if (known)
                for (int word = 0; word < 16; word++) WriteBitmap(writer, bit => snapshot.ReadEventFlag(word * 64 + bit));
            //Per-bit knowledge preserves actual writes imported into an older
            //profile without inventing the other scenario completion bits.
            for (int i = 0; i < 200; i++)
                writer.Write(snapshot.ReadScenarioCompleted(i) switch { null => (byte)0, false => (byte)1, true => (byte)2 });
            for (int i = 0; i < 3; i++)
            {
                uint? progress = snapshot.ReadEndingTrack(i);
                writer.Write(progress.HasValue);
                if (progress.HasValue) writer.Write(progress.Value);
            }
            writer.Write(legacyPreferencesImported);
            for (int i = 0; i < 61; i++)
                writer.Write(snapshot.ReadBonusOfficerUnlocked(i) switch { null => (byte)0, false => (byte)1, true => (byte)2 });
            for (int i = 0; i < 20; i++)
                writer.Write(snapshot.ReadTutorialCompleted(i) switch { null => (byte)0, false => (byte)1, true => (byte)2 });
            for (int i = 0; i < 13; i++)
                writer.Write(snapshot.ReadMainCommandSeen(i) switch { null => (byte)0, false => (byte)1, true => (byte)2 });
        }
        byte[] body = payload.ToArray();
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic); writer.Write(Version); writer.Write(body.Length);
            writer.Write(SHA256.HashData(body)); writer.Write(body);
        }
        return output.ToArray();
    }

    private static (TransferServiceState State, bool LegacyPreferencesImported) Decode(byte[] bytes)
    {
        try
        {
            using var input = new MemoryStream(bytes);
            using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
            bool magic = reader.ReadBytes(4).SequenceEqual(Magic);
            int version = reader.ReadInt32();
            if (!magic || version is not (1 or 2 or 3 or 4 or Version))
                throw new InvalidDataException("Application profile format is unsupported.");
            int length = reader.ReadInt32();
            byte[] checksum = reader.ReadBytes(32);
            if ((version == 1 ? length != 359 : version == 2 ? length is not (360 or 488) :
                    version == 3 ? length is < 625 or > 765 : version == 4 ? length is < 645 or > 785 : length is < 658 or > 798) ||
                length != input.Length - input.Position || checksum.Length != 32)
                throw new InvalidDataException("Application profile payload is incomplete.");
            byte[] payload = reader.ReadBytes(length);
            if (!CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(payload)))
                throw new InvalidDataException("Application profile checksum does not match.");
            using var body = new MemoryStream(payload);
            using var fields = new BinaryReader(body, Encoding.UTF8, leaveOpen: true);
            var service = BindLocalService(); //Bindings only: no reset or missing-field defaults.
            ReadBitmap(fields, service.SetPrimaryFlag);
            ReadBitmap(fields, service.SetSecondaryFlag);
            service.WriteDirtyFlag(fields.ReadByte());
            for (int i = 0; i < 57; i++)
            {
                var date = new TransferServiceDate(fields.ReadUInt16(), fields.ReadByte(), fields.ReadByte(), fields.ReadByte(), fields.ReadByte());
                ValidateDate(date); service.SetDate(i, date);
            }
            if (version >= 2)
            {
                byte known = fields.ReadByte();
                if (known > 1 || version == 2 && (known == 1 ? length != 488 : length != 360))
                    throw new InvalidDataException("Application event history presence is invalid.");
                if (known == 1)
                    for (int word = 0; word < 16; word++) ReadBitmap(fields, (bit, value) => service.SetEventFlag(word * 64 + bit, value));
            }
            bool legacyPreferencesImported = false;
            if (version >= 3)
            {
                for (int i = 0; i < 200; i++) service.SetScenarioCompleted(i, fields.ReadByte() switch
                {
                    0 => null, 1 => false, 2 => true,
                    _ => throw new InvalidDataException("Application scenario completion knowledge is invalid.")
                });
                for (int i = 0; i < 3; i++)
                {
                    byte known = fields.ReadByte();
                    if (known > 1) throw new InvalidDataException("Application Ending track knowledge is invalid.");
                    if (known == 1) service.SetEndingTrack(i, fields.ReadUInt32());
                }
                byte migrated = fields.ReadByte();
                if (migrated > 1) throw new InvalidDataException("Application progress migration marker is invalid.");
                legacyPreferencesImported = migrated == 1;
                for (int i = 0; i < 61; i++) service.SetBonusOfficerUnlocked(i, fields.ReadByte() switch
                {
                    0 => null, 1 => false, 2 => true,
                    _ => throw new InvalidDataException("Application bonus officer knowledge is invalid.")
                });
            }
            if (version >= 4)
                for (int i = 0; i < 20; i++) service.SetTutorialCompleted(i, fields.ReadByte() switch
                {
                    0 => null, 1 => false, 2 => true,
                    _ => throw new InvalidDataException("Application tutorial completion knowledge is invalid.")
                });
            //Older profiles never owned these facts; construction intentionally
            //leaves them unknown until a real launch establishes a seen bit.
            if (version >= 5)
                for (int i = 0; i < 13; i++) service.SetMainCommandSeen(i, fields.ReadByte() switch
                {
                    0 => null, 1 => false, 2 => true,
                    _ => throw new InvalidDataException("Application main command discovery knowledge is invalid.")
                });
            if (body.Position != body.Length) throw new InvalidDataException("Application profile contains unexpected fields.");
            return (service, legacyPreferencesImported);
        }
        catch (EndOfStreamException error)
        { throw new InvalidDataException("Application profile is truncated.", error); }
    }

    private static TransferServiceState BindLocalService() => new()
    {
        PlatformPolicy = TransferServicePlatformPolicy.Absent,
        ClockSnapshotProvider = () =>
        {
            DateTime now = DateTime.Now;
            return new(now.Year - 1900, now.Month - 1, now.Day, now.Hour);
        }
    };

    private static void WriteBitmap(BinaryWriter writer, Func<int, bool?> read)
    {
        ulong bits = 0;
        for (int i = 0; i < 64; i++) if (read(i) ?? throw Missing("completion bit")) bits |= 1UL << i;
        writer.Write(bits);
    }

    private static void ReadBitmap(BinaryReader reader, Action<int, bool?> write)
    {
        ulong bits = reader.ReadUInt64();
        for (int i = 0; i < 64; i++) write(i, (bits & (1UL << i)) != 0);
    }

    private static void ValidateDate(TransferServiceDate date)
    {
        if (date.Year is null || date.Month is not (>= 1 and <= 12) || date.Day is not (>= 1 and <= 31) ||
            date.Hour is not (>= 0 and <= 23) || date.RawByte5 is null)
            throw new InvalidDataException("Application profile completion date is missing or invalid.");
    }
    private static InvalidDataException Missing(string field) => new("Application profile has an unknown " + field + ".");
}
