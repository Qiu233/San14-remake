using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>Sparse Camera compatibility and Native profile encoding boundary.</summary>
public static class SkyCloudWeatherEncoding
{
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static readonly int[] SelectedOffsets =
    [
        0x624, 0x62C, 0x630, 0x634, 0x638, 0x63C, 0x644, 0x648, 0x650, 0x654,
        0x65C, 0x660, 0x664, 0x668, 0x66C, 0x670, 0x674, 0x678, 0x67C, 0x680,
        0x684, 0x688, 0x68C, 0x690, 0x694, 0x698, 0x69C, 0x6A0, 0x6A4, 0x6A8,
        0x6AC, 0x6B0, 0x6B4, 0x6C4, 0x6C8, 0x6CC, 0x6D0, 0x6D4, 0x6D8, 0x6DC,
        0x6E0, 0x6E4, 0x6E8, 0x6EC, 0x6FC, 0x700, 0x704, 0x708, 0x70C, 0x710,
        0x714, 0x718, 0x71C, 0x720, 0x724, 0x728, 0x72C, 0x730, 0x734, 0x738,
        0x73C, 0x740, 0x744, 0x74C, 0x750, 0x758, 0x75C, 0x760, 0x764, 0x768,
        0x76C, 0x770, 0x774, 0x778, 0x77C, 0x780, 0x784, 0x788, 0x78C, 0x790,
        0x794, 0x798, 0x7A0,
    ];
    public static IReadOnlyDictionary<int, uint> EncodeSelected(SkyCloudWeatherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Dictionary<int, uint> words = new(SelectedOffsets.Length);
        foreach (int offset in SelectedOffsets) words.Add(offset, ReadSelected(settings, offset));
        return new ReadOnlyDictionary<int, uint>(words);
    }
    private static uint ReadSelected(SkyCloudWeatherSettings s, int offset) => offset switch
    {
        0x624 => Bits(s.BottomAltitude),
        0x62C => Bits(s.DetailAmount.Clear),
        0x630 => Bits(s.DetailAmount.Cloudy),
        0x634 => Bits(s.HumidDetailThreshold),
        0x638 => s.RaySamples,
        0x63C => s.MinimumRaySamples,
        0x644 => Bits(s.CoverageFactor),
        0x648 => Bits(s.Thickness),
        0x650 => Bits(s.Erosion.Clear),
        0x654 => Bits(s.Erosion.Cloudy),
        0x65C => Bits(s.Density.Clear),
        0x660 => Bits(s.Density.Cloudy),
        0x664 => Bits(s.Temporal.SourceFactorA),
        0x668 => Bits(s.Temporal.SourceFactorB),
        0x66C => s.Temporal.Mode,
        0x670 => Bits(s.Temporal.SourceFactorC),
        0x674 => s.LowCostLevel,
        0x678 => s.RayExponent,
        0x67C => Bits(s.SampleStep),
        0x680 => Bits(s.Hg.Asymmetry0),
        0x684 => Bits(s.Hg.Asymmetry1),
        0x688 => Bits(s.Hg.Weight),
        0x68C => Bits(s.Hg.Intensity),
        0x690 => Bits(s.Base.Period.X),
        0x694 => Bits(s.Base.Period.Y),
        0x698 => Bits(s.Base.Period.Z),
        0x69C => Bits(s.Base.Position.X),
        0x6A0 => Bits(s.Base.Position.Y),
        0x6A4 => Bits(s.Base.Position.Z),
        0x6A8 => Bits(s.Base.Velocity.X),
        0x6AC => Bits(s.Base.Velocity.Y),
        0x6B0 => Bits(s.Base.Velocity.Z),
        0x6B4 => Bits(s.BaseErosion),
        0x6C4 => Bits(s.DetailPeriod.Cloudy.X),
        0x6C8 => Bits(s.DetailPeriod.Cloudy.Y),
        0x6CC => Bits(s.DetailPeriod.Cloudy.Z),
        0x6D0 => Bits(s.DetailPeriod.Clear.X),
        0x6D4 => Bits(s.DetailPeriod.Clear.Y),
        0x6D8 => Bits(s.DetailPeriod.Clear.Z),
        0x6DC => Bits(s.DetailPeriod.Response.Upper),
        0x6E0 => Bits(s.DetailPeriod.Response.Lower),
        0x6E4 => Bits(s.DetailPosition.X),
        0x6E8 => Bits(s.DetailPosition.Y),
        0x6EC => Bits(s.DetailPosition.Z),
        0x6FC => Bits(s.DetailVelocity.Cloudy.X),
        0x700 => Bits(s.DetailVelocity.Cloudy.Y),
        0x704 => Bits(s.DetailVelocity.Cloudy.Z),
        0x708 => Bits(s.DetailVelocity.Clear.X),
        0x70C => Bits(s.DetailVelocity.Clear.Y),
        0x710 => Bits(s.DetailVelocity.Clear.Z),
        0x714 => Bits(s.DetailVelocity.Response.Upper),
        0x718 => Bits(s.DetailVelocity.Response.Lower),
        0x71C => Bits(s.DetailErosion),
        0x720 => Bits(s.Distort.Period.X),
        0x724 => Bits(s.Distort.Period.Y),
        0x728 => Bits(s.Distort.Position.X),
        0x72C => Bits(s.Distort.Position.Y),
        0x730 => Bits(s.Distort.Velocity.X),
        0x734 => Bits(s.Distort.Velocity.Y),
        0x738 => Bits(s.DistortAmplitude.X),
        0x73C => Bits(s.DistortAmplitude.Y),
        0x740 => Bits(s.DistortAmplitude.Z),
        0x744 => Bits(s.CirrusAltitude),
        0x74C => Bits(s.CirrusCoverage.Clear),
        0x750 => Bits(s.CirrusCoverage.Cloudy),
        0x758 => Bits(s.CirrusDistortion.Clear),
        0x75C => Bits(s.CirrusDistortion.Cloudy),
        0x760 => Bits(s.CirrusDistortionAuxiliary),
        0x764 => Bits(s.Cirrus.Period.X),
        0x768 => Bits(s.Cirrus.Period.Y),
        0x76C => Bits(s.Cirrus.Velocity.X),
        0x770 => Bits(s.Cirrus.Velocity.Y),
        0x774 => Bits(s.CirrusDistort.Period.X),
        0x778 => Bits(s.CirrusDistort.Period.Y),
        0x77C => Bits(s.CirrusDistort.Velocity.X),
        0x780 => Bits(s.CirrusDistort.Velocity.Y),
        0x784 => Bits(s.CirrusDistortAmplitude.X),
        0x788 => Bits(s.CirrusDistortAmplitude.Y),
        0x78C => Bits(s.HumidityResponse.Lower),
        0x790 => Bits(s.HumidityResponse.Upper),
        0x794 => Bits(s.CirrusHumidityResponse.Lower),
        0x798 => Bits(s.CirrusHumidityResponse.Upper),
        0x7A0 => s.Resolution,
        _ => throw new InvalidDataException($"Cloud Camera{offset:X} has no closed source writer.")
    };
    public static uint ReadCurrent(SkyCloudWeatherState state, int offset) => offset switch
    {
        0x1D0 => Bits(state.Cloudiness),
        0x1D8 => Bits(state.Humidity),
        0x628 => Bits(state.DetailAmount),
        0x640 => Bits(state.Coverage),
        0x64C => Bits(state.Erosion),
        0x658 => Bits(state.Density),
        0x674 => state.LowCostLevel,
        0x748 => Bits(state.CirrusCoverage),
        0x754 => Bits(state.CirrusDistortion),
        0x7A0 => state.Resolution,
        0x6B8 => Bits(state.DetailPeriod.X),
        0x6BC => Bits(state.DetailPeriod.Y),
        0x6C0 => Bits(state.DetailPeriod.Z),
        0x6F0 => Bits(state.DetailVelocity.X),
        0x6F4 => Bits(state.DetailVelocity.Y),
        0x6F8 => Bits(state.DetailVelocity.Z),
        _ => ReadSelected(state.Settings, offset)
    };
}
