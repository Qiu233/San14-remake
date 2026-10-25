using San14.Data.Formats;

namespace San14.Data;

public sealed partial class G1EffectGpuResources
{
    public G1EffectGpuDrawResources PrepareDrawResources(int runtimeBankIndex,
        ReadOnlyMemory<byte> featureRequest48, G1EffectDrawPacket packet,
        G1EffectGpuTexture? sourceDefaultTexture = null) =>
        PrepareDrawResources(PreparePipeline(runtimeBankIndex, featureRequest48, packet),
            packet, sourceDefaultTexture);

    /// <summary>Execute the ordinary sprite/mesh callback's ordered local
    /// texture writes. External view inputs remain declarations for the current
    /// render graph; a missing source default wrapper is not replaced by a color.</summary>
    public G1EffectGpuDrawResources PrepareDrawResources(G1EffectGpuPipeline pipeline,
        G1EffectDrawPacket packet, G1EffectGpuTexture? sourceDefaultTexture = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline); ArgumentNullException.ThrowIfNull(packet);
        if (!ReferenceEquals(packet.Resources, Resources) ||
            !pipelines.TryGetValue(pipeline.Key, out var owned) || !ReferenceEquals(owned, pipeline))
            throw new ArgumentException("Effect GPU pipeline and draw must belong to this resource owner.");
        if (pipeline.Pass != packet.RenderingType ||
            pipeline.Key.Variant != G1EffectGpuVariant.FromFlags(packet.Flags))
            throw new ArgumentException("Effect GPU pipeline was selected for different current draw fields.");
        var local = new List<G1EffectGpuTextureBinding>();
        var missing = new List<G1EffectGpuMissing>();
        // This is A51300 or A50300 with ordinary Effect2 mode. Both bind
        // primary physical slot0, then optional encoded roles in this order.
        if (packet.RenderingType is not (7 or 17 or 18 or 22 or 109 or 125 or 126 or 127))
            missing.Add(new("TextureCallback", $"Effect pass {packet.RenderingType} needs its actual callback binding producer."));
        else
        {
            WriteSlot(0, packet.TextureSlot, 0);
            int increment = (packet.Flags & 4) != 0 ? 1 : 0;
            int next = increment + 1;
            if (WriteToken(21, checked(packet.TextureSlot + next), false)) next = increment + 2;
            if (increment != 0) next = 4;
            if (WriteToken(19, checked(packet.TextureSlot + next), false)) next++;
            if (WriteToken(130, checked(packet.TextureSlot + next), false)) next++;
            if (packet.MeshSubdrawIndex is not null)
            {
                // A529D0 returns immediately when the first field role is
                // absent. A disabled field uses the real owner default.
                bool firstField = ResolveToken(63) is not null;
                if (firstField)
                {
                    bool enabled = (packet.Flags & 0xC000) != 0;
                    WriteToken(63, checked(packet.TextureSlot + next), !enabled);
                    if (enabled) next++;
                    if (ResolveToken(64) is not null)
                        WriteToken(64, checked(packet.TextureSlot + next), (packet.Flags & 0x80) == 0);
                }
            }
        }
        var external = new List<G1EffectGpuExternalBinding>();
        foreach (var stage in pipeline.Stages)
            foreach (var binding in stage.ResourceDefinitions.Bindings.Where(binding => binding.InputType != 0))
            {
                bool supplied = binding.InputType switch
                {
                    2 => local.Any(item => item.StageCode == stage.StageCode && item.TextureRegister == binding.BindPoint),
                    3 => local.Any(item => item.StageCode == stage.StageCode && item.SamplerRegister == binding.BindPoint),
                    _ => false
                };
                if (supplied && binding.BindCount == 1) continue;
                var registrations = pipeline.ResourceBindings.Entries.Where(entry =>
                    entry.SlotIndex == binding.BindPoint ||
                    string.Equals(entry.Name, binding.Name.Text, StringComparison.Ordinal)).ToArray();
                external.Add(new(stage.StageCode, binding, registrations));
            }
        return new(pipeline, local.AsReadOnly(), external.AsReadOnly(), missing.AsReadOnly());

        G1ShaderResourceBinding? ResolveToken(int token)
        {
            int index = EncodedIndex(token);
            var entries = pipeline.ResourceBindings.Entries.Where(entry =>
                entry.PrimaryEncoded != 0 && EncodedIndex(entry.PrimaryEncoded) == index ||
                entry.SecondarySamplerEncoded != 0 && EncodedIndex(entry.SecondarySamplerEncoded) == index).ToArray();
            if (entries.Length == 0) return null; // Native sentinel slot16.
            if (entries.Any(entry => entry.SlotIndex != entries[0].SlotIndex))
                throw new InvalidDataException($"Effect encoded role {token} has conflicting selected shader registrations.");
            return entries[0];
        }
        bool WriteToken(int token, int textureSlot, bool useDefault)
        {
            var entry = ResolveToken(token);
            if (entry is null || entry.SlotIndex == 16) return false;
            if (entry.SlotIndex < 0) throw new InvalidDataException("Effect shader registration has a negative physical slot.");
            WriteSlot((uint)entry.SlotIndex, textureSlot, token, useDefault);
            return true;
        }
        void WriteSlot(uint physicalSlot, int textureSlot, int token, bool useDefault = false)
        {
            // 876F30's stage4 texture-wrapper command is a pixel binding.
            // Its image and sampler use the same physical slot, but only
            // declarations present in the selected program are published.
            foreach (var stage in pipeline.Stages.Where(stage => stage.StageCode == 0))
            {
                var images = stage.ResourceDefinitions.Bindings.Where(binding =>
                    binding.InputType == 2 && binding.BindPoint == physicalSlot).ToArray();
                if (images.Length == 0) continue;
                if (images.Length != 1 || images[0].BindCount != 1)
                {
                    missing.Add(new("TextureBinding", $"Original pixel slot {physicalSlot} has no unique single image declaration."));
                    continue;
                }
                G1EffectGpuTexture? texture = sourceDefaultTexture;
                if (!useDefault && Resources.TextureSlots.ContainsKey(textureSlot)) texture = PrepareTexture(textureSlot);
                if (texture is null)
                {
                    missing.Add(new("DefaultTexture", $"Encoded role {token}, physical slot {physicalSlot}, source slot {textureSlot} requires the actual Effect2 owner default texture."));
                    continue;
                }
                var samplers = stage.ResourceDefinitions.Bindings.Where(binding =>
                    binding.InputType == 3 && binding.BindPoint == physicalSlot).ToArray();
                if (samplers.Length > 1 || samplers.Any(binding => binding.BindCount != 1))
                {
                    missing.Add(new("SamplerBinding", $"Original pixel slot {physicalSlot} has no unique single sampler declaration."));
                    continue;
                }
                local.RemoveAll(item => item.StageCode == stage.StageCode && item.TextureRegister == physicalSlot);
                local.Add(new(stage.StageCode, physicalSlot, samplers.Length == 1 ? physicalSlot : null, token, texture));
            }
        }
    }

    private static int EncodedIndex(int encoded)
    {
        if (encoded < 0 || encoded > short.MaxValue)
            throw new InvalidDataException("Effect encoded texture role is outside the native signed-short namespace.");
        int index = (encoded & 7) + 8 * (((encoded >> 3) & 15) + 10 * (encoded >> 7));
        if (index >= 88) throw new InvalidDataException("Effect encoded texture role exceeds its original registration table.");
        return index;
    }
}
