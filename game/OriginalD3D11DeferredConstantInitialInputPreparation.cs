using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Initialize each loaded HDR program from this real Main/season
/// and its current light pair. No prior complete GPU constants are inputs.</summary>
public static class OriginalD3D11DeferredConstantInitialInputPreparation
{
    public static void WriteNativeInputs(OriginalGameRepository repository,
        OriginalD3D11CurrentSeasonFrame current, OriginalD3D11CurrentHdrCatalog catalog,
        int outputWidth,int outputHeight)
    {
        var shader = repository.LoadShaderContainer("system",0x9D8E811F);
        var canonical = shader.ReadResourceDefinitions(1);
        var plan = OriginalDeferredTexturePlan.Read(repository.LoadRenderGraph("system",0x024496EE),166,
            repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280),shader,0);
        var reference = plan.ShaderReference;
        if(reference.ShaderObject.ClassId!=OriginalRenderGraphShaderReferences.ShaderObjectClassId ||
            reference.ShaderObject.ObjectId!=0xC96F6A61 || reference.ShaderFileHash!=0x9D8E811F || plan.ViewId!=0)
            throw new NotSupportedException("Deferred initialization requires the actual fresh868 Main/View0 header.");
        var caustics = OriginalDeferredSamplerSources.LoadEarlyCaustics(repository.SourceDirectory,current.Rgb.Inputs.MainFrame.Camera.ExecutableLocale);
        var raw = current.PreparedMain.Span;
        var camera = OriginalPreparedCameraSceneSource.ReadCompletePrefix(raw);
        var target = new Vector3(F(raw,0x20),F(raw,0x24),F(raw,0x28));
        var refraction = OriginalDeferredRefractionMath.Prepare(camera,target,F(raw,0x140),F(raw,0x144),
            (BinaryPrimitives.ReadUInt32LittleEndian(raw[0x158..])&2)!=0,0,1,true);
        var projection = new OriginalDeferredProjectionInputs(OriginalNativeMatrixMath.Invert(Matrix(raw,0xC0),out _));
        // The fresh868/C13E58 initialization precedes any node97 temporary
        // viewport setter and is not borrowed from a postdraw restore packet.
        var viewport = OriginalMainMapCameraDeferredSource.CreateFreshThinSurfaceViewport();
        var pixel = OriginalDeferredSceneConstants.PreparePixelToTexture(outputWidth,outputHeight,0,0,outputWidth,outputHeight);
        var local = OriginalDeferredRefractionMath.PrepareFreshLocalAmbientOcclusion(outputWidth,outputHeight);
        foreach(var route in catalog.Routes)
        {
            var outer = current.Lights.First(light=>(light.GroupMask&route.OuterMask)!=0);
            var inner = current.Lights.FirstOrDefault(light=>(light.GroupMask&route.InnerMask)!=0) ?? outer;
            byte[] effective = OriginalDeferredF103Kind1Modifier.ApplyWithModifier(
                outer.CompleteData.Span,inner.CompleteData.Span,route.Arg18).Bytes;
            var modifier = OriginalDeferredF103Kind1Modifier.HasBuiltinModifier(inner.CompleteData.Span)
                ? inner.CompleteData : effective;
            if((modifier.Span[0x1F7]&1)==0 || (BinaryPrimitives.ReadUInt32LittleEndian(modifier.Span[0x1F8..])&0x40)==0)
                throw new NotSupportedException("HDR initialization needs its edited Hatching/Fade module producer.");
            int count=4;
            while(count>1)
            {
                int offset=(count-1)*32+16;
                if(F(effective,offset)>0||F(effective,offset+4)>0||F(effective,offset+8)>0)break;
                count--;
            }
            var scene = new OriginalDeferredSceneInputs(
                Lighting:OriginalDeferredSceneConstants.ReadPreparedBaseLighting(effective.AsSpan(0,0x1B0),count),
                Camera:camera,Fog:OriginalDeferredSceneConstants.ReadSharedFog(effective.AsSpan(0,0x1B0),route.FogOwnerEnabled),
                Ambient:OriginalDeferredSceneConstants.ReadAmbientModifier(modifier.Span.Slice(0x1B0,0x178),catalog.AmbientMapsRate),
                Hatching:OriginalDeferredSelected383PluginOwner.CreateFreshHatchingResetParameters(),
                PixelToTexture:pixel,Refraction:refraction,Projection:projection,LocalAmbientOcclusion:local,
                TranslucencyViewport:viewport,Occlusion:OriginalDeferredSelected383PluginOwner.CreateFreshOcclusionParameters(),
                ShadowFade:OriginalDeferredSelected383PluginOwner.CreateFreshShadowFadeParameters());
            var packet = OriginalD3D11DeferredConstantInputPreparation.Prepare(canonical,
                route.PixelDefinitions ?? throw new InvalidDataException("HDR route has no loaded RDB program layout."),
                scene,caustics,route.Node==166&&route.PixelProgramIndex==383);
            File.WriteAllBytes(Path.Combine(route.ResourceDirectory,"ps-b0.bin"),packet.Bytes);
            OriginalD3D11DeferredConstantInputPreparation.WriteNativeInputs(route.CameraResourceDirectory,packet);
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
