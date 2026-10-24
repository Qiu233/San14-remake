using San14.Data;

public partial class SceneAudioSession
{
    internal async Task<SceneAudioChannel> PlayPersonnelPresentationAudioAsync(string channel,
        PersonnelPresentationAudioSource source, CancellationToken cancellationToken)
    {
        RequireEngineThread();
        if (_repository is null || _sceneCancellation is null)
            throw new InvalidOperationException("Personnel audio needs an active audio scene.");
        long revision = SceneRevision;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        var sample = await Task.Run(() =>
        {
            lock (_repository)
            {
                linked.Token.ThrowIfCancellationRequested();
                return source.Resolve(AudioSelectionResources.Load(_repository, source.Bank.SelectionBankId));
            }
        }, linked.Token);
        linked.Token.ThrowIfCancellationRequested();
        if (SceneRevision != revision) throw new OperationCanceledException("Personnel audio scene has retired.");
        return await PlaySampleAsync(channel, sample, cancellationToken: linked.Token,
            volumeGroup: source.IsVoice ? AudioVolumeGroup.Voice : AudioVolumeGroup.Effects);
    }
}
