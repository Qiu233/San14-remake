using Godot;

public partial class Main
{
    private UiSourceType6Host? _type6Host;
    private void StartUiComposition()
    {
        var host = new UiSourceType6Host { Name = "UiType6Composition" };
        AddChild(host); host.Configure(this, error => ShowFailure(error, startup: false));
        _type6Host = host;
    }
}
