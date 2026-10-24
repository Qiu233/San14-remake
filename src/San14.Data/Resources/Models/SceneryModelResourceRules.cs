using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SceneryModelResourceRule(int Category, string DefinitionName, int Capacity)
{
    public uint DefinitionId => ResourceNameHash.Compute(DefinitionName);
}

/// <summary>Offline extracted category89..96 rows. Their kind2 names select
/// actual definition object IDs; no executable, table ordinal or graphics-ready
/// state is consulted at runtime.</summary>
public static class SceneryModelResourceRules
{
    public static IReadOnlyList<SceneryModelResourceRule> All { get; } = Array.AsReadOnly(new[]
    {
        new SceneryModelResourceRule(89,"FE4Model［set－tur－meshou－pailou－01］",20),
        new SceneryModelResourceRule(90,"FE4Model［set－tur－meshou－pailou－02］",20),
        new SceneryModelResourceRule(91,"FE4Model［set－tur－meshou－yama－01］",20),
        new SceneryModelResourceRule(92,"FE4Model［set－tur－meshou－yama－02］",20),
        new SceneryModelResourceRule(93,"FE4Model［set－tur－meshou－house－01］",20),
        new SceneryModelResourceRule(94,"FE4Model［set－tur－meshou－house－02］",20),
        new SceneryModelResourceRule(95,"FE4Model［set－tur－meshou－stele－01］",20),
        new SceneryModelResourceRule(96,"FE4Model［set－tur－meshou－temple－01］",20)
    });

    internal static SceneModelReference Resolve(LoadedSceneResources scene, SceneryModelResourceRule rule)
    {
        var definitions = scene.ModelCatalog.Document.Objects.Where(o => o.ObjectId == rule.DefinitionId).ToArray();
        if (definitions.Length != 1 || definitions[0].Kind != DokObjectKind.Inline || definitions[0].ClassId != 0xC4B9B28D)
            throw new InvalidDataException($"Scenery category{rule.Category}: actual scene definition {rule.DefinitionId:X8} unavailable.");
        uint target = Scalar(definitions[0],0x0B6E1578);
        var models = scene.Dependencies.SelectMany(d => d.Resource.Document.Objects
            .Where(o => o.ObjectId == target).Select(o => (Document:d.Resource,Object:o))).ToArray();
        if (models.Length != 1 || models[0].Object.Kind != DokObjectKind.Inline || models[0].Object.ClassId != 0xD40B3C8F)
            throw new InvalidDataException($"Scenery definition {rule.DefinitionId:X8}: declared model dependency is missing or ambiguous.");
        return new(models[0].Document,models[0].Object,Scalar(models[0].Object,0x8AB68B3F));
    }

    private static uint Scalar(DokObject source,uint name)
    {
        var fields=source.Properties.Where(p=>p.NameHash==name).ToArray();
        if(fields.Length!=1||fields[0].TypeCode!=5||fields[0].ElementCount!=1||fields[0].RawValue.Length!=4)
            throw new InvalidDataException($"Scenery model field {name:X8} requires one declared uint32.");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
}
