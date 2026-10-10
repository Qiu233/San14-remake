using San14.Data.Formats;

namespace San14.Data;

internal static class HdrShaderSelectionRules
{
    internal static HdrVariantKey PrepareVariant(HdrVariantKey initial,
        IReadOnlyDictionary<HdrVariantField, uint> capabilities,
        HdrShaderPlugins plugins, HdrSelectionSettings settings)
    {
        HdrVariantKey key = initial;
        void Set(HdrVariantField field, uint requested)
        {
            if ((capabilities[field] & (1u << (int)(requested & 31))) == 0) return;
            key = field switch
            {
                HdrVariantField.SpecularOcclusion=>key with {SpecularOcclusion=requested},
                HdrVariantField.SharedTargets=>key with {SharedTargets=requested},
                HdrVariantField.FirstLighting=>key with {FirstLighting=requested},
                HdrVariantField.SecondLighting=>key with {SecondLighting=requested},
                HdrVariantField.SharedAuxiliary=>key with {SharedAuxiliary=requested},
                HdrVariantField.LightPair=>key with {LightPair=requested},
                HdrVariantField.LightPairAuxiliary=>key with {LightPairAuxiliary=requested},
                HdrVariantField.CombinedLighting=>key with {CombinedLighting=requested},
                HdrVariantField.LocalLightQueriesEmpty=>key with {LocalLightQueriesEmpty=requested},
                HdrVariantField.VolumetricFog=>key with {VolumetricFog=requested},
                HdrVariantField.RefractionResources=>key with {RefractionResources=requested},
                HdrVariantField.Reflection=>key with {Reflection=(HdrReflectionMode)requested},
                HdrVariantField.RefractionSource=>key with {RefractionSource=requested},
                HdrVariantField.SharedDistance=>key with {SharedDistance=requested},
                _=>throw new ArgumentOutOfRangeException(nameof(field))
            };
        }
        static uint Value(bool value) => value ? 1u : 0u;
        var context = settings.Context;
        if (!plugins.Ocean && plugins.Refraction && !plugins.ThinSpecularSource)
            Set(HdrVariantField.RefractionSource,Value(context.RefractionSource));
        bool specular = !plugins.Ocean && (settings.ColorMode == HdrColorMode.CameraSpecular
            ? settings.CameraSpecularOcclusion == 1 : settings.CameraSpecularOcclusion != 0);
        Set(HdrVariantField.SpecularOcclusion,Value(specular));
        Set(HdrVariantField.SharedTargets,Value(plugins.Ocean ? !settings.PreRendererPresent : context.SharedTargets));
        if (!plugins.Ocean)
        {
            Set(HdrVariantField.SharedAuxiliary,Value(context.SharedTargets));
            Set(HdrVariantField.SharedDistance,Value(context.SharedTargets));
        }
        Set(HdrVariantField.LightPair,Value(context.LightPair));
        Set(HdrVariantField.LightPairAuxiliary,Value(context.LightPair));
        Set(HdrVariantField.CombinedLighting,context.CombinedLighting ? 3u : 0u);
        Set(HdrVariantField.FirstLighting,Value(context.FirstLighting));
        Set(HdrVariantField.SecondLighting,Value(context.SecondLighting));
        Set(HdrVariantField.LocalLightQueriesEmpty,Value(settings.LocalLightQueriesEmpty));
        if (plugins.VolumetricFog && context.VolumetricFog) Set(HdrVariantField.VolumetricFog,1);
        bool extended = context.ExtendedReflection && settings.ColorMode != HdrColorMode.CameraSpecular;
        Set(HdrVariantField.Reflection,(uint)(extended ? HdrReflectionMode.Extended :
            settings.ReflectionAvailable ? HdrReflectionMode.Available : HdrReflectionMode.Unavailable));
        if (!plugins.Ocean && plugins.Refraction)
            Set(HdrVariantField.RefractionResources,Value(!settings.RefractionResourcesAvailable));
        return key;
    }

    internal static G1ShaderFeatureSelection SelectFeature(HdrFeatureKey current,
        IReadOnlyList<HdrFeatureKey> features)
    {
        int bestIndex=-1,bestPenalty=int.MaxValue,bestExcess=int.MaxValue,visited=0;
        bool zero=false; G1ShaderFeatureScore? selected=null;
        for(int i=0;i<features.Count;i++)
        {
            var score=Score(current,features[i]); visited++;
            if(score.Penalty==0)
            {
                zero=true;
                if(score.Excess<bestExcess) {bestExcess=score.Excess;bestIndex=i;selected=score;}
                if(score.Excess==0) break;
                bestPenalty=0;
            }
            else if(score.Penalty<bestPenalty) {bestPenalty=score.Penalty;bestIndex=i;selected=score;}
        }
        return new(bestIndex,zero,selected,visited);
    }

    private static G1ShaderFeatureScore Score(HdrFeatureKey c, HdrFeatureKey r)
    {
        unchecked
        {
            uint request=c.Flags&0xF77FFu,missing=request-(request&r.Flags);
            int penalty=(int)(((missing&~0x80u)<<16)+((missing>>7)&1));
            int excess=(r.Flags&~request)!=0 ? 1 : 0;
            ReadOnlySpan<uint> counts=[c.DirectionalLights,c.PointLights,c.SpotLights,c.HemisphereLights];
            ReadOnlySpan<uint> available=[r.DirectionalLights,r.PointLights,r.SpotLights,r.HemisphereLights];
            for(int i=0;i<4;i++)
                if(available[i]<counts[i]) penalty+=(int)((counts[i]-available[i])<<8);
                else if((r.Flags&(1u<<i))!=0) excess+=(int)(available[i]-counts[i]);
                else penalty+=(int)((available[i]-counts[i])<<4);
            if(c.FogMode==0) excess+=Difference(r.FogMode,0);
            else
            {
                if(r.FogMode!=5 || c.FogMode is 4 or 7) penalty+=Difference(r.FogMode,c.FogMode);
                penalty+=Difference(r.FogQuality,c.FogQuality);
            }
            penalty+=Difference(c.DirectionalMode,r.DirectionalMode)<<12;
            if((r.Flags&0x100)==0) penalty+=Difference(c.ShadowMode,r.ShadowMode);
            else if((int)r.ShadowMode>=7 && (int)c.ShadowMode>=7 && r.ShadowMode!=c.ShadowMode)
                penalty+=Difference(c.ShadowMode,r.ShadowMode);
            else if((int)r.ShadowMode<(int)c.ShadowMode || ((c.ShadowMode&4)!=0 && (int)r.ShadowMode>7))
                penalty+=(int)(c.ShadowMode-r.ShadowMode);
            else excess+=Difference(c.ShadowMode,r.ShadowMode);
            if(c.ShadowMode!=0)
            {
                penalty+=Difference(c.ShadowQuality,r.ShadowQuality)<<12;
                if(r.LocalShadowMode!=5 || c.LocalShadowMode==2)
                    penalty+=Difference(c.LocalShadowMode,r.LocalShadowMode)<<8;
            }
            return new(penalty,excess);
        }
    }
    private static int Difference(uint left,uint right)
    {
        int value=unchecked((int)(left-right)),sign=value>>31;
        return unchecked((value^sign)-sign);
    }
}
