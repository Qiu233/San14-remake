using Godot;
using San14.Core;
using San14.Data;

internal sealed partial class SceneAudioBinding
{
    private const string PersonnelEffectChannel = "personnel-presentation-effect";
    private CancellationTokenSource? _personnelEffect;

    internal void PlayPersonnelPresentationAudio(TalentCommandPresentationRequest? talent,
        DiplomacyArrivalAudioRequest? arrival, NarrationVoiceLanguage language)
    {
        try
        {
            if (talent?.Voice is { } voice)
            {
                PlayPersonnelNarrationVoice(voice);
                return;
            }
            var source = PersonnelPresentationAudioSource.Select(talent, arrival, language);
            CancellationTokenSource lifetime;
            string channel;
            if (source.IsVoice)
            {
                StopNarrationVoice();
                _narrationVoice = lifetime = new CancellationTokenSource();
                channel = NarrationVoiceChannel;
            }
            else
            {
                StopPersonnelEffect();
                _personnelEffect = lifetime = new CancellationTokenSource();
                channel = PersonnelEffectChannel;
            }
            //An outbox receipt does not end playback. Only replacement/scene
            //retirement cancels this lifetime, matching the nonblocking source.
            _ = PlayPersonnelPresentationAudioAsync(source, channel, lifetime);
        }
        catch (Exception error) { GD.PushWarning("Personnel audio: " + error.Message); }
    }

    internal void PlayPersonnelNarrationVoice(TalentNarrationVoiceRequest voice)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(voice);
            //The source-time message branch already selected this sound.
            //Its persisted language is independent of the current UI locale.
            var selectedLanguage = voice.Language switch
            {
                PersonnelVoiceLanguage.Japanese => NarrationVoiceLanguage.Japanese,
                PersonnelVoiceLanguage.Chinese => NarrationVoiceLanguage.Chinese,
                _ => throw new InvalidDataException("Invalid personnel voice language.")
            };
            PlayNarrationVoice(new(voice.SpeakerId, voice.VoiceType, voice.VoiceId,
                selectedLanguage, voice.VariantIndex, voice.SoundId));
        }
        catch (Exception error) { GD.PushWarning("Personnel voice: " + error.Message); }
    }

    private async Task PlayPersonnelPresentationAudioAsync(PersonnelPresentationAudioSource source,
        string channel, CancellationTokenSource lifetime)
    {
        try { await _session.PlayPersonnelPresentationAudioAsync(channel, source, lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (ReferenceEquals(source.IsVoice ? _narrationVoice : _personnelEffect, lifetime))
                GD.PushWarning("Personnel audio: " + error.Message);
        }
    }

    internal void StopPersonnelPresentationAudio()
    {
        StopNarrationVoice();
        StopPersonnelEffect();
    }

    private void StopPersonnelEffect()
    {
        _personnelEffect?.Cancel(); _personnelEffect?.Dispose(); _personnelEffect = null;
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree())
            _session.StopChannel(PersonnelEffectChannel);
    }
}
