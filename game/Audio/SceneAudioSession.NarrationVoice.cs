using San14.Data;

public partial class SceneAudioSession
{
    internal async Task<SceneAudioChannel> PlayNarrationVoiceAsync(string channel,
        NarrationVoiceRequest request, CancellationToken cancellationToken)
    {
        RequireEngineThread();
        if (_repository is null || _sceneCancellation is null)
            throw new InvalidOperationException("Narration voice needs an active audio scene.");
        long revision = SceneRevision;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        var sample = await Task.Run(() =>
        {
            lock (_repository)
            {
                linked.Token.ThrowIfCancellationRequested();
                var binding = NarrationVoiceSelection.Bank(request.Language);
                return NarrationVoiceSelection.Resolve(request,
                    AudioSelectionResources.Load(_repository, binding.SelectionBankId));
            }
        }, linked.Token);
        linked.Token.ThrowIfCancellationRequested();
        if (SceneRevision != revision) throw new OperationCanceledException("Narration voice scene has retired.");
        return await PlaySampleAsync(channel, sample, cancellationToken: linked.Token,
            volumeGroup: AudioVolumeGroup.Voice);
    }
}
