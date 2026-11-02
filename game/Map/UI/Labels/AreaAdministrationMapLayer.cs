using Godot;
using San14.Data;

/// <summary>Retained held-area buttons, refreshed from the actual draft and
/// projected only with the same Main camera already displayed by the map.</summary>
internal partial class AreaAdministrationMapLayer : Control
{
    private readonly Node2D canvas = new() { Name = "DomesticCanonicalCanvas" };
    private readonly Dictionary<int, AreaAdministrationMapButton> buttons = [];
    private AreaAdministrationDraft? draft;
    private MapSceneView map = null!;
    private GameResourceRepository repository = null!;
    private AdministrationPageChrome.Resources resources = null!;
    private Action<string>? unavailable;
    private long revision = -1;
    private MainMapCameraFrame? lastCamera;
    private bool retired, available;

    internal void Configure(MapSceneView map, GameResourceRepository repository,
        AdministrationPageChrome.Resources resources, AreaAdministrationDraft draft, Action<string> unavailable)
    {
        this.map = map; this.repository = repository; this.resources = resources; this.draft = draft; this.unavailable = unavailable;
        MouseFilter = MouseFilterEnum.Ignore; ClipContents = true;
        AddChild(canvas); Resized += ResizeCanvas; ResizeCanvas();
        RefreshDraft(); RefreshInput();
    }
    private void ResizeCanvas()
    { canvas.Scale = Size / new Vector2(1920, 1080); lastCamera = null; }

    public override void _Process(double delta)
    {
        if (retired || draft is null) return;
        if (draft.Revision != revision) RefreshDraft();
        if (!available) return;
        if (map.DisplayedAdministrationCamera is not { } frame) { lastCamera = null; HideButtons(); return; }
        if (ReferenceEquals(frame, lastCamera)) return;
        lastCamera = frame;
        int band = MapLabelPresentation.PrimaryCameraLevel(frame.ZoomSelection);
        foreach (var button in buttons.Values)
        {
            button.ApplyBand(band);
            if (map.TryProjectAdministrationArea(button.HexId, frame, out var screen))
            {
                var position = screen + button.ProjectionOffset;
                if (button.Position != position) button.Position = position;
                if (!button.Visible) button.Show();
            }
            else if (button.Visible) button.Hide();
        }
    }
    private void RefreshDraft()
    {
        if (draft is null) return;
        revision = draft.Revision;
        var publication = draft.QueryMapPresentation();
        available = false;
        if (!publication.Available) { HideButtons(); unavailable?.Invoke(publication.UnresolvedInput ?? "area-map.publication"); return; }
        try
        {
            var retained = new HashSet<int>();
            foreach (var row in publication.Value!.Rows)
            {
                bool? valid = AreaAdministrationMapButton.Known(row.NativeValid);
                if (valid is null) throw new InvalidDataException("area-map.area_validity");
                if (!valid.Value) continue;
                retained.Add(row.AreaId);
                if (!buttons.TryGetValue(row.AreaId, out var button))
                {
                    button = new AreaAdministrationMapButton(); canvas.AddChild(button);
                    try
                    {
                        button.Configure(repository, resources, row, Allowed, Picked);
                        buttons.Add(row.AreaId, button);
                    }
                    catch
                    { button.Hide(); canvas.RemoveChild(button); button.QueueFree(); throw; }
                }
                else button.Apply(row);
            }
            foreach (int id in buttons.Keys.Where(id => !retained.Contains(id)).ToArray())
            {
                var old = buttons[id]; buttons.Remove(id); old.Hide(); canvas.RemoveChild(old); old.QueueFree();
            }
            available = true; lastCamera = null; RefreshInput();
        }
        catch (InvalidDataException error) { HideButtons(); unavailable?.Invoke(error.Message); }
    }
    internal void RefreshInput()
    {
        if (retired) return;
        foreach (var button in buttons.Values)
        {
            bool disabled = !Allowed(button.HexId);
            if (button.Disabled != disabled) button.Disabled = disabled;
        }
    }
    private bool Allowed(int hex) => !retired && map.AdministrationMapPointerAllowed(hex);
    private void Picked(int hex) { if (Allowed(hex)) map.PickAdministrationMapTarget(hex); }
    private void HideButtons() { foreach (var button in buttons.Values) if (button.Visible) button.Hide(); }
    internal void Retire()
    { retired = true; draft = null; unavailable = null; Hide(); SetProcess(false); }
}
