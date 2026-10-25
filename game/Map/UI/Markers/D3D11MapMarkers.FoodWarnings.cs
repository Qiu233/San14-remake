using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private readonly Dictionary<(Kind, int), UiSourceSimpleTooltip> foodWarnings = [];
    private Shader? foodWarningShader;
    private int foodWarningInformationMode;

    internal void ApplyFoodWarnings(GameResourceRepository repository, NormalTurnFoodWarningPublication publication)
    {
        //Resolve all live receivers before replacing the prior publication.
        //An empty source list still retires every previous warning.
        var receivers = publication.Markers.Select(row =>
        {
            var key = (row.MarkerLocationId <= 51 ? Kind.City : Kind.Gate, row.MarkerLocationId);
            if (!_markers.TryGetValue(key, out var marker) || marker.Hex != row.HexId)
                throw new InvalidDataException("map.food-warning.actual_base_receiver");
            return key;
        }).Distinct().ToArray();
        foreach (var key in foodWarnings.Keys.Where(key => !receivers.Contains(key)).ToArray())
        {
            var old = foodWarnings[key]; foodWarnings.Remove(key);
            old.Hide(); RemoveChild(old); old.QueueFree();
        }
        foreach (var key in receivers)
        {
            if (!foodWarnings.TryGetValue(key, out var view))
            {
                foodWarningShader ??= UiSourceSprite.CreateShader();
                view = new UiSourceSimpleTooltip(); AddChild(view);
                view.Configure(repository, _assets!, foodWarningShader, _font!, lineSpacing: 4, followPointer: false);
                foodWarnings.Add(key, view);
            }
            view.DisplayText(publication.Text);
        }
        PositionFoodWarnings();
    }

    private void PositionFoodWarnings()
    {
        foreach (var (key, view) in foodWarnings)
        {
            if (!_markers.TryGetValue(key, out var marker)) { view.Hide(); continue; }
            view.Visible = marker.OnScreen && marker.CityPresentation.Display < 2 &&
                foodWarningInformationMode == 0 &&
                (_hoveredHex == marker.Hex || _pressedHex == marker.Hex);
            view.Scale = Size / CanonicalSize;
            //The source positions this persistent tooltip above its Base
            //control; it never tracks the pointer or takes modal input.
            var root = _citySources!.Simple.Nodes[0];
            var geometry = CityGateMapMarkerSources.SimpleGeometry(marker.Troops, true, marker.Kind == Kind.Gate);
            float width = geometry.Plate.Left + geometry.Plate.Width;
            view.Position = (marker.Screen + new Vector2(root.HeaderValues[0] + width / 2 - view.Size.X / 2,
                -view.Size.Y)) * view.Scale;
        }
    }
}
