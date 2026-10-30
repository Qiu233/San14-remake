using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Prepares shared physical model media. Population, material selection
/// and table ownership belong to the calling resource preparation stage.</summary>
internal sealed class D3D11ModelResourceWriter(GameResourceRepository repository,Dictionary<string,byte[]> files)
{
    readonly HashSet<string> writtenStatic=new(StringComparer.OrdinalIgnoreCase);

    public void WritePacket(string directory,string meshDirectory,MapStaticModelAsset asset,int drawIndex,
        LoadedShaderContainer shader,NamedModelShader provider,int combination,int vs,int ps,
        byte[] vertex,byte[] pixel,LoadedModelShaderBindings bindings,ModelQueueStateSource queue,
        bool reverseDepth,bool stencilEnabled=false,byte? stencilReference=null)
    {
        var geometry=asset.RawTransforms.Geometry;var draw=asset.Draws[drawIndex];
        var layout=geometry.Layouts[draw.LayoutIndex];
        var signature=shader.ReadSignatures(vs).Parts.Single(s=>s.Kind==DxbcSignatureKind.Input);
        foreach(var input in signature.Parameters.Where(p=>p.RawSystemValue==0))
            if(layout.Attributes.Count(a=>ModelVertexInputs.ReadSemantic(a.SemanticCode).Name==input.SemanticName.Text
                &&ModelVertexInputs.ReadSemantic(a.SemanticCode).Index==input.SemanticIndex)!=1)
                throw new NotSupportedException("Static model VS input semantic is unsupported.");
        WriteGeometryAndPrograms(directory,meshDirectory,geometry,draw,shader,vs,ps,
            vertex,pixel,bindings,queue,reverseDepth,null,stencilEnabled,stencilReference);
        WriteStaticTextures(directory,asset,drawIndex,provider,shader,combination);
    }

    public void WriteGeometryAndPrograms(string directory,string meshDirectory,
        G1ModelGeometry geometry,G1DrawRecord draw,LoadedShaderContainer shader,int vs,int ps,
        byte[] vertex,byte[] pixel,LoadedModelShaderBindings bindings,ModelQueueStateSource queue,
        bool reverseDepth,IReadOnlySet<int>? singleInstanceStreams=null,
        bool stencilEnabled=false,byte? stencilReference=null)
        => WriteGeometryAndPrograms(directory,meshDirectory,geometry,draw,shader,vs,ps,
            vertex,pixel,bindings,ModelRendererStateSourceReader.PrepareQueueDepth(queue,reverseDepth),
            singleInstanceStreams,stencilEnabled,stencilReference);

