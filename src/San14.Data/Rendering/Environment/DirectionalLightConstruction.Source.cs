using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public static partial class DirectionalLightConstruction
{
    // LightData is an interop destination; authored controls remain typed and independently owned.
    private static ConstructionResult ConstructOwned(DirectionalLightSettings settings,
        DirectionalLightConstructionInputs input, ReadOnlySpan<byte> template)
    {
        byte[] constructor = new byte[LightDataBytes];
        byte[] mask = Enumerable.Repeat((byte)1, LightDataBytes).ToArray();
        foreach (var (offset, length) in new (int,int)[] {
            (0x10C,4),(0x11C,4),(0x150,96),(0x1F4,8),(0xF21,3),(0xF2C,4),(0x10B8,8) })
        {
            template.Slice(offset,length).CopyTo(constructor.AsSpan(offset,length));
            Array.Clear(mask,offset,length);
        }
        for (int index = 0; index < settings.Lights.Count; index++)
        {
            var value = settings.Lights[index];
            Store(constructor, 0x10 + index * 32, value.Color);
            Store(constructor, 0x80 + index * 4, value.Fade);
        }
        for (int index = 0; index < settings.Rims.Count; index++)
            LightModifierEncoding.WriteIndexedRim(constructor, index, settings.Rims[index]);
        for (int index = 0; index < settings.DirtChannels.Count; index++)
            LightModifierEncoding.WriteDirtChannel(constructor, index, settings.DirtChannels[index]);
        for (int index = 0; index < settings.ReflectanceLayers.Count; index++)
            LightModifierEncoding.WriteReflectanceLayer(constructor, index, settings.ReflectanceLayers[index]);
        for (int index = 0; index < settings.WeatheringMasks.Count; index++)
        {
            var value = settings.WeatheringMasks[index];
            Store(constructor, 0xE38 + index * 4, value.Fade);
            Store(constructor, 0xE98 + index * 4, value.MinimumFade);
        }
        for (int index = 0; index < settings.RampRims.Count; index++)
        {
            var value = settings.RampRims[index];
            Store(constructor, 0xA08 + index * 28, value.Elevation);
            Store(constructor, 0xA0C + index * 28, value.Azimuth);
            Store(constructor, 0xA10 + index * 28, value.Scale);
            Store(constructor, 0xA14 + index * 28, value.Sharpness);
            Store(constructor, 0xA18 + index * 28, value.ColorScale);
        }
        Store(constructor, 0x90, settings.WorldPosition);
        Store(constructor, 0xE0, settings.AmbientColor);
        Store(constructor, 0xEC, settings.Shadow.ReceiveShadowFade);
        Store(constructor, 0x100, settings.Hemisphere.SkyColor);
        Store(constructor, 0x110, settings.Hemisphere.GroundColor);
        LightModifierEncoding.WriteMaterial(constructor, settings.Material, settings.MaterialFlags);
        LightModifierEncoding.WriteLightMaps(constructor, settings.LightMaps);
        Store(constructor, 0x270, settings.Rim.RmScale);
        Store(constructor, 0x274, settings.Rim.RmGScale3);
        Store(constructor, 0x280, settings.Eye.EyetEyetRGBScale);
        Store(constructor, 0x284, settings.Eye.EyetEyetAlphaScale);
        Store(constructor, 0x288, settings.Eye.EyetEyetLSERGBFade);
        Store(constructor, 0x28C, settings.Eye.EyetEyetLSEAlphaFade);
        Store(constructor, 0x290, settings.AccessoryLights.Light0Scale);
        Store(constructor, 0x294, settings.AccessoryLights.Light1Scale);
        Store(constructor, 0x298, settings.AccessoryLights.Light2Scale);
        Store(constructor, 0x29C, settings.AccessoryLights.Light3Scale);
        Store(constructor, 0x2A0, settings.AccessoryLights.ScaleX);
        Store(constructor, 0x2A4, settings.AccessoryLights.ScaleZ);
        Store(constructor, 0x2A8, settings.AccessoryLights.SpeedX);
        Store(constructor, 0x2AC, settings.AccessoryLights.SpeedZ);
        Store(constructor, 0x2B0, settings.AccessoryLights.OffsetX);
        Store(constructor, 0x2B4, settings.AccessoryLights.OffsetZ);
        Store(constructor, 0x2B8, settings.LightProbe.Scale);
        Store(constructor, 0x2BC, settings.LightProbe.OffsetY);
        LightModifierEncoding.WriteSubsurface(constructor, settings.Subsurface);
        LightModifierEncoding.WriteOcclusion(constructor, settings.Occlusion);
        LightModifierEncoding.WriteLightingScales(constructor, settings.IndirectLighting,
            settings.SurfaceEffects, settings.SubLights, settings.Hemisphere.Scale);
        Store(constructor, 0x9B8, settings.IndexedRamp.MaskFade);
        Store(constructor, 0x9BC, settings.IndexedRamp.MaskScale);
        Store(constructor, 0x9C0, settings.IndexedRamp.MaskRotation);
        Store(constructor, 0x9C4, settings.IndexedRamp.MaskUVelocity);
        Store(constructor, 0x9C8, settings.IndexedRamp.MaskVVelocity);
        Store(constructor, 0x9CC, settings.IndexedRamp.FirstLightRampCoefficientAdjuster);
        Store(constructor, 0x9D0, settings.IndexedRamp.ParallelSubLightsRampCoefficientAdjuster);
        Store(constructor, 0x9D4, settings.IndexedRamp.FirstLightRampCoordinateScaler);
        Store(constructor, 0x9D8, settings.IndexedRamp.ParallelSubLightsRampCoordinateScaler);
        Store(constructor, 0x9DC, settings.IndexedRamp.FirstLightRampCoordinateBias);
        Store(constructor, 0x9E0, settings.IndexedRamp.ParallelSubLightsRampCoordinateBias);
        Store(constructor, 0x9E4, settings.IndexedRamp.IRDFade);
        Store(constructor, 0x9E8, settings.IndexedRamp.OccThresForIRShadow);
        Store(constructor, 0x9EC, settings.IndexedRamp.IRShadowFade);
        Store(constructor, 0x9F0, settings.IndexedRamp.IRLocalLightFade);
        Store(constructor, 0x9F4, settings.IndexedRamp.IRLocalLightScale);
        Store(constructor, 0x9F8, settings.IndexedRamp.IRAFade);
        Store(constructor, 0x9FC, settings.IndexedRamp.RampCoordRangeMin);
        Store(constructor, 0xA00, settings.IndexedRamp.RampCoordRangeMax);
        Store(constructor, 0xA04, settings.IndexedRamp.SDOccFade);
        Store(constructor, 0xA78, settings.HairHighlight.Elevation);
        Store(constructor, 0xA7C, settings.HairHighlight.Azimuth);
        Store(constructor, 0xA80, settings.HairHighlight.Rotation);
        Store(constructor, 0xA84, settings.HairHighlight.ReflectionRatio);
        Store(constructor, 0xA88, settings.HairHighlight.Scaler);
        Store(constructor, 0xA94, settings.HairHighlight.Threshold);
        Store(constructor, 0xA98, settings.HairHighlight.LightElevationRatio);
        Store(constructor, 0xA9C, settings.HairHighlight.LightAzimuthRatio);
        Store(constructor, 0xAA0, settings.Edge.Thickness);
        Store(constructor, 0xAA4, settings.Edge.DepthThreshold);
        Store(constructor, 0xAA8, settings.Edge.ReflectScale);
        Store(constructor, 0xAAC, settings.Edge.HighlightRamp);
        Store(constructor, 0xAB0, settings.Edge.CameraRimSharpness);
        Store(constructor, 0xAB4, settings.Edge.HighlightColor);
        Store(constructor, 0xAC4, settings.Edge.BackThickness);
        Store(constructor, 0xAC8, settings.Edge.BackDepthThreshold);
        Store(constructor, 0xACC, settings.Edge.BackRimScale);
        Store(constructor, 0xAD0, settings.Edge.BackRimSharpness);
        Store(constructor, 0xAD4, settings.IndexedRamp.RimDirectionType);
        LightModifierEncoding.WriteHatchingShadow(constructor, settings.HatchingShadow);
        Store(constructor, 0xB0C, settings.Outline.IntensityMin);
        Store(constructor, 0xB10, settings.Outline.IntensityMax);
        Store(constructor, 0xB14, settings.Outline.ZDecayRatio);
        Store(constructor, 0xB18, settings.Outline.ThicknessConstantRatio);
        Store(constructor, 0xB1C, settings.Outline.LightEffectFade);
        Store(constructor, 0xB20, settings.Outline.AmbientLightEffectScaler);
        Store(constructor, 0xB2C, settings.Outline.LPVEffectScaler);
        Store(constructor, 0xB38, settings.Outline.ScreenSpaceTextureScaler);
        Store(constructor, 0xB3C, settings.Outline.ColorScaler);
        Store(constructor, 0xB48, settings.Outline.ColorSaturationFade);
        Store(constructor, 0xB4C, settings.Outline.ThicknessScaler);
        Store(constructor, 0xB50, settings.Outline.ThicknessProjScaleMax);
        Store(constructor, 0xB54, settings.Outline.PatternMapFade);
        Store(constructor, 0xB58, settings.Outline.ZScale);
        Store(constructor, 0xB5C, settings.Outline.ThicknessNear);
        Store(constructor, 0xB60, settings.Outline.ThicknessFar);
        Store(constructor, 0xB64, settings.Outline.FadeMinThickness);
        Store(constructor, 0xB68, settings.Outline.CalcMode);
        Store(constructor, 0xB69, settings.Outline.ColorMode);
        Store(constructor, 0xB6A, settings.Outline.AlphaTest);
        Store(constructor, 0xB6B, settings.Outline.Normalization);
        Store(constructor, 0xB6C, settings.Outline.Fog);
        Store(constructor, 0xB6D, settings.Outline.AutoHDRScaleExposure);
        Store(constructor, 0xB6E, settings.Outline.CameraReverse);
        Store(constructor, 0xB70, settings.SurfaceEffects.NatureSnowFade);
        Store(constructor, 0xB74, settings.SurfaceEffects.NatureRippleFade);
        Store(constructor, 0xB78, settings.SurfaceEffects.CausticsFade);
        Store(constructor, 0xB7C, settings.SurfaceEffects.ShadowMaskFade);
        Store(constructor, 0xB80, settings.SurfaceEffects.FurZWriteAlphaThreshold);
        Store(constructor, 0xB84, settings.Weathering.AlbedoFade);
        Store(constructor, 0xB88, settings.Weathering.ReflectanceFade);
        Store(constructor, 0xB8C, settings.Weathering.GlossinessFade);
        Store(constructor, 0xB90, settings.Weathering.WetDebug);
        Store(constructor, 0xB94, settings.Weathering.WetDebugType);
        Store(constructor, 0xB98, settings.Weathering.WetScaler);
        Store(constructor, 0xCBC, settings.Weathering.TextureBlendIdDebug);
        Store(constructor, 0xCC0, settings.Weathering.TextureBlendFadeDebug);
        LightModifierEncoding.WriteAlbedoRetouch(constructor, settings.AlbedoRetouch);
        Store(constructor, 0xDE8, settings.EnvironmentMapScale);
        Store(constructor, 0xDF4, settings.ShadowOutline.Width);
        Store(constructor, 0xDF8, settings.ShadowOutline.Threshold);
        Store(constructor, 0xDFC, settings.ShadowOutline.Fade);
        Store(constructor, 0xE00, settings.ShadowOutline.SamplingType);
        Store(constructor, 0xE04, settings.Illustration.AmbientDiffuseUpScale);
        Store(constructor, 0xE08, settings.Illustration.AmbientLightDownScale);
        Store(constructor, 0xE0C, settings.Illustration.FogColorRGB);
        Store(constructor, 0xE18, settings.Illustration.FogUpScale);
        Store(constructor, 0xEF8, settings.Eye.EyeTwinkleGlobalScale);
        Store(constructor, 0xEFC, settings.Eye.EyeTwinkleGlobalExponentScale);
        Store(constructor, 0xF00, settings.Eye.EyeTwinkleGlobalShadowFade);
        Store(constructor, 0xF04, settings.Hemisphere.HeightSkyColor);
        Store(constructor, 0xF10, settings.Hemisphere.HeightGroundColor);
        Store(constructor, 0xF1C, settings.Shadow.CasterPlane);
        Store(constructor, 0xF1D, settings.Shadow.ReceiveShadowType);
        Store(constructor, 0xF1E, settings.Shadow.ReceiveShadowmapFilter);
        Store(constructor, 0xF1F, settings.Shadow.CasterPlaneForLocalLight);
        Store(constructor, 0xF20, settings.Shadow.CasterPlaneForReflectiveShadowMap);
        Store(constructor, 0xF24, settings.Shadow.ShadowReceiverSlopeScaledBias);
        Store(constructor, 0xF28, settings.Shadow.ShadowReceiverSlopeScale);
        Store(constructor, 0xF30, settings.Rain.EraseStart);
        Store(constructor, 0xF34, settings.Rain.EraseEnd);
        Store(constructor, 0xF38, settings.Rain.SlopeLimit);
        Store(constructor, 0xF3C, settings.Rain.Scale);
        Store(constructor, 0xF40, settings.Rain.FlowSpeedX);
        Store(constructor, 0xF44, settings.Rain.FlowSpeedZ);
        Store(constructor, 0xF48, settings.Rain.BlendSpeed);
        Store(constructor, 0xF4C, settings.Rain.SpecularScale);
        Store(constructor, 0xF58, settings.Rain.MaskMapScaleX);
        Store(constructor, 0xF5C, settings.Rain.MaskMapScaleZ);
        Store(constructor, 0xF60, settings.Rain.MaskMapOffsetX);
        Store(constructor, 0xF64, settings.Rain.MaskMapOffsetZ);
        Store(constructor, 0xFC0, settings.Celestial.MonthDay);
        Store(constructor, 0xFC4, settings.Celestial.DateTime);
        Store(constructor, 0xFC8, settings.Celestial.Longitude);
        Store(constructor, 0xFCC, settings.Celestial.Latitude);
        Store(constructor, 0xFD0, settings.Celestial.Altitude);
        Store(constructor, 0xFD4, settings.Celestial.NorthDir);
        Store(constructor, 0xFD8, settings.Celestial.MoonAge);
        Store(constructor, 0xFDC, settings.Celestial.CelestialOption);
        Store(constructor, 0x1090, settings.IndirectLighting.LocalSpaceSubLightColor2);
        Store(constructor, 0x10A0, settings.IndirectLighting.LocalSpaceSubLightColor3);
        Store(constructor, 0x10B0, settings.Extensions);
        foreach (int at in new[] {0x9C,0x334,0x338,0xE1C,0xE28,0x109C,0x10AC})
            PutFloat(constructor,at,1);
        PutFloat(constructor,0xE20,5000);PutFloat(constructor,0xE24,.5f);
        for (int i=0;i<4;i++)
        {
            Vector4 q=DirectionalLightSource.NormalizeQuaternion(settings.Lights[i].WorldQuaternion);
            PutVector(constructor,0xA0+i*16,q);
            PutVector(constructor,i*32,DirectionalLightSource.RotatePreparedQuaternion(q));
        }
        Vector4 hemi=DirectionalLightSource.NormalizeQuaternion(settings.Hemisphere.WorldQuaternion);
        PutVector(constructor,0xF0,hemi);
        PrepareHemisphere(constructor,hemi);
        bool swap = (settings.Celestial.CelestialOption&2)!=0;
        int first=swap?32:0, second=swap?0:32;
        constructor.AsSpan(first,16).CopyTo(constructor.AsSpan(0xF80,16));
        constructor.AsSpan(second,16).CopyTo(constructor.AsSpan(0xF90,16));
        constructor.AsSpan(first+16,16).CopyTo(constructor.AsSpan(0xFA0,16));
        constructor.AsSpan(second+16,16).CopyTo(constructor.AsSpan(0xFB0,16));
        uint[] declared=DeclaredKeys(settings);
        uint?[] resolved=new uint?[declared.Length];
        ResolveResources(settings,declared,input.ResourceBindings,false,constructor,resolved);
        byte[] cpu=new byte[0x48];
        PutWord(cpu,0x30, (settings.CurrentFieldWriterFlags&2)!=0 ? 0x70000000u : 0u);
        PutWord(cpu,0x34,settings.Capacity.MaximumPrimitives);PutWord(cpu,0x38,settings.Capacity.GroupMask);
        PutWord(cpu,0x3C,settings.Capacity.MaximumShadowCasters);PutWord(cpu,0x40,settings.Capacity.MaximumSubShadowCasters);
        PutWord(cpu,0x44,settings.Capacity.MaximumReflectiveShadowCasters);
        byte[] prepared=constructor.ToArray();PrepareNullCamera(prepared,input.ContextFlags3E4);
        return new(constructor,prepared,cpu,mask,declared,resolved);
    }

    private static uint[] DeclaredKeys(DirectionalLightSettings settings) =>
    [
        settings.EnvironmentMaps.First, settings.EnvironmentMaps.Second,
        settings.Resources.AmbientDiffuseMap,
        settings.Resources.IndexedRamp,
        settings.Resources.HairHighlight,
        settings.Resources.IndexedRampAdditionalMask,
        settings.Resources.OutlineColor,
        settings.Resources.DirtArray,
        settings.Resources.AlbedoRetouchEnvironmentMap,
        settings.Resources.IllustrationGlobalMask,
        settings.Resources.WeatheringArray,
        settings.AdditionalWeatheringTextures[0].Array,
        settings.AdditionalWeatheringTextures[1].Array,
        settings.AdditionalWeatheringTextures[2].Array,
        settings.Resources.WeatheringSecondaryArray,
        settings.AdditionalWeatheringTextures[0].SecondaryArray,
        settings.AdditionalWeatheringTextures[1].SecondaryArray,
        settings.AdditionalWeatheringTextures[2].SecondaryArray,
        settings.Resources.EyeTwinkle,
        settings.Resources.DarkMapEmission,
        settings.Resources.AlbedoRetouch2GlobalMask,
        settings.Resources.LightProbe
    ];
    private static IReadOnlyList<uint> ResolveResources(DirectionalLightSettings settings,uint[] declared,
        IReadOnlyList<DirectionalLightResourceBinding> bindings,bool enumerate,byte[]? light,uint?[]? resolvedKeys=null)
    {
        var requests=new HashSet<uint>();
        ulong Resolve(uint key,uint requiredClass,out uint? resolvedKey)
        {
            resolvedKey=null;
            if(key==0)return 0;
            requests.Add(key);
            var b=bindings.SingleOrDefault(b=>b.SourceObjectKey==key);
            if(b is null)
            {
                if(enumerate)return 0;
                throw new InvalidDataException($"Directional resource {key:X8} needs an explicit lookup result.");
            }
            if(!b.CpuDataReady||(requiredClass!=0&&b.InterfaceClassId!=requiredClass))return 0;
            resolvedKey=b.SourceObjectKey;
            return b.DataHandle;
        }
        ulong providerData=Resolve(declared[21],0x970DB365,out var providerKey);
        if(resolvedKeys is not null)resolvedKeys[21]=providerKey;
        for(int i=0;i<21;i++)
        {
            ulong handle=Resolve(declared[i],0xB672FAA2,out var resolvedKey);
            if(i==1&&handle==0)handle=Resolve(0x80F9DB7B,0xB672FAA2,out resolvedKey);
            if(resolvedKeys is not null)resolvedKeys[i]=resolvedKey;
            if(light is not null)PutHandle(light,0xFE0+i*8,handle);
        }
        if(light is not null)PutHandle(light,0x1088,providerData);
        for(int i=0;i<3;i++)
        {
            ulong handle=Resolve(settings.RainMaps[i],i==1?0x3059B9C3u:0xB672FAA2u,out _);
            if(light is not null)PutHandle(light,0xF68+i*8,handle);
        }
        Resolve(settings.Resources.Fog,0,out _);Resolve(settings.Resources.Provider,0,out _);
        if(!enumerate&&!requests.SetEquals(bindings.Select(b=>b.SourceObjectKey)))
            throw new InvalidDataException("Directional bindings include an unused source key.");
        return Array.AsReadOnly(requests.Order().ToArray());
    }

    private static void PrepareNullCamera(byte[] light,uint contextFlags3E4)
    {
        for(int i=0;i<4;i++)
            PutVector(light,i*32,DirectionalLightSource.RotatePreparedQuaternion(ReadVector(light,0xA0+i*16)));
        PrepareHemisphere(light,ReadVector(light,0xF0));
        LightModifierClassification.Apply(light,contextFlags3E4);
    }
    private static void PrepareHemisphere(byte[] light,Vector4 quaternion)
    {
        PutVector(light,0x120,DirectionalLightSource.RotatePreparedQuaternion(quaternion,true));
        for(int component=0;component<4;component++)
        {
            float upper=ReadFloat(light,0x100+component*4),lower=ReadFloat(light,0x110+component*4);
            PutFloat(light,0x130+component*4,CMul(CSub(upper,lower),.5f));
            PutFloat(light,0x140+component*4,CMul(CAdd(lower,upper),.5f));
        }
    }
    private static Vector4 ReadVector(byte[] bytes,int offset)=>new(ReadFloat(bytes,offset),ReadFloat(bytes,offset+4),ReadFloat(bytes,offset+8),ReadFloat(bytes,offset+12));
    private static float ReadFloat(byte[] bytes,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset,4));
    private static uint ReadWord(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset,4));
    private static void PutFloat(byte[] bytes,int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset,4),value);
    private static void PutWord(byte[] bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),value);
    private static void PutHandle(byte[] bytes,int offset,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset,8),value);
    private static void PutVector(byte[] bytes,int offset,Vector4 value)
    {PutFloat(bytes,offset,value.X);PutFloat(bytes,offset+4,value.Y);PutFloat(bytes,offset+8,value.Z);PutFloat(bytes,offset+12,value.W);}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CMul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CAdd(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CSub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CDiv(float a,float b)=>a/b;
    private static void Store(byte[] bytes, int offset, byte value) => bytes[offset] = value;
    private static void Store(byte[] bytes, int offset, uint value) => PutWord(bytes, offset, value);
    private static void Store(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Store(byte[] bytes, int offset, float value) => PutFloat(bytes, offset, value);
    private static void Store(byte[] bytes, int offset, Vector3 value)
    { PutFloat(bytes, offset, value.X); PutFloat(bytes, offset + 4, value.Y); PutFloat(bytes, offset + 8, value.Z); }
    private static void Store(byte[] bytes, int offset, Vector4 value) => PutVector(bytes, offset, value);
    private static void Store(byte[] bytes, int offset, ReadOnlyMemory<byte> value) => value.Span.CopyTo(bytes.AsSpan(offset));
}
