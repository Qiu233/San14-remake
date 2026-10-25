using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>Prepare the complete resource bank independently of current program
/// selection. Native installs only recipes whose actual producers are ready.</summary>
public static class D3D11HdrProgramMediaPreparation
{
    private static readonly string[] LightingNames=["litDir","litCol","litOcc","ambLit","nDirLit",
        "fog","cFog","Scat","fAmbMapsDORate","vAmbDifScl","vAmbSpcScl","vOccRate"];
    private static readonly int[] LightingSizes=[64,64,16,16,4,16,16,64,4,16,16,8];
    private static readonly string[] RefractionNames=["vCameraPos","vCameraDir","vCameraViewY","vD2Z","mRrW2T"];
    private static readonly int[] RefractionSizes=[12,12,12,8,48];

    public static void WriteNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11CurrentSeasonResources resources,
        D3D11CurrentHdrCatalog catalog,SceneColorTargetResources target,string inputRoot,int outputWidth,int outputHeight)
    {
        string directory=Path.Combine(inputRoot,"hdr-bank");
        Directory.CreateDirectory(directory);
        var formats=target.Formats;
        int width=target.Width,height=target.Height;
        File.WriteAllText(Path.Combine(directory,"target.txt"),FormattableString.Invariant(
            $"{target.LogicalName:X8} {formats.ResourceFormat} {formats.ShaderViewFormat} {formats.TargetViewFormat} {width} {height}\n"));
        var cameraProperties=current.Rgb.Inputs.MainFrame.Camera.Source.Camera.Properties;
        float CausticsDimension(uint hash)
        {
            var property=cameraProperties.Single(property=>property.NameHash==hash);
            if(property.TypeCode!=8 || property.ElementCount!=1 || property.RawValue.Length!=4)
                throw new InvalidDataException("Caustics dimensions require scalar float declarations.");
            return BinaryPrimitives.ReadSingleLittleEndian(property.RawValue);
        }
        File.WriteAllText(Path.Combine(directory,"caustics.txt"),FormattableString.Invariant(
            $"{CausticsDimension(0x98DE41BD):R} {CausticsDimension(0xB2102CBF):R}\n"));
        var canonical=catalog.Resources[HdrShaderResources.MainLightingNode].Shader.ReadResourceDefinitions(1);
        var caustics=RenderSamplerSourceAdapters.LoadEarlyCaustics(repository.SourceDirectory,resources.Source.Locale);
        var scene=CreateConstructorInputs(current,catalog,width,height);
        foreach(var declaration in catalog.Resources.Declarations)
        {
            string bank=Path.Combine(directory,declaration.NodeIndex.ToString(CultureInfo.InvariantCulture));
            var vertexDefinitions=new Dictionary<int,DxbcResourceDefinitions>();
            var vertexInterfaces=new Dictionary<int,bool>();
            var pixelInterfaces=new Dictionary<int,bool>();
            foreach(var recipe in HdrProgramBankEncoding.WriteNativeInputs(declaration,bank))
            {
                Directory.CreateDirectory(recipe.Directory);
                var program=recipe.Program;
                var reasons=new List<string>();
                var pixel=program.PixelDefinitions;
                if(!vertexDefinitions.TryGetValue(program.VertexProgramIndex,out var vertex))
                {
                    vertex=declaration.Shader.ReadResourceDefinitions(program.VertexProgramIndex);
                    vertexDefinitions.Add(program.VertexProgramIndex,vertex);
                    vertexInterfaces.Add(program.VertexProgramIndex,declaration.Shader.ReadSignatures(program.VertexProgramIndex).Parts
                        .Where(part=>part.Kind==DxbcSignatureKind.Input).SelectMany(part=>part.Parameters).All(input=>input.RawSystemValue==6));
                }
                if(!vertexInterfaces[program.VertexProgramIndex])
                    reasons.Add("Vertex program requires an input outside the fullscreen vertex ID.");
                if(!pixelInterfaces.TryGetValue(program.PixelProgramIndex,out bool singleTarget))
                {
                    var outputs=declaration.Shader.ReadSignatures(program.PixelProgramIndex).Parts
                        .Where(part=>part.Kind==DxbcSignatureKind.Output).SelectMany(part=>part.Parameters).ToArray();
                    singleTarget=outputs.Length==1 && outputs[0].SemanticIndex==0 && outputs[0].Register==0 &&
                        outputs[0].SemanticName.Text.Equals("SV_Target",StringComparison.OrdinalIgnoreCase);
                    pixelInterfaces.Add(program.PixelProgramIndex,singleTarget);
                }
                if(!singleTarget)
                    reasons.Add("Pixel program requires an output outside the single Scene target.");
                WriteDeclarations(recipe.Directory,vertex,pixel);
                foreach(var binding in vertex.Bindings)
                    reasons.Add("Vertex resource has no producer: "+binding.Name.Text);
                foreach(var binding in pixel.Bindings.Where(binding=>binding.InputType==0 && binding.Name.Text!="$Globals"))
                    reasons.Add("Pixel constant buffer has no producer: "+binding.Name.Text);
                var globals=pixel.Buffers.SingleOrDefault(buffer=>buffer.Name.Text=="$Globals");
                var globalBinding=pixel.Bindings.SingleOrDefault(binding=>binding.InputType==0 && binding.Name.Text=="$Globals");
                if(globals is null || globalBinding is null)
                    reasons.Add("Pixel Globals buffer or binding is absent.");
                else
                {
                    bool hasHatching=globals.Variables.Any(field=>IsUsed(field) &&
                        (field.Name.Text is "hsRampPrm" or "hsAOPrm" or "hsUVMat"));
                    bool hasShadowFade=globals.Variables.Any(field=>IsUsed(field) && field.Name.Text=="fSdmFade");
                    File.WriteAllText(Path.Combine(recipe.Directory,"policy.txt"),
                        $"{(hasHatching?1:0)} {(hasShadowFade?1:0)}\n");
                    // These are constructor values. Before drawing, the native
                    // current owners overwrite lighting, camera and refraction
                    // through this program's own named layouts.
                    var packet=D3D11DeferredConstantInputPreparation.PrepareRecipe(canonical,pixel,scene,caustics,false);
                    var clusterFields=WriteClusterLayout(recipe.Directory,pixel,globals,reasons);
                    // RDEF may retain disabled plugin declarations. Unread
                    // lanes keep their constructor/template storage; they do
                    // not request an invented current value or upload.
                    reasons.AddRange(packet.MissingFields.Where(name=>!clusterFields.Contains(name) &&
                            globals.Variables.Any(field=>field.Name.Text==name && IsUsed(field)))
                        .Select(field=>"Constant field has no producer: "+field));
                    string file="ps-b"+globalBinding.BindPoint+".bin";
                    File.WriteAllBytes(Path.Combine(recipe.Directory,file),packet.Bytes);
                    File.WriteAllText(Path.Combine(recipe.Directory,"cbuffers.txt"),
                        FormattableString.Invariant($"PS {globalBinding.BindPoint} {file}\n"));
                    File.WriteAllLines(Path.Combine(recipe.Directory,"camera-writes.txt"),packet.CameraWriteLayout);
                    WriteHeightFogLayout(recipe.Directory,pixel,globals,reasons);
                    var lighting=CreateWrites(globals,LightingNames,LightingSizes,reasons);
                    using(var stream=File.Create(Path.Combine(recipe.Directory,"hdr-lighting-layout.bin")))
                    using(var writer=new BinaryWriter(stream))
                    {
                        writer.Write(0x314C4C48u);writer.Write(globalBinding.BindPoint);
                        writer.Write(globals.ByteSize);writer.Write(lighting.Count);
                        foreach(var write in lighting)
                        {writer.Write(write.Kind);writer.Write(write.Destination);writer.Write(0);writer.Write(write.Bytes);}
                    }
                    File.WriteAllLines(Path.Combine(recipe.Directory,"refraction-writes.txt"),
                        CreateWrites(globals,RefractionNames,RefractionSizes,reasons).Select(write=>
                            FormattableString.Invariant($"{write.Kind} {write.Destination} 0 {write.Bytes}")));
                }
                reasons.AddRange(D3D11HdrResourceBindings.Write(repository,declaration,program,
                    recipe.Directory,resources.Source.Locale,current,outputWidth,outputHeight));
                if(program.PixelDefinitions.Bindings.Any(binding=>binding.Name.Text=="tMaterialTable"))
                {
                    var material=catalog.Resources.MaterialTable(declaration.NodeIndex);
                    if(material.IsEmpty)reasons.Add("Declared material table has no source rows.");
                    else File.WriteAllBytes(Path.Combine(recipe.Directory,"material-table.bin"),material.ToArray());
                }
                File.WriteAllLines(Path.Combine(recipe.Directory,"unsupported.txt"),reasons.Distinct(StringComparer.Ordinal));
            }
        }
    }

    private sealed record FieldWrite(int Kind,int Destination,int Bytes);
    private static bool IsUsed(DxbcRdefVariable field)=>(field.Flags&2)!=0;

    private static HashSet<string> WriteClusterLayout(string directory,DxbcResourceDefinitions definitions,
        DxbcRdefBuffer globals,List<string> reasons)
    {
        string[] names=["vClstZPrm","nClstPtch","fClstScl","vFadeDist"];
        int[] widths=[16,4,4,8],elements=[0,2,0,0],columns=[4,1,1,2];
        var supplied=new HashSet<string>(StringComparer.Ordinal);var rows=new List<string>();
        for(int kind=0;kind<names.Length;kind++)
        {
            var field=globals.Variables.SingleOrDefault(field=>field.Name.Text==names[kind]);
            if(field is null)continue;
            if(!IsUsed(field))continue;
            var type=definitions.Types.Single(type=>type.Offset==field.TypeOffset);
            int count=Math.Max(elements[kind],1),width=widths[kind];
            if(type.BaseType!=(kind==1?2:3) || type.Class!=(columns[kind]==1?0:1) ||
                type.Rows!=1 || type.Columns!=columns[kind] || type.Elements!=elements[kind] ||
                field.ByteSize!=(count-1)*16+width)
            {reasons.Add("Cluster field shape has no producer: "+names[kind]);continue;}
            for(int element=0;element<count;element++)
                rows.Add(FormattableString.Invariant($"{kind} {field.StartOffset+element*16} {element*width} {width}"));
            supplied.Add(names[kind]);
        }
        File.WriteAllLines(Path.Combine(directory,"cluster-writes.txt"),rows);
        return supplied;
    }

    private static void WriteHeightFogLayout(string directory,DxbcResourceDefinitions definitions,
        DxbcRdefBuffer globals,List<string> reasons)
    {
        string[] names=["hFogType","cHFog","hFogParam","hFogFlucPrms"];
        int[] words=[1,4,4,1],elements=[3,3,3,60];
        var rows=new List<string>();
        for(int kind=0;kind<names.Length;kind++)
        {
            var field=globals.Variables.SingleOrDefault(field=>field.Name.Text==names[kind]);
            if(field is null || !IsUsed(field))continue;
            var type=definitions.Types.Single(type=>type.Offset==field.TypeOffset);
            int width=words[kind]*4;
            if(type.BaseType!=(kind==0?2:3) || type.Class!=(words[kind]==1?0:1) ||
                type.Rows!=1 || type.Columns!=words[kind] || type.Elements!=elements[kind] ||
                field.ByteSize!=(elements[kind]-1)*16+width)
            {
                reasons.Add("Height fog field shape has no producer: "+names[kind]);continue;
            }
            for(int element=0;element<elements[kind];element++)
                rows.Add(FormattableString.Invariant($"{kind} {field.StartOffset+element*16} {element*width} {width}"));
        }
        File.WriteAllLines(Path.Combine(directory,"height-fog-writes.txt"),rows);
    }
    private static List<FieldWrite> CreateWrites(DxbcRdefBuffer globals,string[] names,int[] sizes,List<string> reasons)
    {
        var result=new List<FieldWrite>();
        for(int index=0;index<names.Length;index++)
        {
            var field=globals.Variables.SingleOrDefault(field=>field.Name.Text==names[index]);
            if(field is null || !IsUsed(field))continue;
            if(field.ByteSize!=sizes[index])reasons.Add("Current field shape has no producer: "+names[index]);
            else result.Add(new(index,field.StartOffset,field.ByteSize));
        }
        return result;
    }

    private static void WriteDeclarations(string directory,DxbcResourceDefinitions vertex,DxbcResourceDefinitions pixel)
    {
        var rows=new List<string>();
        void Add(string stage,DxbcResourceDefinitions definitions)
        {
            foreach(var buffer in definitions.Buffers)
            {
                var binding=definitions.Bindings.SingleOrDefault(binding=>binding.InputType==0 && binding.Name.Text==buffer.Name.Text);
                rows.Add(FormattableString.Invariant($"{stage} {buffer.Name.Text} {binding?.BindPoint} {buffer.ByteSize}"));
                foreach(var field in buffer.Variables)
                    rows.Add(FormattableString.Invariant($"field {field.Name.Text} {field.StartOffset} {field.ByteSize}"));
            }
        }
        Add("VS",vertex);Add("PS",pixel);
        File.WriteAllLines(Path.Combine(directory,"declarations.txt"),rows);
    }

    private static DeferredSceneInputs CreateConstructorInputs(D3D11CurrentSeasonFrame current,
        D3D11CurrentHdrCatalog catalog,int width,int height)
    {
        var raw=current.PreparedMain.Span;
        var camera=PreparedCameraSceneSource.ReadCompletePrefix(raw);
        var target=new Vector3(F(raw,0x20),F(raw,0x24),F(raw,0x28));
        var refraction=DeferredRefractionMath.Prepare(camera,target,F(raw,0x140),F(raw,0x144),
            (BinaryPrimitives.ReadUInt32LittleEndian(raw[0x158..])&2)!=0,0,1,true);
        var light=current.Lights[0];
        var lighting=light.Lighting.PrepareLighting(light.Lighting,catalog.SuppressAmbient).Lighting;
        var color=lighting.Lights[0].Color;
        return new(Lighting:lighting,Camera:camera,
            Fog:DeferredSceneConstants.PrepareSharedFog(light.State.Fog,catalog.FogOwnerEnabled,new(color.X,color.Y,color.Z)),
            Ambient:light.Lighting.PrepareAmbient(catalog.AmbientMapsRate),
            Hatching:DeferredSelected383PluginOwner.CreateFreshHatchingResetParameters(),
            HeightFog:DeferredHeightFogInputs.SuccessfulThreeLayerDefaults(),
            PixelToTexture:DeferredSceneConstants.PreparePixelToTexture(width,height,0,0,width,height),
            Refraction:refraction,Projection:new(NativeMatrixMath.Invert(Matrix(raw,0xC0),out _)),
            LocalAmbientOcclusion:DeferredRefractionMath.PrepareFreshLocalAmbientOcclusion(width,height),
            TranslucencyViewport:MainMapCameraDeferredSource.CreateFreshThinSurfaceViewport(),
            Occlusion:new(light.Lighting.Modifier.OcclusionRates),
            ShadowFade:DeferredSelected383PluginOwner.CreateFreshShadowFadeParameters());
    }
    private static float F(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
    private static Matrix4x4 Matrix(ReadOnlySpan<byte> bytes,int offset)=>new(
        F(bytes,offset),F(bytes,offset+4),F(bytes,offset+8),F(bytes,offset+12),
        F(bytes,offset+16),F(bytes,offset+20),F(bytes,offset+24),F(bytes,offset+28),
        F(bytes,offset+32),F(bytes,offset+36),F(bytes,offset+40),F(bytes,offset+44),
        F(bytes,offset+48),F(bytes,offset+52),F(bytes,offset+56),F(bytes,offset+60));
}
