using Godot;
using San14.Core;
using San14.Data;
using System.Text.Json;

public partial class Main
{
    private OriginalSceneAudioSession? _sceneAudioSession;
    private Label _sceneAudioStatus = null!;
    private readonly Dictionary<string, OriginalSceneAudioChannel> _sceneAudioChannels = new(StringComparer.Ordinal);

    private void AttachSceneAudioSession(OriginalAudioPlaybackService playback)
    {
        if (_repository is null || _sceneAudioSession is not null) return;
        var session = new OriginalSceneAudioSession { Name = "OriginalSceneAudioSession" };
        AddChild(session);
        session.Configure(playback,
            new OriginalGameRepository(_repository.SourceDirectory, _overrides.DirectoryPath),
            new OriginalAudioRepository(_repository.SourceDirectory, _overrides.DirectoryPath));
        session.ChannelChanged += SceneAudioChannelChanged;
        _sceneAudioSession = session;
        RefreshSceneAudioContext();
    }

    private void RefreshSceneAudioContext()
    {
        if (_sceneAudioSession is { } session)
        {
            string scenario = _world?.ScenarioKey ?? "source-catalogue";
            string key = _sceneView is not null ? "source-scene-preview:" + scenario :
                _playerForceId is int force ? $"campaign:{scenario}:force:{force}" : "scenario-selection:" + scenario;
            if (session.SceneKey != key) session.BeginScene(key);
            SetMeta("scene_audio_scene_key", session.SceneKey ?? "");
            SetMeta("scene_audio_scene_revision", session.SceneRevision);
            SetMeta("scene_audio_active_channels", _sceneAudioChannels.Count);
            // A complete selector query and manager bindings are still caller inputs.
            // Scene attachment never chooses a cue or creates a playing channel.
            SetMeta("scene_audio_selection_resolved", false);
        }
        UpdateSceneAudioStatus();
    }

    private void LeaveSceneAudioContext()
    {
        if (_sceneAudioSession is { } session && GodotObject.IsInstanceValid(session) && session.IsInsideTree())
        {
            session.LeaveScene();
            SetMeta("scene_audio_scene_revision", session.SceneRevision);
        }
        _sceneAudioChannels.Clear();
        SetMeta("scene_audio_scene_key", ""); SetMeta("scene_audio_active_channels", 0);
    }

    private void SceneAudioChannelChanged(OriginalSceneAudioChannel channel)
    {
        if (channel.Status == "stopped") _sceneAudioChannels.Remove(channel.Channel);
        else _sceneAudioChannels[channel.Channel] = channel;
        SetMeta("scene_audio_channel_snapshot_json", JsonSerializer.Serialize(channel));
        SetMeta("scene_audio_active_channels", _sceneAudioChannels.Count);
        UpdateSceneAudioStatus();
    }

    private void UpdateSceneAudioStatus()
    {
        if (_sceneAudioStatus is null || _failed || !GodotObject.IsInstanceValid(_sceneAudioStatus)) return;
        string status = _sceneAudioSession is not null ? "ready_unresolved" :
            _uiAudioAvailability == "unavailable" ? "unavailable" : "loading";
        _sceneAudioStatus.Text = _ui.Get("scene_audio_" + status);
        _sceneAudioStatus.TooltipText = _ui.Get("scene_audio_selection_unknown");
        SetMeta("scene_audio_status", status);
    }

    private string RenderMonthlyIncomeReceipt(GameState world, MonthlyIncomeReportEntry receipt)
    {
        string city = world.Cities[receipt.CityId].Name;
        MonthlyIncomeQuote quote = receipt.Quote;
        if (quote.Status == PeriodEconomyCalculationStatus.Available)
            return _ui.Format("play_report_monthly_receipt_available", city, quote.Gold, quote.Supplies);
        return _ui.Format(quote.Status == PeriodEconomyCalculationStatus.PartialCurrentContext
                ? "play_report_monthly_receipt_partial" : "play_report_monthly_receipt_unavailable",
            city, quote.MissingInputs.Length);
    }

    private string RenderEconomyRuleGap(GameState world, PeriodReportEntry entry)
    {
        string city = entry.CityId is int id && world.Cities.TryGetValue(id, out CityState? place)
            ? place.Name : _ui.Get("detail_unknown");
        return _ui.Format("play_report_" + entry.Kind, city) +
            (entry.MissingRuleKeys is { Length: > 0 } missing ? "  " + _ui.Format("play_report_missing_rule_count", missing.Length) : "");
    }

    private string RenderSourceEventReport(GameState world, OriginalCampaignEventReportView entry)
    {
        string identity = entry.EventId is int eventId ? _ui.Format("play_report_source_event_identity", eventId) : _ui.Get("play_report_event_identity");
        if (entry.Kind == "triggered") return _ui.Format("play_report_event_triggered", identity);
        if (entry.Kind == nameof(PlayableEventEffectKind.Message))
            return _ui.Format("play_report_event_message", identity);
        string? effect = entry.Kind switch
        {
            nameof(PlayableEventEffectKind.SetAgriculture) => "agriculture_set",
            nameof(PlayableEventEffectKind.AddAgriculture) => "agriculture_add",
            nameof(PlayableEventEffectKind.SetCommerce) => "commerce_set",
            nameof(PlayableEventEffectKind.AddCommerce) => "commerce_add",
            nameof(PlayableEventEffectKind.SetBarracks) => "barracks_set",
            nameof(PlayableEventEffectKind.AddBarracks) => "barracks_add",
            nameof(PlayableEventEffectKind.SetSecurity) => "security_set",
            nameof(PlayableEventEffectKind.AddSecurity) => "security_add",
            _ => null
        };
        if (effect is not null)
        {
            string area = entry.AreaId is int id && world.Areas.TryGetValue(id, out AreaState? place) && !string.IsNullOrWhiteSpace(place.Name)
                ? place.Name : _ui.Get("detail_unknown");
            return _ui.Format("play_report_event_area_effect", identity, area, _ui.Get("play_report_event_effect_" + effect), entry.Amount);
        }
        if (entry.Kind == nameof(PlayableEventEffectKind.SetFlag)) return _ui.Format("play_report_event_flag", identity);
        if (entry.Kind == nameof(PlayableEventEffectKind.AddCounter)) return _ui.Format("play_report_event_counter", identity);
        return _ui.Format("play_report_event_effect_unknown", identity);
    }

    private void PreserveSourceReportMetadata(GameState world, int playerForce)
    {
        MonthlyIncomeReportEntry[] income = (world.MonthlyIncomeReports ?? []).Where(entry => entry.ForceId == playerForce).ToArray();
        OriginalCampaignEventReportView[] events = OriginalCampaignEventReportAdapter.Create(world, playerForce).Where(entry => entry.Period == world.PeriodNumber).ToArray();
        SetMeta("play_report_monthly_receipt_count", income.Length);
        SetMeta("play_report_monthly_receipts_json", JsonSerializer.Serialize(income));
        SetMeta("play_report_source_event_count", events.Length);
        SetMeta("play_report_source_events_json", JsonSerializer.Serialize(events));
        SetMeta("play_report_rule_receipts_json", JsonSerializer.Serialize(world.PeriodReports.Where(entry =>
            entry.ForceId == playerForce && (entry.RuleId is not null || entry.MissingRuleKeys is not null))));
        SetMeta("play_report_period_entries_json", JsonSerializer.Serialize(world.PeriodReports.Where(entry => entry.ForceId == playerForce)));
    }
}
