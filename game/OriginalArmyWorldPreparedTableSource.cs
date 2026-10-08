using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

/// <summary>Import-time source boundary for the supported owned fresh Army
/// ModelCPU. Retained mode0 groups prove initial Primitive50=null; actual empty
/// Model ordinal43 proves no declaration material set. Controller assignments
/// are a separate reached writer. Later external writes retire the caller's
/// current preparation rather than reuse these initial values.</summary>
public sealed class OriginalArmyWorldPreparedTableSource
{
    private readonly HashSet<(LoadedOriginalArmyCatalog Catalog, uint Model, int Draw)> imported = [];
    private readonly Dictionary<(LoadedOriginalArmyCatalog Catalog,int Category),LoadedOriginalArmyModelDynamicShaderSource> dynamicSources=[];
    private readonly Dictionary<(LoadedOriginalArmyCatalog Catalog,int Category),OriginalModelDynamicShaderCurrentFields<OriginalNativeWorldShaderTableState,object>> dynamicFields=[];
    public string ImportFreshNullWrapper(LoadedOriginalArmyCatalog catalog, int category,
        G1ModelGeometry geometry, int drawIndex)
    {
        uint modelId = OriginalCeArmyModels.KnownSourceCategories[category].ModelObjectId;
        if (!imported.Contains((catalog, modelId, drawIndex)))
        {
            if(!dynamicSources.ContainsKey((catalog,category)))
            {
                var dynamicSource=OriginalArmyModelDynamicShaderSource.ImportFresh(catalog,category);
                dynamicSources.Add((catalog,category),dynamicSource);
                dynamicFields.Add((catalog,category),dynamicSource.ConstructFreshRegisteredFields<OriginalNativeWorldShaderTableState,object>());
            }
            var definition = catalog.Definitions.Single(d => d.ModelObject.ObjectId == modelId);
            var model = definition.ModelObject;
            var field = model.Properties[43];
            if (field.Index != 43 || field.NameHash != 0x6C3B792D || field.TypeCode != 5 || field.ElementCount != 0 || field.RawValue.Length != 0)
                throw new NotSupportedException("Army fresh table publication needs the actual nonempty Model material-set producer.");
            if (category is 0 or 1)
                foreach (var (index, hash) in new (int, uint)[] { (34, 0x34CF9E5C), (51, 0xCEE5DDAE), (52, 0x5EDA237E) })
                {
                    var worldField = definition.Definition.Properties[index];
                    if (worldField.Index != index || worldField.NameHash != hash || worldField.TypeCode != 5 ||
                        worldField.ElementCount != 0 || worldField.RawValue.Length != 0)
                        throw new NotSupportedException("Ordinary Army WorldPQ has a reached container material-set producer; its actual final table is required.");
                }
            var group = G1ModelDrawReader.Read(geometry).Groups.Single(g => g.DrawIndices.Contains(drawIndex));
            if (BinaryPrimitives.ReadUInt16LittleEndian(group.RawBytes.AsSpan(16, 2)) != 0)
                throw new NotSupportedException("Army Primitive50 has a reached native group mode1 producer; its actual wrapper/table writer is required.");
            imported.Add((catalog, modelId, drawIndex));
        }
        return catalog.Dictionary.Source.ResourceUri + "@" + catalog.Dictionary.Source.DecodedSha256 +
            ";Model=" + modelId.ToString("X8") + ";fresh-owned-no-external-Primitive50-writer;" +
            "Model.ordinal43-empty/initial-C34CBC-base;"+dynamicSources[(catalog,category)].SourceEvidence+
            ";actual-fresh-C243C0-false-retains-base;ABB5BC-u16label16=0/F57D94-F57D99/F581DA/F5AD61-Primitive50-null";
    }

    public OriginalNativeWorldShaderTableState ReadActualFreshPriorTable(LoadedOriginalArmyCatalog catalog,int category,
        G1ModelGeometry geometry,int drawIndex,OriginalNativeWorldShaderTableState actualBaseTable)
    {
        _=ImportFreshNullWrapper(catalog,category,geometry,drawIndex);
        actualBaseTable.Registry.RequireCurrent(actualBaseTable);
        var current=dynamicFields[(catalog,category)];
        // DrawRows90=null is the original query's first gate for every selector.
        // Its no-selection result does not require guessing World2F8.
        if(current.DrawRows90 is not null)throw new InvalidOperationException("Fresh source registration unexpectedly published a dynamic table.");
        return actualBaseTable;
    }
}
