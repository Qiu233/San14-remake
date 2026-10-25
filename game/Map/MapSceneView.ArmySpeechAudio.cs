using Godot;
using San14.Data;

public partial class MapSceneView
{
    private SubViewport? _armySpeechAudioViewport;
    private MapMonthlyAudioWorld? _armySpeechAudioWorld;

    internal ArmySpeechAudioContext? CaptureArmySpeechAudioContext()
    {
        if (!IsPlayableMapReady || _sourceMainCamera is null || _sourceMainCameraFrame is null) return null;
        if (_armySpeechAudioWorld is null)
        {
            //VoicePlayer uses a ground-projected listener. Its own audio world
            //keeps this separate from the territory SE listener at the camera eye.
            _armySpeechAudioViewport = new()
            {
                Name = "ArmySpeechAudioViewport", Size = new(1, 1), OwnWorld3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, AudioListenerEnable3D = true
            };
            AddChild(_armySpeechAudioViewport);
            _armySpeechAudioWorld = new()
            {
                Name = "ArmySpeechAudioWorld", ReadCamera = ReadArmySpeechListenerTransform
            };
            _armySpeechAudioViewport.AddChild(_armySpeechAudioWorld);
        }
        var listener = ReadArmySpeechListenerPosition();
        return listener is null ? null : new(_armySpeechAudioWorld, listener);
    }

    private NarrationSpatialVoicePosition? ReadArmySpeechListenerPosition() =>
        _sourceMainCamera is { } owner && _sourceMainCameraFrame is { } frame
            ? ArmyMapSpeechVoiceGeometry.QueryListener(frame, owner.CurrentRuntime.Bounds, true) : null;

    private Transform3D? ReadArmySpeechListenerTransform()
    {
        if (_sourceMainCameraFrame is not { } frame || ReadArmySpeechListenerPosition() is not { } listener) return null;
        var camera = CreateSourceCameraGodotTransform(frame.Camera);
        return new(camera.Basis, new(listener.X, listener.Y, listener.Z));
    }
}