    /// <summary>Media writer for a caller's actual independently prepared depth
    /// state. Capture uses its own EC7100 scope rather than the Main queue.</summary>
    public void WriteGeometryAndPrograms(string directory,string meshDirectory,
        G1ModelGeometry geometry,G1DrawRecord draw,LoadedShaderContainer shader,int vs,int ps,
        byte[] vertex,byte[] pixel,LoadedModelShaderBindings bindings,ModelQueueDepthState depth,
        IReadOnlySet<int>? singleInstanceStreams=null,
        bool stencilEnabled=false,byte? stencilReference=null)
    {
        foreach(var buffer in geometry.VertexBuffers)
        {
            string path=Path.Combine(meshDirectory,$"vb-{buffer.Index}.bin");
            if(writtenStatic.Add(path))AddBytes(path,buffer.Data);
        }
        foreach(var buffer in geometry.IndexBuffers)
        {
            string path=Path.Combine(meshDirectory,$"ib-{buffer.Index}.bin");
            if(writtenStatic.Add(path))AddBytes(path,buffer.Data);
        }
        AddBytes(Path.Combine(directory,"vs.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,vs).ToArray());
        AddBytes(Path.Combine(directory,"ps.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,ps).ToArray());
        AddBytes(Path.Combine(directory,"vs-b0.bin"),vertex);AddBytes(Path.Combine(directory,"ps-b0.bin"),pixel);
        var layout=geometry.Layouts[draw.LayoutIndex];
        AddLines(Path.Combine(directory,"layout.txt"),layout.Attributes.Select(a=>
        {
            var semantic=ModelVertexInputs.ReadSemantic(a.SemanticCode);
            string row=FormattableString.Invariant($"{semantic.Name} {semantic.Index} {Dxgi(a.TypeCode)} {a.BufferReferenceSlot} {a.ByteOffset}");
            if(singleInstanceStreams is not null)
                row+=singleInstanceStreams.Contains(layout.BufferReferences[a.BufferReferenceSlot])?" 1 1":" 0 0";
            return row;
        }));
        AddLines(Path.Combine(directory,"streams.txt"),layout.BufferReferences.Select((i,slot)=>
            $"{slot} {geometry.VertexBuffers[i].Stride} {Relative(directory,Path.Combine(meshDirectory,$"vb-{i}.bin"))}"));
        var raster=ModelRendererStateSourceReader.ReadGroupRaster(bindings,draw.Index)
            ??throw new NotSupportedException("Source raster override is absent.");
        string reference=stencilReference is byte value?$" {value}":string.Empty;
        AddText(Path.Combine(directory,"draw.txt"),
            $"{draw.IndexCount} {draw.IndexStart} 0 4 {geometry.IndexBuffers[draw.IndexBufferIndex].BitsPerIndex}\n"+
            $"{Relative(directory,Path.Combine(meshDirectory,$"ib-{draw.IndexBufferIndex}.bin"))}\n"+
            $"{raster.NativeFillMode} {raster.NativeCullMode} {(raster.FrontCounterClockwise?1:0)}\n"+
            $"{(depth.DepthTest?1:0)} {(depth.DepthWrite?1:0)} {depth.CompareCode+1} {(stencilEnabled?1:0)}{reference}\n");
    }

    void WriteStaticTextures(string directory,MapStaticModelAsset asset,int drawIndex,
        NamedModelShader provider,LoadedShaderContainer shader,int combination)
        =>WriteStaticTextures(directory,asset.TextureResources,drawIndex,provider,shader,combination);

    public void WriteStaticTextures(string directory,LoadedModelTextureResources resources,int drawIndex,
        NamedModelShader provider,LoadedShaderContainer shader,int combination)
    {
        var plan=ModelStageTextures.Resolve(resources,drawIndex,provider,shader,combination,new(false,false,0));
        var textureRows=new List<string>();var samplerRows=new List<string>();
        foreach(var binding in plan.Bindings)
        {
            var image=ModelStageTextures.LoadImage(repository,plan,binding.TextureRegister);
            var formats=ModelTextureFormatMath.Resolve(image.Image.PixelFormat,(byte)(image.Image.RawHeader[19]&1));
            string texture=Path.Combine("textures",$"{image.Container.Source.FileId:X8}-{image.Image.Index}-{formats.ShaderViewFormat}.tex");
            if(writtenStatic.Add(texture))AddBytes(texture,EncodeTexture(image,formats.ShaderViewFormat));
            textureRows.Add($"{binding.TextureRegister} {formats.ResourceFormat} {formats.ShaderViewFormat} {Relative(directory,texture)}");
            string sampler=$"ps-s{binding.SamplerRegister}.bin";
            AddBytes(Path.Combine(directory,sampler),image.Sampler.EncodeD3D11Descriptor());
            samplerRows.Add($"{binding.SamplerRegister} {sampler}");
        }
        AddLines(Path.Combine(directory,"textures-native.txt"),textureRows);
        AddLines(Path.Combine(directory,"samplers-native.txt"),samplerRows);
    }

    /// <summary>Tree2 uses the same numeric PB2 material registrations as the
    /// ground templates. VS wind resources remain with the shared current owner.</summary>
    public void WriteTree2Textures(string directory, LoadedModelTextureResources resources, int drawIndex,
        NamedModelShader provider, LoadedShaderContainer shader, int combination)
    {
        var plan = ProceduralTree2TextureSource.Resolve(resources, drawIndex, provider, shader, combination);
        var textureRows = new List<string>(); var samplerRows = new List<string>();
        foreach (var binding in plan.Bindings)
        {
            var image = ModelStageTextures.LoadImage(repository, plan, binding.TextureRegister);
            var formats = ModelTextureFormatMath.Resolve(image.Image.PixelFormat, (byte)(image.Image.RawHeader[19] & 1));
            string texture = $"ps-t{binding.TextureRegister}.tex";
            AddBytes(Path.Combine(directory, texture), EncodeTexture(image.Container, image.Image, formats.ShaderViewFormat));
            string sampler = $"ps-s{binding.SamplerRegister}.bin";
            AddBytes(Path.Combine(directory, sampler), image.Sampler.EncodeD3D11Descriptor());
            textureRows.Add(FormattableString.Invariant($"{binding.TextureRegister} {formats.ResourceFormat} {formats.ShaderViewFormat} {texture}"));
            samplerRows.Add(FormattableString.Invariant($"{binding.SamplerRegister} {sampler}"));
        }
        AddLines(Path.Combine(directory, "textures-native.txt"), textureRows);
        AddLines(Path.Combine(directory, "samplers-native.txt"), samplerRows);
    }

    void AddBytes(string path,byte[] bytes)=>files[path.Replace('\\','/').TrimStart('.','/')]=bytes.ToArray();
    void AddText(string path,string value)=>AddBytes(path,Encoding.UTF8.GetBytes(value));
    void AddLines(string path,IEnumerable<string> lines)
    {
        string[] rows=lines.ToArray();
        AddText(path,rows.Length==0?string.Empty:string.Join(Environment.NewLine,rows)+Environment.NewLine);
    }
    static string Relative(string from,string path)=>Path.GetRelativePath(from,path).Replace('\\','/');
    static uint Dxgi(ushort type)=>type switch
    {0=>41,1=>16,2=>6,3=>2,5=>30,7=>14,10=>34,11=>10,13=>28,_=>throw new NotSupportedException($"Unsupported source vertex type {type}.")};
    static byte[] EncodeTexture(ModelStageTextureImage source,uint format)
        =>EncodeTexture(source.Container,source.Image,format);

    internal static byte[] EncodeTexture(LoadedTextureContainer container,G1TextureImage image,uint format)
    {
        if(image.LayerCount!=1)throw new NotSupportedException("Source view is not a single layer.");
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        foreach(uint word in new[]{(uint)image.Width,(uint)image.Height,(uint)image.LayerCount,(uint)image.MipCount,format})writer.Write(word);
        for(int mip=0;mip<image.MipCount;mip++)
        {
            var m=image.Mips[mip];byte[] bytes=image.GetEncodedMip(container.Source.Bytes,mip,0).ToArray();
            int pitch=image.PixelFormat switch
            {
                0x59 or 0x5C=>Math.Max(1,(m.Width+3)/4)*8,
                0x5B or 0x5E or 0x5F=>Math.Max(1,(m.Width+3)/4)*16,
                0x00 or 0x01 or 0x02=>m.Width*4,0x03=>m.Width*8,0x04=>m.Width*16,
                _=>throw new NotSupportedException("Unsupported source texture pitch.")
            };
            writer.Write((uint)pitch);writer.Write((uint)bytes.Length);writer.Write((uint)bytes.Length);writer.Write(bytes);
        }
        return stream.ToArray();
    }
}
