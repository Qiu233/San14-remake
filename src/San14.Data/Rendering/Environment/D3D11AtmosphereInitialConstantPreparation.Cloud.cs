using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public static partial class D3D11AtmosphereInitialConstantPreparation
{
    private static D3D11MainCloudConstants PrepareCloud(SkyCloudMainProgramSelection programs,
        D3D11CloudRenderResources renderResources,D3D11AtmosphereInitialInputs current,
        Vector4 eye,Vector4 target,Vector4 up,CameraLens lens,Matrix4x4 worldToProjection,
        SkySceneSharedAtmosphere? atmosphere)
    {
        var present=current.Present;
        var material=present.CloudMaterial;
        var weather=material.Weather;
        var options=current.Options;
        // Fresh phase/history/counter construction is explicit. No retained
        // frame is loaded; only this actual first owned delta is applied.
        var animation=SkyCloudAnimationState.PrepareInitial(material,options.CloudElapsedSeconds);
        var volume=material.PrepareVolume(animation);
        float[] previousVector=[animation.PendingDisplacement.X,animation.PendingDisplacement.Y,animation.PendingDisplacement.Z,0];
        float far=lens.ReverseDepth?0:1,near=lens.ReverseDepth?1:0;
        int reconstructionWidth=renderResources.ReconstructionWidth;
        int reconstructionHeight=renderResources.ReconstructionHeight;
        D3D11MainCloudColorConstants? color=null;
        if(atmosphere is not null)
        {
            float radius=present.AcceptedSeasonCamera.Inputs.Source.RequireRenderingAtmosphere().PlanetRadius;
            var p2w=NativeMatrixMath.Invert(worldToProjection,out _);
            Vector3 direction=new(S(target.X,eye.X),S(target.Y,eye.Y),S(target.Z,eye.Z));
            float length=(float)Math.Sqrt(A(A(M(direction.X,direction.X),M(direction.Y,direction.Y)),M(direction.Z,direction.Z)));
            float inverse=D(1,length);direction=new(M(direction.X,inverse),M(direction.Y,inverse),M(direction.Z,inverse));
            var rayCamera=MapCameraConstructionMath.CompleteAB6C60(new(0,0,0,1),new(direction,1),up,
                lens with{Near=1,Far=100},2,NativeCameraTangentDispatch.Sse);
            var rays=NativeMatrixMath.Invert(rayCamera.WorldToProjection100,out _);
            float[] vectors=new float[16];
            for(int corner=0;corner<4;corner++)
            {
                float x=(corner>>1)*2-1,y=1-(corner&1)*2;
                Vector4 a=Transform(x,y,far,rays),b=Transform(x,y,near,rays);float ai=D(1,a.W),bi=D(1,b.W);
                vectors[corner*4]=S(M(b.X,bi),M(a.X,ai));vectors[corner*4+1]=S(M(b.Y,bi),M(a.Y,ai));
                vectors[corner*4+2]=S(M(b.Z,bi),M(a.Z,ai));vectors[corner*4+3]=S(M(b.W,bi),M(a.W,ai));
            }
            var vertex=new Fields();vertex.Set("ksgClipPlane",Words(options.CloudClipPlane));vertex.Set("g_mtP2W",Words(p2w));
            vertex.Set("g_vCameraDirVectors",vectors);vertex.Set("g_vProjFarNear",far,near);vertex.SetInt("g_iBayerPosNum",0);
            // Bayer offsets use the reconstruction grid. The color pass viewport
            // is smaller, while the history target owns this grid's dimensions.
            vertex.Set("g_vProcSize", reconstructionWidth, reconstructionHeight,
                D(1, reconstructionWidth), D(1, reconstructionHeight));
            var pixel=new Fields();
            var outer=current.Lights.First(light=>(light.GroupMask&0x80)!=0).CompleteData;
            pixel.Set("litDir",Words(V(outer.Span,0)));pixel.Set("vEye",Words(eye));pixel.Set("fog",Words(atmosphere.Fog.Value));
            pixel.Set("Scat",atmosphere.Scattering.SelectMany(Words).ToArray());pixel.Set("g_mtPrevW2P",Words(Matrix4x4.Identity));
            var common=volume.PreparePixel(eye,lens.VerticalFovRadians,radius);
            pixel.Set("g_vBaseTexOffset",common.BaseTextureOffset.X,common.BaseTextureOffset.Y,common.BaseTextureOffset.Z);
            pixel.Set("g_vBaseTexScale",common.BaseTextureScale.X,common.BaseTextureScale.Y,common.BaseTextureScale.Z);
            pixel.Set("g_vDetailTexOffset",common.DetailTextureOffset.X,common.DetailTextureOffset.Y,common.DetailTextureOffset.Z);
            pixel.Set("g_vDetailTexScale",common.DetailTextureScale.X,common.DetailTextureScale.Y,common.DetailTextureScale.Z);
            pixel.Set("g_vDistortTexOffset",common.DistortTextureOffset.X,common.DistortTextureOffset.Y);
            pixel.Set("g_vDistortTexScale",common.DistortTextureScale.X,common.DistortTextureScale.Y);
            pixel.Set("g_vCirrusTexOffset",common.CirrusTextureOffset.X,common.CirrusTextureOffset.Y);
            pixel.Set("g_vCirrusTexScale",common.CirrusTextureScale.X,common.CirrusTextureScale.Y);
            pixel.Set("g_vCirrusDistortTexOffset",common.CirrusDistortTextureOffset.X,common.CirrusDistortTextureOffset.Y);
            pixel.Set("g_vCirrusDistortTexScale",common.CirrusDistortTextureScale.X,common.CirrusDistortTextureScale.Y);
            pixel.Set("g_vRaySampleNum",Words(common.RaySampleCount));pixel.Set("g_vEarthRadius",Words(common.EarthRadius));
            pixel.Set("g_vCloudParams0",Words(common.CloudParameters0));pixel.Set("g_vCloudParams1",Words(common.CloudParameters1));
            pixel.Set("g_vCloudParams2",Words(material.SampleStepParameters));pixel.Set("g_vDistortScale",Words(common.DistortionScale));
            pixel.Set("g_vCirrusParams",Words(common.CirrusParameters));
            pixel.Set("g_vCirrusDistortScale",common.CirrusDistortionScale.X,common.CirrusDistortionScale.Y);
            pixel.Set("g_vErodeScale",common.ErosionScale.X,common.ErosionScale.Y);
            pixel.Set("g_vHgParams0",Words(material.HgParameters0));pixel.Set("g_vHgParams1",Words(material.HgParameters1));
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
            pixel.Set("g_vSky2SunDir",M(rawSun.X,-1),M(rawSun.Y,-1),M(rawSun.Z,-1),SkyCloudMaterial.SunHorizonOffset);
            pixel.Set("g_vSky2MoonRGB",M(current.MoonRgb.X,moon),M(current.MoonRgb.Y,moon),M(current.MoonRgb.Z,moon),moon);
            pixel.Set("g_vSky2MoonDir",M(rawMoon.X,-1),M(rawMoon.Y,-1),M(rawMoon.Z,-1),0);
            var tables=present.AcceptedSeasonCamera.Inputs.Source.RequireRenderingAtmosphere();
            Vector4 rayleigh=new(M(tables.RayleighExtinction.X,.01f),M(tables.RayleighExtinction.Y,.01f),M(tables.RayleighExtinction.Z,.01f),0);
            if(weather.Saturation<1){var hsv=SkylightColorMath.RgbToHsv(rayleigh);hsv.Y=M(hsv.Y,weather.Saturation);rayleigh=SkylightColorMath.HsvToRgb(hsv);}
            pixel.Set("g_vSky2RayleighExtinction",rayleigh.X,rayleigh.Y,rayleigh.Z,A(SkyCloudMaterial.AtmosphereHeight,radius));
            pixel.Set("g_vSky2MieExtinction",M(tables.MieExtinction.X,.01f),M(tables.MieExtinction.Y,.01f),M(tables.MieExtinction.Z,.01f),0);
            pixel.Set("g_vClampValue",0,0,0,ctl.Scalar1F0);
            color=new(Pack(programs.Color.VertexDefinitions,176,vertex),Pack(programs.Color.PixelDefinitions,560,pixel));
        }
        var historyVertex=new Fields();historyVertex.Set("ksgClipPlane",Words(options.CloudClipPlane));historyVertex.Set("g_vProjFarNear",far,near);
        var historyPixel=new Fields();historyPixel.SetInt("g_iBayerPosNum",0);historyPixel.Set("g_vPrevVector",previousVector);
        historyPixel.Set("g_vProcSize", reconstructionWidth, reconstructionHeight,
            D(1, reconstructionWidth), D(1, reconstructionHeight));
        var compositeVertex=new Fields();compositeVertex.Set("ksgClipPlane",Words(options.CloudClipPlane));compositeVertex.Set("g_vProjFarNear",far,near);
        var compositePixel=new Fields();
        var axes=SkyCloudAnimationEncoding.CreateAxes(material,programs.Color.PixelDefinitions,includeReservedChannels:true);
        byte[] matrixBytes=new byte[64];var matrix=Words(worldToProjection);
        for(int i=0;i<matrix.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(matrixBytes.AsSpan(i*4),matrix[i]);
        byte[] previousMatrixBytes=new byte[64];var previousMatrix=Words(Matrix4x4.Identity);
        for(int i=0;i<previousMatrix.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(previousMatrixBytes.AsSpan(i*4),previousMatrix[i]);
        byte[] PackCompositePixel()=>programs.Composite.PixelDefinitions.Buffers.Count==0?Array.Empty<byte>():
            Pack(programs.Composite.PixelDefinitions,programs.Composite.PixelDefinitions.Buffers.Single().ByteSize,compositePixel);
        return new(programs,color,
            Pack(programs.History.VertexDefinitions,32,historyVertex),Pack(programs.History.PixelDefinitions,48,historyPixel),
            Pack(programs.Composite.VertexDefinitions,32,compositeVertex),PackCompositePixel(),axes,matrixBytes,previousMatrixBytes,weather.Cloudiness);
    }

    private static Vector4 Transform(float x,float y,float z,Matrix4x4 m)=>new(A(A(A(M(x,m.M11),M(y,m.M21)),M(z,m.M31)),m.M41),
        A(A(A(M(x,m.M12),M(y,m.M22)),M(z,m.M32)),m.M42),A(A(A(M(x,m.M13),M(y,m.M23)),M(z,m.M33)),m.M43),A(A(A(M(x,m.M14),M(y,m.M24)),M(z,m.M34)),m.M44));
}
