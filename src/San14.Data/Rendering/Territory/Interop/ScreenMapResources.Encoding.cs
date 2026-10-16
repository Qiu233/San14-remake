using System.Numerics;

namespace San14.Data;

public sealed partial class ScreenMapResources
{
    public const string NativeDirectory="territory-visual-input";

    /// <summary>Writes static media once. PTV3 excludes Zone pixels and current
    /// ownership: the Native map supplies its actual retained seven-layer owner.</summary>
    public void WriteNativeInputs(string inputRoot)
    {
        string directory=Path.Combine(inputRoot,NativeDirectory);
        Directory.CreateDirectory(directory);
        using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,"profile.bin"))))
        {
            writer.Write(0x33565450u); // PTV3: style resources without a Zone seed.
            writer.Write(62u);writer.Write(1u); // DXGI R8_UINT, one byte per Zone element.
            string[] tableNames=["FillAlpha","Width0","BorderAlpha","FrameWidth","FrameAlpha",
                "Width6","LineAlpha19","CellAlpha11","Width1","LineAlpha11","Style1Width0",
                "Width45","SpecialWidth45","Curvature","ZoomValues"];
            foreach(string name in tableNames)
                foreach(int value in settings.Tables[name])writer.Write(value);
            foreach(var rgb in new[]{settings.FrameRgb,settings.Cell11Rgb,settings.Line11Rgb,settings.Line19Rgb})
            {writer.Write(rgb.X);writer.Write(rgb.Y);writer.Write(rgb.Z);}
            writer.Write(settings.FrameWidthCutoff);writer.Write(constants);
            foreach(var color in baseCells)WriteColor(writer,color);
            foreach(var color in baseLines)WriteColor(writer,color);
            // Fresh ScreenMap owns no allocated regular colors. The real
            // current ownership publication performs the sole allocation.
            for(int slot=0;slot<TerritoryConstants.PaletteCount;slot++)writer.Write(60);
        }
        File.WriteAllBytes(Path.Combine(directory,"territory-vs.dxbc"),vertex);
        File.WriteAllBytes(Path.Combine(directory,"territory-ps.dxbc"),pixel);
        foreach(var sampler in samplers)
            File.WriteAllBytes(Path.Combine(directory,$"sampler-{sampler.Key}.bin"),sampler.Value.ToArray());
        for(int slot=0;slot<stockSamplers.Length;slot++)
            File.WriteAllBytes(Path.Combine(directory,$"stock-copy-sampler-{slot}.bin"),stockSamplers[slot]);
        // The normal project shader producer already owns stock-copy VS/PS.
    }

    private static void WriteColor(BinaryWriter writer,Vector4 value)
    {writer.Write(value.X);writer.Write(value.Y);writer.Write(value.Z);writer.Write(value.W);}
}
