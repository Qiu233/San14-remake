using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

internal static class CameraDeclarationImport
{
    internal static CameraDeclarationDefaults Read(DokObject camera)
    {
        var p = new CameraNumericPropertyReader(camera);
        var pose = new MapCameraCurrentPose(new Vector3(p.Scalar(0, 0x27D889E5u, 3, 0), p.Scalar(0, 0x27D889E5u, 3, 1), p.Scalar(0, 0x27D889E5u, 3, 2)),
            new Quaternion(p.Scalar(1, 0xF52EA910u, 4, 0), p.Scalar(1, 0xF52EA910u, 4, 1), p.Scalar(1, 0xF52EA910u, 4, 2), p.Scalar(1, 0xF52EA910u, 4, 3)), Vector3.One);
        var lens = new CameraLens(p.Scalar(3, 0x06741408u, 1, 0), p.Scalar(4, 0x003546E9u, 1, 0), p.Scalar(5, 0xBA7543EAu, 1, 0), p.Scalar(6, 0xA70685F8u, 1, 0), p.Scalar(7, 0x0624AD1Du, 1, 0), 0, 0);
        return new(new PerspectiveCameraDeclaration(pose, lens, p.Scalar(2, 0x04C1A415u, 1, 0)),
            new CameraAmbientDefaults(p.Scalar(840, 0xAA84CF1Cu, 1, 0), p.Scalar(841, 0x71003548u, 1, 0), p.Word(864, 0x0B6C81F1u, 4, 1, 0), p.Word(842, 0x2F288D0Cu, 4, 1, 0)),
            p.Word(165, 0xF3CBBCABu, 4, 1, 0), p.Scalar(928, 0x273D81CEu, 1, 0));
    }
}
