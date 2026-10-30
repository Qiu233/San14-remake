using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

public sealed partial class G1EffectMapInputs
{
    private static uint ReadCameraIesReference(DokObject camera)
    {
        var value = camera.Properties.Single(property => property.NameHash == 0x9148D7AD);
        if (value.TypeCode != 5 || value.ElementCount != 1 || value.RawValue.Length != 4)
            throw new InvalidDataException("Effect IES requires the current Camera resource reference.");
        return BinaryPrimitives.ReadUInt32LittleEndian(value.RawValue);
    }

    private G1EffectGpuFrameBinding? PrepareLightingExternal(G1EffectGpuExternalBinding external, uint outerObjectId)
    {
        if (external.StageCode != 0) return null;
        var declaration = external.Declaration;
        if (external.Registrations.Any(entry => entry.PrimaryEncoded == 32 && entry.Name == "sIES"))
        {
            if (cameraIesReference != 0)
                throw new NotSupportedException("Current nonzero Camera IES requires its actual image owner.");
            if (declaration.InputType == 3)
                return G1EffectGpuFrameBinding.StandaloneSampler(0, declaration.BindPoint, nullImageSampler);
            if (declaration.InputType == 2 && declaration.Dimension == 5 && declaration.ReturnType == 5)
                return G1EffectGpuFrameBinding.Graph(0, declaration.BindPoint, G1EffectGpuGlobalTexture.CameraIesNull);
            throw new InvalidDataException("Current Effect IES has another resource shape.");
        }
        if (external.Registrations.Any(entry => entry.PrimaryEncoded == 7 && entry.Name == "sLit"))
        {
            if (declaration.InputType == 3 || declaration.InputType == 2 && declaration.Dimension == 4 && declaration.ReturnType == 5)
                return G1EffectGpuFrameBinding.Graph(0, declaration.BindPoint, G1EffectGpuGlobalTexture.PatternLighting, outerObjectId);
            throw new InvalidDataException("Current Effect Pattern has another resource shape.");
        }
        (G1EffectGpuGlobalTexture? Role, uint Return) cluster = declaration.Name.Text switch
        {
            "tCllLightIndices" => (G1EffectGpuGlobalTexture.ClusterIndices, 4u),
            "tCllLightPositions" => (G1EffectGpuGlobalTexture.ClusterPositions, 5u),
            "tCllLightAttributes" => (G1EffectGpuGlobalTexture.ClusterAttributes, 5u),
            "tCllLightExAttributes" => (G1EffectGpuGlobalTexture.ClusterExAttributes, 4u),
            "tCllSptLightAttributes" => (G1EffectGpuGlobalTexture.ClusterSpotAttributes, 5u),
            "tCllSptLightExAttributes" => (G1EffectGpuGlobalTexture.ClusterSpotExAttributes, 4u),
            _ => (null, 0u)
        };
        if (cluster.Role is null) return null;
        if (declaration.InputType != 2 || declaration.Dimension != 1 || declaration.ReturnType != cluster.Return)
            throw new InvalidDataException("Current Effect cluster has another typed buffer shape.");
        return G1EffectGpuFrameBinding.Graph(0, declaration.BindPoint, cluster.Role.Value);
    }
}
