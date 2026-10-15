using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The CommonSetting Environment columns select a SceneDatabase file,
/// independently of the A_CE name hash used to find that row.</summary>
public sealed record MapSeasonActionSource(MapSeasonSelection Selection,
    LoadedObjects CommonSetting, int SourceRowIndex, uint DatabaseFileId,
    string ResourceUri, string SourceEvidence);

public sealed record MapSeasonActionDatabase(MapSeasonActionSource Action,
    LoadedObjects Objects, IReadOnlyList<DokObject> CameraPackages);

/// <summary>A declared Camera package in the selected environment database.
/// Reading it does not apply the native external-package interpolation callback.</summary>
public sealed record MapSeasonCameraPackage(MapSeasonActionSource Action,
    LoadedObjects Objects, DokObject Camera, string SourceEvidence)
{
    public uint SkyDomeSceneKey { get; } = CameraDeclarationImport.ReadSkyDomeSceneKey(Camera);
}

public sealed record MapSeasonEffectInfo(byte[] Bytes, SourceProvenance Source);

public sealed record MapSeasonEffectRequest(MapSeasonSelection Selection,
    MapSeasonEffectInfo Info, int SourceRowIndex, byte[] RawRow,
    bool UsesFe4Namespace, uint DatabaseFileId, string ResourceUri,
    string NamedObject, uint NamedObjectHash, string SourceEvidence);

public sealed record MapSeasonEffectObject(MapSeasonEffectRequest Request,
    LoadedObjects Objects, DokObject Effect, string SourceEvidence);

public sealed record MapSeasonSourceResources(MapSeasonSelection Selection,
    MapSeasonActionDatabase Action, MapSeasonEffectObject Effect)
{
    public bool NativePackageApplicationExecuted => false;
    public bool FinalSeasonDisplayVerified => false;
}

/// <summary>On-demand consumption of the actual CommonSetting environment file
/// columns and effect_info.bin namespace rows. Native named-object IDs never
/// become archive file IDs. Source/override identities are retained and reparsed.</summary>
public static class MapSeasonResourceConsumer
{
    public const uint CommonSettingFileId = 0x6D011726;
    public const uint CommonSettingObjectId = 0x61AD16CF;
    public const uint CommonSettingClassId = 0xDAC911D7;
    public const uint CameraPackageClassId = 0x23E7C64E;
    public const uint EffectObjectClassId = 0xE736B039;
    public const uint Ce1EffectDatabaseFileId = 0x97485E9B;
    public const uint Fe4EffectDatabaseFileId = 0xB7EFA6B7;
    public const int EffectInfoRowCount = 137;
    public const string EffectInfoRelativePath = "0000/effect_info.bin";
    public const string SourceEffectInfoSha256 =
        "281F8E624D20DCBDF7F91CE1F44212B9F592F78F14EB4A022A929550BCD61477";
    public const string NativeEvidence =
        "docs/.local/ce-season-resource-consumer-20261002/contract.json";

