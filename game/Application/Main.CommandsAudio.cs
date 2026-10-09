using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private MilitaryOrdersPanel _militaryOrders = null!;
    private TacticOrdersPanel _tacticOrders = null!;
    private DiplomacyOrdersPanel _diplomacyOrders = null!;
    private PersonnelOrdersPanel _personnelOrders = null!;
    private Label _uiAudioStatus = null!;
    private AudioPlaybackService? _uiAudioService;
    private UiAudioClips? _uiAudioClips;
    private readonly CancellationTokenSource _uiAudioLifetime = new();
    private readonly HashSet<ulong> _uiAudioButtons = [];
    private bool _uiAudioHooked;
    private string _uiAudioAvailability = "loading";
    private string? _uiAudioReason;
    private int _uiAudioSelectCount, _uiAudioCancelCount;

    private void BuildExtendedCommandPanels()
    {
        _militaryOrders = new MilitaryOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Military).AddChild(_militaryOrders);
        _tacticOrders = new TacticOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Military).AddChild(_tacticOrders);
        _diplomacyOrders = new DiplomacyOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Diplomacy).AddChild(_diplomacyOrders);
        _personnelOrders = new PersonnelOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Personnel).AddChild(_personnelOrders);
    }

    private void ConfigureExtendedCommandLocale()
    {
        _militaryOrders.Configure(_ui.Get, command => RunPlayAction(() => ApplyPlayableOrder(command)));
        _tacticOrders.Configure(_ui.Get, _ui.TacticReason, command => RunPlayAction(() => ApplyPlayableOrder(command)));
        _diplomacyOrders.Configure(_ui.Get, command => RunPlayAction(() => ApplyPlayableOrder(command)));
        _personnelOrders.Configure(_ui.Get, command => RunPlayAction(() => ApplyPlayableOrder(command)));
        UpdateUiAudioStatus();
        UpdateSceneAudioStatus();
    }

    private void RefreshExtendedCommands()
    {
        int? unit = _playUnit.Selected >= 0 ? _playUnit.GetSelectedId() : null;
        int? city = _playCity.Selected >= 0 ? _playCity.GetSelectedId() : null;
        _militaryOrders.Refresh(_world, unit, _playerForceId, _selectedCell?.Id);
        _tacticOrders.Refresh(_world, unit, _playerForceId, _playRules?.TacticRules ?? UnresolvedTacticRules.Instance,
            _playRules?.Routes, _selectedCell?.Id);
        _diplomacyOrders.Refresh(_world, _playerForceId, city);
        _personnelOrders.Refresh(_world, _playerForceId);
        RefreshMilitaryDeploymentComposition();
        RefreshTransportOrders();
    }

    private void BeginUiAudio()
    {
        GetTree().NodeAdded += HookUiAudioNode;
        _uiAudioHooked = true;
        HookUiAudioSubtree(this);
        _ = PrepareUiAudioAsync();
    }

    private async Task PrepareUiAudioAsync()
    {
        try
        {
            if (_repository is null) return;
            if (!AudioDecoderRuntime.TryCreate(out var decoder, out string? reason))
            {
                _uiAudioReason = reason; _uiAudioAvailability = "unavailable";
                SetMeta("ui_audio_unavailable_reason", reason ?? "unknown");
                UpdateUiAudioStatus();
                GD.PushWarning(reason ?? "Original UI audio decoder is unavailable.");
                return;
            }
            var service = new AudioPlaybackService { Name = "OriginalUiAudioPlayback" };
            AddChild(service);
            _uiAudioService = service;
            service.Configure(new AudioRepository(_repository.SourceDirectory, _overrides.DirectoryPath), decoder!);
            AttachSceneAudioSession(service);
            // Own repository avoids concurrent mutation of the map/scenario cache.
            var audioSource = new GameResourceRepository(_repository.SourceDirectory, _overrides.DirectoryPath);
            UiAudioClips clips = await UiAudioClips.LoadAsync(service, audioSource, _uiAudioLifetime.Token);
            if (_uiAudioLifetime.IsCancellationRequested || !IsInsideTree() || _failed)
            {
                clips.Dispose(); return;
            }
            _uiAudioClips = clips;
            _uiAudioAvailability = "ready";
            SetMeta("ui_audio_source_uri", clips.SourceBankUri);
            SetMeta("ui_audio_source_sha256", clips.SourceBankSha256);
            SetMeta("ui_audio_select_frames", clips.ButtonSelect.Stream.Data.Length / Math.Max(1, clips.ButtonSelect.Stream.Stereo ? 4 : 2));
            SetMeta("ui_audio_cancel_frames", clips.Cancel.Stream.Data.Length / Math.Max(1, clips.Cancel.Stream.Stereo ? 4 : 2));
            UpdateUiAudioStatus();
            GD.Print($"ORIGINAL_UI_AUDIO_READY bank={clips.SourceBankUri} " +
                $"selectFrames={GetMeta("ui_audio_select_frames")} cancelFrames={GetMeta("ui_audio_cancel_frames")} preloadOnly=true");
        }
        catch (OperationCanceledException) when (_uiAudioLifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_uiAudioLifetime.IsCancellationRequested) return;
            _uiAudioAvailability = "unavailable"; _uiAudioReason = error.Message;
            SetMeta("ui_audio_unavailable_reason", error.Message);
            UpdateUiAudioStatus(); GD.PushWarning("Original UI audio unavailable: " + error.Message);
        }
    }

    private void UpdateUiAudioStatus()
    {
        if (_uiAudioStatus is null || _failed || !GodotObject.IsInstanceValid(_uiAudioStatus)) return;
        _uiAudioStatus.Text = _ui.Get("ui_audio_" + _uiAudioAvailability);
        _uiAudioStatus.TooltipText = _uiAudioReason ?? "";
        SetMeta("ui_audio_status", _uiAudioAvailability);
        UpdateSceneAudioStatus();
    }

    private void HookUiAudioSubtree(Node node)
    {
        HookUiAudioNode(node);
        foreach (Node child in node.GetChildren()) HookUiAudioSubtree(child);
    }

    private void HookUiAudioNode(Node node)
    {
        if (node is not BaseButton button || !IsAncestorOf(button) || !_uiAudioButtons.Add(button.GetInstanceId())) return;
        button.Pressed += () => PlayUiAudio(button.Name == "CancelPreparedDeployment" || button.Name == "DiplomacyCancelAlliance" || button.Name == "TacticCancel");
    }

    private void PlayUiAudio(bool cancel)
    {
        if (_uiAudioLifetime.IsCancellationRequested || _uiAudioClips is null || _uiAudioService is null) return;
        try
        {
            // Current client feedback policy: one UI voice, Master at 0 dB.
            // These are not asserted to be the original cue graph or mixer values.
            _uiAudioService.Play(cancel ? _uiAudioClips.Cancel : _uiAudioClips.ButtonSelect, "ui");
            if (cancel) SetMeta("ui_audio_cancel_count", ++_uiAudioCancelCount);
            else SetMeta("ui_audio_select_count", ++_uiAudioSelectCount);
        }
        catch (Exception error)
        {
            _uiAudioAvailability = "unavailable"; _uiAudioReason = error.Message;
            UpdateUiAudioStatus(); GD.PushWarning("Original UI sound playback unavailable: " + error.Message);
        }
    }

    public override void _ExitTree()
    {
        _uiAudioLifetime.Cancel();
        LeaveSceneAudioContext();
        if (_sceneAudioSession is { } sceneSession && GodotObject.IsInstanceValid(sceneSession))
            sceneSession.ChannelChanged -= SceneAudioChannelChanged;
        if (_uiAudioHooked) { GetTree().NodeAdded -= HookUiAudioNode; _uiAudioHooked = false; }
        if (_uiAudioService is { } service && GodotObject.IsInstanceValid(service) && service.IsInsideTree())
            service.Stop("ui");
        _uiAudioClips?.Dispose(); _uiAudioClips = null;
        _uiAudioButtons.Clear();
    }
}
