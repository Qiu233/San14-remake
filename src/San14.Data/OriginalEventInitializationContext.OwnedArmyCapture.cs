namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    /// <summary>Explicit current owner assignment used by a campaign capture.
    /// Null retains unavailable ruler metadata instead of an old source value.</summary>
    public void SetOwnedForceRulerSlot10(int physicalSlot, ushort? ruler)
    {
        Check(physicalSlot, 52, "owned force ruler receiver");
        _forceRulers[physicalSlot] = ruler;
    }
}