    public static MapSeasonActionSource LoadActionSource(
        GameResourceRepository repository, int sourceMonth)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadActionSource(repository.LoadObjects("KIDSSystemResource", CommonSettingFileId), sourceMonth);
    }

    public static MapSeasonActionSource ReadActionSource(LoadedObjects commonSetting,
        int sourceMonth)
    {
        MapSeasonSelection selection = MapSeasonMath.SelectMonth(sourceMonth);
        LoadedObjects canonical = Canonical(commonSetting, CommonSettingFileId);
        DokObject owner = Object(canonical, CommonSettingObjectId, CommonSettingClassId);
        DokProperty names = Property(owner, 4, 0x3302F081, 5);
        DokProperty databases = Property(owner, 5, 0x0D92FA32, 5);
        uint[] nameValues = Words(names), databaseValues = Words(databases);
        TerrainSeasonActionLookup match = TerrainSeasonPreparation.LookupActionObject(
            selection, nameValues, databaseValues);
        if (!match.Matched || match.ActionObjectIdentifier == 0)
            throw new InvalidDataException("Selected original season has no nonzero EnvironmentDatabaseNameHashArray row.");
        uint fileId = match.ActionObjectIdentifier;
        return new(selection, canonical, match.SourceRowIndex, fileId,
            GameResourceId.Rdb("KIDSSystemResource", fileId).ToString(),
            Evidence(canonical, owner) + $"/EnvironmentKeyHashArray[{match.SourceRowIndex}]" +
            $"->EnvironmentDatabaseNameHashArray[{match.SourceRowIndex}]={fileId:X8}; " +
            "TC BCACB2..BCAD07, BCB3F0..BCB467, BCE470; " +
            "B3C100/B3AF90->SceneDatabase class1DC64B2F field0 SceneDatabaseFileNameHash; " + NativeEvidence);
    }

    public static MapSeasonActionDatabase LoadActionDatabase(GameResourceRepository repository,
        MapSeasonActionSource action)
    {
        ArgumentNullException.ThrowIfNull(repository);
        action = ValidateAction(action);
        return ReadActionDatabase(action, repository.LoadObjects("KIDSSystemResource", action.DatabaseFileId));
    }

    public static MapSeasonActionDatabase ReadActionDatabase(MapSeasonActionSource action,
        LoadedObjects database)
    {
        action = ValidateAction(action);
        LoadedObjects canonical = Canonical(database, action.DatabaseFileId);
        DokObject[] cameras = canonical.Document.Objects.Where(item =>
            item.Kind == DokObjectKind.Inline && item.ClassId == CameraPackageClassId).ToArray();
        return new(action, canonical, Array.AsReadOnly(cameras));
    }

    public static MapSeasonCameraPackage ReadCameraPackage(MapSeasonActionDatabase database,
        uint originalCameraObjectId)
    {
        ArgumentNullException.ThrowIfNull(database);
        MapSeasonActionDatabase canonical = ReadActionDatabase(database.Action, database.Objects);
        DokObject camera = Object(canonical.Objects, originalCameraObjectId, CameraPackageClassId);
        return new(canonical.Action, canonical.Objects, camera,
            canonical.Action.SourceEvidence + " -> " + Evidence(canonical.Objects, camera));
    }

    public static MapSeasonEffectInfo LoadEffectInfo(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        GameResourceId id = GameResourceId.File(EffectInfoRelativePath);
        string? overridePath = new ResourceOverrides(repository.OverrideDirectory).Find(id);
        string path = overridePath ?? Path.GetFullPath(Path.Combine(repository.SourceDirectory,
            EffectInfoRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        byte[] bytes = File.ReadAllBytes(path);
        var source = new SourceProvenance(EffectInfoRelativePath, bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)))
        {
            ResourceUri = id.ToString(), PhysicalPath = path, IsOverride = overridePath is not null
        };
        return ValidateInfo(new(bytes, source));
    }

    public static MapSeasonEffectRequest ReadEffectRequest(MapSeasonEffectInfo info,
        int sourceMonth)
    {
        info = ValidateInfo(info);
        MapSeasonSelection selection = MapSeasonMath.SelectMonth(sourceMonth);
        int row = selection.EffectEventTableIndex;
        byte[] raw = info.Bytes.AsSpan(row * 4, 4).ToArray();
        bool fe4 = (raw[0] & 8) != 0;
        uint fileId = fe4 ? Fe4EffectDatabaseFileId : Ce1EffectDatabaseFileId;
        string name = (fe4 ? "FE4Effect" : "CE1Effect") + "［" + selection.EffectEventName + "］";
        return new(selection, info, row, raw, fe4, fileId,
            GameResourceId.Rdb("KIDSSystemResource", fileId).ToString(), name,
            ResourceNameHash.Compute(name),
            $"{info.Source.ResourceUri}@{info.Source.Sha256}#row{row}/byte{row * 4}; " +
            "TC37CFA0->3E50D0(table0=0000/effect_info.bin), 37D1E5..37D1FA copies548 bytes; " +
            "37DBA2..37DBF9 bit3 selects format, 37DCA6..37DCCA selects source slot and named object; " + NativeEvidence);
    }

    public static MapSeasonEffectObject LoadEffectObject(GameResourceRepository repository,
        MapSeasonEffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(repository);
        request = ValidateRequest(request);
        return ReadEffectObject(request, repository.LoadObjects("KIDSSystemResource", request.DatabaseFileId));
    }

    public static MapSeasonEffectObject ReadEffectObject(MapSeasonEffectRequest request,
        LoadedObjects database)
    {
        request = ValidateRequest(request);
        LoadedObjects canonical = Canonical(database, request.DatabaseFileId);
        DokObject effect = Object(canonical, request.NamedObjectHash, EffectObjectClassId);
        return new(request, canonical, effect, request.SourceEvidence + " -> " + Evidence(canonical, effect));
    }

    public static MapSeasonSourceResources Load(GameResourceRepository repository, int sourceMonth)
    {
        MapSeasonActionSource action = LoadActionSource(repository, sourceMonth);
        MapSeasonActionDatabase database = LoadActionDatabase(repository, action);
        MapSeasonEffectRequest effect = ReadEffectRequest(LoadEffectInfo(repository), sourceMonth);
        return new(action.Selection, database, LoadEffectObject(repository, effect));
    }

    private static MapSeasonActionSource ValidateAction(MapSeasonActionSource action)
    {
        ArgumentNullException.ThrowIfNull(action);
        TerrainSeasonPreparation.ValidateSelection(action.Selection);
        MapSeasonActionSource actual = ReadActionSource(action.CommonSetting,
            action.Selection.SeasonIndex * 3 + 1);
        if (actual.SourceRowIndex != action.SourceRowIndex || actual.DatabaseFileId != action.DatabaseFileId ||
            actual.ResourceUri != action.ResourceUri)
            throw new InvalidDataException("Season action selection differs from its original source row.");
        return actual;
    }

    private static MapSeasonEffectRequest ValidateRequest(MapSeasonEffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RawRow);
        TerrainSeasonPreparation.ValidateSelection(request.Selection);
        MapSeasonEffectRequest actual = ReadEffectRequest(request.Info,
            request.Selection.SeasonIndex * 3 + 1);
        if (actual.SourceRowIndex != request.SourceRowIndex || actual.UsesFe4Namespace != request.UsesFe4Namespace ||
            actual.DatabaseFileId != request.DatabaseFileId || actual.ResourceUri != request.ResourceUri ||
            actual.NamedObject != request.NamedObject || actual.NamedObjectHash != request.NamedObjectHash ||
            !actual.RawRow.AsSpan().SequenceEqual(request.RawRow))
            throw new InvalidDataException("Season effect request differs from its original flag row and signed UTF-8 name.");
        return actual;
    }

    private static MapSeasonEffectInfo ValidateInfo(MapSeasonEffectInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(info.Bytes);
        ArgumentNullException.ThrowIfNull(info.Source);
        if (info.Bytes.Length != EffectInfoRowCount * 4 || info.Source.Bytes != info.Bytes.Length ||
            info.Source.ResourceUri != GameResourceId.File(EffectInfoRelativePath).ToString() ||
            Convert.ToHexString(SHA256.HashData(info.Bytes)) != info.Source.Sha256)
            throw new InvalidDataException("Original effect_info.bin identity or complete 137-row bytes differ.");
        return info;
    }

    private static LoadedObjects Canonical(LoadedObjects objects, uint expectedFileId)
    {
        ArgumentNullException.ThrowIfNull(objects);
        TerrainSeasonResources.Identity(objects.Source);
        if (objects.Source.FileId != expectedFileId ||
            objects.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", expectedFileId).ToString())
            throw new InvalidDataException("Season database has a different original archive/file identity.");
        return new(DokReader.Read(objects.Source.Bytes), objects.Source);
    }

    private static DokObject Object(LoadedObjects objects, uint id, uint classId)
    {
        DokObject[] matches = objects.Document.Objects.Where(item => item.ObjectId == id).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != classId)
            throw new InvalidDataException($"Original season object {id:X8} is missing or has a different typed factory.");
        return matches[0];
    }

    private static DokProperty Property(DokObject owner, int ordinal, uint hash, uint type)
    {
        if (owner.Properties.Count <= ordinal) throw new InvalidDataException("Season CommonSetting column is missing.");
        DokProperty property = owner.Properties[ordinal];
        if (property.Index != ordinal || property.NameHash != hash || property.TypeCode != type)
            throw new InvalidDataException("Season CommonSetting column has a different original descriptor.");
        return property;
    }

    private static uint[] Words(DokProperty property)
    {
        if (property.RawValue.Length != property.ElementCount * 4)
            throw new InvalidDataException("Season source column has invalid word bytes.");
        var result = new uint[property.ElementCount];
        for (int i = 0; i < result.Length; i++)
            result[i] = BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(i * 4, 4));
        return result;
    }

    private static string Evidence(LoadedObjects objects, DokObject owner) =>
        $"{objects.Source.ResourceUri}@{objects.Source.DecodedSha256}#object{owner.ObjectId:X8}/class{owner.ClassId:X8}";
}
