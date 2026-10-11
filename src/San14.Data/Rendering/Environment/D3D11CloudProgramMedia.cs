using San14.Data.Formats;

namespace San14.Data;

/// <summary>The installed Native Main and WORK Cloud programs and their
/// consumer layouts. Targets, routing and samplers remain separate media.</summary>
public sealed class D3D11CloudProgramMedia
{
    private D3D11CloudProgramMedia(SkyCloudProgramResources resources,SkyCloudMainProgramSelection main,SkyCloudColorProgramSelection work)
    {
        Resources=resources;Main=main;Work=work;
        ValidateNativeInterfaces();
    }
    public SkyCloudProgramResources Resources{get;}
    public SkyCloudMainProgramSelection Main{get;}
    public SkyCloudColorProgramSelection Work{get;}
    public static D3D11CloudProgramMedia Prepare(SkyCloudProgramResources resources,
        MapSourceCreatedSeasonLightingFrame present,ReadOnlySpan<byte> selectedLight)
    {
        var weather=SkyAppliedSourceCloudWeatherFields.PrepareCurrent(present);
        bool earth=!SkySeasonPackageTextures.Read(present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage).Earth.IsSourceReferenceZero;
        return new(resources,resources.SelectMain(SkyDirectionalRendererFeatureWriter.WriteRequest(selectedLight),
            weather.ReadBits(0x674),weather.ReadBits(0x678),earth),resources.SelectColor(weather.ReadBits(0x674)));
    }
    public void RequireCurrent(MapSourceCreatedSeasonLightingFrame present,ReadOnlySpan<byte> selectedLight)
    {
        var weather=SkyAppliedSourceCloudWeatherFields.PrepareCurrent(present);
        bool earth=!SkySeasonPackageTextures.Read(present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage).Earth.IsSourceReferenceZero;
        var main=Resources.SelectMain(SkyDirectionalRendererFeatureWriter.WriteRequest(selectedLight),weather.ReadBits(0x674),weather.ReadBits(0x678),earth);
        var work=Resources.SelectColor(weather.ReadBits(0x674));
        bool Same(SkyCloudMainPass a,SkyCloudMainPass b)=>a.VertexProgram==b.VertexProgram&&a.PixelProgram==b.PixelProgram;
        if(!Same(Main.Color,main.Color)||!Same(Main.History,main.History)||!Same(Main.Composite,main.Composite)||
            Work.VertexProgram!=work.VertexProgram||Work.PixelProgram!=work.PixelProgram)
            throw new NotSupportedException("Changed Native Cloud programs require a new program resource lifecycle.");
    }

    public void WriteNativeInputs(string inputRoot)
    {
        string main=Path.Combine(inputRoot,"main-cloud-input"),work=Path.Combine(inputRoot,"ambient-input"),
            continuous=Path.Combine(inputRoot,"main-cloud-continuous-input"),weather=Path.Combine(inputRoot,"current-cloud-weather-input");
        foreach(string directory in new[]{main,work,continuous,weather})Directory.CreateDirectory(directory);
        var rows=new List<string>();
        foreach(var pass in new[]{Main.Color,Main.History,Main.Composite})
        {
            string name="pass"+pass.Pass;
            File.WriteAllBytes(Path.Combine(main,name+"-vs.dxbc"),pass.CopyVertexBytes());
            File.WriteAllBytes(Path.Combine(main,name+"-ps.dxbc"),pass.CopyPixelBytes());
            rows.Add(FormattableString.Invariant($"{name} {pass.Combination.Index} {pass.VertexProgram} {pass.PixelProgram} {Size(pass.VertexDefinitions)} {Size(pass.PixelDefinitions)}"));
        }
        File.WriteAllLines(Path.Combine(main,"programs.txt"),rows);
        File.WriteAllBytes(Path.Combine(work,"clouds-vs.dxbc"),Work.Shader.Container.GetProgramBytes(Work.Shader.Source.Bytes,Work.VertexProgram).ToArray());
        File.WriteAllBytes(Path.Combine(work,"clouds-ps.dxbc"),Work.Shader.Container.GetProgramBytes(Work.Shader.Source.Bytes,Work.PixelProgram).ToArray());
        File.WriteAllLines(Path.Combine(continuous,"cb-layout.txt"),new[]{
            $"pass1-vs bayer_pos {Field(Main.Color.VertexDefinitions,"g_iBayerPosNum").StartOffset}",
            $"pass1-ps previous_w2p {Field(Main.Color.PixelDefinitions,"g_mtPrevW2P").StartOffset}",
            $"pass2-ps bayer_pos {Field(Main.History.PixelDefinitions,"g_iBayerPosNum").StartOffset}",
            $"pass2-ps previous_vector {Field(Main.History.PixelDefinitions,"g_vPrevVector").StartOffset}"});
        WriteWeatherLayout(Main.Color.PixelDefinitions,Path.Combine(weather,"main-layout.txt"));
        WriteWeatherLayout(Work.PixelDefinitions,Path.Combine(weather,"work-layout.txt"));
    }

