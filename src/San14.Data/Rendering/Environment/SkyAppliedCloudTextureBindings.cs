using San14.Data.Formats;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import the selected Cloud program's source aliases and consumed
/// register, name and dimension interface once. Images belong to the imported
/// texture set, rather than a per-frame reconstruction of source bindings.</summary>
public static class SkyAppliedCloudTextureBindings
{
    private sealed record Consumer(uint Slot,string Name,uint Dimension);
    private static readonly ConditionalWeakTable<LoadedShaderContainer,
        ConcurrentDictionary<(int Combination,int Pixel),IReadOnlyList<Consumer>>> Directories=new();
    private static IReadOnlyList<Consumer> ReadDirectory(LoadedShaderContainer shader,int combination,int pixel)
    {
        var mappings=shader.ReadG1ResourceBindings(combination).Entries;
        (string Name,int Slot)[] expected=[("sBase",0),("sCirrus",3),
            ("sSkyAmbientAltitude2dTable",4),("sTransmit2dTable",5),("sSctr3dTable",6)];
        if(mappings.Count!=expected.Length)throw new InvalidDataException("Cloud actual selected primary source alias count changed.");
        foreach(var e in expected)
        {
            var a=mappings.SingleOrDefault(a=>a.Name==e.Name);
            if(a is null||a.SlotIndex!=e.Slot)
                throw new InvalidDataException("Cloud source alias does not match its texture consumer.");
        }
        var rdef=shader.ReadResourceDefinitions(pixel);
        Consumer[] rows=[new(0,"sBase",8),new(1,"sDetail",8),new(2,"sDistort",4),
            new(3,"sCirrus",4),new(4,"sSkyAmbientAltitude2dTable",4),
            new(5,"sTransmit2dTable",4),new(6,"sSctr3dTable",8)];
        if(rdef.Bindings.Count(b=>b.InputType==2)!=rows.Length||rdef.Bindings.Count(b=>b.InputType==3)!=rows.Length)
            throw new InvalidDataException("Cloud current program has an unsupported texture/sampler readset.");
        foreach(var row in rows)
        {
            var binding=rdef.Bindings.Single(b=>b.InputType==2&&b.BindPoint==row.Slot);
            var sampler=rdef.Bindings.Single(b=>b.InputType==3&&b.BindPoint==row.Slot);
            if(binding.Name.Text!=row.Name||binding.Dimension!=row.Dimension||sampler.Name.Text!="__smp"+row.Name)
                throw new InvalidDataException("Cloud original consumer readset differs from its real current source binding.");
        }
        return Array.AsReadOnly(rows);
    }
    public static void ValidateProgram(SkyAppliedSourceCloudSelection selection,SkyAppliedCloudTextureSet textures)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(textures);
        selection.ValidateCurrent(selection.AcceptedPresent,selection.SourceStages);
        var directory=Directories.GetValue(selection.Shader,static _=>new());
        _=directory.GetOrAdd((selection.ColorCombination,selection.ColorPixelProgram),
            key=>ReadDirectory(selection.Shader,key.Combination,key.Pixel));
    }
}
