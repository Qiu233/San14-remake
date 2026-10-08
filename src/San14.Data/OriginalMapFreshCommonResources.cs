using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact explicitly selected saved CommonViewSetting. Values are
/// retained from owned canonical source bytes after the original property
/// loader; this object is not a later prepared frame.</summary>
public sealed class LoadedOriginalMapFreshCommonSource
{
    internal LoadedOriginalMapFreshCommonSource(uint flags8, uint flags0C, uint flags10, byte viewAux3F2, byte modelGroupSelector400, string identity)
    { Flags8 = flags8; Flags0C = flags0C; Flags10=flags10; ViewAux3F2 = viewAux3F2; ModelGroupSelector400 = modelGroupSelector400; SourceIdentity = identity; }
    public uint Flags8 { get; }
    public uint Flags0C { get; }
    /// <summary>Property44 AABBEBFF, D22F48 source setting+10. Its third
    /// byte is Sky EA3's actual Aux+12; it is independent of flags0C.</summary>
    public uint Flags10{get;}
    public byte ViewAux3F2 { get; }
    public byte ModelGroupSelector400 { get; }
    public string SourceIdentity { get; }
}

public enum OriginalMapFreshCommonResourceScope
{
    SourceCreatedNoCallbackBeforeAmbientPublication,
    ExplicitCurrent
}

/// <summary>Same-frame F103 Common table Aux and selected View ambient fields.
/// The historical Variant input names Resource788/78C/8B8 refer to stack-relative
/// names; their actual source fields are ambient packet+4C8/+4CC/+5F8. Native
/// E41710 is an enum classifier and does not consult a backend registry.</summary>
public sealed class OriginalMapFreshCommonResourceSnapshot
{
    private readonly byte[]? auxiliary, ambient;
    internal OriginalMapFreshCommonResourceSnapshot(long frameNumber, string settingsOwner,
        string commonTableOwner, string selectedViewOwner, string provenance,
        OriginalMapFreshCommonResourceScope scope, uint flags8, uint? flags0C,
        byte[]? auxiliary, byte[]? ambient, byte? sourceViewAux3F2 = null, byte? sourceModelGroupSelector400 = null,
        byte? sourceAuxiliaryByte12=null)
    {
        FrameNumber = frameNumber; SettingsOwnerIdentity = settingsOwner;
        CommonTableOwnerIdentity = commonTableOwner; SelectedViewOwnerIdentity = selectedViewOwner;
        ProducerProvenance = provenance; Scope = scope; CommonViewFlags8 = flags8;
        Resource240Flags0C = flags0C; this.auxiliary = auxiliary; this.ambient = ambient;
        AmbientWord4C8 = ambient is null ? null : Word(ambient, 0x4C8);
        AmbientWord4CC = ambient is null ? null : Word(ambient, 0x4CC);
        AmbientWord5F8 = ambient is null ? null : Word(ambient, 0x5F8);
        CurrentAuxiliaryExclusion404 = auxiliary is null ?
            scope == OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication ? 0u : null :
            Word(auxiliary, 0x404);
        CurrentViewAux3F2 = auxiliary is null ? sourceViewAux3F2 : auxiliary[0x3F2];
        CurrentModelGroupSelector400 = auxiliary is null ? sourceModelGroupSelector400 : auxiliary[0x400];
        CurrentAuxiliaryByte12=auxiliary is null?sourceAuxiliaryByte12:auxiliary[0x12];
    }
    public long FrameNumber { get; }
    public string SettingsOwnerIdentity { get; }
    public string CommonTableOwnerIdentity { get; }
    public string SelectedViewOwnerIdentity { get; }
    public string ProducerProvenance { get; }
    public OriginalMapFreshCommonResourceScope Scope { get; }
    public uint CommonViewFlags8 { get; }
    public bool CommonViewResource240Present => Resource240Flags0C.HasValue;
    public uint? Resource240Flags0C { get; }
    public bool NativeArg18 => Resource240Flags0C is uint flags && (flags & 0x800) == 0;
    public bool QueryD5BeA0 => ambient is not null;
    public uint? AmbientWord4C8 { get; }
    public uint? AmbientWord4CC { get; }
    public uint? AmbientWord5F8 { get; }
    /// <summary>Native C392B0 ORs this current Aux word into ModelCPU78.
    /// D223C0's source loader writes zero at404 and D1E410 preserves it
    /// when the owned runtime overlay is absent. An absent current Aux has
    /// no resolved exclusion field.</summary>
    public uint? CurrentAuxiliaryExclusion404 { get; }
    /// <summary>Current source CE31D0 primary-mask selector. The saved
    /// Common property25 is loaded into this byte; it is not a guessed0.</summary>
    public byte? CurrentViewAux3F2 { get; }
    public byte? CurrentModelGroupSelector400 { get; }
    /// <summary>The current same-frame Sky enable byte at Aux+12. The
    /// finite fresh source publishes only this proved field, not a complete
    /// invented460B packet. Explicit current absence remains null.</summary>
    public byte? CurrentAuxiliaryByte12{get;}
    public bool CommonViewResource788Registered => AmbientWord4C8 is uint value && OriginalMapFreshCommonResources.ClassifyE41710(value) >= 0;
    public bool CommonViewResource78CRegistered => AmbientWord4CC is uint value && OriginalMapFreshCommonResources.ClassifyE41710(value) >= 0;
    public bool CommonViewResource8B8Registered => AmbientWord5F8 is uint value && OriginalMapFreshCommonResources.ClassifyE41710(value) >= 0;
    /// <summary>Detached full current records. The finite fresh subset supplies
    /// no invented unused Aux bytes or ambient packet.</summary>
    public byte[]? CopyCurrentAuxiliary460() => auxiliary?.ToArray();
    public byte[]? CopyCurrentAmbientA40() => ambient?.ToArray();

