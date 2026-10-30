using San14.Data;

public partial class AudioPlaybackService
{
    internal void ChangeSelectedStorySoundVolumes(float percent)
    {
        RequireEngineThread();
        float gain=Math.Max(0,percent/100f);
        foreach(var effect in _effects)
        {
            if(effect is null||!QueryEffectHandle(effect.Slot,includeReservation:false).Active||
                !CampaignStorySoundControlSource.VolumeEffectIds.Contains(effect.ManagerId))continue;
            //Replace this actual handle's control gain. Repeated commands do
            //not compound it or change the category of later sound requests.
            effect.ScriptVolume=gain;
            RefreshEffectVolume(effect);
        }
    }

    internal void ControlMatchingStorySounds(int managerId,float? fadeMilliseconds)
    {
        RequireEngineThread();
        foreach(var effect in _effects)
        {
            if(effect is null||effect.ManagerId!=managerId||
                !QueryEffectHandle(effect.Slot,includeReservation:false).Active)continue;
            if(fadeMilliseconds is float milliseconds)
                BeginEffectFade(effect,0,milliseconds,stop:true);
            else ReleaseEffect(effect);
        }
    }

    private void BeginEffectFade(EffectVoice effect,float target,float milliseconds,bool stop,bool fromZero=false)
    {
        // Explicit positive-duration mode2 uses the linear source curve. Zero
        // and negative durations take the original immediate-stop branch.
        if (stop) ReleaseEffect(effect, Math.Max(0, milliseconds / 1000f));
        else BeginSourceEnvelope(effect, target, Math.Max(0, milliseconds / 1000f),
            AudioCueEnvelopeCurve.Linear, stop: false, fromZero);
    }

    private void RefreshEffectVolume(EffectVoice effect)
    {
        if(effect.Spatial)
        {
            if(_spatialVoices.TryGetValue(effect.Channel,out var spatial))ApplySpatialVolume(spatial);
        }
        else if(_players.TryGetValue(effect.Channel,out var voice)&&ReferenceEquals(voice.Effect,effect))ApplyVolume(voice);
    }
}
