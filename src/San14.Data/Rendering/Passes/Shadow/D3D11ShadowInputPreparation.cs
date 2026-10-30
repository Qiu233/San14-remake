using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11ShadowInitialLight(uint ObjectId,uint GroupMask,
    int MaximumCasters,CurrentDirectionalLightState State);
public sealed record D3D11ShadowInitialFamily(ReadOnlyMemory<byte> MainPrefix,
    IReadOnlyDictionary<int,uint> MainWords,IReadOnlyList<D3D11ShadowInitialLight> Lights,
    MapCommonRenderState Common,uint CommonRaster3F3);

/// <summary>Mount-time SHC1/SGP1 preparation from current Main/light values,
/// project shadow rules and the already prepared resource model mappings.</summary>
public sealed class D3D11ShadowInputPreparation
{
    private sealed record World(int Index,uint Exclusion,int Flags,float Opacity,int PrimitiveCount);
    private sealed record Template(int Index,int Bytes,bool LocalToProjection,byte[] Vertex,string[] Layout,
        int PixelProgram=-1,byte[]? Pixel=null,byte[]? PixelConstants=null);
    private const byte SupportedDepthPixelFormat=0x4E;
    private const uint ShadowDepthProgramClass=5;
    private const float QueueShadowBiasScale=1f;
    private readonly string locale;
    private readonly LoadedDeferredEarlyPredecessorSource predecessor;
    private readonly LoadedDeferredEarlyShadowPreparationArgumentsSource arguments;
    private readonly LoadedDeferredEarlyShadowDirectionAuxSource directionAux;
    private readonly LoadedDeferredEarlyShadowForceFitSource forceFit;
    private readonly ModelQueueDepthState depth;
    private readonly uint clip;
    private readonly int[] sortFields;
    private readonly World[] worlds;
    private readonly Template[] templates;

