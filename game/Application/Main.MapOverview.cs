using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private void DrawMap() => DrawMap(showBrowserPreview: true);

    private void DrawMap(bool showBrowserPreview, NewGamePreparationTiming? timing = null)
    {
        if (_mapOverview is not { } content || !showBrowserPreview || !DeveloperWorkspaceEnabled) return;
        if (_world?.RulesetId == PlayableSkeleton.RulesetId && !_mapSurfaceAttempted && _repository is not null)
        {
            timing?.Mark("overview-surface-build-begin");
            _mapSurfaceAttempted = true;
            try { _mapSurface = MapSurface.Build(_repository, _map!); }
            catch (Exception error)
            {
                _mapHint.Text = _ui.Get("map_surface_fallback");
                GD.PushWarning(error.ToString());
            }
            timing?.Mark("overview-surface-build-end");
        }
        // Formal force selection borrows the actual native map output.
        // This raster surface belongs only to the visible developer browser.
        MapOverviewView view = MapOverviewView.Capture(content, _world);
        using Image image = MapOverviewRaster.Draw(view, _mapMode.Selected == 0,
            _selectedForceId, _playerForceId, _mapSurface);
        _mapTexture.Texture = ImageTexture.CreateFromImage(image);
        _mapTexture.SetMeta("original_backdrop_available", _mapSurface is not null);
        if (_mapSurface is not null)
        {
            _mapTexture.SetMeta("original_backdrop_source_uri", _mapSurface.SourceUri);
            _mapTexture.SetMeta("original_backdrop_source_sha256", _mapSurface.SourceSha256);
        }
    }

    private void OnMapInput(InputEvent input)
    {
        if (_mapOverview is not { } content || input is not InputEventMouseButton button || !button.Pressed ||
            button.ButtonIndex != MouseButton.Left) return;
        int x = Math.Clamp((int)(button.Position.X / _mapTexture.Size.X * content.Width), 0, content.Width - 1);
        int y = Math.Clamp((int)(button.Position.Y / _mapTexture.Size.Y * content.Height), 0, content.Height - 1);
        SelectMapTarget(y * content.Width + x);
    }

    private void RefreshTileInfo()
    {
        _tileInfo.Text = _selectedCell is { } cell && _mapOverview is { } content
            ? MapOverviewView.DescribeTile(content, cell.Id, _world, _ui) : _ui.TilePrompt;
    }
}
