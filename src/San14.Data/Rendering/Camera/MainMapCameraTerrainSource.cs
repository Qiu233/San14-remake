using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The current Scene Landscape selected by SC 3FEA00 / TC 3FE4D0,
/// joined to its already prepared Field+B0 CPU grid. No renderer state is made.</summary>
public sealed class MainMapCameraTerrainSource
{
    public MapStaticModelGroundHeightSource CurrentField { get; }
    public LoadedTerrainParameterResources Selection { get; }
    public UnitWorldGroundHeightTerrainOwner Owner { get; }
    public UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainOwner> KnownReference =>
        UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainOwner>.Known(Owner);
    public string SourceProvenance { get; }

    private MainMapCameraTerrainSource(MapStaticModelGroundHeightSource currentField,
        LoadedTerrainParameterResources selection, UnitWorldGroundHeightTerrainOwner owner)
    {
        CurrentField = currentField; Selection = selection; Owner = owner;
        SourceProvenance = $"{currentField.Scene.Scene.Source.ResourceUri}@{currentField.Scene.Scene.Source.DecodedSha256}" +
            $"#Scene{currentField.Scene.RootObject.ObjectId:X8}/CF878F8D;" +
            $"{selection.Wrapper.Source.ResourceUri}@{selection.Wrapper.Source.DecodedSha256}#Landscape{selection.WrapperRoot.ObjectId:X8};" +
            $"{currentField.Terrain.Objects.Source.ResourceUri}@{currentField.Terrain.Objects.Source.DecodedSha256}" +
            $"#Terrain{currentField.Terrain.RootObject.ObjectId:X8}/Field{currentField.Terrain.FieldObject.ObjectId:X8};" +
            $"{currentField.Terrain.HeightField.Source.ResourceUri}@{currentField.Terrain.HeightField.Source.DecodedSha256};" +
            $"preparation={currentField.PreparationBranch};edits={currentField.CurrentEdits.Count}";
    }

    /// <summary>Requires an explicitly selected current Field, including its
    /// preparation branch and subsequent edits. The source graph must select
    /// exactly this Landscape, Terrain, Field and MHK revision.</summary>
    public static MainMapCameraTerrainSource FromCurrentField(
        MapStaticModelGroundHeightSource currentField)
    {
        ArgumentNullException.ThrowIfNull(currentField);
        LoadedSceneResources scene = currentField.Scene;
        LoadedTerrainResources terrain = currentField.Terrain;
        LoadedTerrainParameterResources selected = TerrainParameterResources.Read(scene);
        uint landscape = Scalar(scene.RootObject, 0xCF878F8D);
        if (landscape != selected.WrapperRoot.ObjectId)
            throw Error("Scene LandscapeObjectNameHash selects a different current Landscape owner");
        if (!Same(scene.TerrainBundle.Source, terrain.Objects.Source) ||
            selected.TerrainRoot.ObjectId != terrain.RootObject.ObjectId ||
            Scalar(selected.WrapperRoot, 0x445D803E) != terrain.RootObject.ObjectId)
            throw Error("Landscape holder+8 belongs to a different terrain revision");

        // Terrain loader publishes +50 only when the ordinal2 Field array and
        // ordinal3 float position array have counts N and 3*N. The two actual
        // scene declarations contain one Field; BA9890 selects its first Data.
        uint[] fields = Words(terrain.RootObject, 0xE9CC029E, 5);
        uint[] positions = Words(terrain.RootObject, 0xA45AD0E0, 8);
        if (fields.Length != 1 || positions.Length != 3 || fields[0] != terrain.FieldObject.ObjectId ||
            terrain.FieldObject.Kind != DokObjectKind.Inline || terrain.FieldObject.ClassId != 0x468415D9)
            throw Error("current terrain requires the verified single Field entry and its float3 position");
        if (Scalar(terrain.FieldObject, 0xF17661E1) != terrain.HeightField.Source.FileId ||
            currentField.Grid.Source is not { } gridSource ||
            !Same(gridSource.Source, terrain.HeightField.Source))
            throw Error("Field+B0 grid belongs to a different MHK source revision");
        if (!Enum.IsDefined(currentField.PreparationBranch) || currentField.CurrentEdits is null)
            throw Error("current Field preparation or edit sequence is unknown");

        Verify(scene.Scene.Source); Verify(selected.Wrapper.Source);
        Verify(terrain.Objects.Source); Verify(terrain.HeightField.Source); Verify(gridSource.Source);
        // These references represent the owned CPU source graph. The exact
        // prepared grid and its sample array are shared without copying or
        // resetting later native Field edits.
        var data = new UnitWorldGroundHeightTerrainData(
            UnitWorldGroundHeightReference<UnitWorldGroundHeightGrid>.Known(currentField.Grid));
        var entry = new UnitWorldGroundHeightTerrainEntry(
            UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainData>.Known(data));
        IReadOnlyList<UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainEntry>> entries =
            Array.AsReadOnly(new[] { UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainEntry>.Known(entry) });
        var list = new UnitWorldGroundHeightTerrainList(1,
            UnitWorldGroundHeightReference<IReadOnlyList<UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainEntry>>>.Known(entries));
        var holder = new UnitWorldGroundHeightTerrainHolder(
            UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainList>.Known(list));
        var owner = new UnitWorldGroundHeightTerrainOwner(
            UnitWorldGroundHeightReference<UnitWorldGroundHeightTerrainHolder>.Known(holder));
        return new(currentField, selected, owner);
    }

    public float Query(float eyeX, float eyeZ) => UnitWorldGroundHeightScene.QueryBAA9E0(Owner, eyeX, eyeZ);

    public MainMapCameraTerrainPitchResult QualifyPitch(MainMapCameraTerrainPitchInput input) =>
        MapCameraController.QualifyTerrainPitch(input, KnownReference, SourceProvenance);

    private static uint Scalar(DokObject owner, uint name)
    {
        uint[] values = Words(owner, name, 5);
        if (values.Length != 1) throw Error($"object {owner.ObjectId:X8} property {name:X8} requires one scalar");
        return values[0];
    }
    private static uint[] Words(DokObject owner, uint name, uint type)
    {
        DokProperty[] matches = owner.Properties.Where(value => value.NameHash == name).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != type || matches[0].ElementCount < 0 ||
            matches[0].RawValue.LongLength != 4L * matches[0].ElementCount)
            throw Error($"object {owner.ObjectId:X8} requires complete type-{type} property {name:X8}");
        uint[] values = new uint[matches[0].ElementCount];
        for (int i = 0; i < values.Length; i++)
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(matches[0].RawValue.AsSpan(i * 4, 4));
        return values;
    }
    private static bool Same(LoadedRdbAsset a, LoadedRdbAsset b) => a.FileId == b.FileId &&
        a.ResourceUri == b.ResourceUri && a.DecodedSha256.Equals(b.DecodedSha256, StringComparison.OrdinalIgnoreCase);
    private static void Verify(LoadedRdbAsset source)
    {
        if (!Convert.ToHexString(SHA256.HashData(source.Bytes)).Equals(source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw Error($"source {source.ResourceUri} bytes differ from its decoded SHA256");
    }
    private static InvalidDataException Error(string message) => new($"Main map camera terrain: {message}.");
}
