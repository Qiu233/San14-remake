using San14.Core;

namespace San14.Data;

/// <summary>Widths belong to the original resource representation, not to the
/// independent officer state. An unrepresentable channel remains unresolved.</summary>
public static class OfficerMeritSourceAdapter
{
    public static OfficerMeritState Decode(short? merit, ushort? pendingMerit, byte? stage,
        byte? firstSelectionCount, byte? secondSelectionCount, byte? resetCode) =>
        new(merit, pendingMerit, stage, firstSelectionCount, secondSelectionCount, resetCode);

    public static short? EncodeMerit(OfficerMeritState? state) => state?.Merit is int value &&
        value is >= short.MinValue and <= short.MaxValue ? (short)value : null;
    public static ushort? EncodePendingMerit(OfficerMeritState? state) => state?.PendingMerit is int value &&
        value is >= 0 and <= ushort.MaxValue ? (ushort)value : null;
    public static byte? EncodeStage(OfficerMeritState? state) => EncodeByte(state?.RankStageId);
    public static byte? EncodeFirstSelectionCount(OfficerMeritState? state) => EncodeByte(state?.FirstSelectionCount);
    public static byte? EncodeSecondSelectionCount(OfficerMeritState? state) => EncodeByte(state?.SecondSelectionCount);
    public static byte? EncodeResetCode(OfficerMeritState? state) => EncodeByte(state?.ResetCode);
    private static byte? EncodeByte(int? value) => value is >= 0 and <= byte.MaxValue ? (byte)value : null;
}
