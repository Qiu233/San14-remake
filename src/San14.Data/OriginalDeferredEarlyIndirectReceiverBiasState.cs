namespace San14.Data;

/// <summary>The caller's actual retained ShadowReceiverBias singleton fields.
/// Construction imports current CPU values; it does not infer a native null
/// singleton or publish device contents. EBCD70 only borrows this state.</summary>
public sealed class OriginalDeferredEarlyIndirectReceiverBiasState
{
    public OriginalDeferredEarlyIndirectReceiverBiasState(float actual18, float actual1C, string actualOwnerEvidence)
    {
        if (!float.IsFinite(actual18) || !float.IsFinite(actual1C))
            throw new InvalidDataException("Actual ShadowReceiverBias fields must be finite.");
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnerEvidence);
        (Value18, Value1C, ActualOwnerEvidence) = (actual18, actual1C, actualOwnerEvidence);
    }
    public float Value18 { get; private set; }
    public float Value1C { get; private set; }
    public string ActualOwnerEvidence { get; }
    internal void OverrideRecord(float actualF24, float actualF28)
    {
        if (!(actualF24 > 0) || !float.IsFinite(actualF24) || !float.IsFinite(actualF28))
            throw new InvalidDataException("EBCD70 receiver override requires the current positive F24 and finite F28.");
        Value18 = actualF24; Value1C = actualF28;
    }
    internal void ResetAfterRecord()
    {
        // Actual9FE4D0 stores these literals, rather than restoring entry values.
        Value18 = 0; Value1C = 1;
    }
}
