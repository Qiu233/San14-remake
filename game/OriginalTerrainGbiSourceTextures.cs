using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>
/// Exact installed source inputs and their original retained-mip samplers.
/// Field height/control are source inputs to the original generated-field pass;
/// callers must not mistake them for its final runtime outputs.
/// </summary>
public sealed class OriginalTerrainGbiSourceTextures : IDisposable,IAsyncDisposable
{
    public LoadedTerrainShaderResources ShaderSource { get; }
    public LoadedTerrainResources TerrainSource { get; }
    public IReadOnlyList<OriginalTerrainDeviceTexture> Textures { get; }
    public IReadOnlyList<G1TextureSamplerRecipe> Samplers { get; }
    public OriginalTerrainDeviceTexture Height => Textures[0];
    public OriginalTerrainDeviceTexture Control => Textures[1];
    public OriginalTerrainDeviceTexture AlbedoOcclusion => Textures[2];
    public OriginalTerrainDeviceTexture NormalHeight => Textures[3];
    public OriginalTerrainDeviceTexture ReflectanceRoughness => Textures[4];
    private Task? _disposal;

    private OriginalTerrainGbiSourceTextures(LoadedTerrainShaderResources shader,LoadedTerrainResources terrain,
        IReadOnlyList<OriginalTerrainDeviceTexture> textures,IReadOnlyList<G1TextureSamplerRecipe> samplers)
        => (ShaderSource,TerrainSource,Textures,Samplers)=(shader,terrain,textures,samplers);

    public static async Task<OriginalTerrainGbiSourceTextures> BuildAsync(OriginalGameRepository repository,
        LoadedTerrainParameterResources parameters,LoadedTerrainResources terrain)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(terrain);
        if(parameters.Scene.TerrainBundle.Source.ResourceUri!=terrain.Objects.Source.ResourceUri ||
            parameters.Scene.TerrainBundle.Source.DecodedSha256!=terrain.Objects.Source.DecodedSha256)
            throw new InvalidDataException("GBI parameter and terrain sources differ.");
        LoadedTerrainShaderResources shader=OriginalTerrainShaderResources.Load(repository,parameters);
        LoadedTerrainTextureGroup field=OriginalTerrainTextureResources.LoadGroup(repository,terrain,TerrainTextureGroupKind.Field);
        LoadedTerrainTextureGroup root=OriginalTerrainTextureResources.LoadGroup(repository,terrain,TerrainTextureGroupKind.Root);
        TerrainTextureBinding[] bindings=[Slot(field,0),Slot(field,1),Slot(root,0),Slot(root,1),Slot(root,2)];
        if(bindings[0].Image.PixelFormat!=0x02 || bindings[0].Image.LayerCount!=1 ||
            bindings[1].Image.PixelFormat!=0x01 || bindings[1].Image.LayerCount!=1 ||
            bindings.Skip(2).Any(binding=>binding.Image.PixelFormat!=0x5B || binding.Image.LayerCount<=1))
            throw new InvalidDataException("GBI source inputs need scalar float height, BGRA control and three BC3 arrays.");
        var recipes=bindings.Select(binding=>G1TextureSamplerReader.ReadRetainedSource(binding.Image)).ToArray();
        // Validate the shader-declaration adapter before allocating GPU resources.
        foreach(G1TextureSamplerRecipe recipe in recipes) OriginalTerrainSamplerHints.Create(recipe);
        var uploaded=new List<OriginalTerrainDeviceTexture>();
        try
        {
            for(int i=0;i<bindings.Length;i++)
            {
                TerrainTextureBinding binding=bindings[i];
                bool srgb=binding.Image.RawHeader.Length>=20 && (binding.Image.RawHeader[19]&1)!=0;
                OriginalTerrainDeviceTexture texture=await OriginalTerrainDeviceTexture.BuildAsync(i<2 ? field : root,
                    i<2 ? (uint)i : (uint)(i-2),srgb);
                uploaded.Add(texture);
                texture.Texture.SetMeta("source_sampler_key",recipes[i].RawKey);
                texture.Texture.SetMeta("source_sampler_descriptor",recipes[i].EncodeD3D11Descriptor());
                texture.Texture.SetMeta("source_sampler_mips_retained",true);
                texture.Texture.SetMeta("original_generated_field_output",false);
            }
            return new(shader,terrain,uploaded.AsReadOnly(),Array.AsReadOnly(recipes));
        }
        catch
        {
            for(int i=uploaded.Count-1;i>=0;i--) await uploaded[i].DisposeAsync();
            throw;
        }
    }

    /// <summary>Declarations for these exact sources, usable by preview/derived passes.</summary>
    public string CreateSamplerDeclarations()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        string[] names=["gbi_land_height","gbi_land_control","gbi_albedo_occlusion","gbi_normal_height","gbi_reflectance_roughness"];
        return string.Join('\n',Enumerable.Range(0,Textures.Count).Select(i=>
            $"uniform sampler{(Textures[i].SourceImage.LayerCount>1 ? "2DArray" : "2D")} {names[i]} : "+
            OriginalTerrainSamplerHints.Create(Samplers[i],Textures[i].Texture.GetMeta("uploaded_rd_srgb").AsBool())+";"));
    }

    /// <summary>Explicit source preview binding. The actual map uses generated Field outputs.</summary>
    public void BindSourcePreview(ShaderMaterial material)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        ArgumentNullException.ThrowIfNull(material);
        material.SetShaderParameter("gbi_land_height",Height.Texture);
        material.SetShaderParameter("gbi_land_control",Control.Texture);
        material.SetShaderParameter("gbi_albedo_occlusion",AlbedoOcclusion.Texture);
        material.SetShaderParameter("gbi_normal_height",NormalHeight.Texture);
        material.SetShaderParameter("gbi_reflectance_roughness",ReflectanceRoughness.Texture);
    }
    private static TerrainTextureBinding Slot(LoadedTerrainTextureGroup group,uint slot)=>
        group.Bindings.SingleOrDefault(binding=>binding.Slot==slot) ??
        throw new InvalidDataException($"Terrain texture table {group.TableSource.ResourceUri} has no slot {slot}.");
    private Task DisposeCoreAsync()=>Release();
    private async Task Release()
    {
        for(int i=Textures.Count-1;i>=0;i--) await Textures[i].DisposeAsync();
    }
    public ValueTask DisposeAsync()=>new(_disposal ??= DisposeCoreAsync());
    public void Dispose()=>_ = (_disposal ??= DisposeCoreAsync());
}
