using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public static partial class OriginalD3D11AtmosphereInitialConstantPreparation
{
    private sealed record CloudConstants(OriginalD3D11AtmosphereProgramSelection Programs,byte[] Vertex,byte[] Pixel,
        byte[] HistoryVertex,byte[] HistoryPixel);

    private static CloudConstants PrepareCloud(OriginalGameRepository repository,OriginalD3D11AtmosphereInitialInputs current,
        Vector4 eye,Vector4 target,Vector4 up,OriginalCameraLens lens,Matrix4x4 worldToProjection,
        OriginalSkySceneSharedAtmosphere atmosphere,byte[] rendererRequest,bool earthPresent)
    {
        var present=current.Present;var weather=OriginalSkyAppliedSourceCloudWeatherFields.PrepareCurrent(present);
        var options=current.Options;var input=CloudInputs(weather,present);
        float R(int at)=>input[at];
        // Fresh phase/history/counter construction is explicit. No retained
        // frame is loaded; only this actual first owned delta is applied.
        foreach(var axis in CloudAxes)input[axis.Phase]=CloudRemainder(S(0,M(options.CloudElapsedSeconds,R(axis.Wind))),R(axis.Period));
        float[] previousVector=new float[4];
        for(int lane=0;lane<3;lane++)previousVector[lane]=CloudRemainder(M(options.CloudElapsedSeconds,R(0x110+lane*4)),R(0xF0+lane*4));
        var p2w=OriginalNativeMatrixMath.Inverse3F6EB0(worldToProjection,out _);
        Vector3 direction=new(S(target.X,eye.X),S(target.Y,eye.Y),S(target.Z,eye.Z));
        float length=(float)Math.Sqrt(A(A(M(direction.X,direction.X),M(direction.Y,direction.Y)),M(direction.Z,direction.Z)));
        float inverse=D(1,length);direction=new(M(direction.X,inverse),M(direction.Y,inverse),M(direction.Z,inverse));
        var rayCamera=OriginalMapCameraConstructionMath.CompleteAB6C60(new(0,0,0,1),new(direction,1),up,
            lens with{Near=1,Far=100},2,OriginalNativeCameraTangentDispatch.Sse);
        var rays=OriginalNativeMatrixMath.Inverse3F6EB0(rayCamera.WorldToProjection100,out _);
        float far=lens.ReverseDepth?0:1,near=lens.ReverseDepth?1:0;float[] vectors=new float[16];
        for(int corner=0;corner<4;corner++)
        {
            float x=(corner>>1)*2-1,y=1-(corner&1)*2;
            Vector4 a=Transform(x,y,far,rays),b=Transform(x,y,near,rays);float ai=D(1,a.W),bi=D(1,b.W);
            vectors[corner*4]=S(M(b.X,bi),M(a.X,ai));vectors[corner*4+1]=S(M(b.Y,bi),M(a.Y,ai));
            vectors[corner*4+2]=S(M(b.Z,bi),M(a.Z,ai));vectors[corner*4+3]=S(M(b.W,bi),M(a.W,ai));
        }
        var vertex=new Fields();vertex.Set("ksgClipPlane",Words(options.CloudClipPlane));vertex.Set("g_mtP2W",Words(p2w));
        vertex.Set("g_vCameraDirVectors",vectors);vertex.Set("g_vProjFarNear",far,near);vertex.SetInt("g_iBayerPosNum",0);
        vertex.Set("g_vProcSize",options.Width,options.Height,D(1,options.Width),D(1,options.Height));
        var pixel=new Fields();
        var outer=current.Lights.First(light=>(light.GroupMask&0x80)!=0).CompleteData;
        pixel.Set("litDir",Words(V(outer.Span,0)));pixel.Set("vEye",Words(eye));pixel.Set("fog",Words(atmosphere.Fog.Value));
        pixel.Set("Scat",atmosphere.Scattering.SelectMany(Words).ToArray());pixel.Set("g_mtPrevW2P",Words(Matrix4x4.Identity));
        foreach(var group in CloudTextureGroups)
        {
            float[] scale=new float[group.Lanes],offset=new float[group.Lanes];
            for(int lane=0;lane<group.Lanes;lane++)
            {scale[lane]=D(1,R(group.Period+lane*4));offset[lane]=M(A(R(group.Phase+lane*4),R(group.Offset+lane*4)),scale[lane]);}
            pixel.Set("g_v"+group.Name+"TexOffset",offset);pixel.Set("g_v"+group.Name+"TexScale",scale);
        }
        pixel.Set("g_vRaySampleNum",S(R(0x90),R(0x94)),R(0x94),0,lens.VerticalFovRadians);
        float bottom=A(R(0x9C),R(0x228));pixel.Set("g_vEarthRadius",R(0x228),bottom,A(bottom,R(0xA0)),A(R(0x218),R(0x228)));
        pixel.Set("g_vCloudParams1",S(1,R(0x98)),R(0xAC),R(0xB4),D(R(0xA4),R(0xA8)));
        pixel.Set("g_vCloudParams2",D(R(0xB8),4),M(R(0xB8),2.5f),D(1,R(0xA8)),R(0xB0));
        pixel.Set("g_vCloudParams0",R(0x9C),R(0xA0),D(1,R(0xA0)),R(0x22C));
        pixel.Set("g_vDistortScale",M(D(1,R(0x130)),R(0x180)),M(D(1,R(0x134)),R(0x184)),M(D(1,R(0x138)),R(0x188)),R(0xD8));
        pixel.Set("g_vCirrusParams",R(0x218),S(1,R(0x21C)),R(0x220),R(0x224));
        pixel.Set("g_vCirrusDistortScale",M(R(0x1D0),D(1,R(0x198))),M(R(0x1D4),D(1,R(0x19C))));pixel.Set("g_vErodeScale",R(0xC0),R(0xC4));
        float h0=M(R(0xC8),R(0xC8)),h1=M(R(0xCC),R(0xCC));
        pixel.Set("g_vHgParams0",M(D(S(1,h0),12.566371f),R(0xD4)),M(R(0xC8),-2),A(h0,1),R(0xD0));
        pixel.Set("g_vHgParams1",M(D(S(1,h1),12.566371f),R(0xD4)),M(R(0xCC),-2),A(h1,1),0);
        var ctl=current.Controller.Controller;float sun=ctl.SkyInfoZW7C78.X,moon=ctl.SkyInfoZW7C78.Y;
        Vector4 rawSun=current.Controller.SunPosition!.Value,rawMoon=current.Controller.MoonPosition!.Value;
        if(sun>0&&M(rawSun.Y,-1)>-.15f)
        {
            float intensity=A(A(M(current.MoonRgb.Y,1f/3),M(current.MoonRgb.X,1f/3)),M(current.MoonRgb.Z,1f/3));
            if(D(.001953125f,current.ControllerGeometry40)>intensity)moon=0;
        }
        // Keep actual D3 setter RGB. Reconstructing it from Sky2's normalized
        // cache and maximum would introduce a separate binary32 round trip.
        Vector3 sunRgb=current.SunRgb;
        pixel.Set("g_vSky2SunRGB",M(sunRgb.X,sun),M(sunRgb.Y,sun),M(sunRgb.Z,sun),sun);
        pixel.Set("g_vSky2SunDir",M(rawSun.X,-1),M(rawSun.Y,-1),M(rawSun.Z,-1),R(0x230));
        pixel.Set("g_vSky2MoonRGB",M(current.MoonRgb.X,moon),M(current.MoonRgb.Y,moon),M(current.MoonRgb.Z,moon),moon);
        pixel.Set("g_vSky2MoonDir",M(rawMoon.X,-1),M(rawMoon.Y,-1),M(rawMoon.Z,-1),0);
        var tables=present.AcceptedSeasonCamera.Inputs.Source.Transmittance.Tables;
        Vector4 rayleigh=new(M(tables.RayleighExtinction.X,.01f),M(tables.RayleighExtinction.Y,.01f),M(tables.RayleighExtinction.Z,.01f),0);
        if(R(0xD8)<1){var hsv=OriginalSkylightColorMath.RgbToHsv(rayleigh);hsv.Y=M(hsv.Y,R(0xD8));rayleigh=OriginalSkylightColorMath.HsvToRgb(hsv);}
        pixel.Set("g_vSky2RayleighExtinction",rayleigh.X,rayleigh.Y,rayleigh.Z,A(R(0x22C),R(0x228)));
        pixel.Set("g_vSky2MieExtinction",M(tables.MieExtinction.X,.01f),M(tables.MieExtinction.Y,.01f),M(tables.MieExtinction.Z,.01f),0);
        pixel.Set("g_vClampValue",0,0,0,ctl.Scalar1F0);
        var shader=repository.LoadShaderContainer("system",0x42ED52ED);var bindings=shader.ReadG1Bindings();
        byte[] requested=bindings.Variants.Records[0].RawBytes.ToArray();
        foreach(var edit in new[]{(0,Math.Min(weather.ReadBits(0x674),2u)),(4,Math.Clamp(weather.ReadBits(0x678),4u,6u)),
            (8,earthPresent?0u:1u),(12,1u),(16,0u),(20,1u),(32,0u)})BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(edit.Item1),edit.Item2);
        var variant=bindings.Variants.Match(requested)??throw new NotSupportedException("Main Cloud current binding variant is absent.");
        int feature=Feature(bindings,rendererRequest);
        G2ShaderCombination Combo(uint pass)=>bindings.ResolveCombination([(uint)feature,0,0,0,0,0,(uint)variant.Index],pass);
        var color=Programs(Combo(1));var history=Programs(Combo(2));
        var historyVertex=new Fields();historyVertex.Set("ksgClipPlane",Words(options.CloudClipPlane));historyVertex.Set("g_vProjFarNear",far,near);
        var historyPixel=new Fields();historyPixel.SetInt("g_iBayerPosNum",0);historyPixel.Set("g_vPrevVector",previousVector);
        historyPixel.Set("g_vProcSize",options.Width,options.Height,D(1,options.Width),D(1,options.Height));
        return new(color,Pack(shader.ReadResourceDefinitions(color.VertexProgram),176,vertex),
            Pack(shader.ReadResourceDefinitions(color.PixelProgram),560,pixel),
            Pack(shader.ReadResourceDefinitions(history.VertexProgram),32,historyVertex),Pack(shader.ReadResourceDefinitions(history.PixelProgram),48,historyPixel));
    }

    private static Dictionary<int,float> CloudInputs(OriginalSkyAppliedSourceCloudWeatherFields w,OriginalMapSourceCreatedSeasonLightingFrame present)
    {
        // Consumed fresh 11919E0/1193800 constructor values, then current
        // selected weather setters. The offsets name mathematical inputs,
        // never a fabricated full shader/Camera allocation.
        Dictionary<int,float> v=new(){[0xA8]=1000,[0xB0]=1,[0x1A0]=0,[0x1A4]=0,[0x1C0]=0,[0x1C4]=0,[0x1D0]=75000,[0x1D4]=75000,
            [0x1E0]=2000000,[0x1E4]=2000000,[0x1F0]=0,[0x1F4]=0,[0x200]=2000000,[0x204]=2000000,[0x210]=0,[0x214]=0,
            [0x22C]=8000000,[0x230]=-.06f};
        void Copy(int target,int camera,int count=1){for(int i=0;i<count;i++)v[target+i*4]=w.ReadFloat(camera+i*4);}
        Copy(0x9C,0x624);Copy(0xB4,0x628);v[0x90]=w.ReadBits(0x638);v[0x94]=w.ReadBits(0x63C);
        Copy(0x98,0x640);Copy(0xA0,0x648);Copy(0xA4,0x64C);Copy(0xAC,0x658);Copy(0xB8,0x67C);
        v[0xD8]=F(present.PreparedCameraPrefix.Span,0x22C);v[0xC8]=CloudClamp(w.ReadFloat(0x680));v[0xCC]=CloudClamp(w.ReadFloat(0x684));
        Copy(0xD0,0x688);Copy(0xD4,0x68C);Copy(0xF0,0x690,3);Copy(0x100,0x69C,3);Copy(0x110,0x6A8,3);
        Copy(0xC0,0x6B4);Copy(0xC4,0x71C);Copy(0x130,0x6B8,3);Copy(0x140,0x6E4,3);Copy(0x150,0x6F0,3);
        Copy(0x168,0x720,2);Copy(0x170,0x728,2);Copy(0x178,0x730,2);Copy(0x180,0x738,3);
        Copy(0x218,0x744,4);Copy(0x198,0x764,2);Copy(0x1A8,0x76C,2);Copy(0x1B8,0x774,2);Copy(0x1C8,0x77C,4);
        foreach(int period in new[]{0xF0,0xF4,0xF8,0x130,0x134,0x138,0x168,0x16C,0x198,0x19C,0x1B8,0x1BC})v[period]=CloudSize(v[period]);
        v[0x228]=present.AcceptedSeasonCamera.Inputs.Source.Transmittance.Tables.PlanetRadius;
        return v;
    }
    private static float CloudSize(float value)=>value>=float.MaxValue?float.MaxValue:value>.0011920929f?value:.0011920929f;
    private static float CloudClamp(float value)=>value>=.9999881f?.9999881f:value>-.9999881f?value:-.9999881f;
    private static float CloudRemainder(float value,float period)
    {
        if(!float.IsFinite(value)||!float.IsFinite(period)||period<=0)throw new InvalidDataException("Cloud fresh phase requires finite positive periods.");
        double x=Math.Abs((double)value),y=period;if(x==y)return BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value)&0x80000000);if(x<y)return value;
        int a=(int)((BitConverter.DoubleToUInt64Bits(x)>>52)&2047),b=(int)((BitConverter.DoubleToUInt64Bits(y)>>52)&2047),blocks=a>b?(a-b)/24:0;
        double scaled=CloudMultiply(y,Math.ScaleB(1,blocks*24));
        for(int n=0;n<blocks;n++){x=CloudSubtract(x,CloudMultiply((int)CloudDivide(x,scaled),scaled));scaled=CloudMultiply(scaled,Math.ScaleB(1,-24));}
        x=CloudSubtract(x,CloudMultiply((int)CloudDivide(x,scaled),scaled));if(value<0)x=-x;return(float)x;
    }
    private static Vector4 Transform(float x,float y,float z,Matrix4x4 m)=>new(A(A(A(M(x,m.M11),M(y,m.M21)),M(z,m.M31)),m.M41),
        A(A(A(M(x,m.M12),M(y,m.M22)),M(z,m.M32)),m.M42),A(A(A(M(x,m.M13),M(y,m.M23)),M(z,m.M33)),m.M43),A(A(A(M(x,m.M14),M(y,m.M24)),M(z,m.M34)),m.M44));
    [MethodImpl(MethodImplOptions.NoInlining)]private static double CloudMultiply(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double CloudSubtract(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double CloudDivide(double a,double b)=>a/b;
    private static readonly (int Phase,int Wind,int Period)[] CloudAxes=[(0xE0,0x110,0xF0),(0xE4,0x114,0xF4),(0xE8,0x118,0xF8),
        (0x120,0x150,0x130),(0x124,0x154,0x134),(0x128,0x158,0x138),(0x160,0x178,0x168),(0x164,0x17C,0x16C),
        (0x190,0x1A8,0x198),(0x194,0x1AC,0x19C),(0x1B0,0x1C8,0x1B8),(0x1B4,0x1CC,0x1BC)];
    private static readonly (string Name,int Phase,int Offset,int Period,int Lanes)[] CloudTextureGroups=[
        ("Base",0xE0,0x100,0xF0,3),("Detail",0x120,0x140,0x130,3),("Distort",0x160,0x170,0x168,2),
        ("Cirrus",0x190,0x1A0,0x198,2),("CirrusDistort",0x1B0,0x1C0,0x1B8,2)];
}
