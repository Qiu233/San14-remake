using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The normal map Host's Landscape caster. Its shader and height
/// image remain original resources; the current accepted patch stream is shared
/// with ordinary terrain by the Native owner.</summary>
public sealed class TerrainShadowResources
{
    private readonly byte[] vertexProgram;
    private TerrainShadowResources(LoadedShaderContainer shader, byte[] vertex, uint flags, uint exclusion)
        =>(Shader,vertexProgram,CommonFlags,Exclusion)=(shader,vertex,flags,exclusion);
    public LoadedShaderContainer Shader { get; }
    public uint CommonFlags { get; }
    public uint Exclusion { get; }

    public static TerrainShadowResources Load(GameResourceRepository repository, LoadedTerrainParameterResources terrain)
    {
        uint Scalar(uint hash)
        {
            var p=terrain.WrapperRoot.Properties.Single(p=>p.NameHash==hash);
            if(p.TypeCode!=5||p.ElementCount!=1||p.RawValue.Length!=4)
                throw new InvalidDataException("Landscape shadow resource requires a declared scalar.");
            return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
        }
        var shader=repository.LoadShaderContainer("system",Scalar(0x4D0B6005));
        var bindings=shader.ReadG1Bindings();
        if(bindings.PassCount!=1||bindings.Variants.RecordWidth!=48||bindings.Features.Count!=0||
            bindings.RawDimensionCounts.Take(6).Any(v=>v>1))
            throw new NotSupportedException("Landscape shadow needs another source selector.");
        // Source matching ignores the frontend texture-format class (word2)
        // when word0 is zero, as it is in every declared LSSM variant.
        // Both depth-class5 rows resolve to the same depth-only program.
        var candidates=bindings.Variants.Records.Where(r=>BinaryPrimitives.ReadUInt32LittleEndian(r.RawBytes.Span[4..])==5).ToArray();
        var programs=new List<int>();
        foreach(var row in candidates)
        {
            uint[] indices=new uint[7];indices[6]=(uint)row.Index;
            var c=bindings.ResolveCombination(indices,0);
            if(c.Stages.Single(s=>s.StageCode==0).Entry is not null)
                throw new NotSupportedException("Landscape depth shadow requires a separate pixel owner.");
            programs.Add(c.Stages.Single(s=>s.StageCode==1).Entry!.ProgramIndex);
        }
        if(programs.Count!=2||programs.Distinct().Count()!=1)
            throw new NotSupportedException("Landscape shadow programs require their explicit current selector.");
        int vs=programs[0];var rdef=shader.ReadResourceDefinitions(vs);
        var cb=rdef.Buffers.Single();
        if(cb.ByteSize!=64||cb.Variables.Count!=1||cb.Variables[0].Name.Text!="mW2P"||cb.Variables[0].StartOffset!=0)
            throw new InvalidDataException("Landscape shadow vertex constants differ.");
        var common=CommonRenderResources.ImportSettings(repository.LoadObjects("KIDSSystemResource",CommonRenderResources.SourceFileId));
        uint flags=MapPostEffect3CommonOverlaySource.ApplyDefaultHostFlags8(common.Flags8);
        return new(shader,shader.Container.GetProgramBytes(shader.Source.Bytes,vs).ToArray(),flags,Scalar(0xC0C5A5B5));
    }

    public void WriteNativeInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,"vs.dxbc"),vertexProgram);
        // D43190 uses Main mode11 / distant9. Both choose record100 LSSM;
        // these are not Native raster-family indices or shader depth classes.
        File.WriteAllText(Path.Combine(directory,"policy.txt"),string.Create(CultureInfo.InvariantCulture,$"{CommonFlags} 11 9\n"));
        File.WriteAllText(Path.Combine(directory,"source.json"),JsonSerializer.Serialize(new{
            Shader.Source.ResourceUri,Shader.Source.DecodedSha256,CommonFlags,Exclusion,
            writer="default-map-Host-overlay;additional-owner-draw-and-landscape-fit-independent",
            sharedGeometry="same-current-View-complete-three-patch-groups",heightTextureRegister=8,
            depthClass=5,pixelProgram=(int?)null}));
    }
}
