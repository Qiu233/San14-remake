using System.Numerics;

namespace San14.Data;

/// <summary>The effect's explicit camera binding, installed in system slot 1.
/// The cutin layout remains a separate lifetime owner; its Control transform
/// and the effect's slot-0 world matrix are not this binding.</summary>
public static class UniqueTacticEmitterBinding
{
    /// <summary>Build the actual dedicated-camera binding for one frame.
    /// E18C40/E199C0 materialize camera S/Q/T, E18D00 binds kind1/node0,
    /// and D93720 rebuilds its matrix with the original S/Q/T kernel.
    /// Retain this callback only for the frame represented by the supplied camera.</summary>
    public static Func<int, int, Matrix4x4?> FromCamera(MapCameraGeometry camera)
    {
        var view = camera.Matrices.WorldToView80;
        // E199C0 transposes the camera view's orthonormal 3x3 basis before
        // extracting its quaternion. Translation is the actual camera eye,
        // not the view-matrix translation or an inverse projection position.
        var basis = new Matrix4x4(
            view.M11, view.M21, view.M31, 0f,
            view.M12, view.M22, view.M32, 0f,
            view.M13, view.M23, view.M33, 0f,
            0f, 0f, 0f, 1f);
        Quaternion rotation = MapCameraController.QuaternionFromBasis12A70(basis);
        var matrix = UnitWorldTransformMath.ScaleQuaternionPositionA23210(
            camera.Pose.Scale90, rotation, camera.Pose.Position80);
        return (slot, node) => slot == 1
            // The actual provider is installed for node0. Its own v8 returns
            // identity for a different node; another slot has no known provider.
            ? node == 0 ? matrix : Matrix4x4.Identity
            : null;
    }
}
