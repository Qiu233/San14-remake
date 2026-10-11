using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Initialize each loaded HDR program from this real Main/season
/// and its current light pair. No prior complete GPU constants are inputs.</summary>
public static class D3D11DeferredConstantInitialInputPreparation
{
    public static void WriteNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current, D3D11CurrentHdrCatalog catalog,
        int outputWidth,int outputHeight)
    {
        var shader = catalog.Resources[HdrShaderResources.MainLightingNode].Shader;
        var canonical = shader.ReadResourceDefinitions(1);
        var caustics = RenderSamplerSourceAdapters.LoadEarlyCaustics(repository.SourceDirectory,current.Rgb.Inputs.MainFrame.Camera.ExecutableLocale);
        var raw = current.PreparedMain.Span;
        var camera = PreparedCameraSceneSource.ReadCompletePrefix(raw);
        var target = new Vector3(F(raw,0x20),F(raw,0x24),F(raw,0x28));
        var refraction = DeferredRefractionMath.Prepare(camera,target,F(raw,0x140),F(raw,0x144),
            (BinaryPrimitives.ReadUInt32LittleEndian(raw[0x158..])&2)!=0,0,1,true);
        var projection = new DeferredProjectionInputs(NativeMatrixMath.Invert(Matrix(raw,0xC0),out _));
        // The fresh868/C13E58 initialization precedes any node97 temporary
        // viewport setter and is not borrowed from a postdraw restore packet.
        var viewport = MainMapCameraDeferredSource.CreateFreshThinSurfaceViewport();
        var pixel = DeferredSceneConstants.PreparePixelToTexture(outputWidth,outputHeight,0,0,outputWidth,outputHeight);
        var local = DeferredRefractionMath.PrepareFreshLocalAmbientOcclusion(outputWidth,outputHeight);
        foreach(var route in catalog.Routes)
        {
            var outer = current.Lights.First(light=>(light.GroupMask&route.OuterMask)!=0);
            var inner = current.Lights.FirstOrDefault(light=>(light.GroupMask&route.InnerMask)!=0) ?? outer;
            var lighting = outer.Lighting.PrepareLighting(inner.Lighting,catalog.SuppressAmbient).Lighting;
            var modifierLight = inner.Lighting.Modifier.Present ? inner : outer;
            var modifier = modifierLight.CompleteData;
            if((modifier.Span[0x1F7]&1)==0 || (BinaryPrimitives.ReadUInt32LittleEndian(modifier.Span[0x1F8..])&0x40)==0)
                throw new NotSupportedException("HDR initialization needs its edited Hatching/Fade module producer.");
            var firstColor = lighting.Lights[0].Color;
            var scene = new DeferredSceneInputs(
                Lighting:lighting,
                Camera:camera,Fog:DeferredSceneConstants.ReadSharedFog(outer.CompleteData.Span[..0x1B0],
                    catalog.FogOwnerEnabled,new Vector3(firstColor.X,firstColor.Y,firstColor.Z)),
                Ambient:modifierLight.Lighting.PrepareAmbient(catalog.AmbientMapsRate),
                Hatching:DeferredSelected383PluginOwner.CreateFreshHatchingResetParameters(),
                PixelToTexture:pixel,Refraction:refraction,Projection:projection,LocalAmbientOcclusion:local,
                TranslucencyViewport:viewport,Occlusion:DeferredSelected383PluginOwner.CreateFreshOcclusionParameters(),
                ShadowFade:DeferredSelected383PluginOwner.CreateFreshShadowFadeParameters());
            var packet = D3D11DeferredConstantInputPreparation.Prepare(canonical,
                route.Program.PixelDefinitions,
                scene,caustics,route.Declaration.NodeIndex==166&&route.Program.PixelProgramIndex==383);
            File.WriteAllBytes(Path.Combine(route.ResourceDirectory,"ps-b0.bin"),packet.Bytes);
            D3D11DeferredConstantInputPreparation.WriteNativeInputs(route.CameraResourceDirectory,packet);
        }
        foreach(var group in catalog.Routes.GroupBy(route=>Path.GetDirectoryName(route.CameraResourceDirectory)!))
            File.WriteAllLines(Path.Combine(group.Key,"passes.txt"),group.Select(route=>route.Identity));
    }

    private static float F(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
    private static Matrix4x4 Matrix(ReadOnlySpan<byte> bytes,int offset)=>new(
        F(bytes,offset),F(bytes,offset+4),F(bytes,offset+8),F(bytes,offset+12),
        F(bytes,offset+16),F(bytes,offset+20),F(bytes,offset+24),F(bytes,offset+28),
        F(bytes,offset+32),F(bytes,offset+36),F(bytes,offset+40),F(bytes,offset+44),
        F(bytes,offset+48),F(bytes,offset+52),F(bytes,offset+56),F(bytes,offset+60));
}
