using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The three required shader registrations held by the actual shared
/// A024EF37 CPU. Object/class ids are not substituted for G1S resource hashes.</summary>
public sealed class SdfBackendShaderResources
{
    private SdfBackendShaderResources(LoadedOrdinaryRsLayerSource source,
        NamedModelShader utility, LoadedShaderContainer utilityPrograms,
        NamedModelShader sdf, LoadedShaderContainer sdfPrograms,
        NamedModelShader cluster, LoadedShaderContainer clusterPrograms,
        NamedModelShader? objects, LoadedShaderContainer? objectPrograms)
        => (Source, Utility, UtilityPrograms, SdfOcclusion, SdfOcclusionPrograms,
            ClusterBox, ClusterBoxPrograms,ObjectOcclusion,ObjectOcclusionPrograms) = (source, utility, utilityPrograms,
                sdf, sdfPrograms, cluster, clusterPrograms,objects,objectPrograms);
    public LoadedOrdinaryRsLayerSource Source { get; }
    public NamedModelShader Utility { get; }
    public LoadedShaderContainer UtilityPrograms { get; }
    public NamedModelShader SdfOcclusion { get; }
    public LoadedShaderContainer SdfOcclusionPrograms { get; }
    public NamedModelShader ClusterBox { get; }
    public LoadedShaderContainer ClusterBoxPrograms { get; }
    public NamedModelShader? ObjectOcclusion { get; }
    public LoadedShaderContainer? ObjectOcclusionPrograms { get; }

    public static SdfBackendShaderResources Load(GameResourceRepository repository,
        LoadedOrdinaryRsLayerSource actualRsSource)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualRsSource);
        var cpu = actualRsSource.Objects.Document.Objects.Single(o => o.ObjectId == 0xA024EF37);
        if (cpu.Kind != DokObjectKind.Inline || cpu.ClassId != 0x71B26C2F || cpu.Properties.Count != 4)
            throw new InvalidDataException("SDF backend requires its actual shared CPU declaration.");
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        NamedModelShader Read(int ordinal, uint hash)
        {
            var p = cpu.Properties[ordinal];
            if (p.Index != ordinal || p.NameHash != hash || p.TypeCode != 5 || p.ElementCount != 1 || p.RawValue.Length != 4)
                throw new InvalidDataException("SDF held shader reference differs from its actual loader field.");
            uint reference = BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
            if (reference == 0) throw new InvalidDataException("SDF required held shader is absent.");
            var owners = new[] {actualRsSource.Objects,shaderObjects}
                .Where(owner => owner.Document.Objects.Any(o => o.ObjectId == reference)).ToArray();
            if (owners.Length != 1)
                throw new InvalidDataException("SDF shader reference has no unique loaded dictionary owner: " + reference.ToString("X8"));
            return ModelShaderResources.ReadDeclaredShader(owners[0], reference);
        }
        var utility = Read(0,0x8BAA2194);
        var sdf = Read(1,0x5C6659B5);
        var cluster = Read(2,0x7B7CC324);
        var objectField=cpu.Properties[3];
        if(objectField.NameHash!=0x62157EF5||objectField.TypeCode!=5||objectField.ElementCount!=1||objectField.RawValue.Length!=4)
            throw new InvalidDataException("SDF optional object shader reference differs from its loader field.");
        var objects=BinaryPrimitives.ReadUInt32LittleEndian(objectField.RawValue)==0?null:Read(3,0x62157EF5);
        return new(actualRsSource, utility,ModelShaderResources.LoadDeclaredG1S(repository,utility),
            sdf,ModelShaderResources.LoadDeclaredG1S(repository,sdf),
            cluster,ModelShaderResources.LoadDeclaredG1S(repository,cluster),objects,
            objects is null?null:ModelShaderResources.LoadDeclaredG1S(repository,objects));
    }
}
