using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyMaterialManagerAssignment(int Manager, IReadOnlyList<uint> MaterialObjectIds);
public sealed record ArmyManagerMaterialDeclaration(RenderResourceDescriptor Source,
    uint Regular, uint Target, uint Alternate, uint BlendState, uint StageTableResource,
    ReadOnlyMemory<byte> FilenameContext);

/// <summary>The complete manager initialization input. These rows name shared
/// MaterialData; they do not request actors, glyphs or ready image products.</summary>
public sealed class ArmyMaterialManagerResources
{
    public ulong ContextGeneration { get; }
    public IReadOnlyList<ArmyMaterialManagerAssignment> Managers { get; }
    public IReadOnlyList<ArmyManagerMaterialDeclaration> Materials { get; }
    public IReadOnlyDictionary<uint, LoadedRdbAsset> StageTables { get; }

    private ArmyMaterialManagerResources(ulong generation, List<ArmyMaterialManagerAssignment> managers,
        List<ArmyManagerMaterialDeclaration> materials, Dictionary<uint, LoadedRdbAsset> stageTables)
        => (ContextGeneration, Managers, Materials, StageTables) = (generation, managers.AsReadOnly(),
            materials.AsReadOnly(), new System.Collections.ObjectModel.ReadOnlyDictionary<uint, LoadedRdbAsset>(stageTables));

    public static ArmyMaterialManagerResources Load(GameResourceRepository repository,
        LoadedArmyCatalog catalog, RenderResourceSnapshot initialization)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(initialization);
        var managers = new List<ArmyMaterialManagerAssignment>();
        var materials = new List<ArmyManagerMaterialDeclaration>();
        var seen = new HashSet<uint>();
        var stageTables = new Dictionary<uint, LoadedRdbAsset>();
        foreach (var manager in ArmyFlagMaterialVariations.ManagerRows)
        {
            var matrix = ArmyFlagMaterialVariations.ReadMatrix(catalog, manager.ManagerKind);
            uint[] rows = Enumerable.Range(1, manager.ForceVariantCount).Select(matrix.MaterialAt).ToArray();
            managers.Add(new(manager.ManagerKind, Array.AsReadOnly(rows)));
            foreach (uint name in rows)
            {
                if (name == 0 || !seen.Add(name)) continue;
                var source = initialization.Lookup(name);
                if (source is null)
                    throw new InvalidDataException($"Army manager Material {name:X8} is absent at initialization.");
                var textures = ModelMaterialTextureDeclaration.Read(source.Object);
                uint stageTable = textures.StageTableResource;
                if (stageTable != 0 && !stageTables.ContainsKey(stageTable))
                    stageTables.Add(stageTable, repository.LoadRdbAsset("MaterialEditor", stageTable));
                materials.Add(new(source, textures.RegularGroup, textures.RenderTargetGroup,
                    textures.AlternateGroup, textures.BlendState, stageTable, textures.Filename));
            }
        }
        return new(initialization.Generation, managers, materials, stageTables);
    }

    public void WriteNativeInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        using (var stream = File.Create(Path.Combine(directory, "manager-initialization.bin")))
        using (var output = new BinaryWriter(stream, Encoding.UTF8))
        {
            output.Write(0x31494D41u); // AMI1: manager rows and their initialization Context generation.
            output.Write(ContextGeneration); output.Write((uint)Managers.Count);
            foreach (var manager in Managers)
            {
                output.Write((uint)manager.Manager); output.Write((uint)manager.MaterialObjectIds.Count);
                for (int index = 0; index < manager.MaterialObjectIds.Count; ++index)
                {
                    output.Write((uint)index + 1); output.Write(manager.MaterialObjectIds[index]);
                }
            }
            output.Write((uint)Materials.Count);
            foreach (var material in Materials)
            {
                output.Write(material.Source.Object.ObjectId); output.Write((uint)material.Source.Object.Index);
                output.Write(material.Source.Scope);
                output.Write(material.Regular); output.Write(material.Target); output.Write(material.Alternate);
                output.Write(material.BlendState); output.Write(material.StageTableResource);
                byte[] uri = Encoding.UTF8.GetBytes(material.Source.Source.Source.ResourceUri);
                output.Write((uint)uri.Length); output.Write(uri);
                output.Write((uint)material.FilenameContext.Length); output.Write(material.FilenameContext.Span);
            }
            output.Write((uint)StageTables.Count);
            foreach (var (resource, source) in StageTables)
            {
                output.Write(resource); output.Write((uint)source.Bytes.Length); output.Write(source.Bytes);
            }
        }
    }

}
