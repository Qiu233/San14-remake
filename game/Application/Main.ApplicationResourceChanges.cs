using San14.Data;

public partial class Main
{
    private GameResourceRepository? applicationResourceChangeRoot;

    private void StartApplicationResourceChanges()
    {
        var root = _repository ?? throw new InvalidOperationException("Application source owner is missing.");
        root.StartResourceChangeNotifications();
        applicationResourceChangeRoot = root;
    }

    private void StopApplicationResourceChanges()
    {
        applicationResourceChangeRoot?.StopResourceChangeNotifications();
        applicationResourceChangeRoot = null;
    }
}
