using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record DeferredEarlyShadowCurrentCounterCameraFields(
    DeferredEarlyShadowCurrentMainFields Main, int PreparedSignedA98, int PreparedSigned106C, string SourceEvidence);

/// <summary>Two actual direct V468 + V418 fields consumed by D78900. CPU80C
/// writes embedded1330+A98; CPU DCC writes1330+106C. CPU106C belongs to the
/// different prepared1320 optics field and is not used here.</summary>
public static class DeferredEarlyShadowViewCounterCameraSource
{
    private sealed class Imported
    {
        public readonly uint A98, Value106C;
        public Imported(MapSeasonCameraPackage package)
        {
            A98 = Read(package, 359, 0x32CD5688, 5);
            Value106C = Read(package, 599, 0x591E159D, 4);
        }
        private static uint Read(MapSeasonCameraPackage package, int ordinal, uint hash, uint type)
        {
            var field = package.Camera.Properties[ordinal];
            if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != type ||
                field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                throw new InvalidDataException("Counter Camera field differs from its actual direct source loader binding.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
    }
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, Imported> imported = new();

    public static DeferredEarlyShadowCurrentCounterCameraFields CaptureCurrentMain(
        DeferredEarlyShadowCurrentMainFields actualMain)
    {
        ArgumentNullException.ThrowIfNull(actualMain);
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(actualMain.ActualMainInputs, actualMain.PreparedMain);
        var selected = actualMain.ActualMainInputs.Source.Selection;
        if (selected.SecondPackage is not null)
            throw new NotSupportedException("The current counter Camera fields require their actual direct V468 selection; interpolation has its own writer.");
        var values = imported.GetValue(selected.FirstPackage, static package => new(package));
        uint Actual(int offset, uint retained) => actualMain.PreparedMain.CurrentSeasonPostEffectScalars!.TryReadWord(offset, out uint value)
            ? value : retained;
        return new(actualMain, unchecked((int)Actual(0xA98, values.A98)), unchecked((int)Actual(0x106C, values.Value106C)),
            selected.FirstPackage.SourceEvidence + ";" + actualMain.PreparedMain.CurrentSeasonPostEffectWriteIdentity!.SourceEvidence +
            ";TC:C37B52/C37B58:actual-selectedCPU80C->Main80C;C4216A/C42170:Main80C->1330+A98;" +
            "C3819A/C381A0:actual-selectedCPUDCC->MainDCC;C43292/C43298:MainDCC->1330+106C;" +
            "D04091/D04097/D04FEB/D04FF1:same-prepared-View-header-copy;C99980/D78CC0;" +
            "source359/32CD5688/type5;source599/591E159D/type4;not-CPU106C/prepared1320");
    }
}
