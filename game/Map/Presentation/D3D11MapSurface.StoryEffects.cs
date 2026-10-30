using San14.Data;

public partial class D3D11MapSurface
{
    // Native session work is serialized on its actual render thread. The
    // caller retains this surface until the returned retirement task drains.
    internal Task RetireStoryEffectOwnerAsync(D3D11CaptureEffects effects, object owner) => OnRenderThread(_ =>
    {
        ulong token = effects.RetireStoryOwnerImmediately(owner);
        if (token != 0) _session?.RetireStoryEffectOwner(token);
        effects.CompleteStoryOwnerRetirement(owner, token);
        return true;
    });
}
