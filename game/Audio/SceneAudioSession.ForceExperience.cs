using San14.Data;

public partial class SceneAudioSession
{
    internal async Task<SceneAudioChannel> PlayForceExperienceGainSoundAsync(string channel,
        CancellationToken cancellationToken)
    {
        RequireEngineThread();
        if (_repository is null || _sceneCancellation is null)
            throw new InvalidOperationException("Force experience sound needs an active audio scene.");
        long revision = SceneRevision;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        var request = await Task.Run(() =>
        {
            lock (_repository)
            {
                linked.Token.ThrowIfCancellationRequested();
                return ForceExperienceGainSoundSource.Resolve(AudioSelectionResources.Load(
                    _repository, ForceExperienceGainSoundSource.SelectionBankId));
            }
        }, linked.Token);
        linked.Token.ThrowIfCancellationRequested();
        if (SceneRevision != revision) throw new OperationCanceledException("Force experience audio scene has retired.");
        return await ChangeSelectionAsync(channel, request, null, true, 0, "Master", linked.Token,
            AudioVolumeGroup.Effects, staticNonSpatialEffect: true);
    }
}
