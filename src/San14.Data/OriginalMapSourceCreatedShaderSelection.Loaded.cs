using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public static partial class OriginalMapSourceCreatedShaderSelection
{
    private sealed record LoadedSelectionSource(LoadedOriginalDeferredTexturePlan Plan,
        G1ShaderBindingTables Tables, uint[] Capabilities);
    private static readonly ConditionalWeakTable<LoadedOriginalDeferredTexturePlan, LoadedSelectionSource> LoadedSources = new();
    internal sealed record LoadedProgramDeclarations(string PixelSha256, DxbcResourceDefinitions PixelDefinitions,
        DxbcResourceDefinitions CanonicalPixelDefinitions, G1ShaderResourceBindings ResourceBindings);
    private sealed class LoadedShaderPrograms
    {
        public readonly object Gate=new();
        public readonly Dictionary<(int Combination,int Pixel),LoadedProgramDeclarations> Programs=new();
        public DxbcResourceDefinitions? CanonicalPixelDefinitions;
    }
    private static readonly ConditionalWeakTable<LoadedShaderContainer, LoadedShaderPrograms> LoadedPrograms = new();

    internal static LoadedProgramDeclarations ReadLoadedProgram(LoadedShaderContainer shader,int combination,int pixel)
    {
        var loaded=LoadedPrograms.GetValue(shader,_=>new());
        lock(loaded.Gate)
        {
            if(loaded.Programs.TryGetValue((combination,pixel),out var found))return found;
            loaded.CanonicalPixelDefinitions??=shader.ReadResourceDefinitions(1);
            var declarations=new LoadedProgramDeclarations(Convert.ToHexString(SHA256.HashData(
                shader.Container.GetProgramBytes(shader.Source.Bytes,pixel))),shader.ReadResourceDefinitions(pixel),
                loaded.CanonicalPixelDefinitions,shader.ReadG1ResourceBindings(combination));
            loaded.Programs.Add((combination,pixel),declarations);
            return declarations;
        }
    }

    private static LoadedSelectionSource ReadLoadedSource(LoadedOriginalDeferredTexturePlan source) =>
        LoadedSources.GetValue(source, LoadSource);

    private static LoadedSelectionSource LoadSource(LoadedOriginalDeferredTexturePlan source)
    {
        // Source-plan ownership lasts across World requests. Read/hash native
        // declarations on that load boundary; actual Camera/light/View keys
        // and the original ordered feature/variant selection remain current.
        if(source.Node.Index!=166||source.Node.Call.FactoryClassId!=0xF103DB00||
            source.Graph.Source.DecodedSha256!=GraphSha256||source.ViewId!=0||source.LightGroupId!=8||
            source.Shader.Source.FileId!=0x9D8E811F||source.Shader.Source.DecodedSha256!=ShaderSha256||
            Convert.ToHexString(SHA256.HashData(source.Shader.Source.Bytes))!=ShaderSha256||
            Convert.ToHexString(SHA256.HashData(source.Graph.Source.Bytes))!=GraphSha256)
            throw new InvalidDataException("F103 fresh selection requires the exact original node166 and9D shader source.");
        var plan=source with
        {
            Node=RenderGraphReader.Read(source.Graph.Source.Bytes).Nodes[166],
            Shader=new(G2ShaderContainerReader.Read(source.Shader.Source.Bytes),source.Shader.Source)
        };
        var ocean=plan.Node.Call.Arguments.SingleOrDefault(a=>a.NameHash==0xE8F3DC36);
        if(ocean is null||ocean.DescriptorWord!=0x01000001||ocean.RawValue.Length!=1||ocean.RawValue[0]!=0)
            throw new InvalidDataException("F103 node166 source requires the proved non-ocean constructor branch.");
        var tables=plan.Shader.ReadG1Bindings();
        if(tables.PassCount!=1||!tables.RawDimensionCounts.SequenceEqual(new uint[]{2,0,0,0,0,0,512})||
            tables.Variants.RecordWidth!=96||tables.Variants.Records.Count!=512)
            throw new InvalidDataException("F103 original selection tables differ from the verified source shape.");
        var masks=new uint[21];
        foreach(var row in tables.Variants.Records)
            for(int word=0;word<masks.Length;word++)
                masks[word]|=1u<<(int)(BinaryPrimitives.ReadUInt32LittleEndian(row.RawBytes.AsSpan(word*4,4))&31);
        var configs=G1ShaderConfigurationReader.Read(plan.Shader.Source.Bytes,plan.Shader.Container);
        if(!configs.Records.Any(r=>r.Name=="Refraction")||!configs.Records.Any(r=>r.Name=="VolumetricFog"))
            throw new InvalidDataException("F103 original registered plugin configuration differs.");
        var thin=configs.Records.Single(r=>r.Name=="ThinSurfaceBSDF");
        if(thin.Find(0xFEB72A6D)?.RawValue!=0||thin.Find(0x2BC1B848)?.RawValue!=0)
            throw new InvalidDataException("F103 fresh ThinSurface constructor mode differs from its source.");
        return new(plan,tables,masks);
    }
}
