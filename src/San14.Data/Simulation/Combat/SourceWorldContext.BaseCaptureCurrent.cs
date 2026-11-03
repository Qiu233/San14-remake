using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private ushort?[]? _captureHexWords16;
    private bool _captureHexWords16Owned;
    private Dictionary<int, NativeCityCaptureFields>? _captureCityFields;
    private ushort? _captureWorld1562;
    internal HashSet<int> CaptureHex16Writes { get; } = [];
    internal HashSet<int> CaptureCityC9Writes { get; } = [];
    internal HashSet<int> CaptureCityCFWrites { get; } = [];
    internal bool CaptureWorld1562Written { get; private set; }
    internal bool HasBaseCaptureCurrentWrites => CaptureHex16Writes.Count != 0 ||
        CaptureCityC9Writes.Count != 0 || CaptureCityCFWrites.Count != 0 || CaptureWorld1562Written;

    internal void BindBaseCaptureCurrent(NativeBaseCaptureCurrentState? owner)
    {
        //The saved World is the operation's immutable baseline. A private
        //buffer is needed only when this current actually writes a Hex word.
        _captureHexWords16 = owner?.HexWords16;
        _captureHexWords16Owned = false;
        _captureCityFields = owner is null ? null : new(owner.Cities);
        _captureWorld1562 = owner?.World1562;
        ClearBaseCaptureCurrentWrites();
    }

    public ushort? ReadCaptureHexWord16(int physical)
    {
        Check(physical, MapTileCount, "capture Hex physical slot");
        return _captureHexWords16 is { } words && physical < words.Length ? words[physical] : null;
    }

    public void SetCaptureHexWord16(int physical, ushort? value)
    {
        Check(physical, MapTileCount, "capture Hex physical slot");
        //Null-initialized storage means unknown for all other fields. Only
        //the explicit setter below supplies this reached native write.
        if (_captureHexWords16 is null)
        {
            _captureHexWords16 = new ushort?[MapTileCount];
            _captureHexWords16Owned = true;
        }
        if (physical >= _captureHexWords16.Length)
            throw new InvalidDataException("Current capture Hex owner does not cover this map index.");
        if (!_captureHexWords16Owned)
        {
            _captureHexWords16 = _captureHexWords16.ToArray();
            _captureHexWords16Owned = true;
        }
        _captureHexWords16[physical] = value; CaptureHex16Writes.Add(physical);
    }

    public byte? ReadCaptureCityByteC9(int physical)
    { Check(physical, 52, "capture City physical slot"); return _captureCityFields?.GetValueOrDefault(physical)?.C9; }
    public byte? ReadCaptureCityByteCF(int physical)
    { Check(physical, 52, "capture City physical slot"); return _captureCityFields?.GetValueOrDefault(physical)?.CF; }

    public void SetCaptureCityByteC9(int physical, byte? value)
    {
        Check(physical, 52, "capture City physical slot"); _captureCityFields ??= [];
        var before = _captureCityFields.GetValueOrDefault(physical) ?? new(null, null);
        _captureCityFields[physical] = before with { C9 = value }; CaptureCityC9Writes.Add(physical);
    }
    public void SetCaptureCityByteCF(int physical, byte? value)
    {
        Check(physical, 52, "capture City physical slot"); _captureCityFields ??= [];
        var before = _captureCityFields.GetValueOrDefault(physical) ?? new(null, null);
        _captureCityFields[physical] = before with { CF = value }; CaptureCityCFWrites.Add(physical);
    }

    /// <summary>2FE2B0's physical C9 bit1 writer, after its actual food
    /// predicate. Other C9 bits belong to independent source commands.</summary>
    public void SetCaptureCityFoodWarningBit1(int physical, bool warning)
    {
        byte before = ReadCaptureCityByteC9(physical)
            ?? throw new InvalidDataException("current.CityC9_before_2FE2B0_bit1 is unknown.");
        SetCaptureCityByteC9(physical, warning ? (byte)(before | 2) : (byte)(before & 0xFD));
    }

    public ushort? ReadCaptureWorld1562() => _captureWorld1562;
    public void SetCaptureWorld1562(ushort? value)
    { _captureWorld1562 = value; CaptureWorld1562Written = true; }

    internal void ClearBaseCaptureCurrentWrites()
    {
        CaptureHex16Writes.Clear(); CaptureCityC9Writes.Clear(); CaptureCityCFWrites.Clear();
        CaptureWorld1562Written = false;
    }

    internal void CopyBaseCaptureCurrentTo(SourceWorldContext draft)
    {
        //A borrowed immutable baseline can stay shared. An already-private
        //source may be written again after this copy, so its draft must detach.
        draft._captureHexWords16 = _captureHexWords16Owned ? _captureHexWords16?.ToArray() : _captureHexWords16;
        draft._captureHexWords16Owned = _captureHexWords16Owned;
        draft._captureCityFields = _captureCityFields is null ? null : new(_captureCityFields);
        draft._captureWorld1562 = _captureWorld1562;
        draft.CaptureHex16Writes.UnionWith(CaptureHex16Writes);
        draft.CaptureCityC9Writes.UnionWith(CaptureCityC9Writes);
        draft.CaptureCityCFWrites.UnionWith(CaptureCityCFWrites);
        draft.CaptureWorld1562Written = CaptureWorld1562Written;
    }

    internal void CommitBaseCaptureCurrentFields(SourceWorldContext draft)
    {
        foreach (int slot in draft.CaptureHex16Writes) SetCaptureHexWord16(slot, draft.ReadCaptureHexWord16(slot));
        foreach (int slot in draft.CaptureCityC9Writes) SetCaptureCityByteC9(slot, draft.ReadCaptureCityByteC9(slot));
        foreach (int slot in draft.CaptureCityCFWrites) SetCaptureCityByteCF(slot, draft.ReadCaptureCityByteCF(slot));
        if (draft.CaptureWorld1562Written) SetCaptureWorld1562(draft.ReadCaptureWorld1562());
    }
}
