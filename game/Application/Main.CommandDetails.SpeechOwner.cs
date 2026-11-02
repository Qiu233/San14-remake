using San14.Client.Gameplay;
using San14.Core;

public partial class Main
{
    private Action<GameState>? _commandDetailsAdoptSpeechWorld;

    // Only an actual held inspection World admits its input during execution.
    // The normal path preserves RunUiAction's ordinary stage admission.
    private void RunInspectionUiAction(GameState world, Action action)
        => RunUiAction(action, allowDuringPeriod: _advancingPeriod &&
            _session is { } session && IsInspectionWorldCurrent(session, world));

    // Independent inspectors supply their own exact retained-root admission.
    private bool IsInformationBrowserCurrent(MapInformationBrowserView browser) =>
        _commandDetailsView == browser || browser.IsRetainedSpeechHostCurrent;

    private void AdoptCommandDetailsSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_commandDetailsView is null || _commandDetailsAdoptSpeechWorld is not { } adopt ||
            _commandDetailsSession != session || _commandDetailsMap != _residentMap ||
            !ReferenceEquals(_commandDetailsWorld, previous)) return;
        _commandDetailsWorld = replacement;
        adopt(replacement);
    }

    // One retained details tree owns this callback chain. Hidden parents keep
    // their captured world, while an exiting child releases only its own link.
    // Pages without a current speech consumer keep their existing retirement.
    private void RetainCommandDetailSpeechWorld(DetailsView view, Action<GameState> adopt)
    {
        if (view.TryRetainIndependentSpeechWorld(adopt)) return;
        if (_commandDetailsView is null) return;
        var parent = _commandDetailsAdoptSpeechWorld;
        Action<GameState> retained = next => { parent?.Invoke(next); adopt(next); };
        _commandDetailsAdoptSpeechWorld = retained;
        view.TreeExiting += () =>
        {
            if (ReferenceEquals(_commandDetailsAdoptSpeechWorld, retained))
                _commandDetailsAdoptSpeechWorld = parent;
        };
    }
}