    public void ValidateFrame(long frameNumber, string commonTableOwner, string selectedViewOwner)
    {
        if (frameNumber != FrameNumber || commonTableOwner != CommonTableOwnerIdentity ||
            selectedViewOwner != SelectedViewOwnerIdentity)
            throw new InvalidDataException("Common Aux and ambient source fields belong to a different accepted frame or View owner.");
    }
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}

/// <summary>Finite original source construction and explicit current reads.
/// This owned factory constructs its source settings with the literal no-callback
/// argument proved by D214DE/D214E3, then applies the saved property loader.
/// CommonContext D01580 clears overlay-active C+3324.bit2; typed getter
/// D5BAF0 consequently supplies null to F43360/D1E410. Source-to-mutable then
/// mutable-to-arena copying preserves the saved flags. The before-Ambient
/// route applies only to a newly constructed selected View whose D4BD30
/// publisher has not run; later packet publication must use the current route.</summary>
public static class OriginalMapFreshCommonResources
{
    public const uint SourceFileId = 0x6D011726;
    public const uint SourceObjectId = 0x899F101B;
    public const string SourceResourceUri = "san14://rdb/KIDSSystemResource/6D011726";
    public const string SourceSha256 = "7CBF8F97D8AE7375346C97DD433F83B8831F8461689C98391527BE20BA8B3F61";

    public static LoadedOriginalMapFreshCommonSource ReadSourceTemplate(LoadedObjects source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var asset = source.Source;
        if (asset is null || asset.Bytes is null || asset.FileId != SourceFileId ||
            asset.ResourceUri != SourceResourceUri || asset.DecodedSha256 != SourceSha256 ||
            asset.OverrideSource is not null || asset.IndexSource is null || asset.IndexSource.IsOverride)
            throw new InvalidDataException("Fresh Common source requires original KIDSSystemResource6D011726 without overrides.");
        byte[] owned = asset.Bytes.ToArray();
        if (Convert.ToHexString(SHA256.HashData(owned)) != SourceSha256)
            throw new InvalidDataException("Fresh Common source bytes differ from their exact original identity.");
        var canonical = DokReader.Read(owned);
        VerifyDocument(source.Document, canonical);
        var candidates = canonical.Objects.Where(o => o.ObjectId == SourceObjectId).ToArray();
        if (candidates.Length != 1 || candidates[0].Kind != DokObjectKind.Inline ||
            candidates[0].ClassId != 0xEF3D849D || candidates[0].Reference is not null || candidates[0].Properties.Count != 102)
            throw new InvalidDataException("Fresh Common source requires the exact inline102-property object899F101B/classEF3D849D.");
        uint Scalar(int ordinal, uint hash, uint type)
        {
            var row = candidates[0].Properties[ordinal];
            if (row.Index != ordinal || row.NameHash != hash || row.TypeCode != type ||
                row.ElementCount != 1 || row.ElementSize != 4 || row.RawValue.Length != 4)
                throw new InvalidDataException("Fresh Common source scalar differs from its exact native property binding.");
            return BinaryPrimitives.ReadUInt32LittleEndian(row.RawValue);
        }
        uint selector = Scalar(25, 0xA0BFFAC2, 4);
        uint modelSelector = Scalar(35, 0x92E9E9E7, 4);
        if (selector > byte.MaxValue || modelSelector > byte.MaxValue) throw new InvalidDataException("Source Common selectors require their byte loader domains.");
        return new(Scalar(0, 0x74C9D1B9, 4), Scalar(1, 0xF668216E, 5),Scalar(44,0xAABBEBFF,5), (byte)selector, (byte)modelSelector,
            SourceResourceUri + $"#object/{SourceObjectId:X8}@{SourceSha256};CommonViewSetting;constructor=TC:D02010/SC:D02D40;loader=TC:D223C0/SC:D230F0");
    }