    private static int Size(DxbcResourceDefinitions definitions)=>definitions.Buffers.Count==0?0:checked((int)definitions.Buffers.Single().ByteSize);
    private static DxbcRdefVariable Field(DxbcResourceDefinitions definitions,string name)=>definitions.Buffers.Single().Variables.Single(v=>v.Name.Text==name);
    private static void RequireFields(DxbcResourceDefinitions definitions,int size,params (string Name,int Offset,int Size)[] fields)
    {
        if(Size(definitions)!=size||(size==0&&definitions.Buffers.Count!=0))throw new NotSupportedException("Native Cloud constant-buffer extent is unsupported.");
        foreach(var expected in fields)
        {
            var field=Field(definitions,expected.Name);
            var type=definitions.Types.Single(type=>type.Offset==field.TypeOffset);
            if(field.StartOffset!=expected.Offset||field.ByteSize!=expected.Size||type.BaseType!=3||type.Members.Count!=0)
                throw new NotSupportedException("Native Cloud camera interface is unsupported: "+expected.Name);
        }
        if(size==0?definitions.Bindings.Any(binding=>binding.InputType==0):
            definitions.Bindings.Count(binding=>binding.InputType==0)!=1||definitions.Bindings.Single(binding=>binding.InputType==0).BindPoint!=0)
            throw new NotSupportedException("Native Cloud requires its constants at register b0.");
    }
    private void ValidateNativeInterfaces()
    {
        (string,int,int)[] vertex=[("ksgClipPlane",0,16),("g_mtP2W",16,64),("g_vCameraDirVectors",80,64),("g_vProjFarNear",144,8)];
        RequireFields(Main.Color.VertexDefinitions,176,vertex);RequireFields(Work.VertexDefinitions,160,vertex);
        RequireFields(Main.Color.PixelDefinitions,560,("litDir",0,16),("vEye",16,16),("fog",32,16),("Scat",48,64),
            ("g_vRaySampleNum",288,16),("g_vSky2SunRGB",448,16),("g_vSky2SunDir",464,16),
            ("g_vSky2MoonRGB",480,16),("g_vSky2MoonDir",496,16),("g_vClampValue",544,16));
        RequireFields(Work.PixelDefinitions,512,("litDir",0,16),("vEye",16,16),("fog",32,16),("Scat",48,64),
            ("g_vSky2SunRGB",384,16),("g_vSky2SunDir",400,16),("g_vSky2MoonRGB",416,16),("g_vSky2MoonDir",432,16),("g_vClampValue",496,16));
        foreach(var pass in new[]{Main.History,Main.Composite})RequireFields(pass.VertexDefinitions,32,("ksgClipPlane",0,16),("g_vProjFarNear",16,8));
        RequireFields(Main.History.PixelDefinitions,48);
        RequireFields(Main.Composite.PixelDefinitions,0);
        RequireTextures(Main.Color.PixelDefinitions,[("sBase",8u),("sDetail",8u),("sDistort",4u),("sCirrus",4u),("sSkyAmbientAltitude2dTable",4u),("sTransmit2dTable",4u)]);
        RequireTextures(Main.History.PixelDefinitions,[("sReprojRaymarchedCloud",4u),("sReprojRaymarchedCloudAlpha",4u),("sReprojRaymarchedInfo",4u),("sReprojPrevVolumetricCloud",4u),("sReprojPrevVolumetricCloudAlpha",4u)]);
        RequireTextures(Main.Composite.PixelDefinitions,[("sReprojectedVolumetricCloud",4u),("sReprojectedVolumetricCloudAlpha",4u)]);
        RequireTextures(Work.PixelDefinitions,[("sBase",8u),("sDetail",8u),("sDistort",4u),("sCirrus",4u),("sSkyAmbientAltitude2dTable",4u),("sTransmit2dTable",4u),("sSctr3dTable",8u)]);
        foreach(var pass in new[]{Main.Color,Main.History,Main.Composite})
        {
            var outputs=pass.Shader.ReadSignatures(pass.PixelProgram).Parts.Where(part=>part.Kind==DxbcSignatureKind.Output).SelectMany(part=>part.Parameters).ToArray();
            uint count=pass.Pass==1?3u:pass.Pass==2?2u:1u;
            if(outputs.Length!=count||outputs.Select(output=>output.SemanticIndex).Distinct().Count()!=count||
                outputs.Any(output=>!output.SemanticName.Text.Equals("SV_Target",StringComparison.OrdinalIgnoreCase)||output.SemanticIndex>=count))
                throw new NotSupportedException("Native Cloud output target interface is unsupported.");
            RequireVertexId(pass.Shader,pass.VertexProgram);
        }
        RequireVertexId(Work.Shader,Work.VertexProgram);
    }
    private static void RequireVertexId(LoadedShaderContainer shader,int program)
    {
        var inputs=shader.ReadSignatures(program).Parts.Where(part=>part.Kind==DxbcSignatureKind.Input).SelectMany(part=>part.Parameters);
        if(inputs.Any(input=>!input.SemanticName.Text.Equals("SV_VertexID",StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Native Cloud geometry requires a procedural vertex shader without vertex-buffer attributes.");
    }
    private static void RequireTextures(DxbcResourceDefinitions definitions,(string Name,uint Dimension)[] expected)
    {
        if(definitions.Bindings.Count(b=>b.InputType==2)!=expected.Length||definitions.Bindings.Count(b=>b.InputType==3)!=expected.Length)
            throw new NotSupportedException("Native Cloud texture and sampler interface is unsupported.");
        for(uint slot=0;slot<expected.Length;slot++)
        {
            var texture=definitions.Bindings.Single(b=>b.InputType==2&&b.BindPoint==slot);
            var sampler=definitions.Bindings.Single(b=>b.InputType==3&&b.BindPoint==slot);
            if(texture.Name.Text!=expected[slot].Name||texture.Dimension!=expected[slot].Dimension||sampler.Name.Text!="__smp"+expected[slot].Name||texture.BindCount!=1||sampler.BindCount!=1)
                throw new NotSupportedException("Native Cloud texture role is unsupported.");
        }
    }
    private static void WriteWeatherLayout(DxbcResourceDefinitions definitions,string path)
    {
        string[] names=["g_vBaseTexScale","g_vDetailTexScale","g_vDistortTexScale","g_vCirrusTexScale","g_vCirrusDistortTexScale","g_vRaySampleNum","g_vEarthRadius","g_vCloudParams1","g_vCloudParams2","g_vCloudParams0","g_vDistortScale","g_vCirrusParams","g_vCirrusDistortScale","g_vErodeScale","g_vHgParams0","g_vHgParams1","g_vSky2RayleighExtinction","g_vSky2MieExtinction","g_vStableDim","g_vClampValue"];
        var buffer=definitions.Buffers.Single();var rows=new List<string>{buffer.ByteSize.ToString(System.Globalization.CultureInfo.InvariantCulture)};
        for(int field=0;field<names.Length;field++)if(buffer.Variables.SingleOrDefault(variable=>variable.Name.Text==names[field]) is { } variable)
            rows.Add(FormattableString.Invariant($"{field} {variable.StartOffset} {variable.ByteSize/4}"));
        File.WriteAllLines(path,rows);
    }
}
