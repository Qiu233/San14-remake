public partial class MapSceneView
{
    private object? informationMapOwner;
    private bool InformationMapReadOnly => informationMapOwner is not null;

    internal bool BeginInformationMap(object owner, int targetHex)
    {
        if (informationMapOwner is not null || !FocusCommanderHex(targetHex)) return false;
        informationMapOwner = owner;
        CloseCityCommandMenu(); ResetUnitSelectionPointer();
        SetDetailsOpen(false);
        return true;
    }

    internal void EndInformationMap(object owner)
    {
        if (!ReferenceEquals(informationMapOwner, owner)) return;
        informationMapOwner = null;
        ResetSourceCameraInput();
    }
}
