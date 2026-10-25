using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private ArmyMapSpeechLayer? _armySpeechLayer;
    internal bool ArmySpeechManagerVisible => IsPlayableMapReady && IsVisibleInTree() &&
        _d3d11Markers?.ArmySpeechManagerVisible == true;
    internal bool HasArmySpeechLayout(int armyId) => _d3d11Markers?.HasArmySpeechLayout(armyId) == true;

    internal void SetArmyMapSpeechDriver(Action<double>? update)
    {
        if (update is null)
        {
            if (_armySpeechLayer is { } old)
            { _armySpeechLayer = null; old.GetParent()?.RemoveChild(old); old.QueueFree(); }
            return;
        }
        if (_armySpeechLayer is null)
        {
            var layer = new ArmyMapSpeechLayer { Name = "ArmyMapSpeech", ZIndex = 40 };
            _armySpeechLayer = layer; AddChild(layer);
            layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            layer.Configure(_repository, _text.SourceLocale, this);
        }
        _armySpeechLayer.Update = update;
    }

    internal void PresentArmyMapSpeech(IReadOnlyList<ArmyMapSpeechDisplay> visible) => _armySpeechLayer?.Present(visible);

    private bool TryArmySpeechPoint(int hexId, out Vector3 position)
    {
        position = default;
        if ((uint)hexId > 48400u || TerrainMeshes is null) return false;
        //The saved unknown-position sentinel selects the actual Hex0 table
        //entry in the original map projection boundary.
        if (hexId == 48400) hexId = 0;
        var point = CeHexWorldCoordinates.Project(hexId);
        position = new((float)point.X, MapGrounding.SampleHeight(TerrainMeshes, point.X, point.Z) + 200, (float)point.Z);
        return true;
    }

    internal bool TryProjectArmySpeech(Vector3 position, out Vector2 pixel)
    {
        pixel = default;
        var camera = _d3d11PresentedInput?.SourceFrame ?? _sourceMainCameraFrame;
        return camera is not null && D3D11MapMarkers.TryProject(position, camera.Camera.Matrices.WorldToProjection100, out pixel);
    }

    private partial class ArmyMapSpeechLayer : Control
    {
        private sealed record Bubble(ArmyMapSpeechBubbleView View, int HexId, Vector3 Position);
        private readonly Dictionary<long, Bubble> _bubbles = [];
        private readonly HashSet<long> _retained = [];
        private readonly Node2D _canvas = new() { Name = "CanonicalArmySpeech" };
        private IReadOnlyList<ArmyMapSpeechDisplay> _visible = [];
        private MapSceneView _map = null!;
        private GameResourceRepository _repository = null!;
        private UiAssets _assets = null!;
        private Shader _shader = null!;
        private MapFont _font = null!;
        internal Action<double>? Update { get; set; }

        internal void Configure(GameResourceRepository repository, string locale, MapSceneView map)
        {
            MouseFilter = MouseFilterEnum.Ignore; ClipContents = true;
            _map = map; _repository = repository;
            _assets = new(repository); _shader = UiSourceSprite.CreateShader(); _font = new(repository, locale);
            AddChild(_canvas);
        }

        internal void Present(IReadOnlyList<ArmyMapSpeechDisplay> visible) => _visible = visible;

        public override void _Process(double delta)
        {
            Update?.Invoke(delta);
            if (_visible.Count == 0 && _bubbles.Count == 0) return;
            _canvas.Scale = Size / new Vector2(1920, 1080);
            foreach (var bubble in _bubbles.Values) bubble.View.Hide();
            _retained.Clear();
            foreach (var display in _visible)
            {
                _retained.Add(display.Sequence);
                if (!_bubbles.TryGetValue(display.Sequence, out var bubble))
                {
                    if (!_map.TryArmySpeechPoint(display.HexId, out var point)) continue;
                    var view = new ArmyMapSpeechBubbleView { Name = "ArmySpeech_" + display.Sequence };
                    _canvas.AddChild(view); view.Configure(_repository, _assets, _shader, _font);
                    bubble = new(view, display.HexId, point); _bubbles.Add(display.Sequence, bubble);
                }
                bubble.View.ApplyText(display.Text);
                if (_map.ArmySpeechManagerVisible && _map.TryProjectArmySpeech(bubble.Position, out var pixel))
                {
                    bubble.View.Position = new((int)pixel.X - 144, (int)pixel.Y - 140);
                    bubble.View.Show();
                }
            }
            foreach (long sequence in _bubbles.Keys.Where(id => !_retained.Contains(id)).ToArray())
            { _bubbles[sequence].View.QueueFree(); _bubbles.Remove(sequence); }
        }

        public override void _ExitTree()
        { Update = null; _font?.Dispose(); _shader?.Dispose(); }
    }
}
