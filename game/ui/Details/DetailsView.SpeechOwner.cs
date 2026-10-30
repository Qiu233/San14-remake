using San14.Core;

public abstract partial class DetailsView
{
    private Func<bool>? _retainedSpeechHostCurrent;
    private Action<GameState>? _retainedSpeechWorld;

    // An independent details root owns its actual descendants. This is local
    // to that retained UI tree; ordinary pages keep the command host lifetime.
    internal void BindRetainedSpeechHost(Func<bool> current)
    {
        _retainedSpeechHostCurrent = current;
        TreeExiting += () =>
        {
            _retainedSpeechHostCurrent = null;
            _retainedSpeechWorld = null;
        };
    }

    internal bool IsRetainedSpeechHostCurrent => _retainedSpeechHostCurrent is { } current &&
        IsInsideTree() && !IsQueuedForDeletion() && current();

    internal bool TryRetainIndependentSpeechWorld(Action<GameState> adopt)
    {
        for (Godot.Node? node = this; node is not null; node = node.GetParent())
        {
            if (node is not DetailsView host || host._retainedSpeechHostCurrent is null) continue;
            host._retainedSpeechWorld += adopt;
            TreeExiting += () => host._retainedSpeechWorld -= adopt;
            return true;
        }
        return false;
    }

    internal void AdoptRetainedSpeechWorld(GameState replacement) => _retainedSpeechWorld?.Invoke(replacement);
}
