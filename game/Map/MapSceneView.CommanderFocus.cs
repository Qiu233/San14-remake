using Godot;
using San14.Data;

public partial class MapSceneView
{
    internal bool FocusCommanderHex(int physicalHex)
    {
        if ((uint)physicalHex >= 48400u || _sourceMainCamera is null ||
            _sourceMainCameraInputs is not { } input) return false;
        var focus = CeHexWorldCoordinates.Project(physicalHex);
        var request = new MainMapCameraControllerRequest(500,new((float)focus.X,0,(float)focus.Z,1),
            -1,62.83185308,-1,1);
        var prepared = MapCameraController.MaterializeRequest(_sourceMainCamera.CurrentRuntime,
            _sourceMainCamera.Parameters,request,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        _sourceMainCamera.QueueTimedRequest(prepared,unchecked((int)Time.GetTicksMsec()));
        return true;
    }
}
