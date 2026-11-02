public partial class MapSceneView
{
    private bool sourceCameraInputSuspended;

    // A real pending modal may need one more source frame to borrow its
    // simulation owner. Suspend navigation without stopping that frame driver.
    internal void SetSourceCameraInputSuspended(bool suspended)
    {
        sourceCameraInputSuspended = suspended;
        ResetSourceCameraInput();
    }
}
