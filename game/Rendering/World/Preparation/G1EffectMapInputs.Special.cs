using San14.Data;

public sealed partial class G1EffectMapInputs
{
    private Func<uint, G1EffectDrawPacket, G1EffectGpuPacketInputs>? PrepareSpecialPacketInputs(
        G1EffectGpuCameraConstants camera, G1EffectGpuLightConstants light,
        G1EffectGpuPatternConstants? patternConstants, uint commonFlags, uint? actualLightTag)
    {
        // D33 performs these gates independently of the tail row count.
        if ((commonFlags & 4) == 0 || actualLightTag is not { } tag || tag == 0 || unchecked((sbyte)tag) < 0)
            return null;
        // Actual D3432D twelve-word local, distinct from ordinary D32's
        // shadow/fog feature request. Bank3 retains its constructor row.
        var feature = new G1EffectGpuFeatureRequest(0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 3).ToBytes();
        return (node, packet) =>
        {
            if (node is not (141 or 147) || !packet.IsTail || !G1EffectSpecialTail.IsRenderingType(packet.RenderingType))
                throw new InvalidDataException("Special Effect requires its mode-three physical tail and original scope.");
            uint recipeIndex = packet.MeshSubdrawIndex is null ? packet.HeaderFlags >> 3 & 7 : packet.HeaderFlags & 7;
            uint[] recipes = [0x00151440, 0x00111440, 0x00111140, 0x00111442, 0x00111142, 0x00101140, 0x00001100];
            if (blendMode != 1 || recipeIndex >= recipes.Length)
                throw new NotSupportedException("Special Effect requires its actual current blend recipe.");
            var effect = G1EffectGpuEffectConstants.CreateFreshOwned(packet, packet.AlphaReference != 0);
            return new G1EffectGpuPacketInputs(node == 141 ? 3 : 2, feature, new(node, 0, uint.MaxValue),
                new(camera, effect, light, patternConstants),
                G1EffectGpuBlend.EncodePackedRecipe(recipes[recipeIndex]), External)
                { RetainedFeatureRow = node == 141 ? 0 : null };
        };
    }
}