    /// <summary>Successful owned saved-property application and arena Aux publication.
    /// The owned source settings use the original D214DE/D214E3 constructor's
    /// null callback argument before the102-property loader. That loader does
    /// not replace+F0. The CommonContext and selected View are fresh, and no
    /// runtime settings callback or overlay is installed before this boundary.
    /// This does not resolve the original registry creator F7ED0's argument.
    /// This scope must end before any D4BD30/D6ABE0 ambient publication.</summary>
    public static OriginalMapFreshCommonResourceSnapshot PrepareSourceCreatedBeforeAmbientPublication(
        LoadedOriginalMapFreshCommonSource source, long frameNumber, string settingsOwnerIdentity,
        string commonTableOwnerIdentity, string selectedViewOwnerIdentity, string producerProvenance)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIdentity(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity, producerProvenance);
        return new(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity,
            producerProvenance + ";" + source.SourceIdentity +
            ";ownedSourceSettingsCtor=TC:D214DE/D214E3;ownedSourceSettingsF0=0;D223C0-propertiesApplied;freshCommonContext3324.bit2=0;D5BAF0-overlayAbsent;F43360-ownedTemplateApplied;D12B69-arenaAuxPublished;freshView1B90=0;beforeD4BD30",
            OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication,
            source.Flags8, source.Flags0C, null, null, source.ViewAux3F2, source.ModelGroupSelector400,
            (byte)(source.Flags10>>16));
    }

    /// <summary>Read explicitly resolved complete current records after their
    /// owned writers. Null Aux means the current Common table+240 is absent;
    /// null ambient means the selected View+1B90 is currently absent. These are
    /// explicit current selections, independent of constructor defaults.</summary>
    public static OriginalMapFreshCommonResourceSnapshot ReadExplicitCurrent(long frameNumber,
        string settingsOwnerIdentity, string commonTableOwnerIdentity, string selectedViewOwnerIdentity,
        ReadOnlyMemory<byte>? completeCurrentAuxiliary460, ReadOnlyMemory<byte>? completeCurrentAmbientA40,
        string producerProvenance)
    {
        ValidateIdentity(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity, producerProvenance);
        if (completeCurrentAuxiliary460 is { Length: not 0x460 } || completeCurrentAmbientA40 is { Length: not 0xA40 })
            throw new InvalidDataException("Current Common resources require the complete460-byte Aux and A40-byte ambient records when present.");
        byte[]? aux = completeCurrentAuxiliary460?.ToArray(), ambient = completeCurrentAmbientA40?.ToArray();
        uint flags8 = aux is null ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(aux.AsSpan(8, 4));
        uint? flags0C = aux is null ? null : BinaryPrimitives.ReadUInt32LittleEndian(aux.AsSpan(0xC, 4));
        return new(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity,
            producerProvenance, OriginalMapFreshCommonResourceScope.ExplicitCurrent, flags8, flags0C, aux, ambient);
    }

    /// <summary>Exact TC E41710 / SC counterpart: clear bit31; 1..3 map
    /// to0..2, every other uint maps to-1.</summary>
    public static int ClassifyE41710(uint value) => (value & 0x7FFFFFFF) is >= 1 and <= 3
        ? (int)((value & 0x7FFFFFFF) - 1) : -1;

    private static void ValidateIdentity(long frameNumber, params string[] identities)
    {
        if (frameNumber <= 0 || identities.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Common source fields require one actual positive frame and complete owned producer identities.");
    }
    private static void VerifyDocument(DokDocument supplied, DokDocument canonical)
    {
        static bool Equal(byte[]? x, byte[] y) => x is not null && x.AsSpan().SequenceEqual(y);
        if (supplied is null || supplied.FormatCode != canonical.FormatCode || supplied.Identifier != canonical.Identifier ||
            !Equal(supplied.RawHeader, canonical.RawHeader) || supplied.Objects is null || supplied.Objects.Count != canonical.Objects.Count)
            throw new InvalidDataException("Supplied Common DOK metadata differs from canonical source bytes.");
        for (int i = 0; i < canonical.Objects.Count; i++)
        {
            var x = supplied.Objects[i]; var y = canonical.Objects[i];
            if (x is null || x.Index != y.Index || x.Offset != y.Offset || x.Length != y.Length || x.ObjectId != y.ObjectId ||
                x.ClassId != y.ClassId || x.Kind != y.Kind || x.Reference != y.Reference || !Equal(x.RawHeader, y.RawHeader) ||
                !Equal(x.Padding, y.Padding) || x.Properties is null || x.Properties.Count != y.Properties.Count)
                throw new InvalidDataException("Supplied Common DOK object metadata differs from canonical source bytes.");
            for (int j = 0; j < y.Properties.Count; j++)
            {
                var a = x.Properties[j]; var b = y.Properties[j];
                if (a is null || a.Index != b.Index || a.DescriptorOffset != b.DescriptorOffset || a.ValueOffset != b.ValueOffset ||
                    a.TypeCode != b.TypeCode || a.ElementCount != b.ElementCount || a.NameHash != b.NameHash || a.ElementSize != b.ElementSize ||
                    !Equal(a.RawDescriptor, b.RawDescriptor) || !Equal(a.RawValue, b.RawValue))
                    throw new InvalidDataException("Supplied Common DOK property metadata differs from canonical source bytes.");
            }
        }
    }
}