    private D3D11ShadowInputPreparation(GameResourceRepository repository,D3D11ModelInputPreparation field,
        D3D11CityGateTemplateBank primary,string language)
    {
        locale=language;
        predecessor=DeferredEarlyPredecessorSource.Load(repository,locale);
        if(predecessor.ShadowDepthDeclaration.PixelFormat!=SupportedDepthPixelFormat)
            throw new NotSupportedException("Shadow startup supports its depth4E/class5 resource.");
        arguments=DeferredEarlyShadowPreparationArgumentsSource.Read(DefaultView0Source.LoadDeclarationDefaults(repository));
        directionAux=DeferredEarlyShadowDirectionSource.ReadSourceCreatedTemplate(
            repository.LoadObjects("KIDSSystemResource",CommonRenderResources.SourceFileId));
        forceFit=LoadedDeferredEarlyShadowForceFitSource.Load(repository,locale);
        clip=MainMapCameraStartupSource.Load(repository,locale).StaticProjectionProfile;
        var queue=ReadShadowQueue(repository.LoadRenderGraph("system",0x024496EE));
        var entry=ModelRendererStateSourceReader.PrepareQueueDepth(queue,
            MainMapContextOwnerSource.LoadFreshHostDefaults(locale).ContextOwner3E4Bit29);
        // RenderType2's reached reverse-bit-clear queue override uses compare3.
        depth=entry with { PackedSourceState=(entry.PackedSourceState&~0x78UL)|(3UL<<3),CompareCode=3 };
        sortFields=WorldPrimitiveSortDescriptor.ReadActualFields(
            WorldPrimitiveOrderSource.LoadDefaultView0(repository).Descriptor(WorldPrimitiveSortFamily.Shadow).ToArray()).ToArray();
        worlds=field.SourceShadowMappings.Select(value=>new World(value.SceneInstanceIndex,value.Exclusion78,
            value.Flags10C,value.OpacityB0,value.PrimitiveCount)).ToArray();
        var shadowQueue=DeferredEarlyShadowQueueSource.Read(predecessor,WorldGeometryLateCpuSource.Load(repository,locale).Native);
        var selector=LoadedDeferredEarlyShadowSelectionSource.Load(repository,locale);
        var matrixSource=LoadedDeferredEarlyShadowVertexMatrixNativeSource.Load(repository);
        var shaderObjects=repository.LoadObjects("system",0x718CCB8B);
        var result=new List<Template>();
        foreach(var template in field.SourceShadowTemplates)
        {
            MapStaticModelAsset? treeAsset=null;int treeDraw=0;
            if(template.FinalTable.Object.ClassId==0xAB9C4087)
            {
                var source=field.SourcePrimitiveMappings.First(row=>row.TemplateIndex==template.TemplateIndex);
                var placement=field.SourcePlacements.Instances.Single(row=>row.Source.Index==source.SceneInstanceIndex);
                treeAsset=field.SourceAssets.LoadSceneInstance(placement.Source);treeDraw=source.DrawIndex;
            }
            Add(template.TemplateIndex,template.FinalTable,template.Matrix,template.VertexLayout,treeAsset,treeDraw);
        }
        int fieldTemplates=result.Count;
        string[] primaryDirectories=File.ReadAllLines(Path.Combine(primary.Directory,"models.txt"));
        foreach(var profile in primary.Profiles)
        {
            // These binding fields identify RDB resources, not native rule defaults.
            string[] key=profile.AssetSourceKey.Split('|');
            var resource=GameResourceId.Parse(key[1]);string[] parts=resource.Key.Split('/');
            var dictionary=repository.LoadObjects(parts[0],uint.Parse(parts[1],NumberStyles.HexNumber,CultureInfo.InvariantCulture));
            var textures=ModelTextureResources.Load(repository,dictionary,
                uint.Parse(key[3],NumberStyles.HexNumber,CultureInfo.InvariantCulture));
            var bindings=ModelShaderResources.LoadBindings(repository,textures);
            for(int draw=0;draw<profile.DrawIndices.Count;draw++)
            {
                int template=profile.TemplateIndices[draw];
                var named=ModelShaderResources.ResolveDrawBaseSource(bindings,profile.DrawIndices[draw],shaderObjects);
                var matrix=ModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings,profile.DrawIndices[draw])
                    ?? throw new NotSupportedException("Startup shadow needs this template's nonrigid source owner.");
                Add(fieldTemplates+template,named.Binding.Shader,matrix,
                    File.ReadAllLines(Path.Combine(primary.Directory,primaryDirectories[template],"layout.txt")));
            }
        }
        templates=result.OrderBy(value=>value.Index).ToArray();
        void Add(int index,NamedModelShader final,ModelMatrixBindingInput matrix,IReadOnlyList<string> resourceLayout,
            MapStaticModelAsset? treeAsset=null,int treeDraw=0)
        {
            if(final.Object.ClassId==0xAB9C4087)
            {
                var tree=Tree2ShadowTemplateSource.Prepare(repository,
                    treeAsset??throw new NotSupportedException("Tree2 shadow requires its actual owned model material."),
                    treeDraw,final,matrix,resourceLayout);
                result.Add(new(index,272,false,tree.Vertex,tree.Layout,tree.PixelProgram,tree.Pixel,tree.PixelConstants));
                return;
            }
            var renderer=shadowQueue.LoadRenderer(repository,shadowQueue.ReadFinalInterface(final));
            var state=selector.CreateOwnedRenderer(renderer);var frontend=selector.ConstructFreshFrontendFields();
            frontend.ApplyPrimitiveEntry(true);
            var program=state.PrepareCurrent(matrix,ShadowDepthProgramClass,frontend.Alpha1BE,frontend.RenderTarget0Blend1C0).ReadProgram(0);
            if(program.VertexProgram is not {} vertex || program.PixelProgram is not null)
                throw new NotSupportedException("Startup shadow template requires its separate alpha material owner.");
            var contract=DeferredEarlyShadowVertexMatrixSource.Import(matrixSource,renderer,vertex);
            if(contract.Skinned||contract.Cloth)throw new NotSupportedException("Startup shadow template requires its nonrigid owner.");
            var required=renderer.Shader.ReadSignatures(vertex).Parts.Single(p=>p.Kind==DxbcSignatureKind.Input).Parameters
                .Where(p=>p.RawSystemValue==0).Select(p=>(p.SemanticName.Text,p.SemanticIndex)).ToArray();
            var layout=resourceLayout.Where(line=>
            {
                string[] words=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                return required.Any(name=>name.Text==words[0]&&name.SemanticIndex==uint.Parse(words[1],CultureInfo.InvariantCulture));
            }).ToArray();
            result.Add(new(index,contract.ByteSize,contract.UsesLocalToProjection,
                renderer.Shader.Container.GetProgramBytes(renderer.Shader.Source.Bytes,vertex).ToArray(),layout));
        }
    }

    private static ModelQueueStateSource ReadShadowQueue(LoadedRenderGraph graph)
    {
        // Node19 is the resource's RenderType2 shadow queue. The ordinary
        // model queue reader intentionally accepts only RenderType0.
        var node=graph.Document.Nodes.Single(value=>value.Index==19);
        byte[] Field(uint hash)=>node.Call.Arguments.Single(value=>value.NameHash==hash).RawValue;
        return new(graph,node,BinaryPrimitives.ReadUInt16LittleEndian(Field(0x05D92ACC)),
            BinaryPrimitives.ReadUInt32LittleEndian(Field(0x5B2B79DA)),Field(0x092BC250)[0],
            BinaryPrimitives.ReadUInt32LittleEndian(Field(0x72DB7EBD)),Field(0x9075EDA0)[0],
            Field(0xA98962A5)[0],Field(0xB287F508)[0]!=0,node.Call.Arguments);
    }

    public static D3D11ShadowInputPreparation Load(GameResourceRepository repository,D3D11ModelInputPreparation field,
        D3D11CityGateTemplateBank primary,string locale)
        => new(repository,field,primary,locale);

    public void WriteNativeInputs(string inputsDirectory,D3D11ShadowInitialFamily initial,
        D3D11ShadowInitialFamily distant)
    {
        string directory=Path.Combine(inputsDirectory,"city-gate-shadow-input");Directory.CreateDirectory(directory);
        foreach(var template in templates)
        {
            string output=Path.Combine(directory,"gpu","template-"+template.Index);Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output,"vs.dxbc"),template.Vertex);File.WriteAllLines(Path.Combine(output,"layout.txt"),template.Layout);
            if(template.Pixel is not null)
            {
                File.WriteAllBytes(Path.Combine(output,"ps.dxbc"),template.Pixel);
                File.WriteAllBytes(Path.Combine(output,"ps-b0.bin"),template.PixelConstants!);
            }
        }
        using var admission=new BinaryWriter(File.Create(Path.Combine(directory,"admission.bin")));
        admission.Write(0x31414743u);admission.Write(2);
        WriteFamily("initial",initial);WriteFamily("mode1",distant);
        void WriteFamily(string name,D3D11ShadowInitialFamily input)
        {
            uint Word(int at)=>input.MainWords[at];float F(int at)=>BitConverter.UInt32BitsToSingle(Word(at));
            int selector=unchecked((int)Word(0xA00));
            if(selector is <1 or >32)throw new NotSupportedException("Startup shadow needs its actual selected kind1 record.");
            var selected=input.Lights.First(light=>(light.GroupMask&(1u<<(selector-1)))!=0);
            var currentArguments=arguments.SelectCurrentQuality(Word(0xA88));
            if(directionAux.CaptureCurrent(input.Common).RequiresAngleClamp(currentArguments))
                throw new NotSupportedException("Startup shadow requires its actual clamped direction producer.");
            var direction=DeferredEarlyShadowDirectionSource.PrepareUnclampedNumericFields(selected.State.Lighting,Word(0xA04),F(0xA28));
            var cascades=new DeferredEarlyShadowCascadeMainFields(F(0x9EC),F(0x9F4),F(0x9F8),F(0x9FC),F(0xA2C),Word(0xA84));
            var compact=DeferredEarlyShadowCompactSource.PrepareNumericFields(Word(0xA3C),cascades,
                BinaryPrimitives.ReadSingleLittleEndian(input.MainPrefix.Span[0x144..]));
            float tangent=NativeCameraTangent.Tangent1020A90(
                NativeFloat.Multiply(BinaryPrimitives.ReadSingleLittleEndian(input.MainPrefix.Span[0x148..]),.5f),NativeCameraTangentDispatch.Sse);
            byte[] force=new byte[8];forceFit.ApplyOwnedConstructorFragment(force);
            uint forceFlag=BinaryPrimitives.ReadUInt32LittleEndian(force.AsSpan(4))==0?0u:1u;
            string output=Path.Combine(directory,name);Directory.CreateDirectory(output);
            using(var writer=new BinaryWriter(File.Create(Path.Combine(output,"cpu-profile.bin"))))
            {
                writer.Write(0x31434853u);writer.Write(clip);writer.Write(tangent);writer.Write(cascades.Far9EC);writer.Write(cascades.FarOverrideA2C);
                writer.Write(compact.PassCount);foreach(float end in compact.PassEndsBB0ToBBC)writer.Write(end);Vector(writer,direction.Camera160);
                writer.Write(forceFlag);writer.Write(predecessor.ShadowDepthDeclaration.Width);writer.Write(predecessor.ShadowDepthDeclaration.Height);
                writer.Write(1u);writer.Write(compact.Mask22C);writer.Write((uint)selected.MaximumCasters);writer.Write(worlds.Length);writer.Write(input.Lights.Count);
                for(int i=0;i<input.Lights.Count;i++){writer.Write(input.Lights[i].GroupMask);writer.Write(i);}
                foreach(var world in worlds)
                {
                    // Resource preparation has no successful View. Native
                    // calculates current admission from this metadata and its
                    // actual View, World masks and same-request ordered lights.
                    writer.Write(world.Index);writer.Write(0u);writer.Write(world.Exclusion);writer.Write(world.Flags);writer.Write(world.Opacity);writer.Write(world.PrimitiveCount);
                }
            }
            using(var writer=new BinaryWriter(File.Create(Path.Combine(output,"gpu-profile.bin"))))
            {
                writer.Write(0x31504753u);writer.Write(depth.DepthTest?1u:0u);writer.Write(depth.DepthWrite?1u:0u);writer.Write(depth.CompareCode+1);writer.Write(depth.StencilEnabled?1u:0u);
                writer.Write(ShadowDepthProgramClass);writer.Write(sortFields.Length);foreach(int field in sortFields)writer.Write(field);
                writer.Write(templates.Length);foreach(var template in templates){writer.Write(template.Index);writer.Write(template.Bytes);writer.Write(template.LocalToProjection?1u:0u);writer.Write(template.PixelProgram);}
                writer.Write(F(0x9F0));writer.Write(cascades.Far9EC);Vector(writer,direction.BasisB0);Vector(writer,direction.BasisC0);Vector(writer,direction.DirectionD0);writer.Write(Word(0xA38));
            }
            admission.Write(input.Common.ModelExclusionMask!.Value);admission.Write(1u);admission.Write(input.Lights.Count);
            foreach(var light in input.Lights){admission.Write(light.GroupMask);admission.Write((uint)light.State.HeaderCasterPlane);admission.Write((uint)light.State.CasterPlaneForLocalLight);admission.Write((uint)light.State.CasterPlaneForReflectiveShadowMap);admission.Write(0u);}
            WriteRaster(Path.Combine(inputsDirectory,"shadow-raster-source-input",name),input,compact.PassCount);
        }
    }

    private void WriteRaster(string directory,D3D11ShadowInitialFamily input,int count)
    {
        uint selector=input.MainWords[0xA40];
        if(input.CommonRaster3F3!=0||selector>1)
            throw new NotSupportedException("Startup shadow raster requires its supported bias source branch.");
        // D43190 writes ShadowCPU5A4=.5 then1 before shadow submission.
        float scale=QueueShadowBiasScale;
        // D43190 A40==0 publishes A08 once; A40==1 replaces it per pass.
        float[] values=new[]{0xA08,0xA10,0xA18,0xA20}.Select(at=>
            BitConverter.UInt32BitsToSingle(input.MainWords[selector==0?0xA08:at])).ToArray();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"inputs.txt"),FormattableString.Invariant($"{selector} {input.CommonRaster3F3} {scale:R} ")+
            string.Join(" ",values.Select(value=>value.ToString("R",CultureInfo.InvariantCulture)))+"\n");
        float half=scale*.5f,slope=half+half;
        File.WriteAllLines(Path.Combine(directory,"raster.txt"),Enumerable.Range(0,count).Select(pass=>
            FormattableString.Invariant($"{pass} {(int)(values[pass]*16777216f)} {slope:R} 0 3 3 1 1 1 0 0")));
    }
    private static void Vector(BinaryWriter writer,Vector4 value){writer.Write(value.X);writer.Write(value.Y);writer.Write(value.Z);writer.Write(value.W);}
}
