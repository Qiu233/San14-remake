using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private ArmyMapSpeechLayer? _armySpeechLayer;
    internal bool ArmySpeechManagerVisible => IsPlayableMapReady && IsVisibleInTree() &&
        _d3d11Markers?.ArmySpeechManagerVisible == true;
    internal bool HasArmySpeechLayout(int armyId) => _d3d11Markers?.HasArmySpeechLayout(armyId) == true;

    internal bool PrepareArmySpeechLayout(ArmyMapSpeechEntry entry)
    {
        //Capture and defeat allocate their own UnitAction layout. Other
        //actions borrow the actual Army view and cannot revive a retired one.
        if (_armySpeechLayer is null || entry.ActionCode is not (11 or 14) && !HasArmySpeechLayout(entry.ArmyId)) return false;
        return _armySpeechLayer.Prepare(entry);
    }

    internal San14.Client.Gameplay.ArmyTraitSpeechViews? CaptureArmyTraitSpeechViews()
        => IsPlayableMapReady && UseD3D11Map ? _d3d11Markers?.CaptureArmySpeechViews() : null;

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

    internal void PresentArmyMapSpeech(IReadOnlyList<ArmyMapSpeechDisplay> visible, ArmyMapSpeechState snapshot)
        => _armySpeechLayer?.Present(visible, snapshot);

    private bool TryArmySpeechPoint(int hexId, out Vector3 position)
    {
        position = default;
        if ((uint)hexId > 48400u || SourceMainCameraTerrain is not { } terrain) return false;
        var point = ArmyMapSpeechVoiceGeometry.QueryPacketPosition(hexId, terrain);
        position = new(point.X, point.Y + 200, point.Z);
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
        private sealed record Bubble(ArmyMapSpeechBubbleView View, int HexId, Vector3 Position, bool Owned);
        private readonly Dictionary<long, Bubble> _bubbles = [];
        private readonly HashSet<long> _retained = [];
        private ArmyMapSpeechState? _source;
        private readonly Dictionary<long, ArmyMapSpeechEntry> _entries = [];
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

        internal void Present(IReadOnlyList<ArmyMapSpeechDisplay> visible, ArmyMapSpeechState snapshot)
        {
            _visible = visible;
            if (ReferenceEquals(_source, snapshot)) return;
            _source = snapshot;
            _retained.Clear();
            _entries.Clear();
            foreach (var entry in snapshot.Entries) { _retained.Add(entry.Sequence); _entries.Add(entry.Sequence, entry); }
            foreach (long sequence in _bubbles.Keys.Where(id => !_retained.Contains(id)).ToArray())
            { Release(_bubbles[sequence]); _bubbles.Remove(sequence); }
        }

        internal bool Prepare(ArmyMapSpeechEntry entry)
        {
            long sequence = entry.Sequence;
            int hexId = entry.HexId, style = entry.ActionCode == 25 ? 1 : 0;
            bool owned = entry.ActionCode is 11 or 14;
            var borrowed = owned ? null : _map._d3d11Markers?.ArmySpeechLayout(entry.ArmyId);
            if (_bubbles.TryGetValue(sequence, out var retained))
            {
                if (GodotObject.IsInstanceValid(retained.View) && retained.View.IsInsideTree() && !retained.View.IsQueuedForDeletion() &&
                    retained.HexId == hexId && retained.View.LayoutStyle == style && (owned || retained.View == borrowed)) return true;
                Release(retained); _bubbles.Remove(sequence);
            }
            if (!_map.TryArmySpeechPoint(hexId, out var point)) return false;
            ArmyMapSpeechBubbleView view;
            if (owned)
            {
                view = new() { Name = "ArmySpeech_" + sequence, Visible = false };
                _canvas.AddChild(view);
                try { view.Configure(_repository, _assets, _shader, _font, style); }
                catch { _canvas.RemoveChild(view); view.QueueFree(); throw; }
            }
            else
            {
                if (borrowed is null) return false;
                view = borrowed; view.ApplyStyle(style);
            }
            _bubbles.Add(sequence, new(view, hexId, point, owned));
            return true;
        }

        private static void Release(Bubble bubble)
        {
            if (!GodotObject.IsInstanceValid(bubble.View)) return;
            bubble.View.Hide();
            if (bubble.Owned) bubble.View.QueueFree();
        }

        public override void _Process(double delta)
        {
            Update?.Invoke(delta);
            if (_visible.Count == 0 && _bubbles.Count == 0) return;
            _canvas.Scale = Size / new Vector2(1920, 1080);
            foreach (var bubble in _bubbles.Values)
                if (GodotObject.IsInstanceValid(bubble.View)) bubble.View.Hide();
            foreach (var display in _visible)
            {
                if (!_entries.TryGetValue(display.Sequence, out var entry) || !Prepare(entry)) continue;
                var bubble = _bubbles[display.Sequence];
                bubble.View.ApplyText(display.Text);
                if (_map.ArmySpeechManagerVisible && _map.TryProjectArmySpeech(bubble.Position, out var pixel))
                {
                    bubble.View.Position = new((int)pixel.X - 144, (int)pixel.Y - 140);
                    bubble.View.Show();
                }
            }
        }

        public override void _ExitTree()
        {
            Update = null;
            foreach (var bubble in _bubbles.Values) Release(bubble);
            _bubbles.Clear(); _font?.Dispose(); _shader?.Dispose(); _assets?.Dispose();
        }
    }
}
