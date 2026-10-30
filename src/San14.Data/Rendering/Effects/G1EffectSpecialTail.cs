namespace San14.Data;

/// <summary>The AA09B0 mode-three population. Its physical tail is filled
/// backwards by the live queue writer; it shares that writer's particle pool.</summary>
public static class G1EffectSpecialTail
{
    public static bool IsRenderingType(int value) => value is 64 or 65 or 95 or 96 or 147 or 148 or 169 or 170;

    public static G1EffectDrawBatch Select(G1EffectDrawBatch batch) => batch with
    {
        Packets = batch.Packets.Where(packet => packet.IsTail && IsRenderingType(packet.RenderingType)).Reverse().ToArray()
    };

    public static G1EffectDrawBatch SelectHead(G1EffectDrawBatch batch) => batch with
    {
        Packets = batch.Packets.Where(packet => !packet.IsTail).ToArray()
    };
}
