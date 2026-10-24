using Godot;

public partial class MapSceneView
{
    private bool _keyboardArmyInformationVisible = true;

    internal bool ToggleKeyboardMapInformation(int action)
    {
        if (action == 18) return ToggleKeyboardMapOverlay(action);
        if (!D3D11MapInteractive || _detailsOpen || _d3d11Markers is null || _mapPresentation is null) return false;
        if (action == 21)
        {
            //893640 case4 toggles category0 and applies the same bit to
            //0/1/2/3/6, disabling diplomatic category5 when enabled.
            bool visible = (_mapPresentation.Policy.Category16 & 4) == 0;
            _mapPresentation.SetBaseInformationVisible(visible);
        }
        else if (action == 22) _keyboardArmyInformationVisible = !_keyboardArmyInformationVisible;
        else return false;
        ApplyKeyboardMapInformation();
        return true;
    }

    private void ApplyKeyboardMapInformation()
    {
        if (_d3d11Markers is null || _mapPresentation is null) return;
        _d3d11Markers.SetInformationVisibility((_mapPresentation.Policy.Category16 & 4) != 0,
            _keyboardArmyInformationVisible);
    }
}
