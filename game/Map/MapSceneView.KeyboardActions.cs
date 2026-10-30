using Godot;
using San14.Core;

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
        if(diplomacyTargetMapOwner is not null)
        {
            // MapSelect keeps City/Gate independently of the ordinary base
            // category byte, which also controls Scenery in the normal HUD.
            _d3d11Markers.SetInformationVisibility(_mapPresentation.Policy.Mode==9,false,_mapPresentation.Policy.Mode);return;
        }
        if(strategicTargetMapOwner is not null && strategicTargetMapQuery is {} strategic)
        {
            // The strategic selector owns Area/City/Gate categories separately
            // from the ordinary HUD's base-information byte.
            _d3d11Markers.SetInformationVisibility(strategic.Kind!=StrategicCommandTargetKind.Force,
                false,strategic.MarkerMode);return;
        }
        _d3d11Markers.SetInformationVisibility(InformationCategoryVisible(_mapPresentation.Policy.Category16),
            _keyboardArmyInformationVisible, _mapPresentation.Policy.Mode);
    }
}
