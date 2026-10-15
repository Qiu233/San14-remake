using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;
using static San14.Data.NativeFloat;

namespace San14.Data;

public static partial class SkyCloudConstants
{
    /// <summary>Prepare one current color face from the selected program and already prepared scene values.
    /// Animation, clip history and frame commit remain with their actual owners.</summary>
    public static SkyCloudConstantPacket PrepareCurrentFace(SkyCloudColorProgramSelection program,
        SkySceneSourceFrame frame, int face, SkyCloudMaterial material, SkyCloudVolumeMaterial volume,
        float planetRadius, uint maximumRaySamples, Vector4 clipPlane, Vector3 rawSunRgb, float geometry40)
    {
        var camera = frame.SixCameras[face];
        var lights = frame.Lights;
        var controller = frame.Controller!;
        if (camera.NativeClipProfile != 2 || (camera.Flags148 & ~2u) != 0)
            throw new NotSupportedException("Cloud current source writer closes ordinary Perspective clip2 only.");
        float far = (camera.Flags148 & 2) != 0 ? 0 : 1, near = (camera.Flags148 & 2) != 0 ? 1 : 0;
        Matrix4x4 projectionToWorld = NativeMatrixMath.Invert(camera.MatrixF0, out _);
        Matrix4x4 directionView = CreateCubeDirectionView(face);
        Matrix4x4 projection = CreateCubeProjection((camera.Flags148 & 2) != 0);
        Matrix4x4 rays = NativeMatrixMath.Invert(TerrainFrustumMath.Multiply(directionView, projection), out _);
        float[] cameraDirectionVectors = new float[16];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            float x = (vertex >> 1) * 2 - 1, y = 1 - (vertex & 1) * 2;
            Vector4 farPoint = TransformHomogeneousPoint(x, y, far, rays);
            Vector4 nearPoint = TransformHomogeneousPoint(x, y, near, rays);
            float farReciprocalW = Divide(1, farPoint.W), nearReciprocalW = Divide(1, nearPoint.W);
            // The source writer subtracts the normalized far point from the normalized near point.
            cameraDirectionVectors[vertex * 4] =
                Subtract(Multiply(nearPoint.X, nearReciprocalW), Multiply(farPoint.X, farReciprocalW));
            cameraDirectionVectors[vertex * 4 + 1] =
                Subtract(Multiply(nearPoint.Y, nearReciprocalW), Multiply(farPoint.Y, farReciprocalW));
            cameraDirectionVectors[vertex * 4 + 2] =
                Subtract(Multiply(nearPoint.Z, nearReciprocalW), Multiply(farPoint.Z, farReciprocalW));
            cameraDirectionVectors[vertex * 4 + 3] =
                Subtract(Multiply(nearPoint.W, nearReciprocalW), Multiply(farPoint.W, farReciprocalW));
        }
        Dictionary<string, float[]> vertexValues = new()
        {
            ["ksgClipPlane"] = Components(clipPlane),
            ["g_mtP2W"] = Components(projectionToWorld),
            ["g_vCameraDirVectors"] = cameraDirectionVectors,
            ["g_vProjFarNear"] = [far, near]
        };
        Dictionary<string, float[]> pixelValues = new()
        {
            ["litDir"] = Components(lights.PackageLightPosition),
            ["vEye"] = Components(camera.Position),
            ["fog"] = Components(Vector4.Zero),
            ["Scat"] = frame.Atmosphere.Scattering.SelectMany(Components).ToArray()
        };
        void Set(string name, params float[] words) => pixelValues.Add(name, words);
        var common = volume.PreparePixel(camera.Position, camera.Scalar138, planetRadius);
        Set("g_vBaseTexOffset", common.BaseTextureOffset.X, common.BaseTextureOffset.Y, common.BaseTextureOffset.Z);
        Set("g_vBaseTexScale", common.BaseTextureScale.X, common.BaseTextureScale.Y, common.BaseTextureScale.Z);
        Set("g_vDetailTexOffset", common.DetailTextureOffset.X, common.DetailTextureOffset.Y, common.DetailTextureOffset.Z);
        Set("g_vDetailTexScale", common.DetailTextureScale.X, common.DetailTextureScale.Y, common.DetailTextureScale.Z);
        Set("g_vDistortTexOffset", common.DistortTextureOffset.X, common.DistortTextureOffset.Y);
        Set("g_vDistortTexScale", common.DistortTextureScale.X, common.DistortTextureScale.Y);
        Set("g_vCirrusTexOffset", common.CirrusTextureOffset.X, common.CirrusTextureOffset.Y);
        Set("g_vCirrusTexScale", common.CirrusTextureScale.X, common.CirrusTextureScale.Y);
        Set("g_vCirrusDistortTexOffset", common.CirrusDistortTextureOffset.X, common.CirrusDistortTextureOffset.Y);
        Set("g_vCirrusDistortTexScale", common.CirrusDistortTextureScale.X, common.CirrusDistortTextureScale.Y);
        Set("g_vRaySampleNum", Components(common.RaySampleCount));
        Set("g_vEarthRadius", Components(common.EarthRadius));
        // The shader descriptor order differs from the arithmetic preparation order.
        Set("g_vCloudParams1", Components(common.CloudParameters1));
        Set("g_vCloudParams2", Components(material.SampleStepParameters));
        Set("g_vCloudParams0", Components(common.CloudParameters0));
        Set("g_vDistortScale", Components(common.DistortionScale));
        Set("g_vCirrusParams", Components(common.CirrusParameters));
        Set("g_vCirrusDistortScale", common.CirrusDistortionScale.X, common.CirrusDistortionScale.Y);
        Set("g_vErodeScale", common.ErosionScale.X, common.ErosionScale.Y);
        Set("g_vHgParams0", Components(material.HgParameters0));
        Set("g_vHgParams1", Components(material.HgParameters1));
        float sun = controller.SkyInfoZW7C78.X, moon = controller.SkyInfoZW7C78.Y;
        if (sun > 0 && Multiply(lights.RawSunPosition.Y, -1) > FloatFromBits(0xBE19999A))
        {
            float intensity = Add(
                Add(Multiply(lights.RawMoonRgb.Y, FloatFromBits(0x3EAAAAAB)),
                    Multiply(lights.RawMoonRgb.X, FloatFromBits(0x3EAAAAAB))),
                Multiply(lights.RawMoonRgb.Z, FloatFromBits(0x3EAAAAAB)));
            if (Divide(FloatFromBits(0x3B000000), geometry40) > intensity) moon = 0;
        }
        Set("g_vSky2SunRGB", Multiply(rawSunRgb.X, sun), Multiply(rawSunRgb.Y, sun), Multiply(rawSunRgb.Z, sun), sun);
        Set("g_vSky2SunDir", Multiply(lights.RawSunPosition.X, -1), Multiply(lights.RawSunPosition.Y, -1),
            Multiply(lights.RawSunPosition.Z, -1), SkyCloudMaterial.SunHorizonOffset);
        Set("g_vSky2MoonRGB", Multiply(lights.RawMoonRgb.X, moon), Multiply(lights.RawMoonRgb.Y, moon),
            Multiply(lights.RawMoonRgb.Z, moon), moon);
        Set("g_vSky2MoonDir", Multiply(lights.RawMoonPosition.X, -1), Multiply(lights.RawMoonPosition.Y, -1),
            Multiply(lights.RawMoonPosition.Z, -1), 0);
        Set("g_vSky2RayleighExtinction", 0, 0, 0, Add(SkyCloudMaterial.AtmosphereHeight, planetRadius));
        Set("g_vSky2MieExtinction", 0, 0, 0, 0);
        Set("g_vStableDim", Components(material.StableDimensions));
        Set("g_vClampValue", 0, 0, 0, controller.Scalar1F0);
        var vertexDefinitions = program.VertexDefinitions;
        var pixelDefinitions = program.PixelDefinitions;
        byte[] vertexBytes = PackNamedConstants(vertexDefinitions, 160, vertexValues);
        byte[] pixelBytes = PackNamedConstants(pixelDefinitions, program.PixelConstantByteLength, pixelValues);
        return ReadColor(vertexDefinitions, pixelDefinitions, vertexBytes, pixelBytes, maximumRaySamples);
    }

    private static byte[] PackNamedConstants(DxbcResourceDefinitions definitions, int length,
        Dictionary<string, float[]> fields)
    {
        var buffer = definitions.Buffers.Single();
        if (buffer.ByteSize != length || fields.Count != buffer.Variables.Count)
            throw new InvalidDataException("Cloud named source inventory differs from actual shader.");
        byte[] bytes = new byte[length];
        foreach (var variable in buffer.Variables)
        {
            if (!fields.TryGetValue(variable.Name.Text, out var values) || values.Length * 4 != variable.ByteSize)
                throw new InvalidDataException("Cloud current named writer missing " + variable.Name.Text);
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.IsFinite(values[i]))
                    throw new InvalidDataException("Cloud current named result is nonfinite: " + variable.Name.Text);
                BinaryPrimitives.WriteSingleLittleEndian(
                    bytes.AsSpan(checked((int)variable.StartOffset + i * 4), 4), values[i]);
            }
        }
        return bytes;
    }

    private static Matrix4x4 CreateCubeDirectionView(int face)
    {
        var axis = SkySceneSourceDefaults.CubeFaces[face];
        Vector3 z = new(Subtract(0, axis.Direction.X), Subtract(0, axis.Direction.Y), Subtract(0, axis.Direction.Z));
        Vector3 x = new(
            Subtract(Multiply(z.Z, axis.Up.Y), Multiply(z.Y, axis.Up.Z)),
            Subtract(Multiply(z.X, axis.Up.Z), Multiply(z.Z, axis.Up.X)),
            Subtract(Multiply(z.Y, axis.Up.X), Multiply(z.X, axis.Up.Y)));
        Vector3 y = new(
            Subtract(Multiply(x.Z, z.Y), Multiply(x.Y, z.Z)),
            Subtract(Multiply(x.X, z.Z), Multiply(x.Z, z.X)),
            Subtract(Multiply(x.Y, z.X), Multiply(x.X, z.Y)));
        float DotWithZeroEye(Vector3 direction) =>
            Add(Add(Multiply(0f, direction.X), Multiply(0f, direction.Y)), Multiply(0f, direction.Z));
        return new(x.X, y.X, z.X, 0, x.Y, y.Y, z.Y, 0, x.Z, y.Z, z.Z, 0,
            DotWithZeroEye(x), DotWithZeroEye(y), DotWithZeroEye(z), 1);
    }

    private static Matrix4x4 CreateCubeProjection(bool reverse)
    {
        float z = Divide(reverse ? 1 : 100, reverse ? 99 : -99);
        return new(-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, z, -1, 0, 0, Multiply(z, reverse ? 100 : 1), 0);
    }

    private static Vector4 TransformHomogeneousPoint(float x, float y, float z, Matrix4x4 matrix) => new(
        Add(Add(Add(Multiply(x, matrix.M11), Multiply(y, matrix.M21)), Multiply(z, matrix.M31)), matrix.M41),
        Add(Add(Add(Multiply(x, matrix.M12), Multiply(y, matrix.M22)), Multiply(z, matrix.M32)), matrix.M42),
        Add(Add(Add(Multiply(x, matrix.M13), Multiply(y, matrix.M23)), Multiply(z, matrix.M33)), matrix.M43),
        Add(Add(Add(Multiply(x, matrix.M14), Multiply(y, matrix.M24)), Multiply(z, matrix.M34)), matrix.M44));

    private static float[] Components(Vector4 value) => [value.X, value.Y, value.Z, value.W];
    private static float[] Components(Matrix4x4 value) =>
        [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24,
         value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];
    private static float FloatFromBits(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Divide(float left, float right) => left / right;
}
