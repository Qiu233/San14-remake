using System.Text.Json;
using San14.Data;

// Import-boundary identity map for the same two actual material producers
// loaded by NativeCityAccessoryGpu. A force slot alone is not a glyph key.
static class OriginalD3D11CityGateFlagImports
{
    public static void WriteNativeKeys(string materialSource)
    {
        var keys=new List<(int Manager,OriginalD3D11CityGateFlagMaterialKey Key)>();
        foreach(string line in File.ReadLines(Path.Combine(materialSource,"material-ready.txt")))
        {
            string[] fields=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);
            int manager=int.Parse(fields[0]),force=int.Parse(fields[1]);
            if(manager is not(0 or 1))continue;
            using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(materialSource,fields[^1],"glyph-source.json")));
            var root=document.RootElement;var name=root.GetProperty("name");
            if(root.GetProperty("manager").GetInt32()!=manager||root.GetProperty("force").GetInt32()!=force||
                name.GetProperty("PhysicalForce").GetInt32()!=force)
                throw new InvalidDataException("Imported City/Gate flag producer identity differs.");
            keys.Add((manager,new(force,name.GetProperty("CurrentForceColor49").GetInt32(),root.GetProperty("code").GetUInt16())));
        }
        using var writer=new BinaryWriter(File.Create(Path.Combine(materialSource,"current-flag-keys.bin")));
        writer.Write(keys.Count);
        foreach(var row in keys){writer.Write(row.Manager);writer.Write(row.Key.Force);writer.Write(row.Key.Color49);writer.Write((uint)row.Key.Glyph);}
    }
}
