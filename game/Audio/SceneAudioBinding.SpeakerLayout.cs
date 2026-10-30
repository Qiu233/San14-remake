using San14.Data;
using San14.Core;

internal sealed partial class SceneAudioBinding
{
    internal GameAudioSpeakerLayout CurrentSpeakerLayout => _musicSession.CurrentSpeakerLayout;
    internal SpeakerLayoutAvailability QuerySpeakerLayoutAvailability(GameAudioSpeakerLayout layout) =>
        _musicSession.QuerySpeakerLayoutAvailability(layout);

    internal async Task ApplySpeakerLayoutAsync(GameAudioSpeakerLayout layout, string locale,
        MapMusicPreferences preferences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_playableSession is not null) throw new InvalidOperationException("audio.output.layout_change_requires_title");
        if (CurrentSpeakerLayout == layout) return;
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        await _musicSession.ApplySpeakerLayoutAsync(layout, cancellationToken);
        //60C8F0 restores the source mode1 selection with position flag0 and
        //1000ms attack after reconfiguration. This is not an old PCM cursor.
        await PlayMapMusicIntentAsync(new(0, 1, false, 1000, 0), locale, preferences, cancellationToken);
    }
}
