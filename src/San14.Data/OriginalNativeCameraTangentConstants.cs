namespace San14.Data;

internal sealed record OriginalNativeCameraTangentConstantValues(double QuarterPi,double TwoOverPi,double Half,
    double HalfPiHigh,double HalfPiLow,double Numerator2,double Numerator0,double Denominator2,double Denominator1,
    double Denominator0,double NegativeOne,double OneThird,double SmallInputThreshold);

/// <summary>Exact original binary64 words. Source RVAs and executable guards
/// are retained by OriginalNativeCameraTangentSource, independently validated
/// against original instruction operands by the offline opcode oracle.</summary>
internal static class OriginalNativeCameraTangentConstants
{
    internal const uint TinyInputThresholdBits=0x32000000; // Source double threshold2^-27; verified loader below.
    internal static readonly IReadOnlyDictionary<string,ulong> Words=new Dictionary<string,ulong>
    {
        ["QuarterPi"]=0x3FE921FB54442D18UL,["TwoOverPi"]=0x3FE45F306DC9C883UL,["Half"]=0x3FE0000000000000UL,
        ["HalfPiHigh"]=0x3FF921FB54400000UL,["HalfPiLow"]=0x3DD0B4611A626331UL,
        ["Numerator2"]=0xBF919DBA6EFD6AADUL,["Numerator0"]=0x3FD8A8B0DA56CB17UL,
        ["Denominator2"]=0x3F92E29003C692D9UL,["Denominator1"]=0xBFE07266D7B3511BUL,
        ["Denominator0"]=0x3FF27E84A3E73A2EUL,["NegativeOne"]=0xBFF0000000000000UL,
        ["OneThird"]=0x3FD5555555555555UL,["SmallInputThreshold"]=0x3F20000000000000UL,
        ["TinyInputThreshold"]=0x3E40000000000000UL
    };
    internal static readonly OriginalNativeCameraTangentConstantValues Values=new(
        D("QuarterPi"),D("TwoOverPi"),D("Half"),D("HalfPiHigh"),D("HalfPiLow"),D("Numerator2"),D("Numerator0"),
        D("Denominator2"),D("Denominator1"),D("Denominator0"),D("NegativeOne"),D("OneThird"),D("SmallInputThreshold"));
    private static double D(string name)=>BitConverter.UInt64BitsToDouble(Words[name]);
}
