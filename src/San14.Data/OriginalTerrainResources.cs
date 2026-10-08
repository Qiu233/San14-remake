using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedTerrainResources(LoadedObjects Objects, DokObject RootObject,
    DokObject FieldObject, SpatialTreeDocument SpatialTree, LoadedRdbAsset SpatialSource,
    LoadedHeightField HeightField);

/// <summary>
/// Follows the verified terrain bundle properties to their spatial and height resources.
/// Edition selection and spatial Identifier20 remain unresolved. The original LOD extrema
/// are retained and checked against the linked height grid, including overridden resources.
/// A changed height grid may therefore require a corresponding spatial-tree override.
/// </summary>
public static class OriginalTerrainResources
{
    private const uint RootClass = 0x774DDC07;
    private const uint FieldClass = 0x468415D9;
    private const double SampleRangeTolerance = 0.006;

    public static LoadedTerrainResources Load(OriginalGameRepository repository,
        string archive, uint bundleId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedObjects objects = repository.LoadObjects(archive, bundleId);
        string context = objects.Source.ResourceUri;
        DokObject[] roots = objects.Document.Objects.Where(item =>
            item.Kind == DokObjectKind.Inline && item.ClassId == RootClass).ToArray();
        if (roots.Length != 1)
            throw new InvalidDataException($"Terrain bundle {context} requires one root class {RootClass:X8}; found {roots.Length}.");
        DokObject root = roots[0];
        uint targetId = ReadReference(root, 0xE9CC029E, context);
        DokObject[] targets = objects.Document.Objects.Where(item => item.ObjectId == targetId).ToArray();
        if (targets.Length != 1 || targets[0].Kind != DokObjectKind.Inline ||
            targets[0].ClassId != FieldClass)
            throw new InvalidDataException($"Terrain bundle {context} target {targetId:X8} is missing, ambiguous or has an unsupported class.");
        DokObject field = targets[0];
        uint spatialId = ReadReference(field, 0xF981BC3D, context);
        uint heightId = ReadReference(field, 0xF17661E1, context);
        LoadedRdbAsset spatialSource;
        try { spatialSource = repository.LoadRdbAsset("FieldEditor4", spatialId); }
        catch (KeyNotFoundException exception)
        {
            throw MissingReference(context, field.ObjectId, 0xF981BC3D, spatialId, exception);
        }
        SpatialTreeDocument spatial;
        try { spatial = SpatialTreeReader.Read(spatialSource.Bytes); }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException($"Terrain spatial resource {spatialSource.ResourceUri}: {exception.Message}", exception);
        }
        LoadedHeightField height;
        try { height = repository.LoadHeightField("FieldEditor4", heightId); }
        catch (KeyNotFoundException exception)
        {
            throw MissingReference(context, field.ObjectId, 0xF17661E1, heightId, exception);
        }
        VerifyHeightLink(spatial, spatialSource, height);
        return new(objects, root, field, spatial, spatialSource, height);
    }

    private static InvalidDataException MissingReference(string bundleUri, uint objectId,
        uint propertyName, uint resourceId, KeyNotFoundException exception) => new(
            $"Terrain bundle {bundleUri} object {objectId:X8} property {propertyName:X8} references missing resource {GameResourceId.Rdb("FieldEditor4", resourceId)}. {exception.Message}",
            exception);

    private static uint ReadReference(DokObject item, uint name, string context)
    {
        DokProperty[] properties = item.Properties.Where(property => property.NameHash == name).ToArray();
        if (properties.Length != 1 || properties[0].TypeCode != 5 ||
            properties[0].ElementCount != 1 || properties[0].RawValue.Length != 4)
            throw new InvalidDataException($"Terrain bundle {context} object {item.ObjectId:X8} property {name:X8} must be a unique type-5 scalar reference.");
        return BinaryPrimitives.ReadUInt32LittleEndian(properties[0].RawValue);
    }

    private static void VerifyHeightLink(SpatialTreeDocument spatial, LoadedRdbAsset spatialSource,
        LoadedHeightField loadedHeight)
    {
        MhkHeightField height = loadedHeight.HeightField;
        string context = $"{spatialSource.ResourceUri} and {loadedHeight.Source.ResourceUri}";
        if ((double)height.CellWidth * height.SampleSpacing != spatial.RootSize ||
            (double)height.CellHeight * height.SampleSpacing != spatial.RootSize)
            throw new InvalidDataException($"Terrain resources {context} have different spatial extents.");
        // Only the proven 65-by-65 sample pattern is accepted. No interpolation or
        // silently substituted node ranges are used for incompatible grids.
        int leafCount = 1 << (spatial.LevelCount - 1);
        if (height.CellWidth != height.CellHeight || height.CellWidth % (leafCount * 64) != 0)
            throw new InvalidDataException($"Terrain resources {context} cannot use the verified 65-by-65 LOD sampling pattern.");
        foreach (SpatialTreeNode node in spatial.Nodes)
        {
            int depth = spatial.LevelCount - node.Level - 1;
            int patch = height.CellWidth >> depth, step = patch / 64;
            int originX = node.GridX * patch, originY = node.GridY * patch;
            double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
            for (int row = 0; row <= 64; row++)
                for (int column = 0; column <= 64; column++)
                {
                    double sample = height.GetHeight(originX + column * step, originY + row * step);
                    minimum = Math.Min(minimum, sample);
                    maximum = Math.Max(maximum, sample);
                }
            if (Math.Abs(minimum - node.SampleMinimum) > SampleRangeTolerance ||
                Math.Abs(maximum - node.SampleMaximum) > SampleRangeTolerance)
                throw new InvalidDataException($"Terrain resources {context} disagree at spatial node {node.Index}: source LOD range [{node.SampleMinimum:R}, {node.SampleMaximum:R}], reconstructed [{minimum:R}, {maximum:R}].");
        }
    }
}
